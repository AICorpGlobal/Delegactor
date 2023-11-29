// Licensed to the AiCorp- Buyconn.

using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                var serviceInstance = await _actorNodeManager.GetServiceInstance(request);
                var response = await serviceInstance.Key.InvokeMethod(request);
                return response;
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
            while (!stoppingToken.IsCancellationRequested)
            {
                ++step;

                await Task.Delay(_actorClusterInfo.HeartBeatWindowInSeconds * 1000, stoppingToken);

                await HeartBeatUpdate();

                if (step < 240)
                {
                    continue;
                }

                await _actorNodeManager.RunActorCleanUp();
                step = 0;
            }
        }

        private async Task HeartBeatUpdate()
        {
            await _actorClusterManager.RefreshActorSystemClusterDetails();
            await _actorNodeManager.RefreshActorSystemNodeDetails();
            _actorNodeManager.TimeOutTasks();
            _logger.LogInformation(" Heartbeat from {InstanceId} as  {NodeType} with  {NodeRole} {NodeState}  ",
                _actorNodeInfo.InstanceId, _actorNodeInfo.NodeType, _actorNodeInfo.NodeRole, _actorNodeInfo.NodeState);
        }
    }
}
