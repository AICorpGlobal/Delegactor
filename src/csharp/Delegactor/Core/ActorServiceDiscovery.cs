// Licensed to the AiCorp- Buyconn.

using System.Collections.Concurrent;
using System.Reflection;
using Delegactor.CodeGen;
using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.Logging;

namespace Delegactor.Core
{
    public interface IActorServiceDiscovery
    {
        void LoadActors(ActorNodeInfo nodeInfo);
        Assembly ResolveActorAssemby(string module);
        Type GetServiceType(string requestModule);
    }

    public class ActorServiceDiscovery : IActorServiceDiscovery
    {
        private readonly Func<List<Assembly>> _assemblyList;
        private readonly ConcurrentDictionary<string, Assembly> _discoveredAssemblies = new();
        private readonly ConcurrentDictionary<string, Type> _discoveredTypes = new();
        private readonly ILogger<ActorServiceDiscovery> _logger;
        private readonly IStorage _store;

        public ActorServiceDiscovery(ILogger<ActorServiceDiscovery> logger,
            IStorage store,
            Func<List<Assembly>> assemblyList)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _assemblyList = assemblyList ?? throw new ArgumentNullException(nameof(assemblyList));
        }

        public Assembly ResolveActorAssemby(string module)
        {
            return _discoveredAssemblies[module];
        }

        public Type GetServiceType(string requestModule)
        {
            return _discoveredTypes.GetOrAdd(requestModule, key =>
            {
                return _discoveredAssemblies[key]
                    ?.GetType(key);
            });
        }

        public void LoadActors(ActorNodeInfo nodeInfo)
        {
            var actorNodeDetail = new ActorTypesInfo(nodeInfo);
            var assemblyList = _assemblyList();
            foreach (var assembly in assemblyList)
            {
                var appServices = assembly.GetTypes()
                    .Where(s => s.IsInterface == false && s.BaseType == typeof(ActorBase)).ToList();

                foreach (var appService in appServices)
                {
                    actorNodeDetail.ActorModuleName = appService.FullName;
                    actorNodeDetail.ClusterName = nameof(ActorSystemService);

                    _discoveredAssemblies.AddOrUpdate(appService.FullName,
                        _ => appService.Assembly,
                        (_, _) => appService.Assembly);

                    var interfaces = appService.GetInterfaces();

                    if (interfaces != null)
                    {
                        foreach (var item in interfaces)
                        {
                            if (item == typeof(IActorBase) || item == typeof(IDelegactorProxy<>))
                            {
                                continue;
                            }

                            _discoveredAssemblies.AddOrUpdate(item.FullName, _ => item.Assembly,
                                (_, _) => item.Assembly);
                        }
                    }

                    _store.UpsertTypesInfo(actorNodeDetail);
                }
            }
        }
    }
}
