// Licensed to the AiCorp- Buyconn.

using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using Delegactor.Core;
using Delegactor.Interfaces;
using Delegactor.Models;
using DotNetty.Codecs;
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
        private readonly ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
            _callBackTasksSource;

        private readonly ConcurrentDictionary<string, IChannel> _channels = new();
        private readonly ILogger<DotNettyTcpClientTransportHandler> _childForClientLogger;
        private readonly ILogger<DotNettyTcpServerTransportHandler> _childLogger;
        private readonly ActorClusterInfo _clusterInfo;

        private readonly ILogger<DotNettyTransport> _logger;
        private readonly ResiliencePipeline _resiliencePipeline;
        private readonly IActorNodeResolver _resolver;

        private readonly ITaskThrottler<DotNettyTransport> _taskThrottler;
        private ServerBootstrap _bootstrap;
        private Bootstrap _bootstrapForClientListeners;
        private MultithreadEventLoopGroup _bossGroup;
        private Func<ActorRequest, Task<ActorResponse>> _handleEvent;


        private bool _initCompleted;
        private MultithreadEventLoopGroup _listenerEventLoopGroup;
        private ActorNodeInfo _nodeInfo;
        private IChannel _serverChannel;
        private MultithreadEventLoopGroup _workerGroup;


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
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _clusterInfo = clusterInfo ?? throw new ArgumentNullException(nameof(clusterInfo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _childLogger = childLogger ?? throw new ArgumentNullException(nameof(childLogger));
            _taskThrottler = taskThrottler ?? throw new ArgumentNullException(nameof(taskThrottler));
            _nodeInfo = nodeInfo ?? throw new ArgumentNullException(nameof(nodeInfo));
            _callBackTasksSource = callBackTasksSource ?? throw new ArgumentNullException(nameof(callBackTasksSource));
            _childForClientLogger =
                childForClientLogger ?? throw new ArgumentNullException(nameof(childForClientLogger));
            _resiliencePipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = new TimeSpan(0, 0, 1),
                    MaxRetryAttempts = 50,
                    UseJitter = true
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
            _resiliencePipeline.Execute(_ =>
            {
                var port = GetEphemeralPort();

                try
                {
                    if (port == -1)
                    {
                        throw new NotSupportedException("Unable to find a  port, cannot start now");
                    }

                    if (_nodeInfo.NodeType == nameof(ActorSystemService))
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

                            var dotNettyTcpClientTransportHandler = new DotNettyTcpClientTransportHandler(
                                _callBackTasksSource,
                                _childForClientLogger);

                            pipeline.AddLast(dotNettyTcpClientTransportHandler);
                        }));
                }
                catch (Exception exception)
                {
                    _logger.LogError("{Port} {Error}", port, exception.Message);
                    _initCompleted = false;
                    throw;
                }
            });


            return _initCompleted;
        }


        public Task<ActorResponse> SendRequest(ActorRequest request, bool noWait)
        {
            request.Headers.TryAdd(ConstantKeys.CorrelationIdKey, request.CorrelationId);
            var reload = false;
            _resiliencePipeline.Execute(async _ =>
            {
                var channel = GetChannel(request, reload);
                reload = true;
                await channel.WriteAndFlushAsync(SerializationUtils.SerializeToIByteBuffer(request));
            });

            var tcs = new TaskCompletionSource<ActorResponse>();

            _callBackTasksSource.TryAdd(request.CorrelationId,
                new KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>(tcs, DateTime.Now));

            return tcs.Task;
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
            request.Headers.TryAdd(ConstantKeys.CorrelationIdKey, request.CorrelationId);
            request.Headers.TryAdd(ConstantKeys.RequestTypeKey, ConstantKeys.RequestTypeNotifyKey);

            for (var index = 0; index < _clusterInfo.PartitionsNodes; index++)
            {
                request.Partition = index;
                request.PartitionType = ConstantKeys.PartitionKey;
                await SendRequest(request, true);
            }

            for (var index = 0; index < _clusterInfo.ReplicaNodes; index++)
            {
                request.Partition = index;
                request.PartitionType = ConstantKeys.ReplicaKey;
                await SendRequest(request, true);
            }

            return new ActorResponse(request);
        }

        private int GetEphemeralPort()
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            for (var i = _clusterInfo.EphemeralPortStart; i < _clusterInfo.EphemeralPortEnd; i++)
            {
                if (tcpConnInfoArray.Any(x => x.Port == i))
                {
                    continue;
                }

                return i;
            }

            return -1;
        }

        private IChannel GetChannel(ActorRequest request, bool reload = false)
        {
            var requestPartition =
                NodeManagerUtils.ComputePartitionNumber(_clusterInfo, request.PartitionType, request.Uid);

            var key = $"{request.PartitionType}{requestPartition}";

            if (reload)
            {
                _channels.TryRemove(key, out var _);
            }

            var iChannel = _channels.GetOrAdd(key, _ =>
            {
                //https://github.com/caozhiyuan/DotNetty/blob/dev/src/DotNetty.Rpc/Client/NettyClient.cs
                var endPoint = _resolver.GetIpAddress(requestPartition, request.PartitionType).Result;
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
    }
}
