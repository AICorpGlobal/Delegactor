// Licensed to the AiCorp- Buyconn.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Delegactor.Core;
using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Delegactor.Transport
{
    public class RabbitMqTransport : IActorSystemTransport
    {
        private readonly ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
            _callBackTasksSource;

        private readonly ActorClusterInfo _clusterInfo;

        private readonly Dictionary<string, string> _configuration;

        private readonly ILogger<RabbitMqTransport> _logger;

        private readonly ITaskThrottler<RabbitMqTransport> _taskThrottler;

        private ConnectionFactory _factory;
        private Func<ActorRequest, Task<ActorResponse>> _handleEvent;
        private bool _initCompleted;
        private ActorNodeInfo _nodeInfo;
        private IModel _requestChannel;
        private EventingBasicConsumer _requestConsumer;
        private string _requesterRoutingKey;
        private string _requestQueueName;
        private string _responderRoutingKey;
        private IModel _responseChannel;
        private EventingBasicConsumer _responseConsumer;
        private string _responseQueueName;
        private IConnection _sendConnection;

        public RabbitMqTransport(
            ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
                callBackTasksSource,
            ILogger<RabbitMqTransport> logger,
            ITaskThrottler<RabbitMqTransport> taskThrottler,
            ActorNodeInfo nodeInfo,
            ActorClusterInfo clusterInfo,
            Dictionary<string, string> configuration)
        {
            _callBackTasksSource = callBackTasksSource ?? throw new ArgumentNullException(nameof(callBackTasksSource));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taskThrottler = taskThrottler ?? throw new ArgumentNullException(nameof(taskThrottler));
            _nodeInfo = nodeInfo ?? throw new ArgumentNullException(nameof(nodeInfo));
            _clusterInfo = clusterInfo ?? throw new ArgumentNullException(nameof(clusterInfo));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public bool Init(ActorNodeInfo nodeInfo, Func<ActorRequest, Task<ActorResponse>> handleEvent)
        {
            _nodeInfo = nodeInfo;
            if (_initCompleted)
            {
                return _initCompleted;
            }

            _initCompleted = true;
            try
            {
                var exclusive = _nodeInfo.NodeType == nameof(ActorClientService);

                var nodeInfoPartitionNumber = NodeManagerUtils.GetPartitionNumber(_nodeInfo, _clusterInfo);

                _requestQueueName = _nodeInfo.NodeType == nameof(ActorSystemService)
                    ? $"request_{_nodeInfo.ClusterName}_{_nodeInfo.NodeType}_{nodeInfoPartitionNumber}_{_nodeInfo.NodeRole}"
                    : $"request_{_nodeInfo.ClusterName}_{_nodeInfo.NodeType}_{_nodeInfo.InstanceId}_{_nodeInfo.NodeRole}";

                _requesterRoutingKey = _nodeInfo.NodeType == nameof(ActorSystemService)
                    ? $"request_{_nodeInfo.ClusterName}_{_nodeInfo.NodeType}_{nodeInfoPartitionNumber}_{_nodeInfo.NodeRole}"
                    : $"request_{_nodeInfo.ClusterName}_{_nodeInfo.NodeType}_{_nodeInfo.InstanceId}_{_nodeInfo.NodeRole}";

                _responseQueueName = _nodeInfo.NodeType == nameof(ActorSystemService)
                    ? $"response_{_nodeInfo.ClusterName}_{_nodeInfo.NodeType}_{nodeInfoPartitionNumber}_{_nodeInfo.NodeRole}"
                    : $"response_{_nodeInfo.ClusterName}_{_nodeInfo.NodeType}_{_nodeInfo.InstanceId}_{_nodeInfo.NodeRole}";

                _responderRoutingKey = _nodeInfo.NodeType == nameof(ActorSystemService)
                    ? $"response_{_nodeInfo.ClusterName}_{_nodeInfo.NodeType}_{nodeInfoPartitionNumber}_{_nodeInfo.NodeRole}"
                    : $"response_{_nodeInfo.ClusterName}_{_nodeInfo.NodeType}_{_nodeInfo.InstanceId}_{_nodeInfo.NodeRole}";


                ushort eventsInParallel = 1; // move to configurations

                _factory = new ConnectionFactory { Uri = new Uri(_configuration[ConstantKeys.ConnectionStringKey]) };

                _sendConnection = _factory.CreateConnection();

                _requestChannel = _sendConnection.CreateModel();

                _responseChannel = _sendConnection.CreateModel();


                _responseChannel.BasicQos(0, eventsInParallel, false);

                _responseChannel.BasicQos(0, eventsInParallel, false);


                if (_nodeInfo.NodeType == nameof(ActorSystemService))
                {
                    _requestChannel.ExchangeDeclare(_requestQueueName, ExchangeType.Topic);
                    _requestChannel.QueueDeclare(_requestQueueName, false, exclusive,
                        false);
                    _requestChannel.QueueBind(_requestQueueName, _requestQueueName,
                        _requesterRoutingKey);
                }


                _responseChannel.ExchangeDeclare(_responseQueueName, ExchangeType.Topic,
                    autoDelete: false);
                _responseChannel.QueueDeclare(_responseQueueName, false, exclusive,
                    false);
                _responseChannel.QueueBind(_responseQueueName, _responseQueueName,
                    _responderRoutingKey);


                SetupConsumer(handleEvent);
            }
            catch (Exception exception)
            {
                _logger.LogError("{Error}", exception.Message);
                _initCompleted = false;
            }


            return _initCompleted;
        }

        public Task<ActorResponse> SendRequest(ActorRequest request, bool noWait)
        {
            var partitionNumber =
                NodeManagerUtils.ComputePartitionNumber(_clusterInfo, request.PartitionType, request.Uid);
            //_logger.LogInformation($"Sending request to actor {request.Uid} in {request.PartitionType} : {partitionNumber}");

            var requestRoutingKey =
                $"request_{_clusterInfo.ClusterName}_{nameof(ActorSystemService)}_{partitionNumber}_{request.PartitionType}";
            try
            {
                var prop = _requestChannel.CreateBasicProperties();

                request.Headers.TryAdd(ConstantKeys.ListenerIdKey, _responderRoutingKey);
                request.Headers.TryAdd(ConstantKeys.CorrelationIdKey, request.CorrelationId);

                prop.CorrelationId = request.CorrelationId;
                prop.Persistent = false;

                var messageBytes = SerializationUtils.Serialize(request);

                // lock (_requestChannel)
                {
                    //_logger.LogDebug("send request {CorrelationId} to {RequestRoutingKey}", request.CorrelationId,
                    //    requestRoutingKey);

                    _requestChannel.BasicPublish(requestRoutingKey,
                        requestRoutingKey,
                        prop,
                        messageBytes);
                }

                if (noWait)
                {
                    return Task.FromResult(new ActorResponse(request));
                }

                var tcs = new TaskCompletionSource<ActorResponse>();


                _callBackTasksSource.TryAdd(request.CorrelationId,
                    new KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>(tcs, DateTime.Now));

                return tcs.Task;
            }
            catch (Exception ex)
            {
                _logger.LogError("send failed {Error}", ex);
                return Task.FromResult(new ActorResponse(request, error: ex.Message));
            }
        }

        public bool Shutdown()
        {
            if (_initCompleted == false)
            {
                return true;
            }

            if (_requestChannel != null && _requestChannel.IsOpen)
            {
                _requestChannel.Close();
            }

            if (_responseChannel != null && _responseChannel.IsOpen)
            {
                _responseChannel.Close();
            }

            return true;
        }

        public Task<ActorResponse> SendBroadCastNotify(ActorRequest request)
        {
            try
            {
                var prop = _requestChannel.CreateBasicProperties();

                request.Headers.TryAdd(ConstantKeys.ListenerIdKey, _responderRoutingKey);
                request.Headers.TryAdd(ConstantKeys.CorrelationIdKey, request.CorrelationId);
                request.Headers.TryAdd(ConstantKeys.RequestTypeKey, ConstantKeys.RequestTypeNotifyKey);


                var messageBytes = SerializationUtils.Serialize(request);


                for (var index = 0; index < _clusterInfo.PartitionsNodes; index++)
                {
                    var requestRoutingKey =
                        $"request_{_clusterInfo.ClusterName}_{nameof(ActorSystemService)}_{index}_{ConstantKeys.PartitionKey}";
                    _logger.LogDebug("send request {CorrelationId} to {RequestRoutingKey}", request.CorrelationId,
                        requestRoutingKey);

                    prop.CorrelationId = $"{Guid.NewGuid()}notify{request.CorrelationId}";
                    _requestChannel.BasicPublish(requestRoutingKey,
                        requestRoutingKey,
                        prop,
                        messageBytes);
                }

                for (var index = 0; index < _clusterInfo.ReplicaNodes; index++)
                {
                    var requestRoutingKey =
                        $"request_{_clusterInfo.ClusterName}_{nameof(ActorSystemService)}_{index}_{ConstantKeys.ReplicaKey}";

                    prop.CorrelationId = $"{Guid.NewGuid()}{ConstantKeys.RequestTypeNotifyKey}{request.CorrelationId}";
                    _logger.LogDebug("send request {CorrelationId} to {RequestRoutingKey}", request.CorrelationId,
                        requestRoutingKey);

                    _requestChannel.BasicPublish(requestRoutingKey,
                        requestRoutingKey,
                        prop,
                        messageBytes);
                }


                return Task.FromResult(new ActorResponse(request));
            }
            catch (Exception ex)
            {
                _logger.LogError("send failed {Error}", ex);
                return Task.FromResult(new ActorResponse(request, error: ex.Message));
            }
        }

        public int GetPartitionNumber(ActorNodeInfo nodeInfo, ActorClusterInfo clusterInfo)
        {
            //buggy needs to fix this to scale further

            var partitionNumber = nodeInfo.PartitionNumber.GetValueOrDefault(0);

            var nodeBundle = nodeInfo.NodeRole == ConstantKeys.PartitionKey
                ? partitionNumber
                : partitionNumber % clusterInfo.PartitionsNodes;

            return nodeBundle;
        }

        public void SendResponse(ActorResponse response)
        {
            try
            {
                var prop = _responseChannel.CreateBasicProperties();
                var responseRoutingKey = response.Headers[ConstantKeys.ListenerIdKey].ToString();

                response.Headers.Remove(ConstantKeys.ListenerIdKey);
                response.Headers.Add(ConstantKeys.ListenerIdKey, _responderRoutingKey);
                response.Headers.TryAdd(ConstantKeys.CorrelationIdKey, response.CorrelationId);


                prop.CorrelationId = response.CorrelationId;
                prop.Persistent = false;

                var messageBytes = SerializationUtils.Serialize(response);


                // lock (_responseChannel)
                {
                    _logger.LogDebug("send response {CorrelationId} to {RequestRoutingKey}", response.CorrelationId,
                        _responderRoutingKey);
                    _responseChannel.BasicPublish(responseRoutingKey,
                        responseRoutingKey,
                        prop,
                        messageBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("send failed {Error}", ex);
            }
        }

        private void SetupConsumer(Func<ActorRequest, Task<ActorResponse>> handleEvent)
        {
            _handleEvent = handleEvent;
            _responseConsumer = new EventingBasicConsumer(_responseChannel);

            if (_nodeInfo.NodeType == nameof(ActorSystemService))
            {
                _requestConsumer = new EventingBasicConsumer(_requestChannel);
                _requestConsumer.Received += async (_, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var request = SerializationUtils.Deserialize<ActorRequest>(body);
                    if (request == null)
                    {
                        return;
                    }

                    _logger.LogDebug("got request {CorrelationId} to {RequestRoutingKey}",
                        request.CorrelationId,
                        request);

                    var partitionKey =
                        NodeManagerUtils.ComputePartitionNumber(_clusterInfo, request.PartitionType, request.Uid);
                    if (request.IsNotityRequest == false &&
                        partitionKey != NodeManagerUtils.GetPartitionNumber(_nodeInfo, _clusterInfo))
                    {
                        var resp = new ActorResponse(request)
                        {
                            Response = new InvalidOperationException().Message,
                            IsError = true,
                            Error = "Request arrived at Invalid Partition"
                        };
                        SendResponse(resp);
                        return;
                    }

                    await HandleRequestMesssage(request);
                };
                _requestChannel.BasicConsume(_requestQueueName, true, _requestConsumer);
            }

            _responseConsumer.Received += (_, ea) =>
            {
                var body = ea.Body.ToArray();
                var response = SerializationUtils.Deserialize<ActorResponse>(body);
                var correlationId = response.Headers.ContainsKey(ConstantKeys.OriginCorrelationIdKey)
                    ? response.Headers[ConstantKeys.OriginCorrelationIdKey]
                    : response.CorrelationId;
                if (!_callBackTasksSource.TryRemove(correlationId.ToString(), out var tcs))
                {
                    _logger.LogInformation("ghost message for {Exchange} {CorrelationId}", ea.Exchange,
                        ea.BasicProperties.CorrelationId);

                    return;
                }

                if (response == null)
                {
                    return;
                }

                tcs.Key.TrySetResult(response);
            };

            _responseChannel.BasicConsume(_responseQueueName, true, _responseConsumer);
        }

        private async Task HandleRequestMesssage(ActorRequest request)
        {
            await _taskThrottler.AddTaskAsync(async () =>
            {
                var resp = await _handleEvent(request);
                SendResponse(resp);
            });
        }
    }
}
