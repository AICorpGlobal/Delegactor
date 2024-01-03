// Licensed to the AiCorp- Buyconn.

using Delegactor.Interfaces;
using Delegactor.Models;

namespace Delegactor.Core
{
    public interface IActorClusterManager
    {
        Task<ActorClusterInfo> RefreshActorSystemClusterDetails();
        Task<ActorClusterInfo> RefreshActorClientClusterDetails();
    }

    public class ActorClusterManager : IActorClusterManager
    {
        private readonly ActorClusterInfo _actorClusterInfo;
        private readonly IStorage _store;


        public ActorClusterManager(IStorage store, ActorClusterInfo clusterInfo)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _actorClusterInfo = clusterInfo ?? throw new ArgumentNullException(nameof(clusterInfo));
        }

        public async Task<ActorClusterInfo> RefreshActorSystemClusterDetails()
        {
            var clusterInfo = await _store.GetClusterInfo(_actorClusterInfo.ClusterName);

            clusterInfo = clusterInfo ?? await _store.UpsertClusterInfo(_actorClusterInfo);

            var stats = await _store.GetCountNodes(_actorClusterInfo);

            var n = clusterInfo.ReplicaNodes;
            var m = clusterInfo.PartitionsNodes;

            if (m * (m + 1) / 2 == stats.partitionsCheckSum
                && n * (n + 1) / 2 / 3 == stats.replicasChecksum
                && (clusterInfo.PartitionsNodes != _actorClusterInfo.PartitionsNodes
                    || clusterInfo.ReplicaNodes != _actorClusterInfo.ReplicaNodes))
            {
                _actorClusterInfo.ClusterState = "re-partitioning";
                _actorClusterInfo.PartitionsNodes = clusterInfo.PartitionsNodes;
                _actorClusterInfo.ReplicaNodes = clusterInfo.ReplicaNodes;
                _actorClusterInfo.Signature = clusterInfo.Signature;
                _actorClusterInfo.EphemeralPortStart = clusterInfo.EphemeralPortStart;
                _actorClusterInfo.EphemeralPortEnd = clusterInfo.EphemeralPortEnd;
                // trigger repartitioning 
                await _store.UpsertClusterInfo(_actorClusterInfo);
                return _actorClusterInfo;
            }

            _actorClusterInfo.ClusterState = "operational";
            await _store.UpsertClusterInfo(_actorClusterInfo);
            return clusterInfo;
        }

        public async Task<ActorClusterInfo> RefreshActorClientClusterDetails()
        {
            var clusterInfo = await _store.GetClusterInfo(_actorClusterInfo.ClusterName);
            clusterInfo = _actorClusterInfo.Set(clusterInfo);

            return clusterInfo;
        }
    }
}
