using Akka.Actor;
using Akka.Event;

namespace Akka.IO.TcpTools.Actor
{
    public abstract class WebSocketConnectionActorBaseV2 : ReceiveActor, IWithTimers
    {
        private const int PingIntervalSeconds = 5;

        private readonly string _pingScheduleKey = $"ping_{Guid.NewGuid():N}";
        private ulong _messageTotalLength;
        private MemoryStream _buffer;
        private bool _expectingPong;
        private bool _handshakeCompleted;
        private bool _framing;
        private bool _buffering;
        private StandardMessageType _messageType;

        protected ILoggingAdapter Logger { get; }

        public ITimerScheduler Timers { get; set; }

        public WebSocketConnectionActorBaseV2()
        {
            Logger = Context.GetLogger();

            Receive<Tcp.Received>(OnReceived);
            Receive<Tcp.PeerClosed>(OnPeerClosed);
            Receive<SendPingCommand>(OnSendPingCommand);
        }

        protected override void PreStart()
        {
            Logger?.Info("{0} started", Self.Path.Name);
        }

        protected override void PostStop()
        {
            Logger?.Info("{0} stopped", Self.Path.Name);
        }

        /// <summary>
        /// This method is called when a Text based WebSocket message is received and decoded.
        /// </summary>
        /// <param name="message">The received and decoded message</param>
        /// <returns></returns>
        protected virtual void OnStringReceived(string message)
        {
            Logger?.Info("{0} received a string message", Self.Path.Name);
        }

        protected virtual void OnBytesReceived(byte[] message)
        {
            Logger?.Info("{0} received a byte[] message.", Self.Path.Name);
        }

        /// <summary>
        /// This method is called when a Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnReceived(Tcp.Received received)
        {
            try
            {
                if (!_handshakeCompleted)
                {
                    var secWebSocketKey = MessageTools.GetSecWebSocketKey(received.Data.ToString());
                    if (!string.IsNullOrEmpty(secWebSocketKey))
                    {
                        Sender.Tell(Tcp.Write.Create(ByteString.FromString(MessageTools.CreateAck(secWebSocketKey))));
                        _handshakeCompleted = true;
                        SchedulePing(Sender);
                        return;
                    }
                }

                byte[] data = received.Data.ToArray();

                if (_framing)
                {
                    OnFrameReceived(data);
                    return;
                }

                if (_buffering)
                {
                    OnBufferedReceived(data);
                    return;
                }

                var isValid = MessageTools.ValidateMessage(data);
                if (!isValid)
                {
                    CloseConnectionAndStop(CloseCode.ProtocolError);
                    return;
                }

                _messageType = MessageTools.GetMessageType(data);
                switch (_messageType)
                {
                    case StandardMessageType.Continuation:
                        OnFrameReceived(data);
                        return;

                    case StandardMessageType.Text:
                        OnTextReceived(data);
                        return;

                    case StandardMessageType.Binary:
                        OnBinaryReceived(data);
                        return;

                    case StandardMessageType.Ping:
                        OnPingReceived(data);
                        return;

                    case StandardMessageType.Pong:
                        OnPongReceived(data);
                        return;

                    case StandardMessageType.Close:
                        OnClosedReceived(data);
                        return;
                }

                CloseConnectionAndStop();
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Error during {0}!", nameof(OnReceived));
                CloseConnectionAndStop();
            }
        }

        protected virtual void OnContinuationReceived(byte[] message)
        {

        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Text"/>Tcp message is received.
        /// </summary>
        /// <param name="message">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnTextReceived(byte[] message)
        {
            Logger?.Info("Received a Text message!");

            if (message.IsFinal())
            {
                _messageTotalLength = MessageTools.GetMessageTotalLengthV2(message);

                if ((ulong)message.LongLength != _messageTotalLength)
                {
                    ReceivingBufferedMessage();
                    OnBufferedReceived(message);
                    return;
                }

                var decoded = WebSocketMessageDecoder.DecodeAsString(message);
                OnStringReceived(decoded);
                return;
            }

            ReceivingFrames();
            OnFrameReceived(message);
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Binary"/>Tcp message is received.
        /// </summary>
        /// <param name="message">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnBinaryReceived(byte[] message)
        {
            Logger?.Info("Received a Binary message!");

            if (message.IsFinal())
            {
                _messageTotalLength = MessageTools.GetMessageTotalLengthV2(message);

                if ((ulong)message.LongLength != _messageTotalLength)
                {
                    ReceivingBufferedMessage();
                    OnBufferedReceived(message);
                    return;
                }

                var decoded = WebSocketMessageDecoder.DecodeAsBytes(message);
                OnBytesReceived(decoded);
                return;
            }

            ReceivingFrames();
            OnFrameReceived(message);
        }

        private void OnSendPingCommand(SendPingCommand sendPingCommand)
        {
            _expectingPong = true;
            sendPingCommand.Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(MessageTools.PingMessage)));
        }

