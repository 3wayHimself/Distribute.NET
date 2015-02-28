using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Lidgren.Network;

namespace Master
{
    public enum SlaveStatus
    {
        Idle,
        Running
    }

    public class Slave
    {
        public NetConnection Connection;
        public string Name;
        public SlaveStatus Status;

        NetServer server;

        public Slave(NetConnection connection, string name, NetServer server)
        {
            Connection = connection;
            Name = name;

            this.server = server;
        }

        public void SendProgram(string prgm)
        {
            Status = SlaveStatus.Running;

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write("prgm");
            msg.Write(prgm);
            server.SendMessage(msg, Connection, NetDeliveryMethod.ReliableOrdered);
        }
    }
}
