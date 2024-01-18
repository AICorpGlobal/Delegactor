// Licensed to the AiCorp- Buyconn.

using Delegactor.Models;

namespace Delegactor.Transport
{
    public static class NodeManagerUtils
    {
        public static int GetPartitionNumber(ActorNodeInfo nodeInfo, ActorClusterInfo clusterInfo)
        {
            //buggy needs to fix this to scale further

            var partitionNumber = nodeInfo.PartitionNumber.GetValueOrDefault(0);

            var nodeBundle = nodeInfo.NodeRole == ConstantKeys.PartitionKey
                ? partitionNumber
                : partitionNumber % clusterInfo.PartitionsNodes;

            return nodeBundle;
        }

        // public int ComputeKey(ActorClusterInfo clusterInfo, string argKey)
        // {
        //     if (clusterInfo.PartitionsNodes == 0)
        //         return clusterInfo.PartitionsNodes;
        //
        //     return argKey.ToCharArray().Sum(x => x * 7) % clusterInfo.PartitionsNodes;
        // }

        public static int ComputePartitionNumber(ActorClusterInfo clusterInfo, string partitionType, string argKey)
        {
            if (clusterInfo.PartitionsNodes == 0)
            {
                return clusterInfo.PartitionsNodes;
            }

            var hashBase = argKey.ToCharArray().Sum(x => x * 7);

            if (partitionType == ConstantKeys.PartitionKey)
            {
                return hashBase % clusterInfo.PartitionsNodes;
            }

            return hashBase % clusterInfo.ReplicaNodes;
        }
    }
}
