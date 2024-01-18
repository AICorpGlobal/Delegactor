// Licensed to the AiCorp- Buyconn.

using System.Text;
using DotNetty.Buffers;
using MessagePack;

namespace Delegactor.Transport
{
    public static class SerializationUtils
    {
        public static IByteBuffer SerializeToIByteBuffer<T>(T message)
        {
            var buffer = MessagePackSerializer.Serialize(message);
            var combine = Combine(buffer, "\r\n"u8.ToArray());
            var iByteBuffer = Unpooled.CopiedBuffer(combine);

            return iByteBuffer;
        }

        public static byte[] Serialize<T>(T message)
        {
            var buffer = MessagePackSerializer.Serialize(message);
            return buffer;
        }

        public static byte[] Combine(byte[] first, byte[] second)
        {
            var ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);

            return ret;
        }


        public static T Deserialize<T>(IByteBuffer message)
        {
            var buffer = new byte[message.ReadableBytes];
            message.GetBytes(message.ReaderIndex, buffer, 0, message.ReadableBytes);
            var obj = MessagePackSerializer.Deserialize<T>(buffer);

            return obj;
        }

        public static T Deserialize<T>(byte[] message)
        {
            var obj = MessagePackSerializer.Deserialize<T>(message);

            return obj;
        }
    }
}
