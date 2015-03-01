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
        static List<Task> queue;
        static List<Task> tasks;
        //static List<Program> prgms;

        public static void Main(string[] args)
        {
            bool running = true;

            slaves = new List<Slave>();
            queue = new List<Task>();
            tasks = new List<Task>();
            //prgms = new List<Program>();

            Console.WriteLine("Distribute.NET Master - 1.0");

            NetPeerConfiguration cfg = new NetPeerConfiguration("Distribute.NET");
            cfg.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            cfg.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
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

                string command = a[0];

                if (command == "create")
                {
                    if (a.Length < 4)
                        return;

                    string packName = a[1];

                    Program pack = new Program();
                    pack.Name = packName;

                    foreach (var prgm in a.Skip(2))
                    {
                        if (File.Exists(prgm))
                            pack.AddTask(File.ReadAllText(prgm));
                    }

                    File.WriteAllText(packName + ".prgm", pack.Serialize());

                    Console.WriteLine("Program created: {0}.prgm", packName);
                }
                else if (command == "run")
                {
                    string packName = a[1];

                    if (!File.Exists(packName))
                        return;

                    Program prgm = JsonConvert.DeserializeObject<Program>(File.ReadAllText(packName));
                    prgm.AfterDeserialize();

                    Console.WriteLine("Running program: {0}", prgm.Name);

                    RunProgram(prgm);
                }
            }));
            #endregion

            //DiscoverSlaves(false);

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

            string data;
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
                    data = inc.ReadString();
                    Console.WriteLine(data);

                    if (data == "result")
                    {
                        Task task = tasks.Find(t => t.Assignee.Connection == inc.SenderConnection);
                        task.Assignee.Free();
                        tasks.Remove(task);

                        string result = inc.ReadString();
                        Console.WriteLine("Result for task #{0} in \"{1}\": {2}", task.Index(), task.ParentProgram.Name, result);

                        CheckQueue();
                    }
                    break;

                case NetIncomingMessageType.DiscoveryRequest:
                    Console.WriteLine("Discovery request from {0}", inc.SenderEndPoint.ToString());

                    NetOutgoingMessage outMsg = server.CreateMessage("master");
                    server.SendDiscoveryResponse(outMsg, inc.SenderEndPoint);

                    break;

                case NetIncomingMessageType.StatusChanged:
                    Console.WriteLine("Status of {0}: {1}", inc.SenderEndPoint.ToString(), ((NetConnectionStatus)inc.ReadByte()).ToString());
                    break;

                case NetIncomingMessageType.ConnectionApproval:
                    Console.WriteLine("Connection approval: {0}", inc.SenderEndPoint);

                    data = inc.ReadString();

                    if (data == "slave")
                    {
                        string slaveName = inc.ReadString();

                        Console.WriteLine(slaveName);

                        //inc.SenderConnection.Approve();
                    }
                    else
                        inc.SenderConnection.Deny("Not a slave");

                    inc.SenderConnection.Approve();
                    break;
            }
        }

        static void DiscoverSlaves(bool clear)
        {
            if (clear)
                slaves.Clear();

            server.DiscoverLocalPeers(6969);
        }

        static void RunProgram(Program prgm)
        {
            //prgms.Add(prgm);
            queue.AddRange(prgm.Tasks);
            CheckQueue();
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
            {
                Console.WriteLine("No idle slaves left. Queued tasks: {0}", queue.Count);
                return;
            }

            while (count > 0)
            {
                if (queue.Count == 0)
                {
                    Console.WriteLine("Queue emptied, idle slaves: {0}", count);
                    break;
                }

                Slave idle = idleSlaves[0];
                Task task = queue[0];

                Console.WriteLine("Sending task (#{0} in \"{1}\") to idle slave: {2} ({3})", task.Index(), task.ParentProgram.Name,
                    idle.Name, idle.Connection.RemoteEndPoint);

                idle.SendTask(task);
                tasks.Add(task);

                idleSlaves.RemoveAt(0);
                queue.RemoveAt(0);

                count -= 1;
            }
        }
    }
}
