using System.Collections.Generic;

namespace PurgeBot
{
    public class Mapping
    {
        public Mapping()
        {
            Indices = new List<string>();
            Types = new List<string>();
        }
        public List<string> Indices { get; set; }
        public List<string> Types { get; set; }
    }
}