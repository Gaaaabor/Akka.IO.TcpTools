using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Akka.IO.TcpTools
{
    public static class MessageTools
    {
        private static readonly Regex _keyMatcher = new("(?<=Sec-WebSocket-Key:).*");

        /// <summary>
        /// Represents a standard Close message opcode (0x8).
        /// </summary>
        public const byte CloseOpCode = 136;

        /// <summary>
        /// Represents a standard Ping message opcode (0x9).
        /// </summary>
        public const byte PingOpCode = 137;

        /// <summary>
        /// Represents a standard Pong message opcode (0xA).
        /// </summary>
        public const byte PongOpCode = 138;

        /// <summary>
        /// Represents a standard WebSocket Close message.
        /// </summary>
        public static byte[] CloseMessage = [CloseOpCode, 2, 3, 232];

        /// <summary>
        /// Represents a standard WebSocket ping message.
        /// </summary>
        public static byte[] PingMessage = [PingOpCode, 2, 3, 232];

        /// <summary>
        /// Represents a standard WebSocket pong message.
        /// </summary>
        public static byte[] PongMessage = [PongOpCode, 2, 3, 232];

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
            if (messageBytes.Length == 0)
            {
                return 0;
            }

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

        public static ulong GetMessageTotalLengthV2(byte[] messageBytes)
        {
            if (messageBytes.Length == 0)
            {
                return 0;
            }

            ulong messageLength = messageBytes[1] & (ulong)0b01111111;

            if (messageLength == 126)
            {
                messageLength = BitConverter.ToUInt16([messageBytes[3], messageBytes[2]], 0);
            }
            else if (messageLength == 127)
            {
                messageLength = BitConverter.ToUInt64([messageBytes[9], messageBytes[8], messageBytes[7], messageBytes[6], messageBytes[5], messageBytes[4], messageBytes[3], messageBytes[2]], 0);
            }

            return messageLength;
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

        public static byte[] Decompress(byte[] data)
        {
            using MemoryStream output = new();
            using DeflateStream dstream = new(new MemoryStream(data), CompressionMode.Decompress);
            dstream.CopyTo(output);

            return output.ToArray();
        }

        public static byte[] CreateCloseMessage(string payload, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = encoding.GetBytes(payload);
            return FabricateMessage(bytes, CloseOpCode);
        }

        public static byte[] CreatePingMessage(string payload, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = encoding.GetBytes(payload);
            return FabricateMessage(bytes, PingOpCode);
        }

        public static byte[] CreatePongMessage(string payload, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var bytes = encoding.GetBytes(payload);
            return FabricateMessage(bytes, PongOpCode);
        }

        private static byte[] FabricateMessage(byte[] bytes, byte opCode, bool unmasked = true)
        {
            var result = new byte[bytes.Length + 2];

            if (unmasked)
            {
                result[0] = opCode;
                result[1] = (byte)(bytes.Length > 125 ? 125 : bytes.Length);

                bytes.CopyTo(result, 2);
            }
            else
            {
                //TODO: Masking!
            }

            return result;
        }

        /// <summary>
        /// Purely for development purposes!
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        internal static byte[] Experiment(string payload)
        {
            var pingWithPayload = new Queue<byte>();

            try
            {
                //A single-frame unmasked text message
                //0x81 0x05 0x48 0x65 0x6c 0x6c 0x6f (contains "Hello")
                var unmasked = new byte[] { 0x81, 0x05, 0x48, 0x65, 0x6c, 0x6c, 0x6f };
                var unmaskedMessageType = GetMessageType(unmasked);
                var unmaskedResult = ByteStringReader.ReadAsync(unmasked).GetAwaiter().GetResult();

                //A single - frame masked text message
                //0x81 0x85 0x37 0xfa 0x21 0x3d 0x7f 0x9f 0x4d 0x51 0x58 (contains "Hello")
                var masked = new byte[] { 0x81, 0x85, 0x37, 0xfa, 0x21, 0x3d, 0x7f, 0x9f, 0x4d, 0x51, 0x58 };
                var maskedMessageType = GetMessageType(masked);
                var maskedResult = ByteStringReader.ReadAsync(masked).GetAwaiter().GetResult();

                //A fragmented unmasked text message
                //0x01 0x03 0x48 0x65 0x6c(contains "Hel")
                //0x80 0x02 0x6c 0x6f(contains "lo")
                var fragmented1 = new byte[] { 0x01, 0x03, 0x48, 0x65, 0x6c };
                var fragmented2 = new byte[] { 0x80, 0x02, 0x6c, 0x6f };

                var fragmented1MessageType = GetMessageType(fragmented1);
                var fragmented2MessageType = GetMessageType(fragmented2);

                //Unmasked Ping request and masked Ping response
                //0x89 0x05 0x48 0x65 0x6c 0x6c 0x6f (contains a body of "Hello", but the contents of the body are arbitrary)
                //0x8a 0x85 0x37 0xfa 0x21 0x3d 0x7f 0x9f 0x4d 0x51 0x58 (contains a body of "Hello", matching the body of the ping)

                var unmaskedPingRequestWithPayload = new byte[] { 0x89, 0x05, 0x48, 0x65, 0x6c, 0x6c, 0x6f };
                var unmaskedPingRequestWithPayloadMessageType = GetMessageType(unmaskedPingRequestWithPayload);

                //256 bytes binary message in a single unmasked frame
                //0x82 0x7E 0x0100[256 bytes of binary data]

                //64KiB binary message in a single unmasked frame
                //0x82 0x7F 0x0000000000010000[65536 bytes of binary data]

                var pingWithPayloadMessageType = MessageTools.GetMessageType(pingWithPayload.ToArray());
                var pingWithPayloadResult = ByteStringReader.ReadAsync(pingWithPayload.ToArray()).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
            }

            return pingWithPayload.ToArray();
        }
    }
}
