// Licensed to the AiCorp- Buyconn.

using Delegactor.CodeGen;

namespace Delegactor.Interfaces
{
    public interface IActorProxy
    {
        T GetCastleProxy<T>(string id) where T : class, new();
        T GetCastleProxyViaInterface<T>(string id) where T : class;
        T GetProxyViaInterfaceCodeGen<T>(string id) where T : class;
    }
}
