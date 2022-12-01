using BasicWebSocketConnection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Starting...");

        var host = Host
            .CreateDefaultBuilder(args)
            .ConfigureServices(x => x.AddHostedService<AkkaHostService>())
            .Build();

        Console.WriteLine("Started!");

        host.Run();
    }
}