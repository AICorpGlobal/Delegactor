// Licensed to the AiCorp- Buyconn.

using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Delegactor.Core
{
    public class ActorClient : BackgroundService, IActorClient
    {
        private readonly ActorClusterInfo _actorClusterInfo;
        private readonly IActorClusterManager _actorClusterManager;
        private readonly ActorNodeInfo _actorNodeInfo;
        private readonly IActorNodeManager _actorNodeManager;
        private readonly ILogger<ActorSystem> _logger;
        private readonly IServiceProvider _provider;
        private IActorSystemTransport _actorSystemTransport;
        private Func<ActorRequest, Task<ActorResponse>> _handleEvent;
        private string? _partition;


        public ActorClient(
            ActorClusterInfo actorClusterInfo,
            ActorNodeInfo actorNodeInfo,
            IActorClusterManager actorClusterManager,
            IActorNodeManager actorNodeManager,
            IServiceProvider provider,
            ILogger<ActorSystem> logger)
        {
            _actorClusterInfo = actorClusterInfo;
            _actorNodeInfo = actorNodeInfo ?? throw new ArgumentNullException(nameof(actorNodeInfo));
            _actorClusterManager = actorClusterManager;
            _actorNodeManager = actorNodeManager;
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RunSetup()
        {
            _actorSystemTransport = _provider.GetRequiredService<IActorSystemTransport>();

            await HeartBeatUpdate();
            _actorSystemTransport.Init(_actorNodeInfo, _handleEvent);
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
            await _actorClusterManager.RefreshActorClientClusterDetails();
            await _actorNodeManager.RefreshActorClientNodeDetails();
            _actorNodeManager.TimeOutTasks();
            _logger.LogInformation(" Heartbeat from {InstanceId} as  {NodeType} with  {NodeRole} {NodeState}  ",
                _actorNodeInfo.InstanceId, _actorNodeInfo.NodeType, _actorNodeInfo.NodeRole, _actorNodeInfo.NodeState);
        }
    }

    public interface IActorClient
    {
    }
}
