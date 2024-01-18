// Licensed to the AiCorp- Buyconn.

namespace Delegactor.Models
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ConcurrentMethodAttribute : Attribute
    {
        public InvokeType InvokeType => InvokeType.Concurrent;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RemoteInvokableMethod : Attribute
    {
        private readonly bool IsBroadcastNotify;

        public RemoteInvokableMethod(bool enabled = true, bool fromReplica = false, bool isBroadcastNotify = false)
        {
            IsBroadcastNotify = isBroadcastNotify;
            FromReplica = fromReplica;
            Enabled = enabled;
        }

        public bool FromReplica { get; }

        public bool Enabled { get; }
    }
}
