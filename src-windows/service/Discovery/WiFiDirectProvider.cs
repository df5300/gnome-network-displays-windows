using Gnd.Windows.Shared.Discovery;
using Gnd.Windows.Shared.Platform;

namespace Gnd.Windows.Service.Discovery;

public class WiFiDirectProvider : IDisposable {
    private readonly WiFiDirectDiscovery _discovery;
    private bool _disposed;

    public event EventHandler<DeviceDiscoveredEventArgs>? DeviceFound => _discovery.DeviceFound;
    public event EventHandler<string>? DeviceLost => _discovery.DeviceLost;

    public WiFiDirectProvider() {
        _discovery = new WiFiDirectDiscovery();
    }

    public async Task StartAsync(CancellationToken ct = default) {
        await _discovery.StartDiscoveryAsync(ct);
    }

    public async Task StopAsync() {
        await _discovery.StopDiscoveryAsync();
    }

    public async Task<DeviceConnection?> ConnectAsync(DeviceInfo device, CancellationToken ct = default) {
        // Wi-Fi Direct connection establishment
        // Requires device pairing and WFD session setup
        if (device.Type != DeviceType.WiFiDirect) {
            throw new ArgumentException("Invalid device type", nameof(device));
        }

        // Placeholder for connection logic
        return await Task.FromResult<DeviceConnection?>(null);
    }

    public void Dispose() {
        if (!_disposed) {
            _discovery.Dispose();
            _disposed = true;
        }
    }
}

public class DeviceConnection {
    public required DeviceInfo Device { get; init; }
    public required string SessionId { get; init; }
    public DateTime ConnectedAt { get; init; }
}