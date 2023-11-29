// Licensed to the AiCorp- Buyconn.

// using System.Text.Json;
// using System.Text.Json.Serialization;
// using ZeroMQ;
//
// namespace ActorFramework
// {
//     public class ActorInvokeProtocol
//     {
//         public const string Delimiter = "#";
//         public const string Header = "#";
//         public const string Body = "#";
//         public const string Request = "#";
//         public const string Connect = "#";
//         public const string Response = "#";
//         public const string Reconnect = "#";
//
//         public List<ZFrame> GetZFrames<TBody, THeader>(THeader header, TBody body)
//         {
//             return new List<ZFrame>()
//             {
//                 new(Delimiter),
//                 new(Header),
//                 new(JsonSerializer.Serialize(header)),
//                 new(Body),
//                 new(JsonSerializer.Serialize(body))
//             };
//         }
//
//         // public (THeader, TBody) GetZFrames<TBody, THeader>(List<ZFrame> content)
//         // {
//         //     if (content.Count == 4)
//         //     {
//         //         
//         //     }
//         // }
//     }
// }



