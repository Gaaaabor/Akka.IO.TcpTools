# Akka.IO.TcpTools

This package is created in order to make WebSocket message receiving / sending easier in the Akka ActorSystem.

The WebSocketConnectionActorBase class can be used as a WebSocketClient.
It decodes received messages, can receive framed messages and have the possibility to response to Ping/Pong messages if the OnPingReceivedAsync / OnPongReceivedAsync methods ovewritten.

Example for basic usage:

```C#
using Akka.Actor;
using Akka.DependencyInjection;
using Akka.IO;
using Akka.IO.TcpTools;
using Akka.IO.TcpTools.Actor;
using Microsoft.Extensions.Logging;
using System.Net;

namespace BasicWebSocketConnection
{
    public class BasicWebSocketConnectionManagerActor : ReceiveActor
    {
        private ILogger<BasicWebSocketConnectionManagerActor> _logger;

        public BasicWebSocketConnectionManagerActor(ILogger<BasicWebSocketConnectionManagerActor> logger, int port)
        {
            _logger = logger;

            Context.System
                .Tcp()
                .Tell(new Tcp.Bind(Self, new IPEndPoint(IPAddress.Any, port), options: new[] { new Inet.SO.ReceiveBufferSize(1024) }));

            Receive<Tcp.Bound>(OnBound);
            Receive<Tcp.Connected>(OnConnected);
        }

        protected virtual void OnBound(Tcp.Bound bound)
        {
            _logger.LogInformation("{ActorName} Listening on {LocalAddress}", nameof(BasicWebSocketConnectionManagerActor), bound.LocalAddress);
        }

        protected virtual void OnConnected(Tcp.Connected connected)
        {
            _logger.LogInformation("{ActorName} received a connection from {RemoteAddress}", nameof(BasicWebSocketConnectionManagerActor), connected.RemoteAddress);

            var actorProps = DependencyResolver
                .For(Context.System)
                .Props<BasicWebSocketConnectionActor>();

            var name = $"basicwebsocketconnection_{connected.RemoteAddress}_{Guid.NewGuid():N}";
            var basicWebSocketConnectionActor = Context.ActorOf(actorProps, name);

            _logger.LogInformation("BasicWebSocketConnectionActor created with name: {name}", name);

            Sender.Tell(new Tcp.Register(basicWebSocketConnectionActor));
        }
    }

    public class BasicWebSocketConnectionActor : WebSocketConnectionActorBase
    {
        public BasicWebSocketConnectionActor(ILogger<BasicWebSocketConnectionActor> logger) : base  (logger)
        { }

        protected override async Task OnStringReceivedAsync(string message)
        {
            Logger.LogInformation("Got a string message: {message}", message);

            var payload = await ByteStringWriter.WriteAsTextAsync($"Thanks for your message:    {message}");
            Sender.Tell(Tcp.Write.Create(payload));
        }
    }
}
```