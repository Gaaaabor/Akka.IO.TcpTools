using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Akka.IO.TcpTools
{
    public static class MessageTools
    {
        private static readonly Regex _keyMatcher = new("(?<=Sec-WebSocket-Key:).*");

        /// <summary>
        /// Represents a standard WebSocket close message.
        /// </summary>
        public static byte[] CloseMessage = [136, 2, 3, 232];

        /// <summary>
        /// Represents a standard WebSocket ping message.
        /// </summary>
        public static byte[] PingMessage = [137, 2, 3, 232];

        /// <summary>
        /// Represents a standard WebSocket pong message.
        /// </summary>
        public static byte[] PongMessage = [138, 2, 3, 232];

        /// <summary>
        /// Extracts the Sec-WebSocket-Key from a text based WebSocket message, if not found empty string will be returned.
        /// </summary>
        /// <param name="message">Message to search for</param>
        /// <returns>The Sec-WebSocket-Key or empty string</returns>
        public static string GetSecWebSocketKey(string message)
        {
            var match = _keyMatcher.Match(message);
            if (match.Success && match.Groups.Count >= 1)
            {
                return match.Groups[0].Value.Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// Calculates the WebSocket message's total length.
        /// </summary>
        /// <param name="messageBytes">The first frame which contains the total length</param>
        /// <returns>Total length of the message</returns>
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
        /// Creates a Standard Ack WebSocket message.<br/>
        /// Concatenates the client's provided Sec-WebSocket-Key and the string "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        /// together (it's a "magic string")<br />
        /// then takes the SHA-1 hash of the result and return the base64 encoding of that hash.<br/>
        /// For more information see <see href="https://www.rfc-editor.org/rfc/rfc6455#section-1.3"/>        
        /// </summary>
        /// <param name="key">The Sec-WebSocket-Key</param>
        /// <returns>The standard ack WebSocket message</returns>
        public static string CreateAck(string key)
        {
            const string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

            var keyWithMagic = string.Concat(key, magic);
            var keyWithMagicHashed = SHA1.HashData(Encoding.UTF8.GetBytes(keyWithMagic));
            var keyWithMagicBase64 = Convert.ToBase64String(keyWithMagicHashed);

            return $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {keyWithMagicBase64}\r\n\r\n";
        }

        /// <summary>
        /// Returns the Standard type of the message.
        /// </summary>
        /// <param name="messageBytes">A message in form of byte[]</param>
        /// <returns>The type of the message <see cref="StandardMessageType"/></returns>
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
