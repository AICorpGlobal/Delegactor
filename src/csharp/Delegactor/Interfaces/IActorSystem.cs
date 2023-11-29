// Licensed to the AiCorp- Buyconn.

using Delegactor.Models;

namespace Delegactor.Interfaces
{
    public interface IActorSystem
    {
        Task<ActorResponse> HandleEvent(ActorRequest request);
    }
}
