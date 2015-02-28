using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Lidgren.Network;

namespace Master
{
    class Program
    {
        static NetServer server;

        static List<Slave> slaves;

        public static void Main(string[] args)
        {
            bool running = true;

            slaves = new List<Slave>();

            Console.WriteLine("Distribute.NET Master - 1.0");

            NetPeerConfiguration cfg = new NetPeerConfiguration("Distribute.NET");
            cfg.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            cfg.Port = 6969;

            server = new NetServer(cfg);
            server.RegisterReceivedCallback(new SendOrPostCallback(MessageReceived), new SynchronizationContext());
            server.Start();
            Console.WriteLine("Server started and listening");

            Command.Register("close", new Action<string[]>(a => running = false));
            Command.Register("conns", new Action<string[]>(a =>
            {
                int index = 1;
                foreach (var connection in server.Connections)
                {
                    Console.WriteLine("{0}\t{1}\t{2}", index, connection.RemoteEndPoint.ToString(), connection.AverageRoundtripTime);
                    index += 1;
                }
            }));
            Command.Register("discover", new Action<string[]>(a => DiscoverSlaves(a.Length > 0 && a[0] == "clr")));
            Command.Register("broadcast", new Action<string[]>(a =>
            {
                if (a.Length < 0)
                    return;

                string msg = a[0];
                NetOutgoingMessage outMsg = server.CreateMessage(msg.Length);
                outMsg.Write(msg);
                List<NetConnection> slaveConnections = slaves.Select(s => s.Connection).ToList();
                server.SendMessage(outMsg, slaveConnections, NetDeliveryMethod.ReliableOrdered, 0);
            }));
            Command.Register("slaves", new Action<string[]>(a =>
            {
                int index = 0;
                foreach (var slave in slaves)
                {
                    Console.WriteLine("{0}\t{1}\t{2}", index, slave.Connection.RemoteEndPoint, slave.Name);
                }
            }));
            Command.Register("disconnect", new Action<string[]>(a =>
            {
                if (a.Length < 1)
                    return;

                int index;
                if (!Int32.TryParse(a[0], out index))
                    return;

                slaves[index].Connection.Disconnect("bye");
                slaves.RemoveAt(index);
            }));

            string line;
            while (running)
            {
                line = Console.ReadLine();

                string[] lineArgs = line.Split(' ');
                if (!Command.Run(lineArgs[0], lineArgs.Skip(1).ToArray()))
                    Console.WriteLine("Invalid command");
            }

            Console.WriteLine("Closing...");

            server.Shutdown("bye");
        }

        static void MessageReceived(object peerObj)
        {
            NetPeer peer = (NetPeer)peerObj;
            NetIncomingMessage inc = peer.ReadMessage();

            switch (inc.MessageType)
            {
                case NetIncomingMessageType.DebugMessage:
                    Console.WriteLine("Lidgren debug: {0}", inc.ReadString());
                    break;

                case NetIncomingMessageType.WarningMessage:
                    Console.WriteLine("Lidgren warning: {0}", inc.ReadString());
                    break;

                case NetIncomingMessageType.ErrorMessage:
                    Console.WriteLine("Lidgren error: {0}", inc.ReadString());
                    break;

                case NetIncomingMessageType.Data:
                    Console.WriteLine("Incoming message from {0}: {1}", inc.SenderEndPoint.ToString(), inc.ReadString());
                    break;

                case NetIncomingMessageType.DiscoveryRequest:
                    Console.WriteLine("Discovery request from {0}", inc.SenderEndPoint.ToString());

                    NetOutgoingMessage outMsg = server.CreateMessage();
                    outMsg.Write("master");
                    server.SendDiscoveryResponse(outMsg, inc.SenderEndPoint);

                    break;

                case NetIncomingMessageType.DiscoveryResponse:
                    string str = inc.ReadString();
                    //Console.WriteLine("Response: {0}: {1}", inc.SenderEndPoint.ToString(), name);

                    string[] nameArgs = str.Split(' ');

                    if (nameArgs[0] == "slave")
                    {
                        string name = nameArgs[1];
                        if (slaves.Count(s => s.Connection.RemoteEndPoint == inc.SenderEndPoint && s.Name == name) > 0)
                        {
                            Console.WriteLine("Slave already registered: {0}, {1}", inc.SenderEndPoint, name);
                            break;
                        }

                        NetConnection connection = server.Connect(inc.SenderEndPoint); 

                        Slave slave = new Slave(connection, name);
                        slaves.Add(slave);
                        Console.WriteLine("Slave registered: {0}, {1}", inc.SenderEndPoint, name);
                    }

                    break;
            }
        }

        static void DiscoverSlaves(bool clear)
        {
            if (clear)
                slaves.Clear();

            server.DiscoverLocalPeers(6969);
        }
    }
}
