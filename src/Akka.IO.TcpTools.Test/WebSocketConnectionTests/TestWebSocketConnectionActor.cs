using Akka.Actor;
using Akka.IO.TcpTools.Actor;

namespace Akka.IO.TcpTools.Test.WebSocketConnectionTests
{
    internal class TestWebSocketConnectionActor : WebSocketConnectionActorBaseV2
    {
        private readonly IActorRef _testActor;

        public TestWebSocketConnectionActor(IActorRef testActor)
        {
            _testActor = testActor;
        }

        protected override void OnStringReceived(string message)
        {
            base.OnStringReceived(message);

            _testActor.Forward(message);       
        }
    }
}
