using System.Text;

namespace Akka.IO.TcpTools
{
    public static class WebSocketMessageDecoder
    {
        /// <summary>
        /// Decodes a WebSocket message from a byte[] representation as string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <param name="encoding">The encoding to use upon reading, defaults to UTF8</param>
        /// <returns>The decoded message</returns>
        public static string DecodeAsString(byte[] receivedBytes, Encoding encoding = null)
        {
            if (receivedBytes.Length == 0)
            {
                return string.Empty;
            }

            encoding ??= Encoding.UTF8;
            var result = DecodeAsBytes(receivedBytes);

            if (result.Length == 0)
            {
                return string.Empty;
            }

            return encoding.GetString(result);
        }

        /// <summary>
        /// Decodes a WebSocket message from a byte[] representation as array of byte.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <returns>The decoded message</returns>
        public static byte[] DecodeAsBytes(byte[] receivedBytes)
        {
            if (receivedBytes.Length == 0)
            {
                return [];
            }

            bool isFinal = (receivedBytes[0] & 0x80) != 0;
            bool isMasked = (receivedBytes[1] & 0x80) != 0;
            int opcode = receivedBytes[0] & 0xF;

            int offset = 2;

            ulong messageLength = receivedBytes[1] & (ulong)0x7F;

            if (messageLength == 126)
            {
                messageLength = BitConverter.ToUInt16([receivedBytes[3], receivedBytes[2]], 0);
                offset = 4;
            }
            else if (messageLength == 127)
            {
                messageLength = BitConverter.ToUInt64([receivedBytes[9], receivedBytes[8], receivedBytes[7], receivedBytes[6], receivedBytes[5], receivedBytes[4], receivedBytes[3], receivedBytes[2]], 0);
                offset = 10;
            }

            if (messageLength == 0)
            {
                return [];
            }

            if (!isMasked)
            {
                return receivedBytes[offset..];
            }

            byte[] decodedBytes = new byte[messageLength];
            byte[] maskBytes = [receivedBytes[offset], receivedBytes[offset + 1], receivedBytes[offset + 2], receivedBytes[offset + 3]];
            offset += 4;

            for (ulong i = 0; i < messageLength; ++i)
            {
                var maskByte = maskBytes[i % 4];
                var receivedByte = receivedBytes[(ulong)offset + i];
                decodedBytes[i] = (byte)(receivedByte ^ maskByte);
            }

            return decodedBytes;
        }
    }
}
