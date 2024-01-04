using Akka.Actor;
using Akka.Event;

namespace Akka.IO.TcpTools.Actor
{
    public abstract class WebSocketConnectionActorBaseV2 : ReceiveActor
    {
        private ulong _messageTotalLength;
        private MemoryStream _messageFrames;

        protected ILoggingAdapter Logger { get; }

        public WebSocketConnectionActorBaseV2()
        {
            Logger = Context.GetLogger();

            Receive<string>(OnStringReceived);
            Receive<byte[]>(OnBytesReceived);
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

                var messageType = MessageTools.GetMessageType([.. received.Data]);
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

                    case StandardMessageType.Continuation:
                        // TODO: Handle properly
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
            _messageTotalLength = MessageTools.GetMessageTotalLengthV2(receivedBytes);
            if ((ulong)receivedBytes.Length != _messageTotalLength)
            {
                _messageFrames = new MemoryStream();

                BecomeStacked(() =>
                {
                    Receive<Tcp.Received>(OnBinaryFrameReceived);
                });

                OnBinaryFrameReceived(received);
            }
            else
            {
                var receivedMessage = WebSocketMessageDecoder.DecodeAsBytes(receivedBytes);
                Self.Forward(receivedMessage);
            }
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
            _messageTotalLength = MessageTools.GetMessageTotalLengthV2(receivedBytes);
            if ((ulong)receivedBytes.Length != _messageTotalLength)
            {
                _messageFrames = new MemoryStream();

                BecomeStacked(() =>
                {
                    Receive<Tcp.Received>(OnTextFrameReceived);
                });

                OnTextFrameReceived(received);
            }
            else
            {
                var receivedMessage = WebSocketMessageDecoder.DecodeAsString(receivedBytes);
                Self.Forward(receivedMessage);
            }
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
        /// <param name="frame">The received raw frame</param>
        /// <returns></returns>
        protected virtual void OnTextFrameReceived(Tcp.Received frame)
        {
            try
            {
                Logger?.Info("Received a frame of a Framed message!");

                if (_messageFrames is null)
                {
                    UnbecomeStacked();
                    Self.Forward(frame);
                    return;
                }

                _messageFrames.Write([.. frame.Data]);

                // TODO check: The total size of a message could be larger than the MemoryStream's max size? (Assumption is yes...)
                // TODO 2: Handle the case when the message is not fully received! (timer)
                if (_messageTotalLength == (ulong)_messageFrames.Length)
                {
                    Logger?.Info("Framed message fully received!");

                    UnbecomeStacked();

                    var receivedMessage = WebSocketMessageDecoder.DecodeAsString(_messageFrames.ToArray());
                    Self.Forward(receivedMessage);

                    _messageFrames?.Dispose();
                    _messageFrames = null;
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Error during {0}!", nameof(OnTextFrameReceived));
            }
        }

        protected virtual void OnBinaryFrameReceived(Tcp.Received frame)
        {
            try
            {
                Logger?.Info("Received a frame of a Framed message!");

                if (_messageFrames is null)
                {
                    UnbecomeStacked();
                    Self.Forward(frame);
                    return;
                }

                _messageFrames.Write([.. frame.Data]);

                // TODO check: The total size of a message could be larger than the MemoryStream's max size? (Assumption is yes...)
                // TODO 2: Handle the case when the message is not fully received! (timer)
                if (_messageTotalLength == (ulong)_messageFrames.Length)
                {
                    Logger?.Info("Framed message fully received!");

                    UnbecomeStacked();

                    var receivedMessage = WebSocketMessageDecoder.DecodeAsBytes(_messageFrames.ToArray());
                    Self.Forward(receivedMessage);

                    _messageFrames?.Dispose();
                    _messageFrames = null;
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Error during {0}!", nameof(OnBinaryFrameReceived));
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

            var arr = received.Data.ToArray();
            arr[0] = MessageTools.PongOpCode | 0x80;
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(arr)));

            //var receivedMessage = ByteStringReaderV2.Read(received.Data);
            //var pongMessage = MessageTools.CreatePongMessage(receivedMessage);
            //Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(pongMessage)));
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Pong"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnPongReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Pong!");

            var arr = received.Data.ToArray();
            arr[0] = MessageTools.PingOpCode | 0x80;
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(arr)));

            //var receivedMessage = ByteStringReaderV2.Read(received.Data);
            //var pingMessage = MessageTools.CreatePingMessage(receivedMessage);
            //Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(pingMessage)));
        }

        /// <summary>
        /// This method is called when a <see cref="StandardMessageType.Close"/>Tcp message is received.
        /// </summary>
        /// <param name="received">The received Tcp message</param>
        /// <returns></returns>
        protected virtual void OnClosedReceived(Tcp.Received received)
        {
            Logger?.Info("Received a Connection close!");

            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(MessageTools.CloseMessage)));

            Context.Stop(Self);
        }

        private void CloseConnection()
        {
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(MessageTools.CloseMessage)));
        }
    }
}
