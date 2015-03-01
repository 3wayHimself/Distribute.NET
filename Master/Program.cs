using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Master
{
    public class Program
    {
        public string Name;
        public List<Task> Tasks;

        JsonSerializerSettings settings;

        public Program()
        {
            settings = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All };

            Tasks = new List<Task>();
        }

        public void AddTask(string code)
        {
            Tasks.Add(new Task(this, code));
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, settings);
        }

        public void AfterDeserialize()
        {
            foreach (var task in Tasks)
                task.ParentProgram = this;
        }
    }
}