        /// <summary>
        /// This method is called when the client side closes the Tcp connection.
        /// </summary>
        /// <param name="peerClosed">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnPeerClosed(Tcp.PeerClosed peerClosed)
        {
            Context.Stop(Self);
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Ping"/>Tcp message is received.
        /// </summary>
        /// <param name="message">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnPingReceived(byte[] message)
        {
            Logger?.Info("Received a Ping!");

            if (message.IsFinal())
            {
                _messageTotalLength = MessageTools.GetMessageTotalLengthV2(message);

                if ((ulong)message.LongLength != _messageTotalLength)
                {
                    ReceivingBufferedMessage();
                    OnBufferedReceived(message);
                    return;
                }

                var decoded = WebSocketMessageDecoder.DecodeAsBytes(message);
                var response = MessageTools.CreateMessage(decoded, MessageTools.PongOpCode, true);
                Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(response)));
                return;
            }

            CloseConnectionAndStop(CloseCode.ProtocolError);
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Pong"/>Tcp message is received.
        /// </summary>
        /// <param name="message">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnPongReceived(byte[] message)
        {
            Logger?.Info("Received a Pong!");

            if (!_expectingPong)
            {
                CloseConnectionAndStop();
                return;
            }

            if (message.IsFinal())
            {
                _messageTotalLength = MessageTools.GetMessageTotalLengthV2(message);

                if ((ulong)message.LongLength != _messageTotalLength)
                {
                    ReceivingBufferedMessage();
                    OnBufferedReceived(message);
                    return;
                }

                _expectingPong = false;
                var decoded = WebSocketMessageDecoder.DecodeAsBytes(message);
                var response = MessageTools.CreateMessage(decoded, MessageTools.PingOpCode, true);
                Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(response)));
                return;
            }

            CloseConnectionAndStop(CloseCode.ProtocolError);
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Close"/>Tcp message is received.
        /// </summary>
        /// <param name="message">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnClosedReceived(byte[] message)
        {
            Logger?.Info("Received a Connection close!");

            CloseConnectionAndStop(CloseCode.NormalClosure);
        }

        /// <summary>
        /// When we received a framed message (Final flag is 0), we take messages till we get a Final flag of 1
        /// </summary>
        private void ReceivingFrames()
        {
            _buffer = new MemoryStream();
            _framing = true;
        }

        private void OnFrameReceived(byte[] message)
        {
            _buffer.Write(message);

            if (message.IsFinal())
            {
                var data = _buffer.ToArray();
                _buffer?.Close();
                _buffer?.Dispose();
                _buffer = null;
                _framing = false;

                switch (_messageType)
                {
                    case StandardMessageType.Text:
                        OnStringReceived(WebSocketMessageDecoder.DecodeAsString(data));
                        return;

                    case StandardMessageType.Binary:
                        OnBytesReceived(WebSocketMessageDecoder.DecodeAsBytes(data));
                        return;

                    default:
                        // Only text and binary messages should be framed!
                        return;
                }
            }
        }

        /// <summary>
        /// When we received the whole message (Final flag is 1) but the message is too large.
        /// </summary>
        private void ReceivingBufferedMessage()
        {
            _buffer = new MemoryStream();
            _buffering = true;
        }

        private void OnBufferedReceived(byte[] message)
        {
            _buffer.Write(message);

            if ((ulong)_buffer.Length == _messageTotalLength)
            {
                var data = _buffer.ToArray();
                _buffer?.Close();
                _buffer?.Dispose();
                _buffer = null;
                _buffering = false;

                switch (_messageType)
                {
                    case StandardMessageType.Text:
                        OnStringReceived(WebSocketMessageDecoder.DecodeAsString(data));
                        break;

                    case StandardMessageType.Binary:
                        OnBytesReceived(WebSocketMessageDecoder.DecodeAsBytes(data));
                        break;

                    case StandardMessageType.Ping:
                        OnPingReceived(data);
                        break;

                    case StandardMessageType.Pong:
                        OnPongReceived(data);
                        break;

                    case StandardMessageType.Close:
                    case StandardMessageType.Continuation:
                    case StandardMessageType.Invalid:
                    default:
                        // Invalid buffered messages.
                        break;
                }
            }
        }

        private void CloseConnectionAndStop(CloseCode closeCode = CloseCode.NormalClosure)
        {
            var message = MessageTools.CreateCloseMessage((int)closeCode);
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(message)));

            Timers.Cancel(_pingScheduleKey);
            Self.GracefulStop(TimeSpan.FromSeconds(5));
        }

        private void SchedulePing(IActorRef sender)
        {
            Timers.StartPeriodicTimer(_pingScheduleKey, new SendPingCommand { Sender = sender }, TimeSpan.FromSeconds(PingIntervalSeconds));
        }

        private class SendPingCommand
        {
            public IActorRef Sender { get; init; }
        }
    }
}
