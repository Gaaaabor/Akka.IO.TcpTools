using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Akka.IO.TcpTools
{
    public static class ByteStringWriter
    {
        /// <summary>
        /// Serializes the given message using the first non-interface type of the class and creates a ByteString.
        /// </summary>
        /// <typeparam name="TClass">The typy of the message</typeparam>
        /// <param name="message">The message to send</param>        
        /// <param name="cancellationToken">The message to send</param>
        /// <returns>A ByteString object which contains the serialized message</returns>
        public static async Task<ByteString> WriteAsTextAsync<TClass>(TClass message, CancellationToken cancellationToken = default)
        {
            byte[] messageBytes;
            if (message is string stringMessage)
            {
                messageBytes = Encoding.UTF8.GetBytes(stringMessage);
            }
            else
            {
                var firstNonInterfaceType = GetFirstNonInterfaceType(message);
                var serializedMessage = JsonSerializer.Serialize(message, firstNonInterfaceType);
                messageBytes = Encoding.UTF8.GetBytes(serializedMessage);
            }

            using var memoryStream = new MemoryStream();
            using var webSocket = WebSocket.CreateFromStream(memoryStream, new WebSocketCreationOptions
            {
                IsServer = true
            });

            await webSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, cancellationToken);

            var byteString = ByteString.FromBytes(memoryStream.ToArray());
            return byteString;
        }

        private static Type GetFirstNonInterfaceType<TEntity>(TEntity entity)
        {
            if (entity is null)
            {
                return typeof(object);
            }

            var entityType = entity.GetType();
            if (entityType?.IsInterface ?? false)
            {
                var result = GetFirstNonInterfaceType(entityType.UnderlyingSystemType);
                return result ?? entityType;
            }

            return entityType;
        }
    }
}
