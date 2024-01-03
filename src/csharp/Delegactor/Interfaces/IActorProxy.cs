// Licensed to the AiCorp- Buyconn.

using Delegactor.CodeGen;

namespace Delegactor.Interfaces
{
    public interface IActorProxy
    {
        T GetProxyViaInterfaceCodeGen<T>(string id) where T : class;
    }
}
