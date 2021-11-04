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
        public int Status { get; set; } = 200;
        public int Delay { get; set; } = 0;
        public Dictionary<string, string> Headers { get; set; } = new();
        public string ContentType { get; set; } = "application/text";
        public string Body { get; set; } = "";
        public string BodyFile { get; set; } = "";
    }
}
