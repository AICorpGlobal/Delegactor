// Licensed to the AiCorp- Buyconn.

using MongoDB.Bson.Serialization.Attributes;

namespace Delegactor.Storage.MongoDb
{
    public class MongoDocument<T>
    {
        [BsonId] public string Id { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public T Entry { get; set; }
        public string Signature { get; set; }
    }
}
