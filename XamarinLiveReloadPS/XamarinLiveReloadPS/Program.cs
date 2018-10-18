using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace XamarinLiveReloadPS
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

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Set url to your project");
            var pathDirectory = Console.ReadLine();
            var port = 6000;
            var clients = new List<TcpClient>();
            var tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            tcpListener.Start();

            Task.Run(() =>
            {
                while (true)
                {
                    var client = tcpListener.AcceptTcpClient();
                    Console.WriteLine($"Client connected from {client.Client.RemoteEndPoint}");
                    clients.Add(client);
                    Task.Run(() =>
                    {
                        while (true)
                        {
                            var message = client.ReceiveMessage();
                            switch (message.MessageType)
                            {
                                case MessageType.GetHostname:
                                    client.SendMessage(new Message
                                    {
                                        MessageType = MessageType.GetHostnameResponse,
                                        Payload = Encoding.UTF8.GetBytes(Dns.GetHostName())
                                    });
                                    break;
                            }
                        }
                    });
                }
            });

            var fw = new FileSystemWatcher(pathDirectory)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.LastWrite
            };
            fw.Changed += (sender, eventArgs) =>
            {
                var extension = Path.GetExtension(eventArgs.FullPath);
                if (!extension.Contains("~")) return;

                Console.WriteLine(eventArgs.FullPath);
                var xaml = "";
                try
                {
                    using (var fileStream = new FileStream(eventArgs.FullPath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite))
                    using (var textReader = new StreamReader(fileStream, Encoding.Default))
                    {
                        xaml = textReader.ReadToEnd();
                    }

                    clients.RemoveAll(x => !x.Connected);
                    clients.SendMessage(new Message
                    {
                        MessageType = MessageType.XamlUpdated,
                        Payload = Encoding.UTF8.GetBytes(xaml)
                    });

                }catch(Exception){}

            };
            Console.WriteLine($"Watching for file changes in {pathDirectory}");
            
            Console.ReadLine();
        }
    }
}
