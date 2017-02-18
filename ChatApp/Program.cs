using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ChatApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = new Logger();
            var options = new Options();
            var prompt = new Prompt();

            options.Nickname = prompt.Ask("Enter a nickname", nick =>
            {
                if (string.IsNullOrEmpty(nick))
                {
                    logger.Error("You must provide a nickname");
                    return false;
                }
                return true;
            });

            if (args.Length >= 1)
            {
                var listenPort = options.ListenPort;
                if (!int.TryParse(args[0], out listenPort))
                {
                    logger.Error("Could not parse port argument");
                    return;
                }
                options.ListenPort = listenPort;
            }
            else
            {
                var port = prompt.Ask($"Enter a listening port ({options.ListenPort})", int.Parse, p => (p >= 0 && p < short.MaxValue), -1);
                if (port != -1)
                    options.ListenPort = port;
            }

            var context = new ChatContext(logger, options);

            using (context)
            {
                var quit = false;
                context.SetupListener();
                context.Listen();
                logger.Info($"Listening on {context.Listener.LocalEndPoint}");

                context.ConnectionRequested += (s, e) =>
                {
                    e.Accept = prompt.Ask($"Connection request from {e.Nickname} ({e.EndPoint}), accept? [Yn]", answer => answer != "n", _ => true, true);
                };
                context.MessageReceived += (s, e) => logger.Info($"[{e.Time.ToShortTimeString()}] {e.Nickname}: {e.Message}");
                context.MessageSent += (s, e) => logger.Info($"[{e.Time.ToShortTimeString()}] {options.Nickname}: {e.Message}");

                while (!quit)
                {
                    var inputargs = prompt.Ask($"{options.Nickname}$", s => !string.IsNullOrWhiteSpace(s)).Split(' ');
                    if (inputargs.Length == 0)
                        continue;

                    try
                    {
                        switch (inputargs[0])
                        {
                            case "status":
                                ShowStatus(context);
                                break;

                            case "connect":
                                if (context.Status == ContextStatus.Connected)
                                    throw new Exception("Already connected");

                                if (inputargs.Length < 2)
                                    throw new Exception("Must specify ip:port argument");

                                var ep = ParseAddress(inputargs[1]);
                                context.Connect(ep);

                                break;

                            case "disconnect":
                                if (context.Status != ContextStatus.Connected)
                                    throw new Exception("Not connected");

                                context.Disconnect();
                                break;

                            case "send":
                                if (context.Status != ContextStatus.Connected)
                                    throw new Exception("Not connected");

                                if (inputargs.Length < 2)
                                    throw new Exception("Must provide text to send");

                                context.Send(string.Join(" ", inputargs));
                                break;

                            case "help":
                                ShowHelp();
                                break;

                            case "quit":
                                quit = true;
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e.Message);
                    }
                }
            }
        }

        private static IPEndPoint ParseAddress(string addr)
        {
            var parts = addr.Split(':');
            if (parts.Length != 2)
                throw new FormatException("Not a valid endpoint");

            var ip = IPAddress.Parse(parts[0]);
            var port = int.Parse(parts[1]);

            return new IPEndPoint(ip, port);
        }

        private static void ShowHelp()
        {
            Console.WriteLine("connect [address:port]");
            Console.WriteLine("disconnect");
            Console.WriteLine("send [message]");
            Console.WriteLine("status");
            Console.WriteLine("help");
            Console.WriteLine("quit");
        }

        private static void ShowStatus(ChatContext context)
        {
            context.Logger.Info($"Status: {context.Status}");
        }

    }
}
