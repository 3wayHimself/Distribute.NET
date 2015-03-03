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
        public List<int> WantedTaskOutputs;
        [JsonIgnore]
        public Dictionary<int, IValue> Arguments;
        [JsonIgnore]
        public Slave Assignee;

        public Task(Program parent, string code)
        {
            ParentProgram = parent;
            CodePath = code;

            WantedTaskOutputs = new List<int>();
            Arguments = new Dictionary<int, IValue>();
        }

        public int Index()
        {
            return ParentProgram.Tasks.IndexOf(this);
        }

        public bool CanRun()
        {
            if (WantedTaskOutputs.Count == 0)
                return true;

            return Arguments.Count >= WantedTaskOutputs.Count;
        }
    }
}
