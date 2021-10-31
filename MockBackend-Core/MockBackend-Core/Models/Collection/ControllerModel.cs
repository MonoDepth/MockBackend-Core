using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MockBackend_Core.Models.Collection
{
    public class ControllerModel
    {
        public string Path { get; set; } = "";
        public string Method { get; set; } = "";
        public int Status { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public string ContentType { get; set; } = "";        
        public string Body { get; set; } = "";
    }
}
