using System;
using System.Linq;
using System.Text;

namespace Akka.IO.TcpTools.Standard
{
    public static class WebSocketMessageDecoder
    {
        /// <summary>
        /// Decodes a WebSocket message from a byte[] representation as string.
        /// </summary>
        /// <param name="message">The message in form of a byte[]</param>
        /// <param name="encoding">The encoding to use upon reading, defaults to UTF8</param>
        /// <returns>The decoded message</returns>
        public static string DecodeAsString(byte[] message, Encoding encoding = null)
        {
            if (message.Length == 0)
            {
                return string.Empty;
            }

            encoding ??= Encoding.UTF8;
            var result = DecodeAsBytes(message);

            if (result.Length == 0)
            {
                return string.Empty;
            }

            return encoding.GetString(result);
        }

        /// <summary>
        /// Decodes a WebSocket message from a byte[] representation as array of byte.
        /// </summary>
        /// <param name="message">The message in form of a byte[]</param>
        /// <returns>The decoded message</returns>
        public static byte[] DecodeAsBytes(byte[] message)
        {
            if (message.Length == 0)
            {
                return new byte[0];
            }

            bool isFinal = message.IsFinal();
            bool isMasked = message.IsMasked();
            int opcode = message[0] & 0xF;

            int offset = 2;

            ulong messageLength = message[1] & (ulong)0x7F;

            if (messageLength == 126)
            {
                messageLength = BitConverter.ToUInt16(new byte[] { message[3], message[2] }, 0);
                offset = 4;
            }
            else if (messageLength == 127)
            {
                messageLength = BitConverter.ToUInt64(new byte[] { message[9], message[8], message[7], message[6], message[5], message[4], message[3], message[2] }, 0);
                offset = 10;
            }

            if (messageLength == 0)
            {
                return new byte[0];
            }

            if (!isMasked)
            {
                return message[offset..];
            }

            var messageBytes = message.ToArray();

            byte[] decodedBytes = new byte[messageLength];
            byte[] maskBytes = new byte[] { messageBytes[offset], messageBytes[offset + 1], messageBytes[offset + 2], messageBytes[offset + 3] };
            offset += 4;

            for (ulong i = 0; i < messageLength; ++i)
            {
                var maskByte = maskBytes[i % 4];
                var receivedByte = messageBytes[(ulong)offset + i];
                decodedBytes[i] = (byte)(receivedByte ^ maskByte);
            }

            return decodedBytes;
        }
    }
}
