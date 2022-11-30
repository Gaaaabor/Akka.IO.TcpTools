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
            return ReadAsync(receivedBytes.ToArray(), cancellationToken);
        }

        /// <summary>
        /// Reads Tcp message from a byte[] representation into a string.
        /// </summary>
        /// <param name="receivedBytes">The message in form of a byte[]</param>
        /// <returns>The message</returns>
        public static async Task<string> ReadAsync(byte[] receivedBytes, CancellationToken cancellationToken = default)
        {
            using var memoryStream = new MemoryStream();
            memoryStream.Write(receivedBytes.ToArray());
            memoryStream.Position = 0;

            using var webSocket = WebSocket.CreateFromStream(memoryStream, new WebSocketCreationOptions
            {
                IsServer = true
            });

            var buffer = WebSocket.CreateServerBuffer(receivedBytes.Length);
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
            var message = Encoding.UTF8.GetString(buffer[..result.Count]);
            return message;
        }
    }
}