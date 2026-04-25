using System.Collections.Concurrent;

namespace Gnd.Windows.Shared.Discovery;

public class DeviceDiscoveryAggregator : IDeviceDiscovery, IDisposable {
    private readonly List<IDeviceDiscovery> _sources;
    private readonly ConcurrentDictionary<string, DeviceInfo> _devices = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event EventHandler<DeviceDiscoveredEventArgs>? DeviceFound;
    public event EventHandler<string>? DeviceLost;

    public DeviceDiscoveryAggregator(params IDeviceDiscovery[] sources) {
        _sources = sources.ToList();
        foreach (var source in _sources) {
            source.DeviceFound += OnDeviceFound;
            source.DeviceLost += OnDeviceLost;
        }
    }

    private void OnDeviceFound(object? sender, DeviceDiscoveredEventArgs e) {
        // Deduplicate by ID
        if (_devices.TryAdd(e.Device.Id, e.Device)) {
            DeviceFound?.Invoke(this, e);
        }
    }

    private void OnDeviceLost(object? sender, string deviceId) {
        if (_devices.TryRemove(deviceId, out _)) {
            DeviceLost?.Invoke(this, deviceId);
        }
    }

    public async Task StartDiscoveryAsync(CancellationToken ct) {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = _sources.Select(s => s.StartDiscoveryAsync(_cts.Token));
        await Task.WhenAll(tasks);
    }

    public async Task StopDiscoveryAsync() {
        _cts?.Cancel();
        var tasks = _sources.Select(s => s.StopDiscoveryAsync());
        await Task.WhenAll(tasks);
    }

    public void AddSource(IDeviceDiscovery source) {
        source.DeviceFound += OnDeviceFound;
        source.DeviceLost += OnDeviceLost;
        _sources.Add(source);
    }

    public void RemoveSource(IDeviceDiscovery source) {
        source.DeviceFound -= OnDeviceFound;
        source.DeviceLost -= OnDeviceLost;
        _sources.Remove(source);
    }

    public void Dispose() {
        if (!_disposed) {
            foreach (var source in _sources) {
                source.DeviceFound -= OnDeviceFound;
                source.DeviceLost -= OnDeviceLost;
                if (source is IDisposable disposable) {
                    disposable.Dispose();
                }
            }
            _sources.Clear();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}