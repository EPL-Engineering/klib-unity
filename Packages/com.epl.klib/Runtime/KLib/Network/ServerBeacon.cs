using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace KLibU.Net
{
    // -------------------------------------------------------------------------
    // ServerBeacon
    // Data carried in each UDP discovery broadcast. Serialized as JSON.
    // -------------------------------------------------------------------------

    public class ServerBeacon
    {
        /// <summary>Unique name identifying this server (e.g. "SERVER_A").</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>IP address of the server's TCP service.</summary>
        [JsonProperty("addr")]
        public string Address { get; set; }

        /// <summary>Port of the server's TCP service.</summary>
        [JsonProperty("port")]
        public int TcpPort { get; set; }

        /// <summary>Protocol version. Increment if the beacon format changes.</summary>
        [JsonProperty("ver")]
        public int Version { get; set; } = 1;
    }

}
