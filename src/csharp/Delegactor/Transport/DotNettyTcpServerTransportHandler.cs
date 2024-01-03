﻿// Licensed to the AiCorp- Buyconn.

using System.Text;
using System.Text.Json;
using Delegactor.Core;
using Delegactor.Models;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;

namespace Delegactor.Transport
{
    public class DotNettyTcpServerTransportHandler : ChannelHandlerAdapter
    { 
        private readonly Func<ActorRequest, Task<ActorResponse>> _callBack;
        private readonly ILogger<DotNettyTcpServerTransportHandler> _logger;
        private readonly ActorClusterInfo _clusterInfo;
        private ActorNodeInfo _nodeInfo;
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
            var str = ((IByteBuffer)message).ToString(Encoding.UTF8);
            var request = JsonSerializer.Deserialize<ActorRequest>(str);
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

            var partitionKey = _clusterInfo.ComputeKey(request.Uid);
            if (request.IsNotityRequest == false &&
                partitionKey != _nodeInfo.GetSubscriptionId(_clusterInfo))
            {
                var resp = new ActorResponse(request)
                {
                    Response = new InvalidOperationException().Message,
                    IsError = true,
                    Error = "Request arrived at Invalid Partition"
                };
                context.WriteAndFlushAsync(DotNettyUtils.Serialize(resp)).Wait();

                return;
            }

            _taskThrottler.AddTaskAsync(async () =>
            {
                var resp = await _callBack(request);

                await context.WriteAndFlushAsync(DotNettyUtils.Serialize(resp));
            }).Wait(); //.GetAwaiter().GetResult();

            // _logger.LogInformation("Enqueued request {CorrelationId}", request.CorrelationId);
        }


        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            _logger.LogInformation("Exception: {Exception}", exception);
            context.CloseAsync();
        }
    }
}
