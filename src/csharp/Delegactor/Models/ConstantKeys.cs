// Licensed to the AiCorp- Buyconn.

namespace Delegactor.Models
{
    public static class ConstantKeys
    {
        public const string PartitionKey = "partition";
        public const string ReplicaKey = "replcia";
        public const string OriginCorrelationIdKey = "origin-correlationId";
        public const string CorrelationIdKey = "correlationId";
        public const string RequestTypeKey = "RequestTypeKey";
        public const string RequestTypeNotifyKey = "notify";
        public const string ListenerIdKey = "listenerid";
        public const string ConnectionStringKey = "connectionString";
        public const string OperationalKey = "operational";
        public const string NodeStateFailed = "failed";
        public const string NoNodeRoleKey = "no-role";
        public const string NodeStateNotAPartition = "Not-A-Partition";
        public const string ClusterStateOperational = "operational";
        public const string ClusterStateRepartitioning = "re-partitioning";
        public const string ClientProxyNameKey = "clientproxy";
    }
}
