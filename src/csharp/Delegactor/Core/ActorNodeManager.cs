// Licensed to the AiCorp- Buyconn.

using System.Collections.Concurrent;
using System.Text.Json;
using Delegactor.Interfaces;
using Delegactor.Models;
using Delegactor.Transport;
using Microsoft.Extensions.Logging;

namespace Delegactor.Core
{
    public class ActorNodeManager : IActorNodeManager
    {
        private readonly ConcurrentDictionary<string, DateTime> _activationTimeStamps = new();

        private readonly ConcurrentDictionary<string, KeyValuePair<ActorBase, ActorStates>> _actorCollection = new();
        private readonly ActorNodeInfo _actorNodeInfo;
        private readonly IActorServiceDiscovery _actorServiceDiscovery;

        private readonly ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
            _callBackTaskSource;

        private readonly ActorClusterInfo _clusterInfo;

        private readonly ILogger<ActorNodeManager> _logger;
        private readonly IServiceProvider _provider;
        private readonly IStorage _store;
        private readonly IActorSystemTransport _transport;


        public ActorNodeManager(
            ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
                callBackTaskSource,
            ILogger<ActorNodeManager> logger,
            IActorServiceDiscovery actorServiceDiscovery,
            IServiceProvider provider,
            IStorage store,
            ActorClusterInfo clusterInfo,
            ActorNodeInfo nodeInfo,
            IActorSystemTransport transport)
        {
            _callBackTaskSource = callBackTaskSource ?? throw new ArgumentNullException(nameof(callBackTaskSource));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _actorServiceDiscovery =
                actorServiceDiscovery ?? throw new ArgumentNullException(nameof(actorServiceDiscovery));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clusterInfo = clusterInfo ?? throw new ArgumentNullException(nameof(clusterInfo));
            _actorNodeInfo = nodeInfo ?? throw new ArgumentNullException(nameof(nodeInfo));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task RunActorCleanUp()
        {
            _logger.LogInformation("ActorCleanUp in progress");
            var keys = GetIrrelevantActors();

            await UnloadActors(keys);
        }

        public void TimeOutTasks()
        {
            var timeOutTaskKeys = _callBackTaskSource.Where(x => DateTime.Now.Subtract(x.Value.Value).TotalSeconds > 60)
                .Select(x => x.Key);
            foreach (var taskKey in timeOutTaskKeys)
            {
                if (_callBackTaskSource.TryRemove(taskKey, out var item))
                {
                    item.Key.TrySetException(new TimeoutException($"{taskKey} request timed out"));
                }
            }
        }

        public async Task<List<KeyValuePair<ActorBase, ActorStates>>> GetAllActorInstancesOfAModule(
            ActorRequest request)
        {
            var keys = _actorCollection.Keys.Where(x => x.StartsWith(request.Module)).ToList();
            var actors = _actorCollection.Where(x => keys.Contains(x.Key)).Select(x => x.Value).ToList();
            return actors;
        }


        public async Task ShutDown()
        {
            _logger.LogInformation("Shutting system down");
            if (_actorCollection.IsEmpty == false)
            {
                await UnloadActors(_actorCollection.Keys.ToList());
            }

            _transport.Shutdown();
        }

        public async Task<KeyValuePair<ActorBase, ActorStates>> GetActorInstance(ActorRequest request)
        {
            var requestActorId = $"{request.Module}-+-{request.ActorId}";

            if (request.Name == nameof(ActorBase.OnUnLoad)
                && _actorCollection.ContainsKey(requestActorId))
            {
                _actorCollection.TryRemove(requestActorId, out var actor);

                _activationTimeStamps.TryRemove(requestActorId, out _);

                return actor;
            }

            var serviceInstance = _actorCollection.GetOrAdd(requestActorId, key =>
            {
                var serviceType = _actorServiceDiscovery.GetServiceType(request.Module);
                if (serviceType == null)
                {
                    throw new InvalidOperationException("actor type not found");
                }

                var service = _provider.GetService(serviceType) as ActorBase;

                service!.ActorId = key;
                service!.Module = request.Module;

                service.OnLoad(request).GetAwaiter().GetResult();

                return new KeyValuePair<ActorBase, ActorStates>(service, ActorStates.Loaded);
            });

            _activationTimeStamps.AddOrUpdate(requestActorId,
                key => DateTime.Now,
                (key, _) => DateTime.Now);


            return serviceInstance;
        }

        public async Task ReBuildNode()
        {
            if (_actorNodeInfo.PartitionNumber == null ||
                _actorNodeInfo.PartitionNumber > _clusterInfo.PartitionsNodes)
            {
                _actorNodeInfo.NodeState = "re-partitioning";

                await _store.UpsertNodeInfo(_actorNodeInfo);

                var temp = new ActorNodeInfo(_actorNodeInfo);

                var actorNodeInfo = await _store.GetNodeSlotFromCluster(temp, _clusterInfo);

                _actorNodeInfo.Set(actorNodeInfo);

                _transport.Shutdown();
                if (_actorNodeInfo.NodeState == "operational")
                {
                    _transport.Init(_actorNodeInfo, HandleEvent);
                }
            }

            await RunActorCleanUp();
        }

        public async Task<ActorNodeInfo> RefreshActorSystemNodeDetails()
        {
            var nodeInfo = await _store.GetNodeInfo(_actorNodeInfo.InstanceId);

            nodeInfo = nodeInfo ?? await _store.UpsertNodeInfo(_actorNodeInfo);

            if (nodeInfo.PartitionNumber == null
                || _clusterInfo.ClusterState == "re-partitioning"
                || (nodeInfo.PartitionNumber > _clusterInfo.PartitionsNodes
                    && nodeInfo.NodeRole == ConstantKeys.PartitionKey)
                || (nodeInfo.PartitionNumber > _clusterInfo.ReplicaNodes
                    && nodeInfo.NodeRole == ConstantKeys.ReplicaKey))
            {
                await ReBuildNode();
            }

            _actorNodeInfo.NodeState = "operational";

            await _store.UpsertNodeInfo(_actorNodeInfo);

            return nodeInfo;
        }


        public async Task<ActorNodeInfo> RefreshActorClientNodeDetails()
        {
            var nodeInfo = await _store.GetNodeInfo(_actorNodeInfo.InstanceId);

            nodeInfo = nodeInfo ?? await _store.UpsertNodeInfo(_actorNodeInfo);

            _actorNodeInfo.NodeState = "operational";
            _actorNodeInfo.Set(nodeInfo);

            await _store.UpsertNodeInfo(_actorNodeInfo);

            return nodeInfo;
        }

        public async Task<ActorResponse> HandleEvent(ActorRequest request)
        {
            //TODO: guard
            try
            {
                if (!request.Headers.ContainsKey(ConstantKeys.RequestTypeKey))
                {
                    var actorInstance = await GetActorInstance(request);

                    var response = await actorInstance.Key.InvokeMethod(request);
                    return response;
                }

                if (request.Headers.TryGetValue(ConstantKeys.RequestTypeKey, out var requestType) &&
                    requestType.ToString() == ConstantKeys.RequestTypeNotifyKey)
                {
                    var instances = await GetAllActorInstancesOfAModule(request);

                    Parallel.ForEach(instances, async instance =>
                    {
                        await instance.Key.InvokeMethod(request).ConfigureAwait(false);
                    });
                }

                return new ActorResponse(request);
            }
            catch (Exception exception)
            {
                _logger.LogError(" {Error}", exception.Message);
                return new ActorResponse(new ActorRequest(), error: exception.Message);
            }
        }

        public async Task Start()
        {
            _logger.LogInformation("Shutting system down");
            if (_actorCollection.IsEmpty == false)
            {
                await UnloadActors(_actorCollection.Keys.ToList());
            }

            _transport.Shutdown();
        }


        private async Task UnloadActors(List<string> keys)
        {
            var memberInfo = typeof(ActorBase).GetMethod(nameof(ActorBase.OnUnLoad));
            var unLoadMethodName = memberInfo.GetParameters().First().Name;

            var actorWindowExpiredEvent = new ActorRequest
            {
                Name = nameof(ActorBase.OnUnLoad),
                Parameters = new Dictionary<string, string> { { unLoadMethodName, "actor system" } }
            };

            var request = new ActorRequest
            {
                Name = nameof(ActorBase.OnUnLoad),
                Parameters = new Dictionary<string, string>
                {
                    { unLoadMethodName, JsonSerializer.Serialize(actorWindowExpiredEvent) }
                }
            };

            foreach (var key in keys)
            {
                if (!_actorCollection.TryGetValue(key, out var actorEntry))
                {
                    continue;
                }

                request.Module = actorEntry.Key.Module;
                request.ActorId = actorEntry.Key.ActorId;
                request.CorrelationId = Guid.NewGuid().ToString();
                request.Partition = _actorNodeInfo.PartitionNumber.GetValueOrDefault();
                request.PartitionType = _actorNodeInfo.NodeRole;
                var instance = await GetActorInstance(request);
                await instance.Key.OnUnLoad(request);
            }
        }

        public List<string> GetIrrelevantActors()
        {
            var lifeTime = _actorCollection.ToDictionary(x => x.Key, x => x.Value.Key);


            var expiredActors = lifeTime.Join(
                    _activationTimeStamps,
                    x => x.Key,
                    y => y.Key,
                    (x, y) => new { x.Key, x.Value.ActivationWindow, TimeStamp = y.Value })
                .Where(x => x.ActivationWindow < x.TimeStamp.Subtract(DateTime.Now))
                .Select(x => x.Key).ToList();

            expiredActors = _actorCollection
                .Where(x => _actorNodeInfo.PartitionNumber ==
                            NodeManagerUtils.ComputePartitionNumber(_clusterInfo, _actorNodeInfo.NodeRole, x.Key))
                .Select(x => x.Key).Union(expiredActors).ToList();
            return expiredActors;
        }
    }
}
