// Licensed to the AiCorp- Buyconn.

using Delegactor.Core;
using Delegactor.Models;

namespace Delegactor.Interfaces
{
    public interface IActorNodeManager
    {
        Task ReBuildNode();
        Task<KeyValuePair<ActorBase, ActorStates>> GetActorInstance(ActorRequest request);
        Task RunActorCleanUp();
        Task<ActorNodeInfo> RefreshActorSystemNodeDetails();
        void SetupEventHandler(Func<ActorRequest, Task<ActorResponse>> handleEvent);
        Task<ActorNodeInfo> RefreshActorClientNodeDetails();
        void TimeOutTasks();
        Task<List<KeyValuePair<ActorBase, ActorStates>>> GetAllActorInstancesOfAModule(ActorRequest request);
        Task ShutDown();
    }
}
