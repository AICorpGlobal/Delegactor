// Licensed to the AiCorp- Buyconn.

using Delegactor.CodeGen;
using Delegactor.Models;

namespace Delegactor.Core
{
    public interface IActorBase:IDelegactorProxy<IActorBase>
    {
        int MaxPartitions { get; set; }
        // string ActorId { get; set; }
        TimeSpan ActivationWindow { get; set; }
        string Module { get; set; }
        Task<ActorResponse> InvokeMethod(ActorRequest request);
        Task OnLoad(ActorRequest actorRequest);
        Task OnUnLoad(ActorRequest actorRequest);
    }
}
