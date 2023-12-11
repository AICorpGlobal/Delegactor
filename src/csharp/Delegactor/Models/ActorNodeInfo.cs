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
        }

        public ActorNodeInfo()
        {
        }

        public string NodeRole { get; set; }

        public string InstanceId { get; set; }
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
