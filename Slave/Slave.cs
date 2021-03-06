﻿using System;
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

            name = File.ReadAllText("slavename.txt").Trim();

            mondState = new MondState();
            mondState["assert"] = new MondFunction((_, a) =>
            {
                if (a[0] == MondValue.False)
                {
                    string error = "Assertion failed";
                    if (a.Length > 1)
                    {
                        if (a[1] != MondValue.Null)
                            error = a[1].Serialize();
                    }

                    throw new MondRuntimeException(error);
                }
                return MondValue.Null;
            });

            Console.WriteLine("Distribute.NET Slave - 1.0: {0}", name);

            NetPeerConfiguration cfg = new NetPeerConfiguration("Distribute.NET");
            cfg.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            cfg.Port = 6969;

            client = new NetClient(cfg);
            client.RegisterReceivedCallback(new SendOrPostCallback(MessageReceived), new SynchronizationContext());
            client.Start();

            Console.WriteLine("Client started, finding master...");

            client.DiscoverLocalPeers(6969);

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
            NetOutgoingMessage outMsg = client.CreateMessage();

            switch (inc.MessageType)
            {
                case NetIncomingMessageType.DebugMessage:
                case NetIncomingMessageType.VerboseDebugMessage:
                case NetIncomingMessageType.WarningMessage:
                case NetIncomingMessageType.ErrorMessage:
                    Console.WriteLine("Lidgren: {0}", inc.ReadString());
                    return;

                case NetIncomingMessageType.Data:

                    //Console.WriteLine("Message received");
                    string command = inc.ReadString();

                    if (command == "prgm")
                    {
                        outMsg.Write("result");

                        List<MondValue> args = new List<MondValue>();
                        int argCount = inc.ReadInt32();

                        if (argCount > 0)
                        {
                            for (var i = 0; i < argCount; i++)
                            {
                                string argType = inc.ReadString();
                                Console.WriteLine(argType);

                                switch (argType)
                                {
                                    case "Int32":
                                        args.Add(new MondValue(inc.ReadInt32()));
                                        break;

                                    case "String":
                                        args.Add(new MondValue(inc.ReadString()));
                                        break;

                                    case "Boolean":
                                        args.Add(new MondValue(inc.ReadBoolean() ? 1 : 0));
                                        break;

                                    default:
                                        Console.WriteLine("Invalid argument type: {0}", argType);
                                        break;
                                }
                            }
                        }

                        string prgm = inc.ReadString();
                        string file = inc.ReadString();

                        Console.WriteLine("Program received. Args: {0}", argCount);

                        MondValue func = null;
                        try
                        {
                            func = mondState.Run(prgm, file);
                        }
                        catch (MondCompilerException ex)
                        {
                            Console.WriteLine("MondCompilerException: {0}", ex.Message);
                            if (ex.InnerException != null)
                                Console.WriteLine("Inner ({0}): {1}", ex.GetType().Name, ex.InnerException.Message);

                            outMsg.Write("error");
                            outMsg.Write(ex.Message);
                            client.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered);

                            break;
                        }

                        if (func.Type != MondValueType.Function)
                        {
                            Console.WriteLine("Invalid return type: {0} ({1})", func.Type, func.Serialize());

                            outMsg.Write("error");
                            outMsg.Write("wrong return type; expected function");
                            client.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered);

                            break;
                        }

                        string result;
                        string type;
                        try
                        {
                            MondValue res = mondState.Call(func, args.ToArray());
                            result = res.Serialize();
                            type = res.Type.GetName();
                            //Console.WriteLine(type);
                        }
                        catch (MondRuntimeException ex)
                        {
                            Console.WriteLine("MondRuntimeException: {0}", ex.Message);
                            if (ex.InnerException != null)
                                Console.WriteLine("Inner ({0}): {1}", ex.GetType().Name, ex.InnerException.Message);

                            outMsg.Write("error");
                            outMsg.Write(ex.Message);
                            client.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered);

                            break;
                        }

                        Console.WriteLine("Done. Result: {0}", result);

                        outMsg.Write(result);
                        outMsg.Write(type);

                        client.SendMessage(outMsg, NetDeliveryMethod.ReliableOrdered);
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

                        outMsg.Write("slave");
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
