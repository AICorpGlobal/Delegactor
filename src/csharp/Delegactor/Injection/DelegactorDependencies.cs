// Licensed to the AiCorp- Buyconn.

using System.Collections.Concurrent;
using System.Reflection;
using Delegactor.CodeGen;
using Delegactor.Core;
using Delegactor.Interfaces;
using Delegactor.Models;
using Delegactor.Storage.MongoDb;
using Delegactor.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Polly;
using Polly.Retry;

namespace Delegactor.Injection
{
    public static class DelegactorDependencies
    {
        private static bool ClusterConfigAdded;
        private static bool ClusterStorageAdded;
        private static bool ClusterMessageBackPlaneAdded;

        public static IServiceCollection AddDelegactorSystemDependencies(this IServiceCollection services,
            List<Assembly> assemblies)
        {
            if (ClusterConfigAdded == false)
            {
                throw new InvalidOperationException("missing Cluster Config");
            }

            if (ClusterStorageAdded == false)
            {
                throw new InvalidOperationException("missing Cluster Storage");
            }

            if (ClusterMessageBackPlaneAdded == false)
            {
                throw new InvalidOperationException("missing Cluster MessageBackPlane");
            }


            services.AddSingleton<IActorServiceDiscovery>(ctx =>
            {
                return new ActorServiceDiscovery(
                    ctx.GetService<ILogger<ActorServiceDiscovery>>(),
                    ctx.GetService<IStorage>(),
                    () =>
                    {
                        return assemblies;
                    });
            });
            services.AddSingleton<IActorProxy, ActorProxy>();
            services.AddSingleton<IActorSystem, ActorSystem>();
            services.AddSingleton<IActorClusterManager, ActorClusterManager>();
            services.AddSingleton(typeof(ITaskThrottler<>), typeof(TaskThrottler<>));
            services.AddSingleton<IActorNodeManager, ActorNodeManager>();
            services.AddHostedService<ActorSystem>();
            return services;
        }

        public static IServiceCollection AddDelegactorClientDependencies(this IServiceCollection services,
            List<Assembly> assemblies)
        {
            if (ClusterConfigAdded == false)
            {
                throw new InvalidOperationException("missing Cluster Config");
            }

            if (ClusterStorageAdded == false)
            {
                throw new InvalidOperationException("missing Cluster Storage");
            }

            if (ClusterMessageBackPlaneAdded == false)
            {
                throw new InvalidOperationException("missing Cluster MessageBackPlane");
            }


            services.AddSingleton<IActorServiceDiscovery>(ctx =>
            {
                return new ActorServiceDiscovery(
                    ctx.GetService<ILogger<ActorServiceDiscovery>>(),
                    ctx.GetService<IStorage>(),
                    () =>
                    {
                        return assemblies;
                    });
            });

            services.AddSingleton<IActorProxy, ActorProxy>();
            services.AddSingleton<IActorClient, ActorClient>();
            services.AddSingleton(typeof(ITaskThrottler<>), typeof(TaskThrottler<>));
            services.AddSingleton<IActorClusterManager, ActorClusterManager>();
            services.AddSingleton<IActorNodeManager, ActorNodeManager>();
            services.LoadProxyClients(assemblies);
            services.AddHostedService<ActorClient>();
            return services;
        }

        public static IServiceCollection LoadProxyClients(this IServiceCollection serviceCollection,
            List<Assembly> assemblyList)
        {
            foreach (Assembly assembly in assemblyList)
            {
                var appServices = assembly.GetTypes()
                    .Where(x => x.IsInterface == false
                                && x.GetInterfaces()
                                    .Any(y => string.IsNullOrEmpty(y.FullName)==false && y.FullName.StartsWith(typeof(IDelegactorProxy<>).FullName))).ToList();

                foreach (var appService in appServices)
                {
                    var interfaces = appService.GetInterfaces();

                    if (interfaces == null || interfaces.Length == 0)
                        continue;

                    var entries = interfaces.Where(x => x.FullName.StartsWith(typeof(IDelegactorProxy<>).FullName))
                        .ToList();

                    foreach (var entry in entries)
                    {
                        serviceCollection.AddTransient(entry, appService);
                    }
                }
            }

            return serviceCollection;
        }

        public static IServiceCollection AddClusterConfig(
            this IServiceCollection services,
            ActorNodeInfo actorNodeInfo,
            ActorClusterInfo actorClusterInfo)
        {
            services.AddSingleton(ctx =>
            {
                return actorNodeInfo;
            });
            services.AddSingleton(ctx =>
            {
                return actorClusterInfo;
            });
            ClusterConfigAdded = true;
            return services;
        }

