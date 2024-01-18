// Licensed to the AiCorp- Buyconn.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Calcluate.Contracts;
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
            var ephemeralPortStart = 39500;

            var ephemeralPortEnd = 41500;

            var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    var ipAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                                        .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork)
                                        ?.ToString() ??
                                    string.Empty;

                    services.AddClusterConfig(
                        new ActorNodeInfo
                        {
                            NodeType = nameof(ActorClientService),
                            NodeRole = "client",
                            ClusterName = Environment.MachineName,
                            InstanceId = Guid.NewGuid().ToString(),
                            PartitionNumber = null,
                            LastUpdateTime = DateTime.UtcNow,
                            IpAddress = ipAddress,
                            Port = -1
                        },
                        new ActorClusterInfo
                        {
                            ClusterName = Environment.MachineName,
                            PartitionsNodes = 1,
                            ReplicaNodes = 0,
                            HeartBeatWindowInSeconds = 5,
                            LastUpdateTime = DateTime.UtcNow,
                            EphemeralPortStart = ephemeralPortStart,
                            EphemeralPortEnd = ephemeralPortEnd
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

            Console.WriteLine("Press Enter to Start");
            Console.ReadLine();
            Console.WriteLine("Sending Test Request");

            var calculator = proxy.GetProxyViaInterfaceCodeGen<ICalculator>("1234");
            await calculator.Sum(1, 2);
            calculator = proxy.GetProxyViaInterfaceCodeGen<ICalculator>("4567");
            await calculator.Sum(1, 2);
            //
            
            Console.WriteLine("Press Enter to Start Benchmarking");
            Console.ReadLine();
            //
            // var logger =  host.Services.GetService<ILogger<ICalculator>>();
            benchmark:
            var stopWatch = Stopwatch.StartNew();
            var pool = new ConcurrentBag<Task>();
            Parallel.For(0, 5_00_000, _ => 
            {
                var temp = proxy.GetProxyViaInterfaceCodeGen<ICalculator>(Random.Shared.NextInt64(10000).ToString());
                // await calculator.Sum(3, 2);
                pool.Add(temp.Sum(1, 2));
            });

            Console.WriteLine("Added to pool");
            await Task.WhenAll(pool);
            // for (var i = 0; i < 100000; i++)
            // {            
            //     var calculator = proxy.GetProxyViaInterfaceCodeGen<ICalculator>(Random.Shared.NextInt64(20000).ToString());
            //     pool.Add( calculator.Diff(1, 2));
            // }

            stopWatch.Stop();

            Console.WriteLine($" total time {stopWatch.Elapsed} :: {stopWatch.Elapsed.TotalSeconds} ");
            Console.WriteLine("Press enter to send BroadCast");
            Console.ReadLine();
            await calculator.Notify(1, 2);
            Console.WriteLine("type y to run test: enter to exit ");
            var input = Console.ReadLine();
            if (input.ToLower() == "y")
            {
                goto benchmark;
            }
        }
    }
}
