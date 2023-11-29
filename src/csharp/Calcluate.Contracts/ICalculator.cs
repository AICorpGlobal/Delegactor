// Licensed to the AiCorp- Buyconn.

using System.Threading.Tasks;
using Delegactor.CodeGen;
using Delegactor.Models;

namespace Calcluate.Contracts
{
    public interface ICalculator : IDelegactorProxy<ICalculator>
    {
        [RemoteInvokableMethod(fromReplica: true)]
        public Task<int> Sum(int a, int b, ActorRequest? request = null);
        
        [RemoteInvokableMethod(fromReplica: false)]
        Task<int> Diff(int a, int b, ActorRequest request = null);
    }

 

}
