// Licensed to the AiCorp- Buyconn.

using Delegactor.Core;
using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Delegactor.Storage.MongoDb
{
    // to simplify networking and get start we use this instead of a better technique https://raft.github.io/
    //https://dotnet.github.io/dotNext/features/cluster/raft.html
    //https://docs.google.com/document/d/e/2PACX-1vQYWpDD6L20CSBR4QTlpP2SJDEKcj6VRP-ZI3t_wQ93c3OS96Wk8ojvAFNo3zwYaiz7VUi5EF34JJhZ/pub
    public class MongoStorage : IStorage
    {
        private readonly IMongoCollection<MongoDocument<ActorTypesInfo>> _actorTypesCollection;
        private readonly IMongoCollection<MongoDocument<ActorClusterInfo>> _clusterCollection;
        private readonly ILogger<MongoStorage> _logger;
        private readonly IMongoCollection<MongoDocument<ActorNodeInfo>> _nodeCollection;
        private bool _seedCompleted;


        public MongoStorage(
            ILogger<MongoStorage> logger,
            IMongoCollection<MongoDocument<ActorNodeInfo>> nodeCollection,
            IMongoCollection<MongoDocument<ActorTypesInfo>> actorTypesCollection,
            IMongoCollection<MongoDocument<ActorClusterInfo>> clusterCollection)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _nodeCollection = nodeCollection ?? throw new ArgumentNullException(nameof(nodeCollection));
            _actorTypesCollection =
                actorTypesCollection ?? throw new ArgumentNullException(nameof(actorTypesCollection));
            _clusterCollection = clusterCollection ?? throw new ArgumentNullException(nameof(clusterCollection));
        }

        public async Task<ActorNodeInfo> UpsertNodeInfo(ActorNodeInfo nodeInfo)
        {
            nodeInfo.Signature = string.IsNullOrEmpty(nodeInfo.Signature)
                ? Guid.NewGuid().ToString()
                : nodeInfo.Signature;

            var oldNodeInfoSignature = nodeInfo.Signature;
            nodeInfo.Signature = Guid.NewGuid().ToString();
            var mongoDocument =
                new MongoDocument<ActorNodeInfo>
                {
                    Entry = nodeInfo,
                    Id = nodeInfo.InstanceId,
                    LastUpdateTime = DateTime.UtcNow,
                    Signature = nodeInfo.Signature
                };

            var findOneAndReplaceOptions =
                new FindOneAndReplaceOptions<MongoDocument<ActorNodeInfo>>
                {
                    ReturnDocument = ReturnDocument.After, IsUpsert = true
                };


            try
            {
                await _nodeCollection.FindOneAndReplaceAsync<MongoDocument<ActorNodeInfo>>(
                    x => x.Id == nodeInfo.InstanceId && x.Signature == oldNodeInfoSignature,
                    mongoDocument, findOneAndReplaceOptions);
            }
            catch (Exception e)
            {
                _logger.LogError("Upsert failed {Exception}", e);
            }

            return nodeInfo;
        }

        public Task<ActorNodeInfo> GetNodeInfo(string instanceId)
        {
            var nodeInfo = _nodeCollection.Find(x =>
                    x.Id == instanceId)
                .Sort(new SortDefinitionBuilder<MongoDocument<ActorNodeInfo>>()
                    .Descending(x =>
                        x.Entry.LastUpdateTime)).FirstOrDefault();

            return Task.FromResult(nodeInfo?.Entry);
        }

        public async Task UpsertTypesInfo(ActorTypesInfo typesInfo)
        {
            var mongoDocument =
                new MongoDocument<ActorTypesInfo>
                {
                    Entry = typesInfo, Id = typesInfo.ActorModuleName, LastUpdateTime = DateTime.UtcNow
                };

            var findOneAndReplaceOptions =
                new FindOneAndReplaceOptions<MongoDocument<ActorTypesInfo>> { IsUpsert = true };

            try
            {
                await _actorTypesCollection.FindOneAndReplaceAsync<MongoDocument<ActorTypesInfo>>(
                    x => x.Id == typesInfo.ActorModuleName,
                    mongoDocument, findOneAndReplaceOptions);
            }
            catch (Exception e)
            {
                _logger.LogError("Upsert failed {Exception}", e);
            }
        }

        public async Task<ActorClusterInfo> UpsertClusterInfo(ActorClusterInfo clusterInfo)
        {
            clusterInfo.Signature = string.IsNullOrEmpty(clusterInfo.Signature)
                ? Guid.NewGuid().ToString()
                : clusterInfo.Signature;

            var oldNodeInfoSignature = clusterInfo.Signature;
            clusterInfo.Signature = Guid.NewGuid().ToString();

            var mongoDocument =
                new MongoDocument<ActorClusterInfo>
                {
                    Entry = clusterInfo,
                    Id = clusterInfo.ClusterName,
                    LastUpdateTime = DateTime.UtcNow,
                    Signature = clusterInfo.Signature
                };

            if (_seedCompleted == false &&
                !_clusterCollection.FindSync(x => x.Id == clusterInfo.ClusterName).ToList().Any())
            {
                _seedCompleted = true;
                await _clusterCollection.InsertOneAsync(mongoDocument);
            }

            var findOneAndReplaceOptions =
                new FindOneAndReplaceOptions<MongoDocument<ActorClusterInfo>> { IsUpsert = false };

            try
            {
                await _clusterCollection.FindOneAndReplaceAsync<MongoDocument<ActorClusterInfo>>(
                    x => x.Id == clusterInfo.ClusterName && x.Signature == oldNodeInfoSignature,
                    mongoDocument, findOneAndReplaceOptions);
            }
            catch (Exception e)
            {
                _logger.LogError("Upsert failed {Exception}", e);
            }

            return clusterInfo;
        }

        public Task<ActorClusterInfo> GetClusterInfo(string clusterName)
        {
            var nodeInfo = _clusterCollection.Find(x =>
                    x.Entry.ClusterName == clusterName)
                .Sort(new SortDefinitionBuilder<MongoDocument<ActorClusterInfo>>()
                    .Descending(x =>
                        x.Entry.LastUpdateTime)).FirstOrDefault();

            return Task.FromResult(nodeInfo?.Entry);
        }

        public async Task<ActorNodeInfo> GetNodeSlotFromCluster(ActorNodeInfo actorNodeInfo,
            ActorClusterInfo clusterInfo)
        {
            var temp = _nodeCollection
                .Find(x =>
                    x.Entry.ClusterName == clusterInfo.ClusterName
                    && x.Entry.NodeType == nameof(ActorSystem)
                    && x.LastUpdateTime >= DateTime.UtcNow
                        .Subtract(TimeSpan.FromSeconds(clusterInfo.HeartBeatWindowInSeconds * 3))).ToList();

            var partitions = temp
                .Where(x => x.Entry.PartitionNumber != null && x.Entry.NodeRole == "partition")
                .OrderBy(x => x.Entry.PartitionNumber).ToList();


            var replicas = temp
                .Where(x => x.Entry.PartitionNumber != null && x.Entry.NodeRole == "replica")
                .OrderBy(x => x.Entry.PartitionNumber).ToList();


            var result = await GetPartitionSlot(actorNodeInfo, clusterInfo, partitions);

            if (result.NodeState != "operational")
            {
                result = await GetReplciaSlot(actorNodeInfo, clusterInfo, replicas);
            }

            return result;
        }

        public async Task<(int? partitionsCheckSum, int? replicasChecksum)> GetCountNodes(
            ActorClusterInfo clusterInfo)
        {
            var temp = _nodeCollection
                .Find(x =>
                    x.Entry.ClusterName == clusterInfo.ClusterName
                    && x.Entry.NodeType == nameof(ActorSystem)
                    && x.LastUpdateTime >= DateTime.UtcNow
                        .Subtract(TimeSpan.FromSeconds(clusterInfo.HeartBeatWindowInSeconds * 3))).ToList();

            var partitionsCheckSum = temp
                .Where(x => x.Entry.PartitionNumber != null
                            && x.Entry.NodeRole == "partition"
                            && x.Entry.NodeState == "operational")
                .Sum(x => x.Entry.PartitionNumber);


            var replicasChecksum = temp
                .Where(x => x.Entry.PartitionNumber != null
                            && x.Entry.NodeRole == "replica"
                            && x.Entry.NodeState == "operational")
                .Sum(x => x.Entry.PartitionNumber);

            return (partitionsCheckSum, replicasChecksum);
        }

        public async Task<ActorNodeInfo?> GetNodeInfo(
            ActorClusterInfo clusterInfo, int partitionNumber, string nodeRole)
        {
            var result = _nodeCollection
                .Find(x =>
                    x.Entry.ClusterName == clusterInfo.ClusterName
                    && x.Entry.NodeType == nameof(ActorSystem)
                    && x.Entry.PartitionNumber == partitionNumber
                    && x.Entry.NodeRole == nodeRole
                    && x.LastUpdateTime >= DateTime.UtcNow
                        .Subtract(TimeSpan.FromSeconds(clusterInfo.HeartBeatWindowInSeconds * 3)))
                .ToList().MaxBy(x => x.LastUpdateTime);

            return result?.Entry;
        }

        private async Task<ActorNodeInfo> GetPartitionSlot(ActorNodeInfo actorNodeInfo, ActorClusterInfo clusterInfo,
            List<MongoDocument<ActorNodeInfo>> partitions)
        {
            for (var i = 0; i < partitions.Count; i++)
            {
                if (i != partitions[i].Entry.PartitionNumber)
                {
                    await _nodeCollection.DeleteOneAsync(x => x.Id == partitions[i].Id);

                    partitions[i].Id = actorNodeInfo.InstanceId;
                    partitions[i].Entry.InstanceId = actorNodeInfo.InstanceId;
                    partitions[i].Entry.NodeState = "operational";

                    await UpsertNodeInfo(partitions[i].Entry);

                    return actorNodeInfo.Set(partitions[i].Entry);
                }
            }

            if (partitions.Count == 0 ||
                partitions.Max(x => x.Entry.PartitionNumber) < clusterInfo.PartitionsNodes - 1)
            {
                actorNodeInfo.PartitionNumber = partitions.Count;
                actorNodeInfo.NodeRole = "partition";
                actorNodeInfo.NodeState = "operational";
                await UpsertNodeInfo(actorNodeInfo);
                return actorNodeInfo;
            }

            actorNodeInfo.NodeState = "Not-A-Partition";
            return actorNodeInfo;
        }

        private async Task<ActorNodeInfo> GetReplciaSlot(ActorNodeInfo actorNodeInfo, ActorClusterInfo clusterInfo,
            List<MongoDocument<ActorNodeInfo>> replicas)
        {
            for (var i = 0; i < replicas.Count; i++)
            {
                if (i != replicas[i].Entry.PartitionNumber)
                {
                    await _nodeCollection.DeleteOneAsync(x => x.Id == replicas[i].Id);

                    replicas[i].Entry.InstanceId = actorNodeInfo.InstanceId;
                    replicas[i].Entry.NodeState = "operational";

                    await UpsertNodeInfo(replicas[i].Entry);

                    return actorNodeInfo.Set(replicas[i].Entry);
                }
            }

            if (replicas.Count == 0 || replicas.Max(x => x.Entry.PartitionNumber) < clusterInfo.ReplicaNodes - 1)
            {
                actorNodeInfo.PartitionNumber = replicas.Count;
                actorNodeInfo.NodeRole = "replica";
                actorNodeInfo.NodeState = "operational";
                await UpsertNodeInfo(actorNodeInfo);
                return actorNodeInfo;
            }

            actorNodeInfo.NodeRole = "no-role";
            actorNodeInfo.NodeState = "failed";
            return actorNodeInfo;
        }
    }
}
