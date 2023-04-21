using System.Net.WebSockets;
using System.Text;

namespace Akka.IO.TcpTools.Test
{
    public class ByteStringWriterTests
    {
        [Theory]
        [InlineData("Every time that I look in the mirror")]
        [InlineData("987456159753210")]
        [InlineData("+!%//=()")]
        public async Task WriteAsTextAsync_ShouldWriteReadableStandardMessage(string message)
        {
            // Arrange
            // Act
            var payload = await ByteStringWriter.WriteAsTextAsync(message);

            using var memoryStream = new MemoryStream();
            memoryStream.Write(payload.ToArray());
            memoryStream.Position = 0;

            using var webSocket = WebSocket.CreateFromStream(memoryStream, new WebSocketCreationOptions
            {
                IsServer = true
            });

            var receivedBytes = WebSocket.CreateServerBuffer(message.Length);
            var result = await webSocket.ReceiveAsync(receivedBytes, CancellationToken.None);

            var receivedMessage = Encoding.UTF8.GetString(receivedBytes);

            // Assert
            Assert.Equal(message, receivedMessage);
        }
    }
}
