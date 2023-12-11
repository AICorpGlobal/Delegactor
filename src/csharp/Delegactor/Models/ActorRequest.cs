// Licensed to the AiCorp- Buyconn.

using System.Text.Json.Serialization;

namespace Delegactor.Models
{
    public class ActorRequest
    {
        public ActorRequest()
        {
            Headers = new Dictionary<string, object>();
        }

        public string CorrelationId { get; set; }
        public string Name { get; set; }
        public string Module { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public Dictionary<string, object> Headers { get; set; }

        public int Partition
        {
            get;
            set;
        } = 0;

        public string ActorId { get; set; }
        public string PartitionType { get; set; }

        public string Uid { get => $"{Module}-+-{ActorId}"; }
        
        [JsonIgnore]
        public bool IsNotityRequest { get => Headers.ContainsKey("requestType") && Headers["requestType"].ToString() == "notify"; }
    }
}
