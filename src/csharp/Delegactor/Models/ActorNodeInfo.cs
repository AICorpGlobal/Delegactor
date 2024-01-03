// Licensed to the AiCorp- Buyconn.

using Delegactor.Core;

namespace Delegactor.Models
{
    public class ActorNodeInfo
    {
        public ActorNodeInfo(ActorNodeInfo actorNodeInfo)
        {
            NodeRole = actorNodeInfo.NodeRole;
            InstanceId = actorNodeInfo.InstanceId;
            PartitionNumber = actorNodeInfo.PartitionNumber;
            ClusterName = actorNodeInfo.ClusterName;
            LastUpdateTime = actorNodeInfo.LastUpdateTime;
            NodeType = actorNodeInfo.NodeType;
            Signature = actorNodeInfo.Signature;
            NodeState = actorNodeInfo.NodeState;
            IpAddress = actorNodeInfo.IpAddress;
            Port = actorNodeInfo.Port;
        }

        public ActorNodeInfo()
        {
        }

        public string NodeRole { get; set; }

        public string InstanceId { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int? PartitionNumber { get; set; }
        public string ClusterName { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public string NodeType { get; set; }
        public string NodeState { get; set; }
        public string Signature { get; set; }


        public ActorNodeInfo Set(ActorNodeInfo actorNodeInfo)
        {
            NodeRole = actorNodeInfo.NodeRole;
            InstanceId = actorNodeInfo.InstanceId;
            PartitionNumber = actorNodeInfo.PartitionNumber;
            ClusterName = actorNodeInfo.ClusterName;
            LastUpdateTime = actorNodeInfo.LastUpdateTime;
            NodeType = actorNodeInfo.NodeType;
            NodeState = actorNodeInfo.NodeState;
            Signature = actorNodeInfo.Signature;
            IpAddress = actorNodeInfo.IpAddress;
            Port = actorNodeInfo.Port;
            return this;
        }

        public int GetSubscriptionId(ActorClusterInfo clusterInfo)
        {
            //buggy needs to fix this to scale further

            var partitionNumber = PartitionNumber.GetValueOrDefault(0);

            int nodeBundle = NodeRole == "partition"
                ? partitionNumber
                : (partitionNumber % clusterInfo.PartitionsNodes);

            return nodeBundle;
        }
    }
}
