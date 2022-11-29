using Akka.Actor;
using Microsoft.Extensions.Logging;

namespace Akka.IO.TcpTools.Actor
{
    public abstract class WebSocketConnectionActorBase : ReceiveActor
    {
        private FramedWebSocketMessage _framedWebSocketMessage;

        protected ILogger Logger { get; }

        public WebSocketConnectionActorBase() : this(null)
        { }

        public WebSocketConnectionActorBase(ILogger logger)
        {
            Logger = logger;

            ReceiveAsync<string>(OnStringReceivedAsync);
            ReceiveAsync<Tcp.Received>(OnReceivedAsync);
            ReceiveAsync<Tcp.PeerClosed>(OnPeerClosedAsync);
        }

        protected override void PreStart()
        {
            Logger?.LogInformation("{ActorName} started", Self.Path.Name);
        }

        protected override void PostStop()
        {
            Logger?.LogInformation("{ActorName} stopped", Self.Path.Name);
        }

        /// <summary>
        /// This method is called when a Text based WebSocket message is received and decoded.
        /// </summary>
        /// <param name="message">The received and decoded message</param>
        /// <returns></returns>
        protected virtual Task OnStringReceivedAsync(string message)
        {
            Logger?.LogInformation("{ActorName} received a string message: {message}", Self.Path.Name, message);
            return Task.CompletedTask;
        }

        protected virtual async Task OnReceivedAsync(Tcp.Received received)
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
                        await OnBinaryReceivedAsync(received);
                        return;

                    case StandardMessageType.Text:
                        await OnTextReceivedAsync(received);
                        return;

                    case StandardMessageType.Ping:
                        await OnPingReceivedAsync(received);
                        return;

                    case StandardMessageType.Pong:
                        await OnPongReceivedAsync(received);
                        return;

                    case StandardMessageType.Close:
                        await OnClosedReceivedAsync(received);
                        return;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error during {Name}!", nameof(OnReceivedAsync));
                await Self.GracefulStop(TimeSpan.FromSeconds(5));
            }
        }

        protected virtual Task OnBinaryReceivedAsync(Tcp.Received received)
        {
            Logger?.LogInformation("Received a Binary message!");
            return Task.CompletedTask;
        }

        protected virtual async Task OnTextReceivedAsync(Tcp.Received received)
        {
            Logger?.LogInformation("Received a Text message!");

            var receivedBytes = received.Data.ToArray();
            var totalLength = MessageTools.GetMessageTotalLength(receivedBytes);
            if (totalLength > (ulong)receivedBytes.Length)
            {
                _framedWebSocketMessage = new FramedWebSocketMessage(totalLength);
                _framedWebSocketMessage.Write(receivedBytes);
                BecomeStacked(() =>
                {
                    ReceiveAsync<object>(OnFrameReceivedAsync);
                });

                return;
            }

            var receivedMessage = await ByteStringReader.ReadAsync(receivedBytes);
            Self.Forward(receivedMessage);
        }

        protected virtual Task OnPeerClosedAsync(Tcp.PeerClosed peerClosed)
        {
            Context.Stop(Self);
            return Task.CompletedTask;
        }

        protected virtual async Task OnFrameReceivedAsync(object rawFrame)
        {
            try
            {
                Logger?.LogInformation("Received a frame of a Framed message!");

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
                    Logger?.LogInformation("Framed message fully received!");

                    UnbecomeStacked();

                    var receivedMessage = await ByteStringReader.ReadAsync(_framedWebSocketMessage.ReadAllBytes());
                    Self.Forward(receivedMessage);

                    _framedWebSocketMessage.Close();
                    _framedWebSocketMessage = null;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error during {Name}!", nameof(OnFrameReceivedAsync));
            }
        }

        protected virtual Task OnPingReceivedAsync(Tcp.Received received)
        {
            Logger?.LogInformation("Received a Ping!");
            return Task.CompletedTask;
        }

        protected virtual Task OnPongReceivedAsync(Tcp.Received received)
        {
            Logger?.LogInformation("Received a Pong!");
            return Task.CompletedTask;
        }

        protected virtual Task OnClosedReceivedAsync(Tcp.Received received)
        {
            Logger?.LogInformation("Received a Connection close!");
            Sender.Tell(Tcp.Write.Create(ByteString.FromBytes(MessageTools.CloseMessage)));
            return Task.CompletedTask;
        }

    }
}
