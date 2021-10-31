using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MockBackend_Core.Models
{
    public class StartupArgsModel
    {
        public string? CollectionPath { get; set; }
        public int? HttpPort { get; set; }
        public int? HttpsPort { get; set; }
    }
}
