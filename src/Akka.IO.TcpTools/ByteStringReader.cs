using System.Net.WebSockets;
using System.Text;

namespace Akka.IO.TcpTools
{
    public static class ByteStringReader
    {
        /// <summary>
        /// Reads Tcp message from a ByteString representation into a string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <returns>The message</returns>
        public static Task<string> ReadAsync(ByteString receivedBytes, CancellationToken cancellationToken = default)
        {
            return ReadAsync(receivedBytes.ToArray(), Encoding.UTF8, cancellationToken);
        }

        /// <summary>
        /// Reads Tcp message from a ByteString representation into a string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <param name="encoding">The encoding to use upon reading, defaults to UTF8</param>
        /// <returns>The message</returns>
        public static Task<string> ReadAsync(ByteString receivedBytes, Encoding encoding, CancellationToken cancellationToken = default)
        {
            return ReadAsync(receivedBytes.ToArray(), encoding, cancellationToken);
        }

        /// <summary>
        /// Reads Tcp message from a byte[] representation into a string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <returns>The message</returns>
        public static async Task<string> ReadAsync(byte[] receivedBytes, CancellationToken cancellationToken = default)
        {
            return await ReadAsync(receivedBytes, Encoding.UTF8, cancellationToken);
        }

        /// <summary>
        /// Reads Tcp message from a byte[] representation into a string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <param name="encoding">The encoding to use upon reading, defaults to UTF8</param>
        /// <returns>The message</returns>
        public static async Task<string> ReadAsync(byte[] receivedBytes, Encoding encoding, CancellationToken cancellationToken = default)
        {
            encoding ??= Encoding.UTF8;

            using var memoryStream = new MemoryStream();
            memoryStream.Write(receivedBytes.ToArray());
            var length = memoryStream.Position;
            memoryStream.Position = 0;

            using var webSocket = WebSocket.CreateFromStream(memoryStream, new WebSocketCreationOptions
            {
                IsServer = true
            });
            
            var buffer = WebSocket.CreateServerBuffer(receivedBytes.Length);
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
            var message = encoding.GetString(buffer[..result.Count]);
            return message;
        }
    }
}