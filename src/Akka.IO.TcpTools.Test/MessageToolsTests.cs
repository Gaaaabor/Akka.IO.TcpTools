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
    }
}
