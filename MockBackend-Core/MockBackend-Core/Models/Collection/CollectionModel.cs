using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MockBackend_Core.Models.Collection
{
    public class CollectionModel
    {
        public string Name { get; set; } = "Unamed Collection";
        public string EndPoint { get; set; } = "*";
        public int HttpPort { get; set; } = 0;
        public int HttpsPort { get; set; } = 0;
        public List<ControllerModel> Controllers { get; set; } = new();
    }
}
