// Licensed to the AiCorp- Buyconn.

using Delegactor.Models;

namespace Delegactor.Interfaces
{
    public interface IStorage
    {
        Task<ActorNodeInfo> UpsertNodeInfo(ActorNodeInfo nodeInfo);
        Task<ActorNodeInfo> GetNodeInfo(string instanceId);
        Task UpsertTypesInfo(ActorTypesInfo typesInfo);
        Task<ActorClusterInfo> GetClusterInfo(string clusterName);
        Task<ActorClusterInfo> UpsertClusterInfo(ActorClusterInfo clusterInfo);
        Task<ActorNodeInfo> GetNodeSlotFromCluster(ActorNodeInfo actorNodeInfo, ActorClusterInfo clusterInfo);

        Task<(int? partitionsCheckSum, int? replicasChecksum)> GetCountNodes(ActorClusterInfo clusterInfo);
        Task<ActorNodeInfo?> GetNodeInfo(ActorClusterInfo clusterInfo, int partitionNumber, string nodeRole);
    }
}
