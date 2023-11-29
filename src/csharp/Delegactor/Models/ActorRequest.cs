// Licensed to the AiCorp- Buyconn.

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
    }
}
