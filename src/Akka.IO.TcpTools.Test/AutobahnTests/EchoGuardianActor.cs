using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Event;
using System.Net;

namespace Akka.IO.TcpTools.Test.AutobahnTests
{
    public class EchoGuardianActor : ReceiveActor
    {
        private ILoggingAdapter _logger;

        public EchoGuardianActor(int port)
        {
            _logger = Context.GetLogger();
            Context.System
                .Tcp()
                .Tell(new Tcp.Bind(Self, new IPEndPoint(IPAddress.Any, port), options: new[] { new Inet.SO.ReceiveBufferSize(1024) }));

            Receive<Tcp.Bound>(OnBound);
            Receive<Tcp.Connected>(OnConnected);
        }

        protected virtual void OnBound(Tcp.Bound bound)
        {
            _logger.Info("{ActorName} Listening on {LocalAddress}", nameof(EchoGuardianActor), bound.LocalAddress);
        }

        protected virtual void OnConnected(Tcp.Connected connected)
        {
            _logger.Info("{ActorName} received a connection from {RemoteAddress}", nameof(EchoGuardianActor), connected.RemoteAddress);

            var actorProps = DependencyResolver
                .For(Context.System)
                .Props<EchoActor>();

            var name = $"echoactor_{connected.RemoteAddress}_{Guid.NewGuid():N}";
            var basicWebSocketConnectionActor = Context.ActorOf(actorProps, name);

            _logger.Info("EchoActor created with name: {name}", name);

            Sender.Tell(new Tcp.Register(basicWebSocketConnectionActor));
        }
    }
}
