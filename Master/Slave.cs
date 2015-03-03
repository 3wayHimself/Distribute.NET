using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Lidgren.Network;
using System.IO;

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

            outMsg.Write(task.Arguments.Count);

            if (task.Arguments.Count > 0)
            {
                foreach (var arg in task.Arguments.OrderBy(k => k.Key).Select(k => k.Value))
                {
                    object value = arg.GetValue();
                    Type type = arg.GetValueType();

                    outMsg.Write(type.Name);

                    if (type == typeof(int))
                        outMsg.Write((int)value);
                    else if (type == typeof(string))
                        outMsg.Write((string)value);
                    else if (type == typeof(bool))
                        outMsg.Write((bool)value);
                }
            }

            outMsg.Write(File.ReadAllText(task.CodePath));
            outMsg.Write(task.CodePath);
            server.SendMessage(outMsg, Connection, NetDeliveryMethod.ReliableOrdered);
        }

        public void Free()
        {
            Status = SlaveStatus.Idle;
            RunningTask = null;
        }
    }
}
