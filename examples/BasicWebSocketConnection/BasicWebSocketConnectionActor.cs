using Akka.Actor;
using Akka.IO;
using Akka.IO.TcpTools;
using Akka.IO.TcpTools.Actor;
using Microsoft.Extensions.Logging;

namespace BasicWebSocketConnection
{
    public class BasicWebSocketConnectionActor : WebSocketConnectionActorBase
    {
        public BasicWebSocketConnectionActor(ILogger<BasicWebSocketConnectionActor> logger) : base(logger)
        { }

        protected override async Task OnStringReceivedAsync(string message)
        {
            Logger.LogInformation("Got a string message: {message}", message);

            var payload = await ByteStringWriter.WriteAsTextAsync($"Thanks for your message: {message}");
            Sender.Tell(Tcp.Write.Create(payload));
        }
    }
}
