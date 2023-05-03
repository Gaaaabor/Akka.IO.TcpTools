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
        public async Task ByteStringReader_ShouldReadTheWrittenBytestringWritersMessage(string message)
        {
            // Arrange
            var payload = await ByteStringWriter.WriteAsTextAsync(message);

            // Act
            var receivedMessage = await ByteStringReader.ReadAsync(payload);

            // Assert
            Assert.Equal(message, receivedMessage);
        }

        [Theory]
        [InlineData("utf-16")]
        [InlineData("utf-16BE")]
        [InlineData("utf-32")]
        [InlineData("utf-32BE")]
        [InlineData("us-ascii")]
        [InlineData("iso-8859-1")]
        [InlineData("utf-8")]
        public async Task ByteStringReader_ShouldReadTheWrittenBytestringWritersMessageWithGivenEncoding(string encodingName)
        {
            // Arrange
            var message = "Every time that I look in the mirror, 987456159753210, +!%//=()";
            var encoding = Encoding.GetEncoding(encodingName);

            // Arrange
            var payload = await ByteStringWriter.WriteAsTextAsync(message, encoding);

            // Act
            var receivedMessage = await ByteStringReader.ReadAsync(payload, encoding);

            // Assert
            Assert.Equal(message, receivedMessage);
        }

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

        [Theory]
        [InlineData("utf-16")]
        [InlineData("utf-16BE")]
        [InlineData("utf-32")]
        [InlineData("utf-32BE")]
        [InlineData("us-ascii")]
        [InlineData("iso-8859-1")]
        [InlineData("utf-8")]
        public async Task ByteStringReader_ShouldReadTheWrittenMessageWithGivenEncoding(string encodingName)
        {
            // Arrange
            var message = "Every time that I look in the mirror, 987456159753210, +!%//=()";
            var encoding = Encoding.GetEncoding(encodingName);
            using var memoryStream = new MemoryStream();
            memoryStream.Position = 0;

            using var webSocket = WebSocket.CreateFromStream(memoryStream, new WebSocketCreationOptions
            {
                IsServer = true
            });

            await webSocket.SendAsync(encoding.GetBytes(message), WebSocketMessageType.Text, true, CancellationToken.None);

            // Act
            var receivedMessage = await ByteStringReader.ReadAsync(memoryStream.ToArray(), encoding);

            // Assert
            Assert.Equal(message, receivedMessage);
        }
    }
}