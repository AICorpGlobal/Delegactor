﻿// Licensed to the AiCorp- Buyconn.

using System.Threading.Tasks;
using Calcluate.Contracts;
using Delegactor.Core;
using Delegactor.Models;
using Microsoft.Extensions.Logging;

namespace ConsoleDelegatorApplication
{
    public class Calculator: ActorBase, ICalculator
    {
        private readonly ActorNodeInfo _actorNodeInfo;
        private readonly ILogger<Calculator> _logger;
        private int sum;

        public Calculator()
        {
        }

        public Calculator(ActorNodeInfo actorNodeInfo, ILogger<Calculator> logger)
        {
            _actorNodeInfo = actorNodeInfo;
            _logger = logger;
        }

     
        [RemoteInvokableMethod(fromReplica:true)]
        public virtual async Task<int> Sum(int a, int b, ActorRequest request = null)
        {
            // _logger.LogInformation(" {ActorId} in {TypeOfNode} calculating sum", ActorId, _actorNodeInfo.NodeRole);
            sum += a + b;
            return sum;
        }        
        
        [RemoteInvokableMethod(fromReplica:false)]
        public virtual async Task<int> Diff(int a, int b, ActorRequest request = null)
        {
            // _logger.LogInformation(" {ActorId} in {TypeOfNode} calculating sum", ActorId, _actorNodeInfo.NodeRole);
            sum += a - b;
            return sum;
        }

    }
}
