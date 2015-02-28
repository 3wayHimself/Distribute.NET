using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Master
{
    public class Pack
    {
        public string Name;
        public List<string> Programs;

        JsonSerializerSettings settings;

        public Pack()
        {
            settings = new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All };

            Programs = new List<string>();
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, settings);
        }
    }
}
