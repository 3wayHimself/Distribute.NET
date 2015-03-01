using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Lidgren.Network;
using Mond;

namespace Slave
{
    class Slave
    {
        static NetClient client;
        static string name;
        static MondState mondState;

        public static void Main(string[] args)
        {
            bool running = true;

            if (!File.Exists("slavename.txt"))
            {
                Console.WriteLine("slavename.txt not found");
                Console.ReadKey();
                return;
            }

            name = File.ReadAllText("slavename.txt");

            mondState = new MondState();

            Console.WriteLine("Distribute.NET Slave - 1.0: {0}", name);

            NetPeerConfiguration cfg = new NetPeerConfiguration("Distribute.NET");
            cfg.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            cfg.Port = 6969;

            client = new NetClient(cfg);
            client.RegisterReceivedCallback(new SendOrPostCallback(MessageReceived), new SynchronizationContext());
            client.Start();

            Console.WriteLine("Server started");

            Command.Register("close", new Action<string[]>((a) => running = false));
            Command.Register("conns", new Action<string[]>((a) =>
            {
                int index = 0;
                foreach (var connection in client.Connections)
                {
                    Console.WriteLine("{0}\t{1}\t{2}", index, connection.RemoteEndPoint.ToString(), connection.AverageRoundtripTime);
                    index += 1;
                }
            }));
            Command.Register("discover", new Action<string[]>((a) => client.DiscoverLocalPeers(6969)));

            string line;
            while (running)
            {
                line = Console.ReadLine();

                string[] lineArgs = line.Split(' ');
                if (!Command.Run(lineArgs[0], lineArgs.Skip(1).ToArray()))
                    Console.WriteLine("Invalid command");
            }

            Console.WriteLine("Closing...");

            client.Shutdown("bye");
        }

        static void MessageReceived(object peerObj)
        {
            NetPeer peer = (NetPeer)peerObj;
            NetIncomingMessage inc = peer.ReadMessage();
            NetOutgoingMessage outMsg;

            switch (inc.MessageType)
            {
                case NetIncomingMessageType.DebugMessage:
                case NetIncomingMessageType.VerboseDebugMessage:
                case NetIncomingMessageType.WarningMessage:
                case NetIncomingMessageType.ErrorMessage:
                    Console.WriteLine("Lidgren: {0}", inc.ReadString());
                    break;

                case NetIncomingMessageType.Data:
                    //Console.WriteLine("Message received");
                    string command = inc.ReadString();

                    if (command == "prgm")
                    {
                        string prgm = inc.ReadString();
                        Console.WriteLine("Program received; running: {0}", prgm);
                        string result = mondState.Run(prgm).Serialize();
                        Console.WriteLine("Done. Result: {0}", result);

                        outMsg = client.CreateMessage();
                        outMsg.Write("result");
                        outMsg.Write(result);
                        client.SendMessage(outMsg, inc.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                    }

                    break;

                //case NetIncomingMessageType.DiscoveryRequest:
                //    Console.WriteLine("Discovery request from {0}", inc.SenderEndPoint.ToString());

                //    outMsg = client.CreateMessage("slave");
                //    outMsg.Write(name);
                //    client.SendDiscoveryResponse(outMsg, inc.SenderEndPoint);

                //    break;

                case NetIncomingMessageType.DiscoveryResponse:
                    string peerName = inc.ReadString();

                    if (peerName == "master")
                    {
                        Console.WriteLine("Found master at {0}", inc.SenderEndPoint);

                        outMsg = client.CreateMessage("slave");
                        outMsg.Write(name);
                        client.Connect(inc.SenderEndPoint, outMsg);
                    }
                    break;

                case NetIncomingMessageType.StatusChanged:
                    Console.WriteLine("Status of {0}: {1}", inc.SenderEndPoint.ToString(), ((NetConnectionStatus)inc.ReadByte()).ToString());

                    string addit = inc.ReadString();
                    if (!String.IsNullOrEmpty(addit))
                        Console.WriteLine("Addit: {0}", addit);

                    break;
            }
        }
    }
}
