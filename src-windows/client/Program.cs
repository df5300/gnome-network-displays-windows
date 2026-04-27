using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Gnd.Windows.Client;

namespace Gnd.Windows.Client;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        MainAsync(args).GetAwaiter().GetResult();
    }

    public static async Task MainAsync(string[] args)
    {
        // Build the host with gRPC server
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add gRPC service
                services.AddHostedService<LocalGrpcServer>();

                // Add console logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        // Start the host (gRPC server)
        await host.StartAsync();

        // Now start Avalonia app
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // Stop the host when Avalonia exits
        await host.StopAsync();
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect();
    }
}
