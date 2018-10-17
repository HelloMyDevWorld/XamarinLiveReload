using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xamarin.Forms.Xaml.LiveReload.Server
{
    public enum MessageType
    {
        XamlUpdated
    }

    public class Message
    {
        public MessageType MessageType { get; set; }
        public byte[] Payload { get; set; }
    }
    
    public static class TcpClientExtensions
    {
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

    class Program
    {


        static void Main(string[] args)
        {
            
            Console.WriteLine("Insert URL");
            var url = Console.ReadLine();
            var port = 6000;
            var clients = new List<TcpClient>();
            var tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"),port);
            tcpListener.Start();

            Task.Run(() =>
            {
                while (true)
                {
                    var client = tcpListener.AcceptTcpClient();
                    Console.WriteLine($"Client connected from {client.Client.RemoteEndPoint}");
                    clients.Add(client);
                }
            });

            var directory = Path.GetDirectoryName(url);
            Console.WriteLine("Directory URL: " + directory);
            var fw = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite
            };
            fw.Changed += (sender, eventArgs) =>
            {

                var xaml = "";
                using (var fileStream = new FileStream(url, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var textReader = new StreamReader(fileStream, Encoding.Default)) {
                    xaml = textReader.ReadToEnd();
                }
                clients.RemoveAll(x => !x.Connected);
                clients.SendMessage(new Message
                {
                    MessageType = MessageType.XamlUpdated,
                    Payload = Encoding.UTF8.GetBytes(xaml)
                });
                Console.WriteLine($"Updated");
            };


            
            Console.ReadLine();
        }
    }
}
