using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Master
{
    public class Task
    {
        [JsonIgnore]
        public Program ParentProgram;
        public string CodePath;
        [JsonIgnore]
        public List<int> Arguments; // TODO: support more than just int
        [JsonIgnore]
        public Slave Assignee;

        public Task(Program parent, string code)
        {
            ParentProgram = parent;
            CodePath = code;

            Arguments = new List<int>();
        }

        public int Index()
        {
            return ParentProgram.Tasks.IndexOf(this);
        }
    }
}
