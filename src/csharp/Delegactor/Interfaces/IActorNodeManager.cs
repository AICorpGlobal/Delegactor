// Licensed to the AiCorp- Buyconn.

using Delegactor.Core;
using Delegactor.Models;

namespace Delegactor.Interfaces
{
    public interface IActorNodeManager
    {
        Task ReBuildNode();
        Task<KeyValuePair<ActorBase, ActorStates>> GetServiceInstance(ActorRequest request);
        Task RunActorCleanUp();
        Task<ActorNodeInfo> RefreshActorSystemNodeDetails();
        void SetupEventHandler(Func<ActorRequest, Task<ActorResponse>> handleEvent);
        Task<ActorNodeInfo> RefreshActorClientNodeDetails();
        void TimeOutTasks();
    }
}
