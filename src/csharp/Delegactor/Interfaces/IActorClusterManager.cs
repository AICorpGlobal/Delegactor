// Licensed to the AiCorp- Buyconn.

using Delegactor.Models;

namespace Delegactor.Interfaces
{
    public interface IActorClusterManager
    {
        Task<ActorClusterInfo> RefreshActorSystemClusterDetails();
        Task<ActorClusterInfo> RefreshActorClientClusterDetails();
    }
}
