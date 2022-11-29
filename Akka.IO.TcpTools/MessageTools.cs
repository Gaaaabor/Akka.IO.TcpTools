using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Akka.IO.TcpTools
{
    public static class MessageTools
    {
        private static readonly Regex _keyMatcher = new("Sec-WebSocket-Key: (.*)");
        public static byte[] CloseMessage = new byte[4] { 136, 2, 3, 232 };
        public static byte[] PingMessage = new byte[4] { 137, 2, 3, 232 };
        public static byte[] PongMessage = new byte[4] { 138, 2, 3, 232 };

        /// <summary>
        /// Extracts the Sec-WebSocket-Key from the message, if not found empty string will be returned.
        /// </summary>
        /// <param name="message">Message to search for</param>
        /// <returns>The Sec-WebSocket-Key or empty string</returns>
        public static string GetSecWebSocketKey(string message)
        {
            var match = _keyMatcher.Match(message);
            if (match.Success && match.Groups.Count >= 1)
            {
                return match.Groups[1].Value.Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// Calculates the message's total length.
        /// </summary>
        /// <param name="messageBytes">The first frame which contains the total length.</param>
        /// <returns>Total length of the message.</returns>
        public static ulong GetMessageTotalLength(byte[] messageBytes)
        {
            using var messageStream = new MemoryStream(messageBytes);
            messageStream.Position = 0;

            var packets = new byte[2];
            messageStream.Read(packets, 0, 2);

            var masked = (packets[1] & 1 << 7) != 0;
            var pseudoLength = packets[1] - (masked ? 128 : 0);

            ulong totalLength = 0;
            if (pseudoLength > 0 && pseudoLength < 125)
            {
                totalLength = (ulong)pseudoLength;
            }
            else if (pseudoLength == 126)
            {
                var length = new byte[2];
                messageStream.Read(length, 0, length.Length);
                Array.Reverse(length);
                totalLength = BitConverter.ToUInt16(length, 0);
            }
            else if (pseudoLength == 127)
            {
                var length = new byte[8];
                messageStream.Read(length, 0, length.Length);
                Array.Reverse(length);
                totalLength = BitConverter.ToUInt64(length, 0);
            }

            return totalLength;
        }

        /// <summary>
        ///     Additionally, the server can decide on extension/subprotocol requests here; see Miscellaneous for details.<br />
        ///     The Sec-WebSocket-Accept header is important in that the server must derive it from the Sec-WebSocket-Key that the
        ///     client sent to it.<br />
        ///     To get it, concatenate the client's Sec-WebSocket-Key and the string "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        ///     together (it's a "magic string")<br />
        ///     then take the SHA-1 hash of the result and return the base64 encoding of that hash.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string CreateAck(string key)
        {
            const string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            const string eol = "\r\n"; // HTTP/1.1 defines the sequence CR LF as the end-of-line marker

            var keyWithMagic = string.Concat(key, magic);
            var keyWithMagicHashed = SHA1.HashData(Encoding.UTF8.GetBytes(keyWithMagic));
            var keyWithMagicBase64 = Convert.ToBase64String(keyWithMagicHashed);

            var ackBuilder = new StringBuilder("HTTP/1.1 101 Switching Protocols");
            ackBuilder.Append(eol);
            ackBuilder.Append("Upgrade: websocket");
            ackBuilder.Append(eol);
            ackBuilder.Append("Connection: Upgrade");
            ackBuilder.Append(eol);
            ackBuilder.Append($"Sec-WebSocket-Accept: {keyWithMagicBase64}");
            ackBuilder.Append(eol);
            ackBuilder.Append(eol);

            return ackBuilder.ToString();
        }

        /// <summary>
        /// Returns the Standard type of the message.
        /// </summary>
        /// <param name="messageBytes"></param>
        /// <returns></returns>
        public static StandardMessageType GetMessageType(byte[] messageBytes)
        {
            StandardMessageType messageType = (messageBytes[0] & 15) switch
            {
                0 => StandardMessageType.Continuation,
                1 => StandardMessageType.Text,
                2 => StandardMessageType.Binary,
                8 => StandardMessageType.Close,
                9 => StandardMessageType.Ping,
                10 => StandardMessageType.Pong,
                _ => StandardMessageType.Invalid,
            };

            if (messageType == StandardMessageType.Invalid)
            {
                return messageType;
            }

            // FIN, RSV1, RSV2, RSV3, OP,OP,OP,OP none of the RSV bits should be set
            var isReservedFlags = (messageBytes[0] & 112) != 0;
            if (isReservedFlags)
            {
                return StandardMessageType.Invalid;
            }

            return messageType;
        }
    }
}
