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
        var useVersion2Actor = true;
        system.ActorOf(dependencyResolver.Props<EchoGuardianActor>(port, useVersion2Actor));
    });
});

var app = builder.Build();
app.Run();