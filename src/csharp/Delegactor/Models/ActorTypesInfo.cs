// Licensed to the AiCorp- Buyconn.

namespace Delegactor.Models
{
    public class ActorTypesInfo
    {
        public ActorTypesInfo(ActorNodeInfo nodeInfo)
        {
        }

        public string ClusterName { get; set; }

        public string ActorModuleName { get; set; }

        public int NodeOffset { get; set; }

        public int MinNumberOfPartitions { get; set; }
        public int MaxNumberOfPartitions { get; set; }

        public int Replicas { get; set; }
        public string HashFunction { get; set; }
    }
}
