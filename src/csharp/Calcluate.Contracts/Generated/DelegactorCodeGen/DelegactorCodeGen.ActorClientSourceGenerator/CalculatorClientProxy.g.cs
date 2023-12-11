// Licensed to the AiCorp- Buyconn.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Delegactor.Core;
using Delegactor.CodeGen;
using Delegactor.Interfaces;
using Delegactor.Models;
using Microsoft.Extensions.Logging;
 

            
// Licensed to the AiCorp- Buyconn.

using System.Threading.Tasks;


            
using Delegactor.CodeGen;


            
using Delegactor.Models;



namespace Calcluate.Contracts
{
    public class CalculatorClientProxy : ICalculator, IDelegactorProxy<ICalculator>
    {
        private string _actorId;
        private readonly ILogger<CalculatorClientProxy> _logger;
        private readonly IActorSystemTransport _transport;
        
        private string _module = "Calcluate.Contracts.ICalculator";

        public CalculatorClientProxy(
            IActorSystemTransport transport,
            ILogger<CalculatorClientProxy> logger)
        {
            _transport = transport;
            _logger = logger;
        }

        public string ActorId
        {
            get => _actorId;
            set => _actorId = value;
        }
        



        public async Task<int> Sum (int a, int b, ActorRequest? request = null)
        {
            string invokedMethodName = "Sum";
            

            ActorRequest __request = new ActorRequest
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorId = _actorId,
                Name = invokedMethodName,
                Module = _module,
                PartitionType = "replica"
            };

            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
           

            
            keyValuePairs.Add( "a", a == default ? string.Empty : JsonSerializer.Serialize(a) );


            
            keyValuePairs.Add( "b", b == default ? string.Empty : JsonSerializer.Serialize(b) );


            
            keyValuePairs.Add( "request", request == default ? string.Empty : JsonSerializer.Serialize(request) );


            
            __request.Parameters = keyValuePairs;



            bool noWait = false;
            
 


            ActorResponse resp = await _transport.SendRequest(__request, noWait);  
            
           
            if (resp.IsError)
            {
                throw new AggregateException(resp.Error);
            }



/*<int>*/



            return JsonSerializer.Deserialize<int>(resp.Response);
            


        }
        


        public async Task Notify (int a, int b)
        {
            string invokedMethodName = "Notify";
            

            ActorRequest __request = new ActorRequest
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorId = _actorId,
                Name = invokedMethodName,
                Module = _module,
                PartitionType = "partition"
            };

            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
           

            
            keyValuePairs.Add( "a", a == default ? string.Empty : JsonSerializer.Serialize(a) );


            
            keyValuePairs.Add( "b", b == default ? string.Empty : JsonSerializer.Serialize(b) );


            
            __request.Parameters = keyValuePairs;



            bool noWait = true;

 



            ActorResponse resp = await _transport.SendBroadCastNotify(__request);



/**/



            return;



        }
        


        public async Task<int> Diff (int a, int b, ActorRequest request = null)
        {
            string invokedMethodName = "Diff";
            

            ActorRequest __request = new ActorRequest
            {
                CorrelationId = Guid.NewGuid().ToString(),
                ActorId = _actorId,
                Name = invokedMethodName,
                Module = _module,
                PartitionType = "partition"
            };

            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
           

            
            keyValuePairs.Add( "a", a == default ? string.Empty : JsonSerializer.Serialize(a) );


            
            keyValuePairs.Add( "b", b == default ? string.Empty : JsonSerializer.Serialize(b) );


            
            keyValuePairs.Add( "request", request == default ? string.Empty : JsonSerializer.Serialize(request) );


            
            __request.Parameters = keyValuePairs;



            bool noWait = false;
            
 


            ActorResponse resp = await _transport.SendRequest(__request, noWait);  
            
           
            if (resp.IsError)
            {
                throw new AggregateException(resp.Error);
            }



/*<int>*/



            return JsonSerializer.Deserialize<int>(resp.Response);
            


        }
        


    }
}
