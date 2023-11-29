// Licensed to the AiCorp- Buyconn.

using System.Text.Json;
using Castle.DynamicProxy;
using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.Logging;

namespace Delegactor.Core
{
    public class Interceptor : IInterceptor
    {
        private readonly string _id;
        private readonly ILogger<Interceptor> _logger;
        private readonly ITaskThrottler<Interceptor> _throttler;
        private readonly IActorSystemTransport _transport;

        public Interceptor(IActorSystemTransport transport, ITaskThrottler<Interceptor> throttler,
            ILogger<Interceptor> logger, string id)
        {
            _transport = transport;
            _throttler = throttler;
            _logger = logger;
            _id = id;
        }

        public void Intercept(IInvocation invocation)
        {
            _logger.LogInformation($"Before target {_id} call");
            var invokableMethod = invocation.Method.GetCustomAttributes(typeof(RemoteInvokableMethod), true)
                .FirstOrDefault() as RemoteInvokableMethod;

            if (invokableMethod == null || invokableMethod.Enabled == false)
            {
                _logger.LogDebug("invoking local instance method");
                invocation.Proceed();
                return;
            }

            var request = new ActorRequest
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorId = _id,
                Name = invocation.Method.Name,
                Module = invocation.Method.DeclaringType?.FullName,
                PartitionType = invokableMethod.FromReplica ? "replica" : "partition"
            };
            
            var keyValuePairs = invocation.Method.GetParameters()?.Select(x =>
                    new KeyValuePair<string, string>(x.Name,
                        JsonSerializer.Serialize(invocation.Arguments[x.Position])))
                .ToDictionary(x => x.Key, x => x.Value);

            request.Parameters = keyValuePairs;

            var returnType = invocation.Method.ReturnType;

            var noWait = returnType == typeof(void) || returnType == typeof(Task);
            var responseTask = _transport.SendRequest(request, noWait);

            if (noWait)
            {
                // to ensure that the no wait task has run  to completion 
                _ = responseTask.Result;

                _logger.LogDebug("no wait pattern used");
                return;
            }

            var methodReturnType = invocation.Method.ReturnType;

            var deserializeType = returnType.DeclaringType;

            if (methodReturnType != null && methodReturnType.BaseType == typeof(Task))
            {
                var t1 = new Task<dynamic>(()=>
                {
                    var resp = responseTask.Result;

                    if (resp.IsError)
                    {
                        throw new AggregateException(resp.Error);
                    }


                    if (returnType.IsGenericType && returnType.BaseType == typeof(Task))
                    {
                        deserializeType = returnType.GetGenericArguments()[0];
                    }

                    _logger.LogDebug("got resp {resp}", resp);

                    var invocationReturnValue = JsonSerializer.Deserialize(resp.Response, deserializeType);

                    return Cast(invocationReturnValue, deserializeType);
                });
                
                invocation.ReturnValue = t1;
            }
            else
            {
                var resp = responseTask.Result;

                if (resp.IsError)
                {
                    throw new AggregateException(resp.Error);
                }

                var invocationReturnValue = JsonSerializer.Deserialize(resp.Response, deserializeType);


                invocation.ReturnValue = invocationReturnValue;
            }
        }

        public static dynamic Cast(dynamic obj, Type castTo)
        {
            return Convert.ChangeType(obj, castTo);
        }
    }
}
