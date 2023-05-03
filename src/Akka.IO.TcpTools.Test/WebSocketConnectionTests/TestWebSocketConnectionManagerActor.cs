using Akka.Actor;
using Akka.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Akka.IO.TcpTools.Test.WebSocketConnectionTests
{
    internal class TestWebSocketConnectionManagerActor : ReceiveActor
    {
        private ILogger<TestWebSocketConnectionManagerActor> _logger;
        private readonly IActorRef _testActor;

        public TestWebSocketConnectionManagerActor(ILogger<TestWebSocketConnectionManagerActor> logger, int port, IActorRef testActor)
        {
            _logger = logger;
            _testActor = testActor;

            Context.System
                .Tcp()
                .Tell(new Tcp.Bind(Self, new IPEndPoint(IPAddress.Any, port), options: new[] { new Inet.SO.ReceiveBufferSize(1024) }));

            Receive<Tcp.Bound>(OnBound);
            Receive<Tcp.Connected>(OnConnected);
        }

        protected virtual void OnBound(Tcp.Bound bound)
        {
            _logger.LogInformation("{ActorName} Listening on {LocalAddress}", nameof(TestWebSocketConnectionManagerActor), bound.LocalAddress);
        }

        protected virtual void OnConnected(Tcp.Connected connected)
        {
            _logger.LogInformation("{ActorName} received a connection from {RemoteAddress}", nameof(TestWebSocketConnectionManagerActor), connected.RemoteAddress);

            var actorProps = DependencyResolver
                .For(Context.System)
                .Props<TestWebSocketConnectionActor>(_testActor);

            var name = $"testWebSocketConnection_{connected.RemoteAddress}_{Guid.NewGuid():N}";
            var testWebSocketConnectionActor = Context.ActorOf(actorProps, name);

            _logger.LogInformation("TestWebSocketConnectionActor created with name: {name}", name);

            Sender.Tell(new Tcp.Register(testWebSocketConnectionActor));
        }

    }
}
