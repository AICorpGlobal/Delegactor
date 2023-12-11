// Licensed to the AiCorp- Buyconn.

using Delegactor.CodeGen;

namespace Delegactor.Interfaces
{
    public interface IActorProxy
    {
        [Obsolete("Move your implementation to GetProxyViaInterfaceCodeGen ")]
        T GetCastleProxy<T>(string id) where T : class, new();

        [Obsolete("Move your implementation to GetProxyViaInterfaceCodeGen ")]
        T GetCastleProxyViaInterface<T>(string id) where T : class;
        T GetProxyViaInterfaceCodeGen<T>(string id) where T : class;
    }
}
