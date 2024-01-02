namespace Akka.IO.TcpTools.Test
{
    public class MessageToolsTests
    {
        [Fact]
        public void GetSecWebSocketKey_ShouldReturnSecWebSocketKey()
        {
            // Arrange
            const string secWebSocketKey = "0123456789öüó§'\"+!%/=()ÖÜÓ~ˇ^˘°˛`˙´˝¨¸\\|Ä€Í÷×¤ßłŁ$*>;<}{@&#><äđĐ[]_:?-.,";

            const string webSocketMessage =
                @"GET /chat HTTP/1.1
                  Host: example.com:8000
                  Upgrade: websocket
                  Connection: Upgrade

                  Sec-WebSocket-Key:     0123456789öüó§'""+!%/=()ÖÜÓ~ˇ^˘°˛`˙´˝¨¸\|Ä€Í÷×¤ßłŁ$*>;<}{@&#><äđĐ[]_:?-.,    

                  Sec -WebSocket-Version: 13";

            // Act
            var extractedSecWebSocketKey = MessageTools.GetSecWebSocketKey(webSocketMessage);

            // Assert
            Assert.NotNull(extractedSecWebSocketKey);
            Assert.Equal(secWebSocketKey, extractedSecWebSocketKey);
        }

        [Fact]
        public void GetSecWebSocketKey_ShouldReturnSecWebSocketKeyWithExtraSpace()
        {
            // Arrange
            const string secWebSocketKey = "dGhlIHNhbXBsZSBub25jZQ==";

            const string webSocketMessage =
                @"GET /chat HTTP/1.1
                  Host: example.com:8000
                  Upgrade: websocket
                  Connection: Upgrade
                  Sec-WebSocket-Key: " + secWebSocketKey + @"
                  Sec -WebSocket-Version: 13";

            // Act
            var extractedSecWebSocketKey = MessageTools.GetSecWebSocketKey(webSocketMessage);

            // Assert
            Assert.NotNull(extractedSecWebSocketKey);
            Assert.Equal(secWebSocketKey, extractedSecWebSocketKey);
        }

        [Fact]
        public void GetSecWebSocketKey_ShouldReturnEmptySecWebSocketKey()
        {
            // Arrange
            const string webSocketMessage =
                @"GET /chat HTTP/1.1
                  Host: example.com:8000
                  Upgrade: websocket
                  Connection: Upgrade
                  Sec-WebSasdasdocket-Key: dGhlIHNhbXBsZSBub25jZQ==
                  Sec -WebSocket-Version: 13";

            // Act
            var extractedSecWebSocketKey = MessageTools.GetSecWebSocketKey(webSocketMessage);

            // Assert
            Assert.True(string.IsNullOrEmpty(extractedSecWebSocketKey));
        }

        [Theory]
        [InlineData("WebSocketMessageABCD")]
        [InlineData("918273689716235918235")]
        [InlineData("What does the fox says?")]
        [InlineData("Gering-ding-ding-ding-dingeringeding! Gering-ding-ding-ding-dingeringeding! Gering-ding-ding-ding-dingeringeding! Gering-ding-ding-ding-dingeringeding!")]
        public async Task GetMessageTotalLength_ShouldReturnProperLength(string message)
        {
            // Arrange
            var payload = await ByteStringWriter.WriteAsTextAsync(message);

            // Act
            var messageLength = MessageTools.GetMessageTotalLength(payload.ToArray());

            // Assert            
            Assert.Equal((ulong)message.Length, messageLength);
        }

        [Fact]
        public async Task GetMessageType_ShouldReturnText()
        {
            // Arrange
            var payload = await ByteStringWriter.WriteAsTextAsync("Test text message");

            // Act
            var messageType = MessageTools.GetMessageType(payload.ToArray());

            // Assert
            Assert.Equal(StandardMessageType.Text, messageType);
        }

        [Fact]
        public void GetMessageType_ShouldReturnClose()
        {
            // Arrange
            var payload = MessageTools.CloseMessage;

            // Act
            var messageType = MessageTools.GetMessageType(payload.ToArray());

            // Assert
            Assert.Equal(StandardMessageType.Close, messageType);
        }

        [Fact]
        public async Task GetMessageType_ShouldReturnPingWithPayload()
        {
            // Arrange
            var payload = "Hello";
            var pingMessageWithPayload = MessageTools.CreatePingMessage(payload);

            // Act
            var messageType = MessageTools.GetMessageType(pingMessageWithPayload);            
            var resultPayload = ByteStringReaderV2.Read(pingMessageWithPayload);
            var payloadTotalLength = MessageTools.GetMessageTotalLengthV2(pingMessageWithPayload);

            // Assert
            Assert.Equal(StandardMessageType.Ping, messageType);
            Assert.Equal(payload, resultPayload);
            Assert.Equal((ulong)pingMessageWithPayload.Length, payloadTotalLength);
        }

        [Fact]
        public async Task GetMessageType_ShouldReturnPingWithoutPayload()
        {
            // Arrange
            var pingMessageWithPayload = MessageTools.CreatePingMessage(string.Empty);

            // Act
            var messageType = MessageTools.GetMessageType(pingMessageWithPayload);
            var result = ByteStringReaderV2.Read(pingMessageWithPayload);

            // Assert
            Assert.Equal(StandardMessageType.Ping, messageType);
            Assert.Empty(result);
        }

        [Fact]
        public void GetMessageType_ShouldReturnPong()
        {
            // Arrange
            var payload = MessageTools.PongMessage;

            // Act
            var messageType = MessageTools.GetMessageType(payload.ToArray());

            // Assert
            Assert.Equal(StandardMessageType.Pong, messageType);
        }

        [Fact]
        public void GetMessageType_ShouldReturnInvalid()
        {
            // Arrange
            var payload = new byte[1] { 11 };

            // Act
            var messageType = MessageTools.GetMessageType(payload.ToArray());

            // Assert
            Assert.Equal(StandardMessageType.Invalid, messageType);
        }

        [Fact]
        public void GetMessageType_ShouldReturnBinary()
        {
            // Arrange
            var payload = new byte[1] { 2 };

            // Act
            var messageType = MessageTools.GetMessageType(payload.ToArray());

            // Assert
            Assert.Equal(StandardMessageType.Binary, messageType);
        }
    }
}
