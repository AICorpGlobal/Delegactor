// Licensed to the AiCorp- Buyconn.

using MessagePack;

namespace Delegactor.Models
{
    [MessagePackObject()]
    public class ActorResponse
    {
        public ActorResponse(ActorRequest request, string response = "", string error = "")
        {
            Response = response;
            ActorId = request.ActorId;
            CorrelationId = request.CorrelationId;
            Name = request.Name;
            Module = request.Module;
            Headers = request.Headers;
            IsError = !string.IsNullOrEmpty(error);
            Error = error;
        }

        public ActorResponse()
        {
        }

        [Key(0)] public string ActorId { get; set; }

        [Key(1)] public string CorrelationId { get; set; }
        [Key(2)] public string Error { get; set; }

        [Key(3)] public string Name { get; set; }

        [Key(4)] public string Module { get; set; }

        [Key(5)] public string Response { get; set; }

        [Key(6)] public bool IsError { get; set; }

        [Key(7)] public Dictionary<string, object> Headers { get; set; }
    }
}
