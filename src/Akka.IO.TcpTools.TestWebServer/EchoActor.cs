using Akka.Actor;
using Akka.IO.TcpTools.Actor;

namespace Akka.IO.TcpTools.TestWebServer
{
    public class EchoActor : WebSocketConnectionActorBase
    {
        public EchoActor(ILogger<EchoActor> logger) : base(logger)
        { }

        protected override Task OnReceivedAsync(Tcp.Received received)
        {
            return base.OnReceivedAsync(received);
        }

        protected override async Task OnStringReceivedAsync(string message)
        {
            var payload = await ByteStringWriter.WriteAsTextAsync($"Thanks for your message: {message}");
            Sender.Tell(Tcp.Write.Create(payload));
        }
    }
}
