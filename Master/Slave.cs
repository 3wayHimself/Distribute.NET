using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Lidgren.Network;

namespace Master
{
    public class Slave
    {
        public NetConnection Connection;
        public string Name;

        public Slave(NetConnection connection, string name)
        {
            Connection = connection;
            Name = name;
        }
    }
}
