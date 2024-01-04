using Akka.Actor;
using Akka.IO.TcpTools.Actor;

namespace Akka.IO.TcpTools.Test.AutobahnTests
{
    public class EchoActor : WebSocketConnectionActorBaseV2
    {
        public EchoActor()
        {
            Receive<ByteString>(OnByteStringReceived);
        }

        protected override void OnReceived(Tcp.Received received)
        {
            base.OnReceived(received);
        }

        protected override void OnStringReceived(string message)
        {
            var sender = Sender;
            ByteStringWriter
                .WriteAsTextAsync(message)
                .PipeTo(Self, sender);
        }

        protected override void OnBytesReceived(byte[] message)
        {
            var payload = ByteString.FromBytes(MessageTools.CreateMessage(message, MessageTools.BinaryOpCode, false));
            Sender.Tell(Tcp.Write.Create(payload));
        }

        private void OnByteStringReceived(ByteString message)
        {
            Sender.Tell(Tcp.Write.Create(message));
        }
    }
}
