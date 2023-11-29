// Licensed to the AiCorp- Buyconn.

using Castle.DynamicProxy;
using Delegactor.CodeGen;
using Delegactor.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InvalidOperationException = System.InvalidOperationException;

namespace Delegactor.Core
{
    public class ActorProxy : IActorProxy
    {
        private static readonly ProxyGenerator Generator = new();
        private readonly ILogger<Interceptor> _childLogger;
        private readonly ILogger<ActorProxy> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITaskThrottler<Interceptor> _taskThrottler;
        private readonly IActorSystemTransport _transport;

        public ActorProxy(ILogger<ActorProxy> logger, IServiceProvider provider,
            ITaskThrottler<Interceptor> taskThrottler, IActorSystemTransport transport)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            _taskThrottler = taskThrottler ?? throw new ArgumentNullException(nameof(taskThrottler));
            _childLogger = _serviceProvider.GetRequiredService<ILogger<Interceptor>>();
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public T GetCastleProxy<T>(string id) where T : class, new()
        {
            var proxyInstance =
                Generator.CreateClassProxy<T>(new Interceptor(_transport, _taskThrottler, _childLogger, id));
            return proxyInstance;
        }

        public T GetCastleProxyViaInterface<T>(string id) where T : class
        {
            var proxyInstance =
                Generator.CreateInterfaceProxyWithoutTarget<T>(new Interceptor(_transport, _taskThrottler, _childLogger,
                    id));
            return proxyInstance;
        }

        public T GetProxyViaInterfaceCodeGen<T>(string id) where T : class
        {
            var instance =  _serviceProvider.GetServices<IDelegactorProxy<T>>()
                .First(x => x.GetType().Name.ToLower().EndsWith("clientproxy")) ;
            instance.ActorId = id;
            
            return instance as T ??  throw new InvalidOperationException();
        }
    }
}
