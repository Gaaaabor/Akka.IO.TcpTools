using Akka.Actor;
using Akka.Event;

namespace Akka.IO.TcpTools.Actor
{
    public abstract class WebSocketConnectionActorBaseV2 : ReceiveActor
    {
        private ulong _messageTotalLength;
        private MemoryStream _messageFrames;
        private bool _expectingPong;

        protected ILoggingAdapter Logger { get; }

        public WebSocketConnectionActorBaseV2()
        {
            Logger = Context.GetLogger();

            Receive<Tcp.Received>(OnReceived);
            Receive<Tcp.PeerClosed>(OnPeerClosed);
            Receive<string>(OnStringReceived);
            Receive<byte[]>(OnBytesReceived);
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
                var secWebSocketKey = MessageTools.GetSecWebSocketKey(received.Data.ToString());
                if (!string.IsNullOrEmpty(secWebSocketKey))
                {
                    Sender.Tell(Tcp.Write.Create(ByteString.FromString(MessageTools.CreateAck(secWebSocketKey))));
                    return;
                }

                byte[] data = [.. received.Data];
                var isValid = MessageTools.ValidateMessage(data);
                if (!isValid)
                {
                    CloseConnection(1002);
                    Context.Stop(Self);
                }

                var messageType = MessageTools.GetMessageType(data);
                switch (messageType)
                {
                    case StandardMessageType.Continuation:
                        OnContinuationReceived(received);
                        return;

                    case StandardMessageType.Text:
                        OnTextReceived(received);
                        return;

                    case StandardMessageType.Binary:
                        OnBinaryReceived(received);
                        return;

                    case StandardMessageType.Ping:
                        OnPingReceived(received);
                        return;

                    case StandardMessageType.Pong:
                        OnPongReceived(received);
                        return;

                    case StandardMessageType.Close:
                        OnClosedReceived(received);
                        return;
                }

                CloseConnection();
                Context.Stop(Self);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Error during {0}!", nameof(OnReceived));
                CloseConnection();
                Self.GracefulStop(TimeSpan.FromSeconds(5));
            }
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Binary"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnBinaryReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Binary message!");

            byte[] data = [.. received.Data];
            _messageTotalLength = MessageTools.GetMessageTotalLengthV2(data);

            if ((ulong)data.Length != _messageTotalLength)
            {
                BecomeStacked(ReceivingFrames);
                OnFrameReceived(received);
                return;
            }

            var decoded = WebSocketMessageDecoder.DecodeAsBytes(data);
            Self.Forward(decoded);
        }

        protected virtual void OnContinuationReceived(Tcp.Received received)
        {

        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Text"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnTextReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Text message!");

            byte[] data = [.. received.Data];
            _messageTotalLength = MessageTools.GetMessageTotalLengthV2(data);

            if ((ulong)data.Length != _messageTotalLength)
            {
                BecomeStacked(ReceivingFrames);
                OnFrameReceived(received);
                return;
            }

            var decoded = WebSocketMessageDecoder.DecodeAsString(data);
            Self.Forward(decoded);
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
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnPingReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Ping!");

            byte[] data = [.. received.Data];

            _messageTotalLength = MessageTools.GetMessageTotalLengthV2(data);

            if ((ulong)data.Length != _messageTotalLength)
            {
                BecomeStacked(ReceivingFrames);
                OnFrameReceived(received);
                return;
            }

            var decoded = WebSocketMessageDecoder.DecodeAsBytes(data);
            var response = MessageTools.CreateMessage(decoded, MessageTools.PongOpCode, true);
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(response)));            
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Pong"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnPongReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Pong!");            

            byte[] data = [.. received.Data];

            _messageTotalLength = MessageTools.GetMessageTotalLengthV2(data);

            if ((ulong)data.Length != _messageTotalLength)
            {
                BecomeStacked(ReceivingFrames);
                OnFrameReceived(received);
                return;
            }

            var decoded = WebSocketMessageDecoder.DecodeAsBytes(data);
            var response = MessageTools.CreateMessage(decoded, MessageTools.PingOpCode, true);
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(response)));
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Close"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnClosedReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Connection close!");

            var message = MessageTools.CreateCloseMessage(1000);
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(message)));

            Context.Stop(Self);
        }

        private void ReceivingFrames()
        {
            _messageFrames = new MemoryStream();

            Receive<Tcp.Received>(OnFrameReceived);
        }

        private void OnFrameReceived(Tcp.Received received)
        {
            _messageFrames.Write([.. received.Data]);

            if ((ulong)_messageFrames.Length == _messageTotalLength)
            {
                var completeMessage = new Tcp.Received(ByteString.FromBytes(_messageFrames.ToArray()));

                _messageFrames?.Close();
                _messageFrames?.Dispose();
                _messageFrames = null;

                UnbecomeStacked();
                Self.Forward(completeMessage);
            }
        }

        private void CloseConnection(int closeCode = 1000)
        {
            var message = MessageTools.CreateCloseMessage(closeCode);
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(message)));
        }
    }
}
