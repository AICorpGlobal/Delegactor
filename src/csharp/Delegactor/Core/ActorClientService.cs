// Licensed to the AiCorp- Buyconn.

using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Delegactor.Core
{
    public class ActorClientService : BackgroundService, IActorClient
    {
        private readonly IActorClusterManager _actorClusterManager;
        private readonly ActorNodeInfo _actorNodeInfo;
        private readonly IActorNodeManager _actorNodeManager;
        private readonly ILogger<ActorSystemService> _logger;
        private readonly IServiceProvider _provider;
        private IActorSystemTransport _actorSystemTransport;


        public ActorClientService(
            ActorNodeInfo actorNodeInfo,
            IActorClusterManager actorClusterManager,
            IActorNodeManager actorNodeManager,
            IServiceProvider provider,
            IActorSystemTransport actorSystemTransport,
            ILogger<ActorSystemService> logger)
        {
            _actorNodeInfo = actorNodeInfo ?? throw new ArgumentNullException(nameof(actorNodeInfo));
            _actorClusterManager = actorClusterManager ?? throw new ArgumentNullException(nameof(actorClusterManager));
            _actorNodeManager = actorNodeManager ?? throw new ArgumentNullException(nameof(actorNodeManager));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _actorSystemTransport =
                actorSystemTransport ?? throw new ArgumentNullException(nameof(actorSystemTransport));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RunSetup()
        {
            _actorSystemTransport = _provider.GetRequiredService<IActorSystemTransport>();

            await HeartBeatUpdate();

            _actorSystemTransport.Init(_actorNodeInfo, _actorNodeManager.HandleEvent);
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
            _actorSystemTransport.Shutdown();

            await _actorNodeManager.ShutDown();
        }

        private async Task HeartBeatUpdate()
        {
            await _actorClusterManager.RefreshActorClientClusterDetails();
            await _actorNodeManager.RefreshActorClientNodeDetails();
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

    public interface IActorClient
    {
    }
}
