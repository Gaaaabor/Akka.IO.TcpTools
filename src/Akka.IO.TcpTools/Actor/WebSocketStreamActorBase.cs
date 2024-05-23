using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Logging;

namespace Akka.IO.TcpTools.Actor
{
    public class WebSocketStreamActorBase : ReceiveActor
    {
        private const int BufferSize = 512;
        private readonly ILogger _logger;

        private Queue<byte[]> _fragments;
        private readonly ActorMaterializer _materializer;
        private readonly int _port;

        public WebSocketStreamActorBase(int port, ILogger<WebSocketStreamActorBase> logger)
        {
            _fragments = new Queue<byte[]>();
            _materializer = Context.Materializer();

            _port = port;
            _logger = logger;

            Context.System
                .TcpStream()
                .Bind("127.0.0.1", _port)
                .RunForeach(connection =>
                {
                    _logger.LogInformation($"New connection from: {connection.RemoteAddress}");

                    var receiveFlow = Flow
                        .Create<ByteString>()
                        .Via(Flow.FromFunction<ByteString, ByteString>(OnReceived));

                    connection.HandleWith(receiveFlow, _materializer);

                }, _materializer);
        }

        private ByteString OnReceived(ByteString data)
        {
            try
            {
                var secWebSocketKey = MessageTools.GetSecWebSocketKey(data.ToString());
                if (!string.IsNullOrEmpty(secWebSocketKey))
                {
                    return ByteString.FromString(MessageTools.CreateAck(secWebSocketKey));
                }

                var rawMessage = data.ToArray();
                _fragments.Enqueue(rawMessage);

                if (rawMessage.Length >= BufferSize)
                {
                    return ByteString.FromBytes(MessageTools.CreateMessage(new byte[0], MessageTools.PongOpCode, false));
                }

                if (_fragments.Count > 1)
                {
                    rawMessage = _fragments.SelectMany(x => x).ToArray();
                    _fragments.Clear();
                }
                else
                {
                    _fragments.Clear();
                }

                var messageType = MessageTools.GetMessageType(rawMessage);
                switch (messageType)
                {
                    case StandardMessageType.Binary:
                        return OnBinaryReceived(rawMessage);

                    case StandardMessageType.Text:
                        return OnTextReceived(rawMessage);

                    case StandardMessageType.Ping:
                        return OnPingReceived(rawMessage);

                    case StandardMessageType.Pong:
                        return OnPongReceived(rawMessage);

                    case StandardMessageType.Close:
                        return OnClosedReceived(rawMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during {Name}!", nameof(OnReceived));
                _ = Self.GracefulStop(TimeSpan.FromSeconds(5));
            }

            return ByteString.Empty;
        }

        private ByteString OnClosedReceived(byte[] data)
        {
            _logger.LogInformation("Received close");
            var message = MessageTools.CreateCloseMessage((int)CloseCode.NormalClosure);
            return ByteString.FromBytes(message);
        }

        private ByteString OnPongReceived(byte[] data)
        {
            _logger.LogInformation("Received pong");
            var decoded = WebSocketMessageDecoder.DecodeAsBytes(data);
            var response = MessageTools.CreateMessage(decoded, MessageTools.PingOpCode, true);
            return ByteString.FromBytes(response);
        }

        private ByteString OnPingReceived(byte[] data)
        {
            _logger.LogInformation("Received ping");
            var decoded = WebSocketMessageDecoder.DecodeAsBytes(data);
            var response = MessageTools.CreateMessage(decoded, MessageTools.PongOpCode, true);
            return ByteString.FromBytes(response);
        }

        private ByteString OnTextReceived(byte[] data)
        {
            var pong = ByteString.FromBytes(MessageTools.CreateMessage(new byte[0], MessageTools.PongOpCode, false));
            var message = WebSocketMessageDecoder.DecodeAsString(data);
            _logger.LogInformation($"Received text message: {message}");
            return pong;
        }

        private ByteString OnBinaryReceived(byte[] data)
        {
            var message = WebSocketMessageDecoder.DecodeAsBytes(data);
            _logger.LogInformation($"Received binary message: {message}");
            return ByteString.FromBytes(message);
        }
    }
}
