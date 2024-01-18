// Licensed to the AiCorp- Buyconn.

using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Delegactor.Core
{
    public class ActorSystemService : BackgroundService, IActorSystem
    {
        private readonly ActorClusterInfo _actorClusterInfo;
        private readonly IActorClusterManager _actorClusterManager;
        private readonly ActorNodeInfo _actorNodeInfo;
        private readonly IActorNodeManager _actorNodeManager;
        private readonly IActorServiceDiscovery _actorServiceDiscovery;
        private readonly ILogger<ActorSystemService> _logger;
        private readonly IServiceProvider _provider;


        public ActorSystemService(
            ActorClusterInfo actorClusterInfo,
            ActorNodeInfo actorNodeInfo,
            ILogger<ActorSystemService> logger,
            IActorClusterManager actorClusterManager,
            IActorNodeManager actorNodeManager,
            IServiceProvider provider,
            IActorServiceDiscovery actorServiceDiscovery)
        {
            _actorClusterInfo = actorClusterInfo ?? throw new ArgumentNullException(nameof(actorClusterInfo));
            _actorNodeInfo = actorNodeInfo ?? throw new ArgumentNullException(nameof(actorNodeInfo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _actorClusterManager = actorClusterManager ?? throw new ArgumentNullException(nameof(actorClusterManager));
            _actorNodeManager = actorNodeManager ?? throw new ArgumentNullException(nameof(actorNodeManager));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _actorServiceDiscovery =
                actorServiceDiscovery ?? throw new ArgumentNullException(nameof(actorServiceDiscovery));
        }


        public async Task RunSetup()
        {
            await _actorClusterManager.RefreshActorSystemClusterDetails();
            var actorNodeInfo = await _actorNodeManager.RefreshActorSystemNodeDetails();
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
