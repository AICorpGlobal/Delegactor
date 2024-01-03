// Licensed to the AiCorp- Buyconn.

using System.Net;
using Delegactor.Interfaces;
using Delegactor.Models;

namespace Delegactor.Core
{
    public class ActorNodeResolver : IActorNodeResolver
    {
        private readonly ActorClusterInfo _clusterInfo;
        private readonly IStorage _storage;

        public ActorNodeResolver(ActorClusterInfo clusterInfo, IStorage storage)
        {
            _clusterInfo = clusterInfo;
            _storage = storage;
        }

        public async Task<IPEndPoint> GetIpAddress(int partitionNumber, string partitionType)
        {
            var nodeInfo = await _storage.GetNodeInfo(_clusterInfo, partitionNumber, partitionType);
            if (nodeInfo == null || nodeInfo.Port <= 0)
            {
                throw new KeyNotFoundException();
            }

            return new IPEndPoint(IPAddress.Parse(nodeInfo.IpAddress), nodeInfo.Port);
        }
    }
}
