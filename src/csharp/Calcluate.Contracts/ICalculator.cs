// Licensed to the AiCorp- Buyconn.

using System.Threading.Tasks;
using Delegactor.CodeGen;
using Delegactor.Models;

namespace Calcluate.Contracts
{
    public interface ICalculator : IDelegactorProxy<ICalculator>
    {
        // [ConcurrentMethod]
        [RemoteInvokableMethod(fromReplica: false)]
        public Task<int> Sum(int a, int b, ActorRequest? request = null);

        [RemoteInvokableMethod(isBroadcastNotify: true)]
        public Task Notify(int a, int b);

        [ConcurrentMethod]
        [RemoteInvokableMethod(fromReplica: false)]
        Task<int> Diff(int a, int b, ActorRequest request = null);
    }
}
