using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xamarin.Forms;

namespace Xamarin.Forms.Xaml.LiveReload
{
     public enum MessageType
    {
        None,
        GetHostname,
        GetHostnameResponse,
        XamlUpdated
    }

    public class Message
    {
        public MessageType MessageType { get; set; }
        public byte[] Payload { get; set; }
    }
    
    public static class TcpClientExtensions
    {
        public static Message ReceiveMessage(this TcpClient client)
        {
            var buff = new byte[4096];
            var totalBytesRead = 0;
            var messageType = MessageType.None;
            var payloadSize = 0;
            
            // 1-8 header
            // byte 1-4 message type
            // byte 5-8 payload size
            // 9-? message
            // message string bytes
            var allReadBytes = new List<byte>();
            int bytesRead;
            while ((bytesRead = client.GetStream().Read(buff, 0, buff.Length)) > 0)
            {
                allReadBytes.AddRange(buff.Take(bytesRead));
                totalBytesRead += bytesRead;

                if (totalBytesRead >= 4 && messageType == MessageType.None)
                {
                    messageType = (MessageType) BitConverter.ToInt32(allReadBytes.Take(4).ToArray(), 0);
                }
                if (totalBytesRead >= 8 && payloadSize == 0)
                {
                    payloadSize = BitConverter.ToInt32(allReadBytes.Skip(4).Take(4).ToArray(), 0);
                }

                if (totalBytesRead - 8 >= payloadSize) break;
            }
            
            return new Message
            {
                MessageType = messageType,
                Payload = allReadBytes.Skip(8).ToArray()
            };
        }

        public static void SendMessage(this TcpClient client, Message message)
        {
            SendMessage(new[] { client }, message);
        }

        public static void SendMessage(this IEnumerable<TcpClient> clients, Message message)
        {
            var payload = message.Payload ?? new byte[0];
            var messageTypeBytes = BitConverter.GetBytes((int)message.MessageType);
            var payloadSizeBytes = BitConverter.GetBytes(payload.Length);
            var data = new List<byte>();
            data.AddRange(messageTypeBytes);
            data.AddRange(payloadSizeBytes);
            data.AddRange(payload);
            foreach (var socket in clients)
            {
                try
                {
                    socket.GetStream().Write(data.ToArray(), 0, data.Count);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.ConnectionReset) throw;
                    socket.Close();
                }
            }
        }
    }

    public static class LiveReload
    {
        static Application _application;
        static readonly Regex Regex = new Regex("x:Class=\"([^\"]+)\"");
        
        public static void Enable(Application application, Action<Exception> onException)
        {
            _application = application;

            Task.Run(async () =>
                {

                var c = new TcpClient();
                c.Connect(IPAddress.Parse("127.0.0.1"), 6000);

                //var c = new TcpClient();
                //c.Connect("127.0.0.1", 52222);

                //c.SendMessage(new Message {MessageType = MessageType.GetHostname});
                while (c.Connected)
                {
                    try
                    {
                        var message = c.ReceiveMessage();

                        var xaml = Encoding.UTF8.GetString(message.Payload, 0, message.Payload.Length);
                        var match = Regex.Match(xaml);
                        if (!match.Success) return;
                        var className = match.Groups[1].Value;
                        var page = FindPage(_application.MainPage, className);
                        if (page == null) return;

                        try
                        {
                            await UpdatePageFromXamlAsync(page, xaml);
                        }
                        catch (Exception exception)
                        {
                                    
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.ConnectionReset) throw;
                        c.Close();
                    }
                }
            })
            .ContinueWith(t =>
            {
                if (t.IsFaulted) onException(t.Exception);
            });
        }
        
        static Task UpdatePageFromXamlAsync(Page page, string xaml)
        {
            var tcs = new TaskCompletionSource<object>();
            Device.BeginInvokeOnMainThread(() =>
            {
                var oldBindingContext = page.BindingContext;
                try
                {
                    LoadXaml(page, xaml);
                    page.ForceLayout();
                    tcs.SetResult(null);
                }
                catch (Exception exception)
                {
                    tcs.SetException(exception);
                }
                finally
                {
                    page.BindingContext = oldBindingContext;
                }
            });
            return tcs.Task;
        }

        static Page FindPage(Page page, string fullTypeName)
        {
            if (page == null) return null;

            var p = page.Navigation.ModalStack.LastOrDefault(x => x.GetType().FullName == fullTypeName);
            if (p != null) return p;

            var navigationPage = page as NavigationPage;
            if (navigationPage?.CurrentPage.GetType().FullName == fullTypeName)
            {
                return navigationPage?.CurrentPage;
            }
            var masterDetailPage = page as MasterDetailPage;
            if (masterDetailPage != null)
            {
                p = FindPage(masterDetailPage.Master, fullTypeName);
                if (p != null) return p;

                p = FindPage(masterDetailPage.Detail, fullTypeName);
                if (p != null) return p;
            }

            if (page.GetType().FullName == fullTypeName) return page;

            return null;
        }

        static void LoadXaml(BindableObject view, string xaml)
        {
            var xamlAssembly = Assembly.Load(new AssemblyName("Xamarin.Forms.Xaml"));
            var xamlLoaderType = xamlAssembly.GetType("Xamarin.Forms.Xaml.XamlLoader");
            var loadMethod = xamlLoaderType.GetRuntimeMethod("Load", new[] { typeof(BindableObject), typeof(string) });
            try
            {
                loadMethod.Invoke(null, new object[] { view, xaml });
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
        }
    }
}