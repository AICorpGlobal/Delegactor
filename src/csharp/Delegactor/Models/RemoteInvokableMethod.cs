// Licensed to the AiCorp- Buyconn.

namespace Delegactor.Models
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ConcurrentMethodAttribute : Attribute
    {
        public ConcurrentMethodAttribute(InvokeType invokeType = InvokeType.Concurrent)
        {
            InvokeType = invokeType;
        }

        public InvokeType InvokeType { get; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RemoteInvokableMethod : Attribute
    {
        public RemoteInvokableMethod(bool enabled = true, bool fromReplica = false)
        {
            FromReplica = fromReplica;
            Enabled = enabled;
        }

        public bool FromReplica { get; }

        public bool Enabled { get; }
    }
}
