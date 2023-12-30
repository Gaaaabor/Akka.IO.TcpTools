using Akka.Hosting;
using Akka.IO.TcpTools.TestWebServer;

const int port = 9002;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Error);

builder.Services.AddAkka("Test", builder =>
{
    builder.ConfigureLoggers(setup =>
    {
        setup.LogLevel = Akka.Event.LogLevel.ErrorLevel;
    });

    builder.StartActors((system, registry, dependencyResolver) =>
    {
        system.ActorOf(dependencyResolver.Props<EchoGuardianActor>(port, true));
    });
});

var app = builder.Build();
app.Run();