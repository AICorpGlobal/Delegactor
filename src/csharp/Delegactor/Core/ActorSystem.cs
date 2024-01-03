// Licensed to the AiCorp- Buyconn.

using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Exception = System.Exception;

namespace Delegactor.Core
{
    public class ActorSystem : BackgroundService, IActorSystem
    {
        private readonly ActorClusterInfo _actorClusterInfo;
        private readonly IActorClusterManager _actorClusterManager;
        private readonly ActorNodeInfo _actorNodeInfo;
        private readonly IActorNodeManager _actorNodeManager;
        private readonly IActorServiceDiscovery _actorServiceDiscovery;
        private readonly ILogger<ActorSystem> _logger;
        private readonly IServiceProvider _provider;


        public ActorSystem(
            ActorClusterInfo actorClusterInfo,
            ActorNodeInfo actorNodeInfo,
            ILogger<ActorSystem> logger,
            IActorClusterManager actorClusterManager,
            IActorNodeManager actorNodeManager,
            IServiceProvider provider,
            IActorServiceDiscovery actorServiceDiscovery)
        {
            _actorClusterInfo = actorClusterInfo;
            _actorNodeInfo = actorNodeInfo;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _actorClusterManager = actorClusterManager ?? throw new ArgumentNullException(nameof(actorClusterManager));
            _actorNodeManager = actorNodeManager ?? throw new ArgumentNullException(nameof(actorNodeManager));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _actorServiceDiscovery = actorServiceDiscovery;
        }

        public async Task<ActorResponse> HandleEvent(ActorRequest request)
        {
            try
            {
                if (!request.Headers.ContainsKey("requestType"))
                {
                    var actorInstance = await _actorNodeManager.GetActorInstance(request);

                    var response = await actorInstance.Key.InvokeMethod(request);
                    return response;
                }

                if (request.Headers.TryGetValue("requestType", out var requestType) &&
                    requestType.ToString() == "notify")
                {
                    var instances = await _actorNodeManager.GetAllActorInstancesOfAModule(request);
                    List<Task> tasks = new List<Task>();
                    foreach (var instance in instances)
                    {
                        tasks.Add(instance.Key.InvokeMethod(request));
                    }

                    await Task.WhenAll(tasks);
                }

                return new ActorResponse(request);
            }
            catch (Exception exception)
            {
                _logger.LogError(" {Error}", exception.Message);
                return new ActorResponse(new ActorRequest(), error: exception.Message);
            }
        }


        public async Task RunSetup()
        {
            await _actorClusterManager.RefreshActorSystemClusterDetails();
            _actorNodeManager.SetupEventHandler(HandleEvent);
            var actorNodeInfo = await _actorNodeManager.RefreshActorSystemNodeDetails();
            _provider.GetRequiredService<IActorSystemTransport>();
            _actorServiceDiscovery.LoadActors(actorNodeInfo);
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RunSetup();

            var step = 0;
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
            {
                try
                {
                    while (await timer.WaitForNextTickAsync(stoppingToken))
                    {
                        ++step;

                        await HeartBeatUpdate();

                        if (step < 240)
                        {
                            continue;
                        }

                        await _actorNodeManager.RunActorCleanUp();
                        step = 0;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation(" Hosted Service is stopping.");
                }
            }
            await _actorNodeManager.ShutDown();
        }

        private async Task HeartBeatUpdate()
        {
            await _actorClusterManager.RefreshActorSystemClusterDetails();
            await _actorNodeManager.RefreshActorSystemNodeDetails();
            _actorNodeManager.TimeOutTasks();
            _logger.LogInformation(
                " Heartbeat from {InstanceId} - {IpAddress}:{Port} as  {NodeType} with  {NodeRole}:{PartitionNumber} {NodeState}  ",
                _actorNodeInfo.InstanceId,
                _actorNodeInfo.IpAddress,
                _actorNodeInfo.Port.ToString(),
                _actorNodeInfo.NodeType,
                _actorNodeInfo.NodeRole,
                _actorNodeInfo.PartitionNumber.ToString(),
                _actorNodeInfo.NodeState);
        }
    }
}
