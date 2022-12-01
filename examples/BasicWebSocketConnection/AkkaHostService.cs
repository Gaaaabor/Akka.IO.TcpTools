using Akka.Actor;
using Akka.Configuration;
using Akka.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Data;
using System.Net;
using System.Text;

namespace BasicWebSocketConnection
{
    public class AkkaHostService : IHostedService
    {
        private const string ActorSystemName = "basicwebsocketconnection";

        private IServiceProvider? _serviceProvider;
        private ActorSystem? _actorSystem;

        public AkkaHostService(IServiceProvider? serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            _actorSystem = CreateActorSystem();
            var dependencyResolver = DependencyResolver.For(_actorSystem);

            var port = 9876;
            var actorProps = dependencyResolver.Props<BasicWebSocketConnectionManagerActor>(port);
            _actorSystem.ActorOf(actorProps, "basicwebsocketconnectionmanager");

            return Task.CompletedTask;
        }

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            // strictly speaking this may not be necessary - terminating the ActorSystem would also work
            // but this call guarantees that the shutdown of the cluster is graceful regardless
            await CoordinatedShutdown.Get(_actorSystem).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }

        private ActorSystem CreateActorSystem()
        {
            var hostName = Dns.GetHostName();
            var hostAddresses = Dns.GetHostAddresses(hostName, System.Net.Sockets.AddressFamily.InterNetwork);
            var hostAddress = hostAddresses.ElementAtOrDefault(0) ?? IPAddress.Loopback;

            var builder = new StringBuilder();
            builder.AppendLine($"akka.remote.dot-netty.tcp.hostname={hostAddress}");

            var config = ConfigurationFactory.Default()
                .WithFallback(ConfigurationFactory.ParseString(builder.ToString()));

            var bootstrapSetup = BootstrapSetup
                .Create()
                .WithConfig(config);

            var dependencyResolverSetup = DependencyResolverSetup.Create(_serviceProvider);
            var actorSystemSetup = bootstrapSetup.And(dependencyResolverSetup);
            var actorSystem = ActorSystem.Create(ActorSystemName, actorSystemSetup);

            return actorSystem;
        }
    }
}
