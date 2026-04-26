namespace Gnd.Windows.Shared.Discovery;

public interface IDeviceDiscovery {
    Task StartDiscoveryAsync(CancellationToken ct);
    Task StopDiscoveryAsync();
    event EventHandler<DeviceDiscoveredEventArgs>? DeviceFound;
    event EventHandler<string>? DeviceLost;
}

public class DeviceDiscoveredEventArgs : EventArgs {
    public required DeviceInfo Device { get; init; }
}

public class DeviceInfo {
    public required string Id { get; init; }
    public required string Name { get; init; }
    public DeviceType Type { get; init; }
    public DeviceState State { get; init; }
    public string? IpAddress { get; init; }
    public int? Port { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}

public enum DeviceType {
    Unknown,
    WiFiDirect,
    Miracast,
    Chromecast
}

public enum DeviceState {
    Available,
    Connecting,
    Connected,
    Streaming,
    Error
}