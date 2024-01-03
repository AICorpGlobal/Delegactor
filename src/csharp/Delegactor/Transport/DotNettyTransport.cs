// Licensed to the AiCorp- Buyconn.

using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using Delegactor.Core;
using Delegactor.Interfaces;
using Delegactor.Models;
using DotNetty.Codecs;
using DotNetty.Codecs.Json;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Delegactor.Transport
{
    public class DotNettyTransport : IActorSystemTransport
    {
        private readonly IActorNodeResolver _resolver;
        private readonly ActorClusterInfo _clusterInfo;

        private readonly ILogger<DotNettyTransport> _logger;
        private readonly ILogger<DotNettyTcpServerTransportHandler> _childLogger;

        private readonly ITaskThrottler<DotNettyTransport> _taskThrottler;

        private readonly ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
            _callBackTasksSource;

        private readonly ConcurrentDictionary<string, IChannel> _channels = new();


        private bool _initCompleted;
        private ActorNodeInfo _nodeInfo;
        private Func<ActorRequest, Task<ActorResponse>> _handleEvent;
        private MultithreadEventLoopGroup _bossGroup;
        private MultithreadEventLoopGroup _workerGroup;
        private MultithreadEventLoopGroup _listenerEventLoopGroup;
        private ServerBootstrap _bootstrap;
        private IChannel _serverChannel;
        private Bootstrap _bootstrapForClientListeners;
        private ILogger<DotNettyTcpClientTransportHandler> _childForClientLogger;
        private readonly ResiliencePipeline _resiliencePipeline;


        public DotNettyTransport(
            ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
                callBackTasksSource,
            ILogger<DotNettyTransport> logger,
            ITaskThrottler<DotNettyTransport> taskThrottler,
            ActorNodeInfo nodeInfo,
            ActorClusterInfo clusterInfo,
            IActorNodeResolver resolver,
            ILogger<DotNettyTcpServerTransportHandler> childLogger,
            ILogger<DotNettyTcpClientTransportHandler> childForClientLogger)
        {
            _resolver = resolver;
            _clusterInfo = clusterInfo;
            _logger = logger;
            _childLogger = childLogger;
            _taskThrottler = taskThrottler;
            _nodeInfo = nodeInfo;
            _callBackTasksSource = callBackTasksSource;
            _childForClientLogger = childForClientLogger;
            _resiliencePipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions()
                {
                    BackoffType = DelayBackoffType.Exponential, Delay = new TimeSpan(0, 0, 2), MaxRetryAttempts = 5
                }) // Add retry using the default options
                .Build();
        }


        public bool Init(ActorNodeInfo nodeInfo, Func<ActorRequest, Task<ActorResponse>> handleEvent)
        {
            _nodeInfo = nodeInfo;
            if (_initCompleted)
            {
                return _initCompleted;
            }


            _initCompleted = true;
            int port = GetEphemeralPort();
            if (port == -1)
            {
                throw new NotSupportedException("Unable to find a  port, cannot start now");
            }

            try
            {
                if (_nodeInfo.NodeType == nameof(ActorSystem))
                {
                    _bossGroup = new MultithreadEventLoopGroup();
                    _workerGroup = new MultithreadEventLoopGroup();
                    _bootstrap = new ServerBootstrap()
                        .Group(_bossGroup, _workerGroup)
                        .Channel<TcpServerSocketChannel>()
                        .Option(ChannelOption.SoBacklog, 1000)
                        .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                        {
                            var pipeline = channel.Pipeline;
                            pipeline.AddLast(new DelimiterBasedFrameDecoder(16384, Delimiters.LineDelimiter()));
                            pipeline.AddLast(new JsonObjectDecoder());

                            var dotNettyTcpServerTransportHandler = new DotNettyTcpServerTransportHandler(
                                handleEvent,
                                _childLogger,
                                _clusterInfo, _nodeInfo,
                                _taskThrottler);
                            pipeline.AddLast(dotNettyTcpServerTransportHandler);
                        }));

                    _nodeInfo.Port = port;
                    _serverChannel = _bootstrap.BindAsync(new IPEndPoint(IPAddress.Any, port)).Result;
                }

                _listenerEventLoopGroup = new MultithreadEventLoopGroup();
                _bootstrapForClientListeners = new Bootstrap()
                    .Group(_listenerEventLoopGroup)
                    .Channel<TcpSocketChannel>()
                    .Option(ChannelOption.TcpNodelay, true)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        var pipeline = channel.Pipeline;
                        pipeline.AddLast(new DelimiterBasedFrameDecoder(16384, Delimiters.LineDelimiter()));
                        pipeline.AddLast(new JsonObjectDecoder());

                        var dotNettyTcpClientTransportHandler = new DotNettyTcpClientTransportHandler(
                            _callBackTasksSource,
                            _childForClientLogger,
                            _clusterInfo,
                            _nodeInfo);

                        pipeline.AddLast(dotNettyTcpClientTransportHandler);
                    }));
            }
            catch (Exception exception)
            {
                _logger.LogError("{Error}", exception.Message);
                _initCompleted = false;
            }


            return _initCompleted;
        }

        private int GetEphemeralPort()
        {
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            for (int i = _clusterInfo.EphemeralPortStart; i < _clusterInfo.EphemeralPortEnd; i++)
            {
                if (tcpConnInfoArray.Any(x => x.LocalEndPoint.Port != i))
                {
                    return i;
                }
            }

            return -1;
        }


        public Task<ActorResponse> SendRequest(ActorRequest request, bool noWait)
        {
            request.Headers.TryAdd("CorrelationId", request.CorrelationId);
            bool reload = false;
            // _resiliencePipeline.Execute(async _ =>
            // {
            var channel = GetChannel(request, reload);
            // reload = true;
            channel.WriteAndFlushAsync(DotNettyUtils.Serialize(request));
            // }

            var tcs = new TaskCompletionSource<ActorResponse>();

            _callBackTasksSource.TryAdd(request.CorrelationId,
                new KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>(tcs, DateTime.Now));

            return tcs.Task;
        }

        private IChannel GetChannel(ActorRequest request, bool reload = false)
        {
            var key = $"{request.PartitionType}{request.Partition}";

            if (reload)
            {
                _channels.TryRemove(key, out var _);
            }

            var iChannel = _channels.GetOrAdd(key, _ =>
            {
                //https://github.com/caozhiyuan/DotNetty/blob/dev/src/DotNetty.Rpc/Client/NettyClient.cs
                var endPoint = _resolver.GetIpAddress(request.Partition, request.PartitionType).Result;
                //var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 39500);
                try
                {
                    var channel = _bootstrapForClientListeners.ConnectAsync(endPoint).Result;
                    return channel;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            });

            return iChannel;
        }

        public bool Shutdown()
        {
            if (_initCompleted == false)
            {
                return true;
            }

            if (_bossGroup != null && _bossGroup.IsShutdown == false)
            {
                _bossGroup.ShutdownGracefullyAsync().Wait();
                _workerGroup.ShutdownGracefullyAsync().Wait();
            }

            if (_listenerEventLoopGroup != null && _listenerEventLoopGroup.IsShutdown == false)
            {
                _listenerEventLoopGroup
                    .ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1))
                    .Wait();
            }

            return true;
        }

        public async Task<ActorResponse> SendBroadCastNotify(ActorRequest request)
        {
            request.Headers.TryAdd("CorrelationId", request.CorrelationId);
            request.Headers.TryAdd("requestType", "notify");

            for (int index = 0; index < _clusterInfo.PartitionsNodes; index++)
            {
                request.Partition = index;
                request.PartitionType = "partition";
                await SendRequest(request, true);
            }

            for (int index = 0; index < _clusterInfo.ReplicaNodes; index++)
            {
                request.Partition = index;
                request.PartitionType = "replica";
                await SendRequest(request, true);
            }

            return new ActorResponse(request);
        }
    }
}
