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
        private readonly ActorClusterInfo _clusterInfo;

        private readonly Dictionary<string, string> _configuration;

        private readonly ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>
            _callBackTasksSource;

        private readonly ILogger<RabbitMqTransport> _logger;
        private string _actorRequestTopic;
        private string _actorResponseTopic;
        private ConnectionFactory _factory;
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
            ActorNodeInfo nodeInfo,
            ActorClusterInfo clusterInfo,
            Dictionary<string, string> configuration)
        {
            _callBackTasksSource = callBackTasksSource;
            _logger = logger;
            _nodeInfo = nodeInfo;
            _clusterInfo = clusterInfo;
            _configuration = configuration;
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
                _actorRequestTopic = $"actor_request_topic.{_nodeInfo.ClusterName}";
                _actorResponseTopic = $"actor_response_topic.{_nodeInfo.ClusterName}";

                var exclusive = _nodeInfo.NodeType == nameof(ActorClient);

                var nodeInfoPartitionNumber = _nodeInfo.GetSubscriptionId(_clusterInfo);

                _requestQueueName = _nodeInfo.NodeType == nameof(ActorSystem)
                    ? $"request.{_nodeInfo.ClusterName}.{_nodeInfo.NodeType}.{nodeInfoPartitionNumber}.{_nodeInfo.NodeRole}"
                    : $"request.{_nodeInfo.ClusterName}.{_nodeInfo.NodeType}.{_nodeInfo.InstanceId}.{_nodeInfo.NodeRole}";

                _requesterRoutingKey = _nodeInfo.NodeType == nameof(ActorSystem)
                    ? $"request.{_nodeInfo.ClusterName}.{_nodeInfo.NodeType}.{nodeInfoPartitionNumber}.{_nodeInfo.NodeRole}"
                    : $"request.{_nodeInfo.ClusterName}.{_nodeInfo.NodeType}.{_nodeInfo.InstanceId}.{_nodeInfo.NodeRole}";

                _responseQueueName = _nodeInfo.NodeType == nameof(ActorSystem)
                    ? $"response.{_nodeInfo.ClusterName}.{_nodeInfo.NodeType}.{nodeInfoPartitionNumber}.{_nodeInfo.NodeRole}"
                    : $"response.{_nodeInfo.ClusterName}.{_nodeInfo.NodeType}.{_nodeInfo.InstanceId}.{_nodeInfo.NodeRole}";

                _responderRoutingKey = _nodeInfo.NodeType == nameof(ActorSystem)
                    ? $"response.{_nodeInfo.ClusterName}.{_nodeInfo.NodeType}.{nodeInfoPartitionNumber}.{_nodeInfo.NodeRole}"
                    : $"response.{_nodeInfo.ClusterName}.{_nodeInfo.NodeType}.{_nodeInfo.InstanceId}.{_nodeInfo.NodeRole}";


                ushort eventsInParallel = 1; // move to configurations

                _factory = new ConnectionFactory { Uri = new Uri(_configuration["connectionString"]) };

                _sendConnection = _factory.CreateConnection();

                _requestChannel = _sendConnection.CreateModel();

                _responseChannel = _sendConnection.CreateModel();


                _responseChannel.BasicQos(0, eventsInParallel, false);

                _responseChannel.BasicQos(0, eventsInParallel, false);


                if (_nodeInfo.NodeType == nameof(ActorSystem))
                {
                    _requestChannel.ExchangeDeclare(_actorRequestTopic, ExchangeType.Topic);
                    _requestChannel.QueueDeclare(_requestQueueName, false, exclusive,
                        false);
                    _requestChannel.QueueBind(_requestQueueName, _actorRequestTopic,
                        _requesterRoutingKey);
                }


                _responseChannel.ExchangeDeclare(_actorResponseTopic, ExchangeType.Topic,
                    autoDelete: false);
                _responseChannel.QueueDeclare(_responseQueueName, false, exclusive,
                    false);
                _responseChannel.QueueBind(_responseQueueName, _actorResponseTopic,
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
            var requestRoutingKey =
                $"request.{_clusterInfo.ClusterName}.{nameof(ActorSystem)}.{_clusterInfo.ComputeKey(request.Uid)}.{request.PartitionType}";
            try
            {
                var prop = _requestChannel.CreateBasicProperties();

                request.Headers.TryAdd("listenerId", _responderRoutingKey);
                request.Headers.TryAdd("CorrelationId", request.CorrelationId);


                prop.CorrelationId = request.CorrelationId;

                var messageBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));

                lock (_requestChannel)
                {
                    _logger.LogDebug("send request {CorrelationId} to {RequestRoutingKey}", request.CorrelationId,
                        requestRoutingKey);

                    _requestChannel.BasicPublish(_actorRequestTopic,
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

        public void SendResponse(ActorResponse response)
        {
            try
            {
                var prop = _responseChannel.CreateBasicProperties();
                var responseRoutingKey = response.Headers["listenerId"].ToString();

                response.Headers.Remove("listenerId");
                response.Headers.Add("listenerId", _responderRoutingKey);
                response.Headers.TryAdd("CorrelationId", response.CorrelationId);


                prop.CorrelationId = response.CorrelationId;

                var serialize = JsonSerializer.Serialize(response);
                var messageBytes = Encoding.UTF8.GetBytes(serialize);


                lock (_responseChannel)
                {
                    _logger.LogDebug("send response {CorrelationId} to {RequestRoutingKey}", response.CorrelationId,
                        _responderRoutingKey);
                    _responseChannel.BasicPublish(_actorResponseTopic,
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

                request.Headers.TryAdd("listenerId", _responderRoutingKey);
                request.Headers.TryAdd("CorrelationId", request.CorrelationId);
                request.Headers.TryAdd("requestType", "notify");


                var messageBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));

                lock (_requestChannel)
                {
                    for (int index = 0; index < _clusterInfo.PartitionsNodes; index++)
                    {
                        var requestRoutingKey =
                            $"request.{_clusterInfo.ClusterName}.{nameof(ActorSystem)}.{index}.partition";
                        _logger.LogDebug("send request {CorrelationId} to {RequestRoutingKey}", request.CorrelationId,
                            requestRoutingKey);

                        prop.CorrelationId = $"{Guid.NewGuid()}notify{request.CorrelationId}";
                        _requestChannel.BasicPublish(_actorRequestTopic,
                            requestRoutingKey,
                            prop,
                            messageBytes);
                    }

                    for (int index = 0; index < _clusterInfo.ReplicaNodes; index++)
                    {
                        var requestRoutingKey =
                            $"request.{_clusterInfo.ClusterName}.{nameof(ActorSystem)}.{index}.replica";

                        prop.CorrelationId = $"{Guid.NewGuid()}notify{request.CorrelationId}";
                        _logger.LogDebug("send request {CorrelationId} to {RequestRoutingKey}", request.CorrelationId,
                            requestRoutingKey);

                        _requestChannel.BasicPublish(_actorRequestTopic,
                            requestRoutingKey,
                            prop,
                            messageBytes);
                    }
                }

                return Task.FromResult(new ActorResponse(request));
            }
            catch (Exception ex)
            {
                _logger.LogError("send failed {Error}", ex);
                return Task.FromResult(new ActorResponse(request, error: ex.Message));
            }
        }

        private void SetupConsumer(Func<ActorRequest, Task<ActorResponse>> handleEvent)
        {
            _responseConsumer = new EventingBasicConsumer(_responseChannel);

            if (_nodeInfo.NodeType == nameof(ActorSystem))
            {
                _requestConsumer = new EventingBasicConsumer(_requestChannel);
                _requestConsumer.Received += async (_, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    if (ea.Exchange == _actorRequestTopic)
                    {
                        var request = JsonSerializer.Deserialize<ActorRequest>(message);
                        if (request == null)
                        {
                            return;
                        }

                        _logger.LogDebug("got request {CorrelationId} to {RequestRoutingKey}",
                            request.CorrelationId,
                            request);

                        var partitionKey = _clusterInfo.ComputeKey(request.Uid);
                        if (request.IsNotityRequest == false &&
                            partitionKey != _nodeInfo.GetSubscriptionId(_clusterInfo))
                        {
                            request.Partition = partitionKey;
                            request.Headers.Remove(nameof(request.CorrelationId), out var correlationId);
                            correlationId ??= Guid.NewGuid().ToString();
                            request.Headers.TryAdd("origin-correlationId", correlationId.ToString());
                            request.CorrelationId = Guid.NewGuid().ToString();
                            await SendRequest(request, true);
                            return;
                        }

                        var response = await handleEvent(request);

                        _logger.LogDebug("handle completed request ");

                        SendResponse(response);
                    }
                    else
                    {
                        _logger.LogInformation("ghost message for {Exchange}", ea.Exchange);
                    }
                };
                _requestChannel.BasicConsume(_requestQueueName, true, _requestConsumer);
            }

            _responseConsumer.Received += (_, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                if (ea.Exchange == _actorResponseTopic)
                {
                    var response = JsonSerializer.Deserialize<ActorResponse>(message);
                    var correlationId = response.Headers.ContainsKey("origin-correlationId")
                        ? response.Headers["origin-correlationId"]
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
                }
                else
                {
                    _logger.LogInformation("ghost message for {Exchange} {CorrelationId}", ea.Exchange,
                        ea.BasicProperties.CorrelationId);
                }
            };

            _responseChannel.BasicConsume(_responseQueueName, true, _responseConsumer);
        }
    }
}
