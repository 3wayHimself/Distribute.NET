﻿using System;
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
        public string Code;
        public Slave Assignee;

        public Task(Program parent, string code)
        {
            ParentProgram = parent;
            Code = code;
        }

        public int Index()
        {
            return ParentProgram.Tasks.IndexOf(this);
        }
    }
}