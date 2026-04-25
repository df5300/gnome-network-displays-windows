using System.Diagnostics;

namespace Gnd.Windows.Shared;

/// <summary>
/// Manages the lifecycle of GND service components (Service, Stream, Client)
/// </summary>
public class ServiceManager : IDisposable
{
    private readonly ILogger<ServiceManager> _logger;
    private Process? _serviceProcess;
    private Process? _streamProcess;
    private bool _disposed;

    public const string ServiceExeName = "gnome-network-displays.service.exe";
    public const string StreamExeName = "gnome-network-displays.stream.exe";

    public ServiceManager()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ServiceManager>();
    }

    /// <summary>
    /// Start the Windows Service
    /// </summary>
    public async Task<bool> StartServiceAsync(string servicePath, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting GND Service: {Path}", servicePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(servicePath, ServiceExeName),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _serviceProcess = Process.Start(startInfo);
            if (_serviceProcess == null)
            {
                _logger.LogError("Failed to start service process");
                return false;
            }

            // Wait for service to be ready
            await Task.Delay(2000, ct);

            if (_serviceProcess.HasExited)
            {
                var error = await _serviceProcess.StandardError.ReadToEndAsync(ct);
                _logger.LogError("Service exited unexpectedly: {Error}", error);
                return false;
            }

            _logger.LogInformation("GND Service started successfully (PID: {Pid})", _serviceProcess.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start GND Service");
            return false;
        }
    }

    /// <summary>
    /// Stop the Windows Service
    /// </summary>
    public async Task StopServiceAsync()
    {
        try
        {
            _logger.LogInformation("Stopping GND Service");

            if (_serviceProcess != null && !_serviceProcess.HasExited)
            {
                _serviceProcess.Kill(entireProcessTree: true);
                await _serviceProcess.WaitForExitAsync();
                _serviceProcess.Dispose();
                _serviceProcess = null;
            }

            _logger.LogInformation("GND Service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping GND Service");
        }
    }

    /// <summary>
    /// Start the Stream Renderer process
    /// </summary>
    public async Task<bool> StartStreamRendererAsync(string streamPath, string deviceId, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting Stream Renderer for device: {DeviceId}", deviceId);

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(streamPath, StreamExeName),
                Arguments = $"--device-id {deviceId}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _streamProcess = Process.Start(startInfo);
            if (_streamProcess == null)
            {
                _logger.LogError("Failed to start stream renderer process");
                return false;
            }

            await Task.Delay(1000, ct);

            if (_streamProcess.HasExited)
            {
                var error = await _streamProcess.StandardError.ReadToEndAsync(ct);
                _logger.LogError("Stream renderer exited unexpectedly: {Error}", error);
                return false;
            }

            _logger.LogInformation("Stream Renderer started (PID: {Pid})", _streamProcess.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Stream Renderer");
            return false;
        }
    }

    /// <summary>
    /// Stop the Stream Renderer process
    /// </summary>
    public async Task StopStreamRendererAsync()
    {
        try
        {
            _logger.LogInformation("Stopping Stream Renderer");

            if (_streamProcess != null && !_streamProcess.HasExited)
            {
                _streamProcess.Kill(entireProcessTree: true);
                await _streamProcess.WaitForExitAsync();
                _streamProcess.Dispose();
                _streamProcess = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Stream Renderer");
        }
    }

    /// <summary>
    /// Check if service is running
    /// </summary>
    public bool IsServiceRunning => _serviceProcess != null && !_serviceProcess.HasExited;

    /// <summary>
    /// Check if stream renderer is running
    /// </summary>
    public bool IsStreamRunning => _streamProcess != null && !_streamProcess.HasExited;

    public void Dispose()
    {
        if (!_disposed)
        {
            StopServiceAsync().Wait();
            StopStreamRendererAsync().Wait();
            _disposed = true;
        }
    }
}

public class LoggerFactory
{
    public static LoggerFactory Create(Action<object> configure) => new();
    public ILogger<T> CreateLogger<T>() => new ConsoleLogger<T>();
}

public class ConsoleLogger<T> : ILogger<T>
{
    public void LogInformation(string message, params object[] args) =>
        Console.WriteLine($"[INFO] {string.Format(message, args)}");
    public void LogError(Exception ex, string message, params object[] args) =>
        Console.WriteLine($"[ERROR] {string.Format(message, args)}: {ex.Message}");
    public void LogWarning(string message, params object[] args) =>
        Console.WriteLine($"[WARN] {string.Format(message, args)}");
}

public interface ILogger<T>
{
    void LogInformation(string message, params object[] args);
    void LogError(Exception ex, string message, params object[] args);
    void LogWarning(string message, params object[] args);
}
