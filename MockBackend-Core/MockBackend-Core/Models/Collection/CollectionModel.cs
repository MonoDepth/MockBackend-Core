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
        public string ProxyServerEndpoint { get; set; } = "127.0.0.1";
        public int ProxyServerPort { get; set; } = 0;
        public List<string> DomainsToRelay { get; set; } = new List<string>();
        public List<ControllerModel> Controllers { get; set; } = new();
    }
}
