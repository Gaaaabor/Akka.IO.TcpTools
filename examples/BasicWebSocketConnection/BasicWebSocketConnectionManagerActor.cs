using Akka.Actor;
using Akka.DependencyInjection;
using Akka.IO;
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
}