        public static IServiceCollection AddMongoDbDelegactorStorage(this IServiceCollection services,
            string mongodbConnectionString, string actorSystemDbName)
        {
            services.AddSingleton<IMongoClient>(ctx =>
            {
                var iMongoClient = new MongoClient(mongodbConnectionString);
                return iMongoClient;
            });


            services.AddSingleton(ctx =>
            {
                var client = ctx.GetService<IMongoClient>();
                var db = client.GetDatabase(actorSystemDbName,
                    new MongoDatabaseSettings()
                    {
                        ReadPreference = ReadPreference.PrimaryPreferred,
                        ReadConcern = ReadConcern.Majority,
                        WriteConcern = WriteConcern.WMajority,
                    });


                return db;
            });

            services.AddSingleton(ctx =>
            {
                var mongoDatabase = ctx.GetService<IMongoDatabase>();

                var count = mongoDatabase.ListCollectionNames()
                    .ToList()
                    .Count(x => x.Contains(nameof(ActorNodeInfo)));
                if (count != 0)
                {
                    var temp = mongoDatabase.GetCollection<MongoDocument<ActorNodeInfo>>(nameof(ActorNodeInfo),
                        new MongoCollectionSettings { AssignIdOnInsert = true });
                    return temp;
                }

                mongoDatabase.CreateCollection(nameof(ActorNodeInfo));


                var collection = mongoDatabase.GetCollection<MongoDocument<ActorNodeInfo>>(nameof(ActorNodeInfo),
                    new MongoCollectionSettings { AssignIdOnInsert = true });

                var indexKeysDefinition = Builders<MongoDocument<ActorNodeInfo>>
                    .IndexKeys
                    .Ascending(nameof(MongoDocument<ActorNodeInfo>.LastUpdateTime));

                var indexOptions = new CreateIndexOptions { ExpireAfter = new TimeSpan(1, 0, 0, 0) };

                var indexModel = new CreateIndexModel<MongoDocument<ActorNodeInfo>>(indexKeysDefinition, indexOptions);

                collection.Indexes.CreateOne(indexModel);

                return collection;
            });

            services.AddSingleton(ctx =>
            {
                var mongoDatabase = ctx.GetService<IMongoDatabase>();

                var count = mongoDatabase.ListCollectionNames()
                    .ToList()
                    .Count(x => x.Contains(nameof(ActorClusterInfo)));
                if (count != 0)
                {
                    var temp = mongoDatabase.GetCollection<MongoDocument<ActorClusterInfo>>(
                        nameof(ActorClusterInfo),
                        new MongoCollectionSettings { AssignIdOnInsert = true });
                    return temp;
                }

                mongoDatabase.CreateCollection(nameof(ActorClusterInfo));

                return mongoDatabase.GetCollection<MongoDocument<ActorClusterInfo>>(nameof(ActorClusterInfo),
                    new MongoCollectionSettings { AssignIdOnInsert = true });
            });

            services.AddSingleton(ctx =>
            {
                var mongoDatabase = ctx.GetService<IMongoDatabase>();

                var count = mongoDatabase.ListCollectionNames()
                    .ToList()
                    .Count(x => x.Contains(nameof(ActorTypesInfo)));
                if (count != 0)
                {
                    var temp = mongoDatabase.GetCollection<MongoDocument<ActorTypesInfo>>(
                        nameof(ActorTypesInfo),
                        new MongoCollectionSettings { AssignIdOnInsert = true });
                    return temp;
                }

                mongoDatabase.CreateCollection(nameof(ActorTypesInfo));

                return mongoDatabase.GetCollection<MongoDocument<ActorTypesInfo>>(nameof(ActorTypesInfo),
                    new MongoCollectionSettings { AssignIdOnInsert = true });
            });

            services.AddSingleton<IStorage, MongoStorage>();
            ClusterStorageAdded = true;
            return services;
        }

        public static IServiceCollection AddDelegactorMessageBackPlane(this IServiceCollection services,
            string? rabbitmqMqConnectionString = null)
        {
            if (ClusterConfigAdded == false)
            {
                throw new InvalidOperationException("missing Cluster Config");
            }

            services.AddSingleton((ctx) =>
            {
                ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
                    .AddRetry(new RetryStrategyOptions()
                    {
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = new TimeSpan(0,0,5)
                    }) // Add retry using the default options
                    .Build();
                return pipeline;
            });

            services.AddSingleton((ctx) =>
                new ConcurrentDictionary<string, KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>());

            services.AddSingleton<IActorNodeResolver, ActorNodeResolver>();

            if (string.IsNullOrEmpty(rabbitmqMqConnectionString))

            {
                services.AddSingleton<IActorSystemTransport, DotNettyTransport>(ctx =>
                {
                    var dotNettyTransport = new DotNettyTransport(
                        ctx.GetService<ConcurrentDictionary<string,
                            KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>>(),
                        ctx.GetService<ILogger<DotNettyTransport>>(),
                        ctx.GetService<ITaskThrottler<DotNettyTransport>>(),
                        ctx.GetService<ActorNodeInfo>(),
                        ctx.GetService<ActorClusterInfo>(),
                        ctx.GetService<IActorNodeResolver>(),
                        ctx.GetService<ILogger<DotNettyTcpServerTransportHandler>>(),
                        ctx.GetService<ILogger<DotNettyTcpClientTransportHandler>>());
                    return dotNettyTransport;
                });
            }
            else
            {
                services.AddSingleton<IActorSystemTransport, RabbitMqTransport>(ctx =>
                {
                    var rabbitMqTransport = new RabbitMqTransport(
                        ctx.GetService<ConcurrentDictionary<string,
                            KeyValuePair<TaskCompletionSource<ActorResponse>, DateTime>>>(),
                        ctx.GetService<ILogger<RabbitMqTransport>>(),
                        ctx.GetService<ITaskThrottler<RabbitMqTransport>>(),
                        ctx.GetService<ActorNodeInfo>(),
                        ctx.GetService<ActorClusterInfo>(),
                        new Dictionary<string, string> { { "connectionString", rabbitmqMqConnectionString } });
                    return rabbitMqTransport;
                });
            }

            ClusterMessageBackPlaneAdded = true;
            return services;
        }
    }
}
