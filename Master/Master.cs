using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Lidgren.Network;
using Newtonsoft.Json;

namespace Master
{
    class Master
    {
        static NetServer server;
        static List<Slave> slaves;
        static List<string> queue;

        public static void Main(string[] args)
        {
            bool running = true;

            slaves = new List<Slave>();
            queue = new List<string>();

            Console.WriteLine("Distribute.NET Master - 1.0");

            NetPeerConfiguration cfg = new NetPeerConfiguration("Distribute.NET");
            cfg.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            cfg.Port = 6969;

            server = new NetServer(cfg);
            server.RegisterReceivedCallback(new SendOrPostCallback(MessageReceived), new SynchronizationContext());
            server.Start();
            Console.WriteLine("Server started and listening");

            #region Commands
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
            Command.Register("prgm", new Action<string[]>(a =>
            {
                if (a.Length < 2)
                    return;

                int index;
                if (!Int32.TryParse(a[0], out index))
                    return;

                string prgm = String.Join(" ", a.Skip(1));
                Console.WriteLine(prgm);
                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write("prgm");
                msg.Write(prgm);
                server.SendMessage(msg, slaves[index].Connection, NetDeliveryMethod.ReliableOrdered);

            }));
            Command.Register("pack", new Action<string[]>(a =>
            {
                if (a.Length < 2)
                    return;

                string command = a[0];

                if (command == "create")
                {
                    if (a.Length < 4)
                        return;

                    string packName = a[1];

                    Pack pack = new Pack();
                    pack.Name = packName;

                    foreach (var prgm in a.Skip(2))
                    {
                        if (File.Exists(prgm))
                            pack.Programs.Add(prgm);
                    }

                    File.WriteAllText(packName + ".pack", pack.Serialize());

                    Console.WriteLine("Pack created: {0}.pack", packName);
                }
                else if (command == "run")
                {
                    string packName = a[1];

                    if (!File.Exists(packName))
                        return;

                    Pack pack = JsonConvert.DeserializeObject<Pack>(File.ReadAllText(packName));

                    Console.WriteLine("Running pack: {0}", pack.Name);

                    queue.AddRange(pack.Programs.Select(p => File.ReadAllText(p)));
                    CheckQueue();
                }
            }));
            #endregion

            DiscoverSlaves(false);

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
                    //Console.WriteLine("Message received");
                    string data = inc.ReadString();

                    if (data == "result")
                    {
                        slaves.Find(s => s.Connection.RemoteEndPoint == inc.SenderEndPoint).Status = SlaveStatus.Idle;

                        string result = inc.ReadString();
                        Console.WriteLine("Result from {0}: {1}", inc.SenderEndPoint, result);

                        CheckQueue();
                    }
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

                        Slave slave = new Slave(connection, name, server);
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

        static void CheckQueue()
        {
            if (queue.Count < 1)
            {
                Console.WriteLine("Queue empty");
                return;
            }

            List<Slave> idleSlaves = slaves.Where(s => s.Status == SlaveStatus.Idle).ToList();
            int count = idleSlaves.Count;
            if (count < 1)
                return;

            while (count > 0)
            {
                Console.WriteLine("Sending program to idle slave: {0} ({1})", idleSlaves[0].Name, idleSlaves[0].Connection.RemoteEndPoint);
                idleSlaves[0].SendProgram(queue[0]);
                idleSlaves.RemoveAt(0);
                queue.RemoveAt(0);

                count -= 1;
            }
        }
    }
}
