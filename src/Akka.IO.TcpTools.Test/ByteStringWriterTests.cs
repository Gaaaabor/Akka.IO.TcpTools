using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

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

        [Fact]
        public async Task WriteAsTextAsync_ShouldWriteReadableClassMessage()
        {
            // Arrange
            var message = new TestMessage
            {
                Name = "Eva",
                Age = 28
            };

            // Act
            var payload = await ByteStringWriter.WriteAsTextAsync(message);

            using var memoryStream = new MemoryStream();
            memoryStream.Write(payload.ToArray());
            var length = memoryStream.Position;
            memoryStream.Position = 0;

            using var webSocket = WebSocket.CreateFromStream(memoryStream, new WebSocketCreationOptions
            {
                IsServer = true
            });

            var receivedBytes = WebSocket.CreateServerBuffer((int)length);
            var result = await webSocket.ReceiveAsync(receivedBytes, CancellationToken.None);

            var rawReceivedMessage = Encoding.UTF8.GetString(receivedBytes[0..result.Count]);
            var receivedMessage = JsonSerializer.Deserialize<TestMessage>(rawReceivedMessage);

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(message.Name, receivedMessage.Name);
            Assert.Equal(message.Age, receivedMessage.Age);
        }

        [Fact]
        public async Task WriteAsTextAsync_ShouldWriteReadableInterfaceMessage()
        {
            // Arrange
            ITestMessage message = new TestMessage
            {
                Name = "Beth",
                Age = 29,
                FavFoods = new[] { "Chili", "Potato" }
            };

            // Act
            var payload = await ByteStringWriter.WriteAsTextAsync(message);

            using var memoryStream = new MemoryStream();
            memoryStream.Write(payload.ToArray());
            var length = memoryStream.Position;
            memoryStream.Position = 0;

            using var webSocket = WebSocket.CreateFromStream(memoryStream, new WebSocketCreationOptions
            {
                IsServer = true
            });

            var receivedBytes = WebSocket.CreateServerBuffer((int)length);
            var result = await webSocket.ReceiveAsync(receivedBytes, CancellationToken.None);

            var rawReceivedMessage = Encoding.UTF8.GetString(receivedBytes[0..result.Count]);
            var receivedMessage = JsonSerializer.Deserialize<TestMessage>(rawReceivedMessage);

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Equal(message.Name, receivedMessage.Name);
            Assert.Equal(message.Age, receivedMessage.Age);
            Assert.NotNull(receivedMessage.FavFoods);
            Assert.Equal(message.FavFoods.ElementAtOrDefault(0), receivedMessage.FavFoods.ElementAtOrDefault(0));
            Assert.Equal(message.FavFoods.ElementAtOrDefault(1), receivedMessage.FavFoods.ElementAtOrDefault(1));
        }

        [Theory]
        [InlineData("utf-16")]
        [InlineData("utf-16BE")]
        [InlineData("utf-32")]
        [InlineData("utf-32BE")]
        [InlineData("us-ascii")]
        [InlineData("iso-8859-1")]
        [InlineData("utf-8")]
        public async Task WriteAsTextAsync_ShouldWriteReadableStandardMessageWithGivenEncoding(string encodingName)
        {
            // Arrange
            var message = "Every time that I look in the mirror, 987456159753210, +!%//=()";
            var encoding = Encoding.GetEncoding(encodingName);

            // Act
            var payload = await ByteStringWriter.WriteAsTextAsync(message, encoding);

            using var memoryStream = new MemoryStream();
            memoryStream.Write(payload.ToArray());
            var length = memoryStream.Position;
            memoryStream.Position = 0;

            using var webSocket = WebSocket.CreateFromStream(memoryStream, new WebSocketCreationOptions
            {
                IsServer = true
            });

            var receivedBytes = WebSocket.CreateServerBuffer(encoding.GetByteCount(message));
            var result = await webSocket.ReceiveAsync(receivedBytes, CancellationToken.None);

            var receivedMessage = encoding.GetString(receivedBytes);

            // Assert
            Assert.Equal(message, receivedMessage);
        }

        public interface ITestMessage
        {
            string Name { get; }
            int Age { get; }
            IEnumerable<string> FavFoods { get; }
        }

        public class TestMessage : ITestMessage
        {
            public string Name { get; init; }
            public int Age { get; init; }
            public IEnumerable<string> FavFoods { get; init; }
        }
    }
}
