// Licensed to the AiCorp- Buyconn.

namespace Delegactor.Models
{
    public class ActorClusterInfo
    {
        public string ClusterName { get; set; }
        public int HeartBeatWindowInSeconds { get; set; }
        public int PartitionsNodes { get; set; }
        public string ClusterState { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public string Signature { get; set; }
        public int ReplicaNodes { get; set; }

        public int EphemeralPortStart { get; set; }
        public int EphemeralPortEnd { get; set; }

        public ActorClusterInfo Set(ActorClusterInfo clusterInfo)
        {
            ClusterName = clusterInfo.ClusterName;
            HeartBeatWindowInSeconds = clusterInfo.HeartBeatWindowInSeconds;
            PartitionsNodes = clusterInfo.PartitionsNodes;
            ClusterState = clusterInfo.ClusterState;
            LastUpdateTime = clusterInfo.LastUpdateTime;
            Signature = clusterInfo.Signature;
            ReplicaNodes = clusterInfo.ReplicaNodes;
            EphemeralPortStart = clusterInfo.EphemeralPortStart;
            EphemeralPortEnd = clusterInfo.EphemeralPortEnd;
            return this;
        }
    }
}
