// Licensed to the AiCorp- Buyconn.

using Delegactor.Interfaces;
using Delegactor.Models;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;

namespace Delegactor.Transport
{
    public class DotNettyTcpServerTransportHandler : ChannelHandlerAdapter
    {
        private readonly Func<ActorRequest, Task<ActorResponse>> _callBack;
        private readonly ActorClusterInfo _clusterInfo;
        private readonly ILogger<DotNettyTcpServerTransportHandler> _logger;
        private readonly ActorNodeInfo _nodeInfo;
        private readonly ITaskThrottler<DotNettyTransport> _taskThrottler;

        public DotNettyTcpServerTransportHandler(
            Func<ActorRequest, Task<ActorResponse>> callBack,
            ILogger<DotNettyTcpServerTransportHandler> logger,
            ActorClusterInfo clusterInfo,
            ActorNodeInfo nodeInfo,
            ITaskThrottler<DotNettyTransport> taskThrottler)
        {
            _callBack = callBack;
            _logger = logger;
            _clusterInfo = clusterInfo;
            _nodeInfo = nodeInfo;
            _taskThrottler = taskThrottler;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            _logger.LogInformation("Client connected to client.{ChannelId} {ChannelRemoteAddress}",
                context.Channel.Id,
                context.Channel.RemoteAddress);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var request = SerializationUtils.Deserialize<ActorRequest>((IByteBuffer)message);
            if (request == null)
            {
                _logger.LogWarning("Bad Request");
                return;
            }

            HandleMessage(context, request);
        }

        private void HandleMessage(IChannelHandlerContext context, ActorRequest request)
        {
            // _logger.LogInformation("got request {CorrelationId} ", request.CorrelationId);

            var partitionKey =
                NodeManagerUtils.ComputePartitionNumber(_clusterInfo, request.PartitionType, request.Uid);

            if (request.IsNotityRequest == false &&
                _nodeInfo.NodeRole == request.PartitionType &&
                partitionKey != _nodeInfo.PartitionNumber)
            {
                var resp = new ActorResponse(request)
                {
                    Response = new InvalidOperationException().Message,
                    IsError = true,
                    Error = "Request arrived at Invalid Partition or Type"
                };
                context.WriteAndFlushAsync(SerializationUtils.SerializeToIByteBuffer(resp)).Wait();

                return;
            }

            _taskThrottler.AddTaskAsync(async () =>
            {
                var resp = await _callBack(request);

                await context.WriteAndFlushAsync(SerializationUtils.SerializeToIByteBuffer(resp));
            }).Wait(); //.GetAwaiter().GetResult();

            // _logger.LogInformation("Enqueued request {CorrelationId}", request.CorrelationId);
        }


        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            context.Flush();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            _logger.LogInformation("Exception: {Exception}", exception);
            context.CloseAsync();
        }
    }
}
