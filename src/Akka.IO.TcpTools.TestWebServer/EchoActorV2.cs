using Akka.Actor;
using Akka.IO.TcpTools.Actor;

namespace Akka.IO.TcpTools.TestWebServer
{
    public class EchoActorV2 : WebSocketConnectionActorBaseV2
    {
        public EchoActorV2()
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
            Self.Forward(ByteString.FromBytes(MessageTools.CreateMessage(message, MessageTools.BinaryOpCode, false)));
        }

        private void OnByteStringReceived(ByteString message)
        {
            Sender.Tell(Tcp.Write.Create(message));
        }
    }
}
