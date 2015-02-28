using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slave
{
    public static class Command
    {
        static Dictionary<string, Action<string[]>> commands;

        static Command()
        {
            commands = new Dictionary<string, Action<string[]>>();
        }

        public static void Register(string name, Action<string[]> action)
        {
            if (commands.ContainsKey(name))
                throw new ArgumentException("Command already registered: " + name);

            commands.Add(name, action);
        }

        public static bool Run(string name, string[] args)
        {
            if (!commands.ContainsKey(name))
                return false;

            commands[name](args);
            return true;
        }
    }
}
