// Licensed to the AiCorp- Buyconn.

using System.Threading;
using System.Threading.Tasks;
using Calcluate.Contracts;
using Delegactor.Core;
using Delegactor.Models;
using Microsoft.Extensions.Logging;

namespace ConsoleDelegatorApplication
{
    public class Calculator : ActorBase, ICalculator
    {
        private readonly ActorNodeInfo _actorNodeInfo;
        private readonly ILogger<Calculator> _logger;
        private int sum;
        private static volatile int counter = 1;


        public Calculator()
        {
        }

        public Calculator(ActorNodeInfo actorNodeInfo, ILogger<Calculator> logger)
        {
            _actorNodeInfo = actorNodeInfo;
            _logger = logger;
        }


        [ConcurrentMethod]
        [RemoteInvokableMethod(fromReplica: false)]
        public virtual async Task<int> Sum(int a, int b, ActorRequest request = null)
        {
            // _logger.LogInformation(" {ActorId} in {TypeOfNode} calculating sum", ActorId, _actorNodeInfo.NodeRole);
            sum += a + b;
            // await Task.Delay(200);

            Interlocked.Increment(ref counter);
            if (counter >= 10_00_000)
            {
                _logger.LogInformation("Reached");
            }

            return sum;
        }

        [RemoteInvokableMethod(isBroadcastNotify: true)]
        public async Task Notify(int a, int b)
        {
            _logger.LogInformation(" {ActorId} in {TypeOfNode} Got Notify", ActorId, _actorNodeInfo.NodeRole);
        }

        [ConcurrentMethod]
        [RemoteInvokableMethod(fromReplica: false)]
        public virtual async Task<int> Diff(int a, int b, ActorRequest request = null)
        {
            // _logger.LogInformation(" {ActorId} in {TypeOfNode} calculating sum", ActorId, _actorNodeInfo.NodeRole);
            sum += a - b;
            return sum;
        }
    }
}
