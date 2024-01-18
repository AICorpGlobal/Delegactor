// Licensed to the AiCorp- Buyconn.

using Delegactor.CodeGen;
using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Delegactor.Core
{
    public class ActorProxy : IActorProxy
    {
        private readonly ILogger<ActorProxy> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ActorProxy(ILogger<ActorProxy> logger, IServiceProvider provider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }


        public T GetProxyViaInterfaceCodeGen<T>(string id) where T : class
        {
            var instance = _serviceProvider.GetServices<IDelegactorProxy<T>>()
                .First(x => x.GetType().Name.ToLower().EndsWith(ConstantKeys.ClientProxyNameKey));
            instance.ActorId = id;

            return instance as T ?? throw new InvalidOperationException();
        }
    }
}
