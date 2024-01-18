// Licensed to the AiCorp- Buyconn.

namespace Delegactor.Interfaces
{
    public interface IActorProxy
    {
        T GetProxyViaInterfaceCodeGen<T>(string id) where T : class;
    }
}
