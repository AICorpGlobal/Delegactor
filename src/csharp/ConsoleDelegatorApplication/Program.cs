// Licensed to the AiCorp- Buyconn.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Calcluate.Contracts;
using Delegactor.Core;
using Delegactor.Injection;
using Delegactor.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConsoleDelegatorApplication // Note: actual namespace depends on the project name.
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
                            NodeType = nameof(ActorSystem),
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
                            ReplicaNodes = 1,
                            HeartBeatWindowInSeconds = 5,
                            LastUpdateTime = DateTime.UtcNow,
                            EphemeralPortStart = ephemeralPortStart,
                            EphemeralPortEnd = ephemeralPortEnd
                        }
                    );
                    services.AddMongoDbDelegactorStorage("mongodb://localhost:27017/", "ActorSystemDb");
                    services.AddDelegactorMessageBackPlane();
                    services.AddDelegactorSystemDependencies(new List<Assembly>
                    {
                        typeof(ICalculator).Assembly, typeof(Calculator).Assembly
                    });
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
