// Licensed to the AiCorp- Buyconn.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Calcluate.Contracts;
using ConsoleDelegatorApplication;
using Delegactor.Core;
using Delegactor.Injection;
using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClientApplication // Note: actual namespace depends on the project name.
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
                            NodeType = nameof(ActorClient),
                            NodeRole = "client",
                            ClusterName = Environment.MachineName,
                            InstanceId = Guid.NewGuid().ToString(),
                            PartitionNumber = null,
                            LastUpdateTime = DateTime.UtcNow
                        },
                        new ActorClusterInfo
                        {
                            ClusterName = Environment.MachineName,
                            PartitionsNodes = 1,
                            ReplicaNodes = 2,
                            HeartBeatWindowInSeconds = 5,
                            LastUpdateTime = DateTime.UtcNow
                        }
                    );
                    services.AddMongoDbDelegactorStorage("mongodb://localhost:27017/", "ActorSystemDb");
                    services.AddDelegactorMessageBackPlane("amqp://guest:guest@rabbitmq.mq:5672");
                    services.AddDelegactorClientDependencies(new List<Assembly> { typeof(ICalculator).Assembly });

                    services.AddLogging(builder =>
                        builder.AddConsole());
                }).Build();
            var hostTask = host.StartAsync();


            // var proxy = host.Services.GetService<IActorProxy>();

            var transport = host.Services.GetService<IActorSystemTransport>();

            var logger = host.Services.GetService<ILogger<ICalculator>>();

            var proxy = host.Services.GetService<IActorProxy>();

            // Console.ReadLine();

            // var logger =  host.Services.GetService<ILogger<ICalculator>>();
            benchmark:
            
            Console.WriteLine("Press Enter to start");
            Console.ReadLine();
            
            
            var calculator = proxy.GetProxyViaInterfaceCodeGen<ICalculator>("1234");
            
            var stopWatch = Stopwatch.StartNew();
            await calculator.Sum(1, 2);
            stopWatch.Stop();
            Console.WriteLine($" total time {stopWatch.Elapsed} :: {stopWatch.Elapsed.TotalSeconds} ");            //
            
            await Task.Delay(2000);

            var capacity = 900000;

            List<Task<int>> pool = new List<Task<int>>(capacity);

            for (var i = 0; i < capacity; i++)
            {            
                calculator = proxy.GetProxyViaInterfaceCodeGen<ICalculator>(Random.Shared.NextInt64(20000).ToString());
                pool.Add( calculator.Sum(1, 2));
            }
            // for (var i = 0; i < 100000; i++)
            // {            
            //     var calculator = proxy.GetProxyViaInterfaceCodeGen<ICalculator>(Random.Shared.NextInt64(20000).ToString());
            //     pool.Add( calculator.Diff(1, 2));
            // }

            await Task.WhenAll(pool);
            stopWatch.Stop();

            Console.WriteLine($" total time {stopWatch.Elapsed} :: {stopWatch.Elapsed.TotalSeconds} ");
            Console.WriteLine($"type r to run test: enter to exit ");
            var input = Console.ReadLine();
            if (input.ToLower() == "y")
            {
                goto benchmark;
            }
        }
    }
}
