// Licensed to the AiCorp- Buyconn.

using Delegactor.CodeGen;
using Delegactor.Models;

namespace Delegactor.Interfaces
{
    public interface IActorBase : IDelegactorProxy<IActorBase>
    {
        public int MaxPartitions { get; set; }

        // string ActorId { get; set; }
        public TimeSpan ActivationWindow { get; set; }
        public string Module { get; set; }
        public Task<ActorResponse> InvokeMethod(ActorRequest request);
        public Task OnLoad(ActorRequest actorRequest);
        public Task OnUnLoad(ActorRequest actorRequest);
    }
}
