using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gnd.Windows.Service;

public class ServiceMain : BackgroundService
{
    private readonly ILogger<ServiceMain> _logger;
    private readonly IpcServer _ipcServer;

    public ServiceMain(ILogger<ServiceMain> logger, IpcServer ipcServer)
    {
        _logger = logger;
        _ipcServer = ipcServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Gnome Network Displays Service starting...");

        try
        {
            await _ipcServer.StartAsync(stoppingToken);
            _logger.LogInformation("Gnome Network Displays Service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start IPC server");
            throw;
        }

        // Keep the service running until cancellation is requested
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Gnome Network Displays Service stopping...");
        await _ipcServer.StopAsync(cancellationToken);
        _logger.LogInformation("Gnome Network Displays Service stopped");
        await base.StopAsync(cancellationToken);
    }
}
