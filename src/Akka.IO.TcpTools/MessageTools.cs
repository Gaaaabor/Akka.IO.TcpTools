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
        /// Represents a standard Text message opcode (0x1).
        /// </summary>
        public const byte TextOpCode = 0x1;

        /// <summary>
        /// Represents a standard Binary message opcode (0x2).
        /// </summary>
        public const byte BinaryOpCode = 0x2;

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

        [Obsolete("Please use the GetMessageTotalLengthV2 method")]
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

        /// <summary>
        /// Calculates the WebSocket message's total length.
        /// </summary>
        /// <param name="messageBytes">The first frame which contains the total length</param>
        /// <returns>Total length of the message</returns>
        public static ulong GetMessageTotalLengthV2(byte[] messageBytes)
        {
            if (messageBytes.Length == 0)
            {
                return 0;
            }

            bool isFinal = (messageBytes[0] & 128) != 0;
            bool isMasked = (messageBytes[1] & 128) != 0;

            // Subtracting 128 from the second byte to get rid of the MASK bit
            var messageLength = messageBytes[1] & (ulong)127;

            // If the length is between 0 and 125, then it's the length of the message.
            if (messageLength <= 125)
            {
                messageLength += 2;

                if (isMasked)
                {
                    messageLength += 4;
                }

                // opcode + fin + mask + size (2 bytes) + mask key (4 bytes)
                return messageLength;
            }

            // If the length is 126, then the following 2 bytes (16-bit uint) are the length.
            if (messageLength == 126)
            {
                messageLength = BitConverter.ToUInt16([messageBytes[3], messageBytes[2]], 0);
                messageLength += 4;

                if (isMasked)
                {
                    messageLength += 4;
                }

                // opcode + fin + mask + size (2 bytes + 2 bytes) + mask key (4 bytes)
                return messageLength;
            }

            // If the length is 127, then the following 8 bytes (64-bit uint) are the length.
            if (messageLength == 127)
            {
                messageLength = BitConverter.ToUInt64([messageBytes[9], messageBytes[8], messageBytes[7], messageBytes[6], messageBytes[5], messageBytes[4], messageBytes[3], messageBytes[2]], 0);
                messageLength += 10;

                if (isMasked)
                {
                    messageLength += 4;
                }

                // Longest possible header size is 14 bytes in this case:                
                // opcode + fin + mask + size (2 bytes + 8 bytes) + mask key (4 bytes)
            }

            return messageLength;
        }

        public static ulong GetPayloadLength(byte[] messageBytes)
        {
            if (messageBytes.Length == 0)
            {
                return 0;
            }

            bool isFinal = (messageBytes[0] & 128) != 0;
            bool isMasked = (messageBytes[1] & 128) != 0;

            // Subtracting 128 from the second byte to get rid of the MASK bit
            var payloadLength = messageBytes[1] & (ulong)127;

            // If the length is between 0 and 125, then it's the length of the message.
            if (payloadLength <= 125)
            {
                return payloadLength;
            }

            // If the length is 126, then the following 2 bytes (16-bit uint) are the length.
            if (payloadLength == 126)
            {
                payloadLength = BitConverter.ToUInt16([messageBytes[3], messageBytes[2]], 0);
                return payloadLength;
            }

            // If the length is 127, then the following 8 bytes (64-bit uint) are the length.
            if (payloadLength == 127)
            {
                payloadLength = BitConverter.ToUInt64([messageBytes[9], messageBytes[8], messageBytes[7], messageBytes[6], messageBytes[5], messageBytes[4], messageBytes[3], messageBytes[2]], 0);
                return payloadLength;
            }

            return 0;
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

        public static bool ValidateMessage(byte[] messageBytes)
        {
            var messageType = GetMessageType(messageBytes);

            switch (messageType)
            {
                case StandardMessageType.Continuation:
                case StandardMessageType.Text:
                case StandardMessageType.Binary:
                    return true;

                case StandardMessageType.Close:
                case StandardMessageType.Ping:
                case StandardMessageType.Pong:
                    var payloadLength = GetPayloadLength(messageBytes);
                    return payloadLength <= 125;

                case StandardMessageType.Invalid:
                    return false;
                default:
                    return false;
            }
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

            //if (messageType == StandardMessageType.Invalid)
            //{
            //    return messageType;
            //}

            //// FIN, RSV1, RSV2, RSV3, OP,OP,OP,OP none of the RSV bits should be set
            //var isReservedFlags = (messageBytes[0] & 112) != 0;
            //if (isReservedFlags)
            //{
            //    return StandardMessageType.Invalid;
            //}

            return messageType;
        }

        public static byte[] Decompress(byte[] data)
        {
            using MemoryStream output = new();
            using DeflateStream dstream = new(new MemoryStream(data), CompressionMode.Decompress);
            dstream.CopyTo(output);

            return output.ToArray();
        }

        public static byte[] CreateCloseMessage(int closeCode)
        {
            var payload = BitConverter.GetBytes(closeCode);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(payload);
            }

            payload[0] = CloseOpCode | 128; //OpCode + FIN
            payload[1] = 2; //Size is always 2 as closeCode is between 0 and 4999
            return payload;
        }

        public static byte[] CreatePingMessage(string payload, Encoding encoding = null, bool useMasking = true)
        {
            encoding ??= Encoding.UTF8;
            var bytes = encoding.GetBytes(payload);
            return CreateMessage(bytes, PingOpCode, useMasking);
        }

        public static byte[] CreatePingMessage(byte[] payload, bool useMasking = true)
        {
            return CreateMessage(payload, PingOpCode, useMasking);
        }

        public static byte[] CreatePongMessage(byte[] payload)
        {
            var bytes = new byte[] { 0, 0, 0, 0 };
            bytes[0] = PongOpCode | 128; //OpCode + FIN

            return bytes;
        }

        public static byte[] CreatePongMessage(byte[] payload, bool useMasking = true)
        {
            return CreateMessage(payload, PongOpCode, useMasking);
        }

        public static byte[] CreateMessage(byte[] payload, byte opCode, bool useMasking = true)
        {
            byte[] result;
            int maskOffset;

            if (payload.Length <= 125)
            {
                result = new byte[payload.Length + 2 + (useMasking ? 4 : 0)];
                result[1] = (byte)payload.Length;
                maskOffset = 2;
            }
            else if (payload.Length <= ushort.MaxValue)
            {
                result = new byte[payload.Length + 4 + (useMasking ? 4 : 0)];
                result[1] = 126;
                result[2] = (byte)(payload.Length / 256);
                result[3] = unchecked((byte)payload.Length);
                maskOffset = 2 + sizeof(ushort);
            }
            else
            {
                result = new byte[payload.Length + 10 + (useMasking ? 4 : 0)];
                result[1] = 127;

                int length = payload.Length;
                for (int i = 9; i >= 2; i--)
                {
                    result[i] = unchecked((byte)length);
                    length /= 256;
                }

                maskOffset = 2 + sizeof(ulong);
            }

            result[0] = opCode;
            result[0] |= 0x80; // FIN

            if (useMasking)
            {
                result[1] |= 0x80; // Mask

                var mask = new byte[4];
                RandomNumberGenerator.Fill(mask.AsSpan(0, 4));

                var encoded = new byte[payload.Length];
                for (int i = 0; i < payload.Length; i++)
                {
                    encoded[i] = (byte)(payload[i] ^ mask[i % 4]);
                }

                mask.CopyTo(result, maskOffset);
                encoded.CopyTo(result, maskOffset + 4);
            }
            else
            {
                payload.CopyTo(result, maskOffset);
            }

            return result;
        }
    }
}
