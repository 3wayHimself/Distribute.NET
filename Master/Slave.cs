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
        public Task RunningTask;

        NetServer server;

        public Slave(NetConnection connection, string name, NetServer server)
        {
            Connection = connection;
            Name = name;

            this.server = server;
        }

        public void SendTask(Task task)
        {
            Status = SlaveStatus.Running;
            RunningTask = task;
            task.Assignee = this;

            NetOutgoingMessage outMsg = server.CreateMessage();
            outMsg.Write("prgm");
            outMsg.Write(task.Code);
            server.SendMessage(outMsg, Connection, NetDeliveryMethod.ReliableOrdered);
        }

        public void SetIdle()
        {
            Status = SlaveStatus.Idle;
            RunningTask = null;
        }
    }
}
