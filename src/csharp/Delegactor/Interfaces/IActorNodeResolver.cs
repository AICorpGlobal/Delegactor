// Licensed to the AiCorp- Buyconn.

using System.Net;

namespace Delegactor.Interfaces
{
    public interface IActorNodeResolver
    {
        Task<IPEndPoint> GetIpAddress(int partitionNumber, string partitionType);
    }
}
