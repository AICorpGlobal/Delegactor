// Licensed to the AiCorp- Buyconn.

namespace Delegactor.Models
{
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

        public string Error { get; set; }

        public string ActorId { get; set; }

        public string CorrelationId { get; set; }
        public string Name { get; set; }
        public string Module { get; set; }
        public string Response { get; set; }
        public bool IsError { get; set; }
        public Dictionary<string, object> Headers { get; set; }
    }
}
