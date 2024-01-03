// Licensed to the AiCorp- Buyconn.

using System.Text;
using System.Text.Json;
using DotNetty.Buffers;
using MessagePack;

namespace Delegactor.Transport
{
    public static class DotNettyUtils
    {
        public static IByteBuffer Serialize<T>(T message)
        {
            var serializedPayLoad = JsonSerializer.Serialize(message);

            return Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes($"{serializedPayLoad}\r\n"));
        }
    }
}
