using Akka.Actor;
using Akka.Actor.Setup;
using Akka.DependencyInjection;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Akka.IO.TcpTools.Test.AutobahnTests
{
    public class AutobahnTests : TestKit.Xunit2.TestKit
    {
        private readonly ITestOutputHelper _output;

        public AutobahnTests(ITestOutputHelper output)
        {
            _output = output;
        }

        protected override void InitializeTest(ActorSystem system, ActorSystemSetup config, string actorSystemName, string testActorName)
        {
            var services = new ServiceCollection();
            services.AddLogging();

            var serviceProvider = services.BuildServiceProvider();
            var dependencyResolverSetup = DependencyResolverSetup.Create(serviceProvider);
            config = config.And(dependencyResolverSetup);

            base.InitializeTest(system, config, actorSystemName ?? Guid.NewGuid().ToString("N"), testActorName);
        }

        [Fact]
        public async Task FramingTests()
        {
            const int port = 9002;

            var echoGuardianActor = Sys.ActorOf(DependencyResolver.For(Sys).Props<EchoGuardianActor>(port));

            await TestcontainersSettings.ExposeHostPortsAsync(port);

            var configDir = new DirectoryInfo("Config");
            Assert.True(configDir.Exists);

            var container = new ContainerBuilder()
            .WithResourceMapping(configDir, "/config/")
            .WithName($"fuzzingserver_{Guid.NewGuid():N}")
            .WithImage("crossbario/autobahn-testsuite")
            .WithPortBinding(9001, 9001)
            .WithBindMount(configDir.FullName, "/reports", AccessMode.ReadWrite)
            .Build();

            await container.StartAsync();

            var result = await container.ExecAsync(["wstest", "-m", "fuzzingclient", "-s", "config/fuzzingclient.json"]);

            _output.WriteLine("##### STDERR #####");
            _output.WriteLine(result.Stderr);
            _output.WriteLine("##### STDOUT #####");
            _output.WriteLine(result.Stdout);

            if (container.State == DotNet.Testcontainers.Containers.TestcontainersStates.Running)
            {
                await container.StopAsync();
            }
        }
    }
}
