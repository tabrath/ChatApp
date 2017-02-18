using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChatApp
{
    public class ChatContext : IDisposable
    {
        public Socket Client { get; set; }
        public Socket Listener { get; set; }
        public Logger Logger { get; }
        public Options Options { get; }
        public string ClientNickname { get; set; }
        public EndPoint ClientEndPoint { get; set; }
        public ContextStatus Status { get; protected set; } = ContextStatus.NotReady;

        public event EventHandler<ConnectionRequestEventArgs> ConnectionRequested;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<MessageSentEventArgs> MessageSent;

        public ChatContext(Logger logger, Options options)
        {
            Logger = logger;
            Options = options;
        }

        public void SetupListener()
        {
            if (Listener != null)
                throw new Exception("Listener already setup");

            Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Listener.Bind(new IPEndPoint(IPAddress.Loopback, Options.ListenPort));
            Listener.Listen(1);
        }

        public void Send(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            SocketError error;
            var sent = 0;
            while ((sent += Client.Send(buffer, sent, buffer.Length - sent, SocketFlags.None, out error)) <
                   buffer.Length && error == SocketError.Success)
            {
            }

            if (error != SocketError.Success)
                Logger.Error($"Could not send message ({error})");
            else
                MessageSent?.Invoke(this, new MessageSentEventArgs(message));
        }

        public void Connect(EndPoint ep)
        {
            if (Client != null)
            {
                Logger.Error("You're already connected");
                return;
            }

            if (ep.Equals(Listener.LocalEndPoint))
            {
                Logger.Error("You cannot connect to yourself");
                return;
            }

            Logger.Info("Connecting.. ");
            try
            {
                Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Client.Connect(ep);

                SendHandshake(Client, Options.Nickname);
                ClientNickname = ReceiveHandshake(Client);
                Status = ContextStatus.Connected;

                Logger.Info("Connected");

                Receive();
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                Client = null;
            }
        }

        private void Receive()
        {
            var receiveBuffer = new byte[4096];
            Client?.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None,
                ar =>
                {
                    try
                    {
                        var received = Client.EndReceive(ar);
                        if (received <= 0)
                            throw new Exception("No data");

                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(ClientNickname, Encoding.UTF8.GetString(receiveBuffer, 0, received)));
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Disconnected: {e.Message}");
                        Client = null;
                        Status = ContextStatus.Listening;
                    }
                    finally
                    {
                        if (Status == ContextStatus.Connected)
                            Receive();
                    }
                }, null);
        }

        public void Disconnect()
        {
            if (Client == null)
            {
                Logger.Error("You're not connected");
                return;
            }

            Client.Close();
            Client = null;
            ClientNickname = string.Empty;
            ClientEndPoint = null;
        }

        public void Listen()
        {
            if (Status == ContextStatus.Connected)
                return;

            Status = ContextStatus.Listening;

            Listener?.BeginAccept(ar =>
            {
                try
                {
                    var client = Listener?.EndAccept(ar);
                    if (client == null)
                    {
                        Listen();
                        return;
                    }

                    if (ConnectionRequested != null)
                    {
                        var ep = client.RemoteEndPoint;

                        Logger.Info($"{ep}: Connected");

                        var clientNickname = ReceiveHandshake(client);

                        var args = new ConnectionRequestEventArgs(clientNickname, ep);
                        ConnectionRequested(this, args);

                        if (!args.Accept)
                        {
                            client.Dispose();
                            throw new Exception($"{ep}: Rejected connection");
                        }

                        Client = client;
                        ClientNickname = clientNickname;
                        ClientEndPoint = ep;
                        SendHandshake(Client, Options.Nickname);
                        Status = ContextStatus.Connected;

                        Receive();
                    }
                    else
                        client?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    Status = ContextStatus.NotReady;
                }
                catch (Exception e)
                {
                    Logger.Error(e.Message);
                }
                finally
                {
                    Listen();
                }
            }, null);
        }

        private static void SendHandshake(Socket client, string nickname)
        {
            var buffer = Encoding.UTF8.GetBytes(nickname);
            buffer = new[] { (byte)buffer.Length }.Concat(buffer).ToArray();
            if (client.Send(buffer, 0, buffer.Length, SocketFlags.None) !=
                buffer.Length)
                throw new Exception("could not send nickname");
        }

        private static string ReceiveHandshake(Socket client)
        {
            var buffer = new byte[1];
            if (client.Receive(buffer, 0, 1, SocketFlags.None) != 1)
                throw new Exception("could not receive nickname");
            var length = (int) buffer[0];
            buffer = new byte[length];
            if (client.Receive(buffer, 0, buffer.Length, SocketFlags.None) != length)
                throw new Exception("could not receive nickname");

            return Encoding.UTF8.GetString(buffer);
        }

        public void Dispose()
        {
            Client?.Dispose();
            Listener?.Dispose();
        }
    }
}