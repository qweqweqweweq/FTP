using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Models
{
    public class Commands
    {
        public int id {  get; set; }
        public int userId { get; set; }
        public string command {  get; set; }
        public Commands() { }
    }
}
