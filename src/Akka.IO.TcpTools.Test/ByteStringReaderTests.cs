using System.Net.WebSockets;
using System.Text;

namespace Akka.IO.TcpTools.Test
{
    public class ByteStringReaderTests
    {
        [Theory]
        [InlineData("Every time that I look in the mirror")]
        [InlineData("987456159753210")]
        [InlineData("+!%//=()")]
        public async Task ByteStringReader_ShouldReadTheWrittenStandardMessage(string message)
        {
            // Arrange            
            using var memoryStream = new MemoryStream();
            memoryStream.Position = 0;

            using var webSocket = WebSocket.CreateFromStream(memoryStream, new WebSocketCreationOptions
            {
                IsServer = true
            });

            await webSocket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, CancellationToken.None);

            // Act
            var receivedMessage = await ByteStringReader.ReadAsync(memoryStream.ToArray());

            // Assert
            Assert.Equal(message, receivedMessage);
        }
    }
}