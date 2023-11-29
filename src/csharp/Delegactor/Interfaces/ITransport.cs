// Licensed to the AiCorp- Buyconn.

using Delegactor.Models;

namespace Delegactor.Interfaces
{
    public interface IActorSystemTransport
    {
        bool Init(ActorNodeInfo nodeInfo, Func<ActorRequest, Task<ActorResponse>> handleEvent);

        Task<ActorResponse> SendRequest(ActorRequest request,
            bool noWait);

        void SendResponse(ActorResponse response);

        bool Shutdown();
    }
}
