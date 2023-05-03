using Akka.Actor;
using Akka.IO.TcpTools.Actor;
using Microsoft.Extensions.Logging;

namespace Akka.IO.TcpTools.Test.WebSocketConnectionTests
{
    internal class TestWebSocketConnectionActor : WebSocketConnectionActorBase
    {
        private readonly IActorRef _testActor;

        public TestWebSocketConnectionActor(ILogger<TestWebSocketConnectionActor> logger, IActorRef testActor) : base(logger)
        {
            _testActor = testActor;
        }

        protected override async Task OnStringReceivedAsync(string message)
        {
            await base.OnStringReceivedAsync(message);

            _testActor.Forward(message);
            var payload = await ByteStringWriter.WriteAsTextAsync(message);
            _testActor.Forward(Tcp.Write.Create(payload));
        }
    }
}
