using Akka.Actor;
using Akka.Actor.Setup;
using Akka.DependencyInjection;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
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
            const string testName = nameof(FramingTests);
            const int port = 9002; // TODO: make dynamic

            var echoGuardianActor = Sys.ActorOf(DependencyResolver.For(Sys).Props<EchoGuardianActor>(port));

            await TestcontainersSettings.ExposeHostPortsAsync(port);

            var configDir = new DirectoryInfo("Config");
            var subDir = configDir.CreateSubdirectory(testName);
            Assert.True(configDir.Exists);

            var container = new ContainerBuilder()
                .WithName($"fuzzingserver_{Guid.NewGuid():N}")
                .WithImage("crossbario/autobahn-testsuite")
                .WithPortBinding(9001, 9001)
                .WithBindMount(configDir.FullName, "/reports", AccessMode.ReadWrite)
                .Build();

            await container.StartAsync();

            await PrepareFilesAsync(["1.*"], testName, port, container);

            var result = await container.ExecAsync(["wstest", "-m", "fuzzingclient", "-s", "config/fuzzingclient.json"]);

            _output.WriteLine("##### STDERR #####");
            _output.WriteLine(result.Stderr);
            _output.WriteLine("##### STDOUT #####");
            _output.WriteLine(result.Stdout);
            _output.WriteLine($"{subDir.FullName}\\index.html");

            if (container.State == TestcontainersStates.Running)
            {
                await container.StopAsync();
            }
        }

        [Fact]
        public async Task PingPongTests()
        {
            const string testName = nameof(PingPongTests);
            const int port = 9002; // TODO: make dynamic

            var echoGuardianActor = Sys.ActorOf(DependencyResolver.For(Sys).Props<EchoGuardianActor>(port));

            await TestcontainersSettings.ExposeHostPortsAsync(port);

            var configDir = new DirectoryInfo("Config");
            var subDir = configDir.CreateSubdirectory(testName);
            Assert.True(configDir.Exists);

            var container = new ContainerBuilder()
                .WithName($"fuzzingserver_{Guid.NewGuid():N}")
                .WithImage("crossbario/autobahn-testsuite")
                .WithPortBinding(9001, 9001)
                .WithBindMount(configDir.FullName, "/reports", AccessMode.ReadWrite)
                .Build();

            await container.StartAsync();

            await PrepareFilesAsync(["2.*"], testName, port, container);

            var result = await container.ExecAsync(["wstest", "-m", "fuzzingclient", "-s", "config/fuzzingclient.json"]);

            _output.WriteLine("##### STDERR #####");
            _output.WriteLine(result.Stderr);
            _output.WriteLine("##### STDOUT #####");
            _output.WriteLine(result.Stdout);
            _output.WriteLine($"{subDir.FullName}\\index.html");

            if (container.State == TestcontainersStates.Running)
            {
                await container.StopAsync();
            }
        }

        private static async Task PrepareFilesAsync(string[] testCases, string agentName, int port, IContainer targetContainer)
        {
            var fuzzingClientFileInfo = new FileInfo("Config/fuzzingclient.json");
            Assert.True(fuzzingClientFileInfo.Exists, "fuzzingclient.json is missing from the Config folder!");

            var fuzzingServerFileInfo = new FileInfo("Config/fuzzingserver.json");
            Assert.True(fuzzingServerFileInfo.Exists, "fuzzingserver.json is missing from the Config folder!");

            using var fuzzingClientFileStream = fuzzingClientFileInfo.OpenText();
            var fuzzingClientContent = fuzzingClientFileStream.ReadToEnd();
            fuzzingClientFileStream.Close();

            var newFuzzingClientContent = fuzzingClientContent
                .Replace("\"##TESCASES##\"", string.Join(",", testCases.Select(x => $"\"{x}\"")))
                .Replace("##AGENT##", agentName)
                .Replace("##PORT##", port.ToString());

            var newFuzzingClientContentBytes = Encoding.UTF8.GetBytes(newFuzzingClientContent);

            await targetContainer.CopyAsync(fuzzingServerFileInfo, "/config/");
            await targetContainer.CopyAsync(newFuzzingClientContentBytes, "config/fuzzingclient.json");
        }
    }
}
