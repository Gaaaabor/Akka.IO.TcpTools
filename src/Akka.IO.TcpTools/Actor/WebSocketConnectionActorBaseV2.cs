using Akka.Actor;
using Akka.Event;

namespace Akka.IO.TcpTools.Actor
{
    public abstract class WebSocketConnectionActorBaseV2 : ReceiveActor
    {
        private FramedWebSocketMessage _framedWebSocketMessage;

        protected ILoggingAdapter Logger { get; }

        public WebSocketConnectionActorBaseV2()
        {
            Logger = Context.GetLogger();

            Receive<string>(OnStringReceived);
            Receive<Tcp.Received>(OnReceived);
            Receive<Tcp.PeerClosed>(OnPeerClosed);
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
            Logger?.Info("{0} received a string message: {1}", Self.Path.Name, message);
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

                var messageType = MessageTools.GetMessageType(received.Data.ToArray());
                switch (messageType)
                {
                    case StandardMessageType.Binary:
                        OnBinaryReceived(received);
                        return;

                    case StandardMessageType.Text:
                        OnTextReceived(received);
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

            var receivedBytes = received.Data.ToArray();
            var totalLength = MessageTools.GetMessageTotalLength(receivedBytes);
            if (totalLength > (ulong)receivedBytes.Length)
            {
                _framedWebSocketMessage = new FramedWebSocketMessage(totalLength);
                _framedWebSocketMessage.Write(receivedBytes);
                BecomeStacked(() =>
                {
                    Receive<object>(OnFrameReceived);
                });

                return;
            }

            var receivedMessage = ByteStringReaderV2.Read(receivedBytes);
            Self.Forward(receivedMessage);
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Text"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnTextReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Text message!");

            var receivedBytes = received.Data.ToArray();
            var totalLength = MessageTools.GetMessageTotalLength(receivedBytes);
            if (totalLength > (ulong)receivedBytes.Length)
            {
                _framedWebSocketMessage = new FramedWebSocketMessage(totalLength);
                _framedWebSocketMessage.Write(receivedBytes);
                BecomeStacked(() =>
                {
                    Receive<object>(OnFrameReceived);
                });

                return;
            }

            var receivedMessage = ByteStringReaderV2.Read(receivedBytes);
            Self.Forward(receivedMessage);
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
        /// This method is called for each frame received from a framed message.
        /// </summary>
        /// <param name="rawFrame">The received raw frame</param>
        /// <returns></returns>
        protected virtual void OnFrameReceived(object rawFrame)
        {
            try
            {
                Logger?.Info("Received a frame of a Framed message!");

                if (rawFrame is Tcp.Received frame)
                {
                    if (_framedWebSocketMessage is null)
                    {
                        UnbecomeStacked();
                        Self.Forward(frame);
                        return;
                    }

                    _framedWebSocketMessage.Write(frame.Data.ToArray());
                }
                else
                {
                    Self.Forward(rawFrame);
                    return;
                }

                if (_framedWebSocketMessage.IsCompleted())
                {
                    Logger?.Info("Framed message fully received!");

                    UnbecomeStacked();

                    var receivedMessage = ByteStringReaderV2.Read(_framedWebSocketMessage.ReadAllBytes());
                    Self.Forward(receivedMessage);

                    _framedWebSocketMessage.Close();
                    _framedWebSocketMessage = null;
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Error during {0}!", nameof(OnFrameReceived));
            }
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Ping"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnPingReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Ping!");

            var receivedMessage = ByteStringReaderV2.Read(received.Data);
            var pongMessage = MessageTools.CreatePongMessage(receivedMessage);
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(pongMessage)));
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Pong"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnPongReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Pong!");

            var receivedMessage = ByteStringReaderV2.Read(received.Data);
            var pingMessage = MessageTools.CreatePingMessage(receivedMessage);
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(pingMessage)));
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Close"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnClosedReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Connection close!");

            //var receivedMessage = ByteStringReaderV2.Read(received.Data);
            //var closeMessage = MessageTools.CreateCloseMessage(receivedMessage);
            //Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(MessageTools.CloseMessage)));
            Sender.Tell(Tcp.Close.Instance);

            Context.Stop(Self);
        }

        private void CloseConnection()
        {
            Sender.Tell(Tcp.Close.Instance);
            //Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(MessageTools.CloseMessage)));
        }
    }
}
