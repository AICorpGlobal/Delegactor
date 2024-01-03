// Licensed to the AiCorp- Buyconn.

using System.Text.Json.Serialization;
using MessagePack;

namespace Delegactor.Models
{
    [MessagePackObject()]
    public class ActorRequest
    {
        public ActorRequest()
        {
            Headers = new Dictionary<string, object>();
        }

        [Key(0)]
        public string CorrelationId { get; set; }
        
        [Key(1)]
        public string Name { get; set; }
        
        [Key(2)]
        public string Module { get; set; }
        
        [Key(3)]
        public Dictionary<string, string> Parameters { get; set; }
        
        [Key(4)]
        public Dictionary<string, object> Headers { get; set; }

        
        [Key(5)]
        public int Partition
        {
            get;
            set;
        } = 0;

        
        [Key(6)]
        public string ActorId { get; set; }
        
        [Key(7)]
        public string PartitionType { get; set; }

        
        [IgnoreMember]
        [JsonIgnore]
        public string Uid { get => $"{Module}-+-{ActorId}"; }
        
        [IgnoreMember]
        [JsonIgnore]
        public bool IsNotityRequest { get => Headers.ContainsKey("requestType") && Headers["requestType"].ToString() == "notify"; }
    }
}
