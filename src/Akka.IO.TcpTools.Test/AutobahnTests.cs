//using Akka.Actor;
//using Akka.Actor.Setup;
//using Akka.DependencyInjection;
//using Akka.IO.TcpTools.Test.WebSocketConnectionTests;
//using Microsoft.Extensions.DependencyInjection;

//namespace Akka.IO.TcpTools.Test
//{
//    public class AutobahnTests : TestKit.Xunit2.TestKit
//    {
//        protected override void InitializeTest(ActorSystem system, ActorSystemSetup config, string actorSystemName, string testActorName)
//        {
//            var services = new ServiceCollection();
//            services.AddLogging();
//            var serviceProvider = services.BuildServiceProvider();
//            var dependencyResolverSetup = DependencyResolverSetup.Create(serviceProvider);
//            config = config.And(dependencyResolverSetup);

//            base.InitializeTest(system, config, actorSystemName ?? Guid.NewGuid().ToString("N"), testActorName);
//        }

//        [Fact]
//        public async Task Test1()
//        {
//            const int port = 9001;

//            Sys.ActorOf(DependencyResolver.For(Sys).Props<TestWebSocketConnectionManagerActor>(port, TestActor));

//            var asdf = await FishForMessageAsync<string>(x => !string.IsNullOrEmpty(x), TimeSpan.FromSeconds(20));
//            ;
//            // Create a new instance of a container.
//            var container = new ContainerBuilder()
//                .WithName("fuzzingserver")
//                .WithImage("crossbario/autobahn-testsuite")
//                .WithPortBinding(9001, 9001)
//                .WithBindMount("c:/temp/config", "/config")
//                .WithBindMount("c:/temp/reports", "/reports")
//                .Build();

//            await container.StartAsync();

//            await Task.Delay(30000);

//            using var clientWebSocket = new ClientWebSocket();
//            await clientWebSocket.ConnectAsync(new Uri($"ws://localhost:9001"), CancellationToken.None);

//            var buffer = WebSocket.CreateClientBuffer(1024, 1024);

//            WebSocketReceiveResult result;
//            do
//            {
//                result = await clientWebSocket.ReceiveAsync(buffer, CancellationToken.None);
//                ;
//            } while (result.CloseStatus != WebSocketCloseStatus.NormalClosure);


//            var messageBytes = Encoding.UTF8.GetBytes(message);
//            await clientWebSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, CancellationToken.None);

//            var receivedStringMessage = ExpectMsg<string>(TimeSpan.FromSeconds(10));
//            Assert.Equal(message, receivedStringMessage);
//        }
//    }
//}
