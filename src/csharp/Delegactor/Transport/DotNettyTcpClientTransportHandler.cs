// Licensed to the AiCorp- Buyconn.

using System.Collections.Concurrent;
using Delegactor.Models;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;

namespace Delegactor.Transport
{
    public class DotNettyTcpClientTransportHandler : ChannelHandlerAdapter
    {
        private readonly ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
            _callBackTasksSource;

        private readonly ILogger<DotNettyTcpClientTransportHandler> _logger;

        public DotNettyTcpClientTransportHandler(
            ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
                callBackTasksSource,
            ILogger<DotNettyTcpClientTransportHandler> logger)
        {
            _callBackTasksSource = callBackTasksSource;
            _logger = logger;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            _logger.LogInformation("Client connected to server.{ChannelId} {ChannelRemoteAddress}",
                context.Channel.Id,
                context.Channel.RemoteAddress);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var response = SerializationUtils.Deserialize<ActorResponse>((IByteBuffer)message);
            if (response == null)
            {
                _logger.LogWarning("Bad Request");
                return;
            }

            HandleMessage(response);
        }

        private void HandleMessage(ActorResponse response)
        {
            // _logger.LogInformation("got response {CorrelationId} to {RequestRoutingKey}",
            //     response.CorrelationId,
            //     response);

            var correlationId = response.Headers.TryGetValue(ConstantKeys.OriginCorrelationIdKey, out var header)
                ? header
                : response.CorrelationId;

            if (!_callBackTasksSource.TryRemove(correlationId.ToString(), out var tcs))
            {
                _logger.LogInformation("ghost message with correlationId {CorrelationId} arrived",
                    response.CorrelationId);

                return;
            }

            tcs.Key.TrySetResult(response);
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
