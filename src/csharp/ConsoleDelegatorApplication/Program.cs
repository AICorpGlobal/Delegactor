// Licensed to the AiCorp- Buyconn.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Calcluate.Contracts;
using Delegactor.Core;
using Delegactor.Injection;
using Delegactor.Interfaces;
using Delegactor.Models;
using Delegactor.Storage;
using Delegactor.Storage.MongoDb;
using Delegactor.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ConsoleDelegatorApplication // Note: actual namespace depends on the project name.
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddClusterConfig(
                        new ActorNodeInfo
                        {
                            NodeType = nameof(ActorSystem),
                            ClusterName = Environment.MachineName,
                            InstanceId = Guid.NewGuid().ToString(),
                            PartitionNumber = null,
                            LastUpdateTime = DateTime.UtcNow
                        },
                        new ActorClusterInfo()
                        {
                            ClusterName = Environment.MachineName,
                            PartitionsNodes = 2,
                            ReplicaNodes = 2,
                            HeartBeatWindowInSeconds = 5,
                            LastUpdateTime = DateTime.UtcNow
                        }
                    );
                    services.AddMongoDbDelegactorStorage("mongodb://localhost:27017/", "ActorSystemDb");
                    services.AddDelegactorMessageBackPlane("amqp://guest:guest@rabbitmq.mq:5672");
                    services.AddDelegactorSystemDependencies(new List<Assembly>() { typeof(ICalculator).Assembly ,typeof(Calculator).Assembly });
                    // services.AddTransient<Calculator>();
                    services.AddTransient<ICalculator, Calculator>();
                    services.AddTransient<Calculator>();
                    services.AddLogging(builder =>
                        builder.AddConsole());
                }).Build();
            await host.RunAsync();
            
        }
    }
}
