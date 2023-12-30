using System.Text;

namespace Akka.IO.TcpTools
{
    public static class ByteStringReaderV2
    {
        /// <summary>
        /// Reads Tcp message from a ByteString representation into a string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <returns>The message</returns>
        public static string Read(ByteString receivedBytes)
        {
            return Read(receivedBytes.ToArray(), Encoding.UTF8);
        }

        /// <summary>
        /// Reads Tcp message from a ByteString representation into a string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <param name="encoding">The encoding to use upon reading, defaults to UTF8</param>
        /// <returns>The message</returns>
        public static string Read(ByteString receivedBytes, Encoding encoding)
        {
            return Read(receivedBytes.ToArray(), encoding);
        }

        /// <summary>
        /// Reads Tcp message from a byte[] representation into a string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <returns>The message</returns>
        public static string Read(byte[] receivedBytes)
        {
            return Read(receivedBytes, Encoding.UTF8);
        }

        /// <summary>
        /// Reads Tcp message from a byte[] representation into a string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <param name="encoding">The encoding to use upon reading, defaults to UTF8</param>
        /// <returns>The message</returns>
        public static string Read(byte[] receivedBytes, Encoding encoding)
        {
            if (receivedBytes.Length == 0)
            {
                return string.Empty;
            }

            bool isFinal = (receivedBytes[0] & 0b10000000) != 0;
            bool isMasked = (receivedBytes[1] & 0b10000000) != 0;
            int opcode = receivedBytes[0] & 0b00001111;

            int offset = 2;

            ulong messageLength = receivedBytes[1] & (ulong)0b01111111;

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
                return string.Empty;
            }

            if (!isMasked)
            {
                // TODO: Check if the first two bytes required
                return encoding.GetString(receivedBytes[2..]);
                //return encoding.GetString(receivedBytes);
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

            string text = encoding.GetString(decodedBytes);
            return text;
        }
    }
}
