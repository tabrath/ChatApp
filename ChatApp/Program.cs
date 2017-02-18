using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatApp
{
    public class Options
    {
        public string Nickname { get; set; } = "";
        public int ListenPort { get; set; } = 1337;
    }

    class Program
    {
        static void Main(string[] args)
        {
            var options = new Options();

            Console.WriteLine(string.Join(", ", args));
            Console.Write("Nickname: ");
            options.Nickname = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(options.Nickname))
            {
                Console.WriteLine("You must provide a nickname");
                return;
            }
            if (args.Length >= 1)
            {
                var listenPort = options.ListenPort;
                if (!int.TryParse(args[0], out listenPort))
                {
                    Console.WriteLine("Could not parse port argument");
                    return;
                }
                options.ListenPort = listenPort;
            }

            Socket client = null;
            Socket listener = null;
            try
            {
                var quit = false;
                string clientNickname = null;
                EndPoint clientEndPoint = null;
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var listenEndPoint = new IPEndPoint(IPAddress.Any, options.ListenPort);
                listener.Bind(listenEndPoint);
                listener.Listen(1);
                Console.WriteLine($"Listening on {listenEndPoint}");

                Listen(options.Nickname, listener, (c, n) =>
                {
                    Console.Write($"Connection request from {n} ({c.RemoteEndPoint}), accept? [Yn] ");
                    if (Console.ReadKey().Key == ConsoleKey.N)
                        return false;

                    client = c;
                    clientEndPoint = c.RemoteEndPoint;
                    clientNickname = n;
                    return true;
                }, () =>
                {
                    client = null;
                    clientEndPoint = null;
                    clientNickname = null;
                });

                while (!quit)
                {
                    Console.Write($"{options.Nickname}$ ");
                    var inputargs = Console.ReadLine()?.Trim().Split(' ');
                    if (inputargs == null || inputargs.Length == 0)
                        continue;

                    switch (inputargs[0])
                    {
                        case "status":
                            Console.WriteLine($"Connected: {client != null}");
                            break;

                        case "connect":
                            if (client != null)
                            {
                                Console.WriteLine("Error: you're already connected");
                                break;
                            }

                            if (inputargs.Length == 2)
                            {
                                var addr = inputargs[1].Split(':');
                                if (addr.Length == 2)
                                {
                                    IPAddress ip;
                                    if (!IPAddress.TryParse(addr[0], out ip))
                                    {
                                        Console.WriteLine("Error: could not parse ip address");
                                        break;
                                    }
                                    int port;
                                    if (!int.TryParse(addr[1], out port))
                                    {
                                        Console.WriteLine("Error: could not parse port");
                                        break;
                                    }

                                    Console.Write("Connecting.. ");
                                    try
                                    {
                                        client = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                                            ProtocolType.Tcp);
                                        client.Connect(ip, port);

                                        var nicknameBuffer = Encoding.UTF8.GetBytes(options.Nickname);
                                        nicknameBuffer =
                                            new[] {(byte) nicknameBuffer.Length}.Concat(nicknameBuffer).ToArray();
                                        if (client.Send(nicknameBuffer, 0, nicknameBuffer.Length, SocketFlags.None) !=
                                            nicknameBuffer.Length)
                                            throw new Exception("could not send nickname");

                                        nicknameBuffer = new byte[1];
                                        if (client.Receive(nicknameBuffer, 0, 1, SocketFlags.None) != 1)
                                            throw new Exception("could not receive nickname");
                                        var length = (int) nicknameBuffer[0];
                                        nicknameBuffer = new byte[length];
                                        if (
                                            client.Receive(nicknameBuffer, 0, nicknameBuffer.Length, SocketFlags.None) !=
                                            length)
                                            throw new Exception("could not receive nickname");

                                        clientNickname = Encoding.UTF8.GetString(nicknameBuffer);

                                        Console.WriteLine("done");

                                        var receiveBuffer = new byte[4096];
                                        client.BeginReceive(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None,
                                            ar =>
                                            {
                                                try
                                                {
                                                    var received = client.EndReceive(ar);
                                                    if (received <= 0)
                                                        throw new Exception("No data");

                                                    Console.WriteLine(
                                                        $"{clientNickname} ({clientEndPoint}): {Encoding.UTF8.GetString(receiveBuffer, 0, received)}");
                                                }
                                                catch (Exception e)
                                                {
                                                    Console.WriteLine($"Disconnected: {e.Message}");
                                                    client = null;
                                                }
                                            }, null);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine($"failed: {e.Message}");
                                        client = null;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Error: expected address with port");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Error: expected argument of address:port");
                            }

                            break;

                        case "disconnect":
                            if (client == null)
                            {
                                Console.WriteLine("Error: you're not connected");
                                break;
                            }

                            client.Close();
                            client = null;
                            break;

                        case "send":
                            if (client == null)
                            {
                                Console.WriteLine("Error: you're not connected");
                                break;
                            }

                            if (inputargs.Length == 1)
                            {
                                Console.WriteLine("Error: no message to send");
                                break;
                            }

                            var message = string.Join(" ", inputargs.Skip(1));
                            var buffer = Encoding.UTF8.GetBytes(message);
                            SocketError error;
                            int sent = 0;
                            while ((sent = client.Send(buffer, sent, buffer.Length - sent, SocketFlags.None, out error)) <
                                   buffer.Length && error == SocketError.Success)
                            {
                            }

                            if (error != SocketError.Success)
                                Console.WriteLine($"Error: could not send message ({error})");
                            else
                                Console.WriteLine($"me: {message}");

                            break;

                        case "help":
                            Console.WriteLine("connect [address:port]");
                            Console.WriteLine("disconnect");
                            Console.WriteLine("send [message]");
                            Console.WriteLine("status");
                            Console.WriteLine("help");
                            Console.WriteLine("quit");
                            break;

                        case "quit":
                            quit = true;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
            }
            finally
            {
                client?.Dispose();
                listener?.Dispose();
            }
        }

        private static void Listen(string nickname, Socket listener, Func<Socket, string, bool> accept, Action disconnected)
        {
            listener.BeginAccept(ar =>
            {
                try
                {
                    var client = listener.EndAccept(ar);
                    var ep = client.RemoteEndPoint;

                    Console.WriteLine($"{ep}: Connected");

                    var nicknameBuffer = new byte[1];
                    if (client.Receive(nicknameBuffer, 0, 1, SocketFlags.None) != 1)
                        throw new Exception("could not receive nickname");
                    var length = (int)nicknameBuffer[0];
                    nicknameBuffer = new byte[length];
                    if (client.Receive(nicknameBuffer, 0, nicknameBuffer.Length, SocketFlags.None) != length)
                        throw new Exception("could not receive nickname");

                    nicknameBuffer = Encoding.UTF8.GetBytes(nickname);
                    nicknameBuffer = new[] { (byte)nicknameBuffer.Length }.Concat(nicknameBuffer).ToArray();
                    if (client.Send(nicknameBuffer, 0, nicknameBuffer.Length, SocketFlags.None) != nicknameBuffer.Length)
                        throw new Exception("could not send nickname");

                    var clientNickname = Encoding.UTF8.GetString(nicknameBuffer);

                    if (!accept(client, clientNickname))
                    {
                        Console.WriteLine($"{ep}: Rejected connection");
                        client.Dispose();
                        Listen(nickname, listener, accept, disconnected);
                        return;
                    }

                    try
                    {
                        var buffer = new byte[4096];
                        SocketError error;
                        int received = 0;
                        while ((received = client.Receive(buffer, 0, buffer.Length, SocketFlags.None, out error)) > 0 &&
                               error == SocketError.Success)
                        {
                            Console.WriteLine($"{clientNickname} ({ep}): {Encoding.UTF8.GetString(buffer, 0, received)}");
                        }

                        if (error != SocketError.Success)
                            throw new SocketException((int) error);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"{ep}: Disconnected ({e.Message})");
                    }
                }
                catch (ObjectDisposedException)
                {
                    // swallow
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }

                disconnected();
            }, null);
        }
    }
}
