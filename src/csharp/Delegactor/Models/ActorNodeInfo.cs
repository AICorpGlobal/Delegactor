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

        public int GetListenerId(ActorClusterInfo clusterInfo)
        {
            if (NodeType == nameof(ActorClient))
            {
                return 0;
            }

            var nodeBundle = NodeRole == "partition"
                ? clusterInfo.PartitionsNodes
                : clusterInfo.ReplicaNodes / clusterInfo.PartitionsNodes - 1;
            return PartitionNumber!.Value % nodeBundle;
        }
    }
}
