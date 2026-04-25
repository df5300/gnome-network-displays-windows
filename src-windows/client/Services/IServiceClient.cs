using System;

namespace Gnd.Windows.Client.Services;

public enum DeviceType
{
    Unknown,
    Sink,
    Source
}

public enum DeviceState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public class DeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DeviceType Type { get; set; } = DeviceType.Unknown;
    public DeviceState State { get; set; } = DeviceState.Disconnected;
    public string Address { get; set; } = string.Empty;
    public bool IsConnectable { get; set; }
}

public interface IServiceClient
{
    Task<List<DeviceInfo>> GetDevicesAsync();
    Task ConnectAsync(string deviceId);
    Task DisconnectAsync(string deviceId);
    Task StartDiscoveryAsync();
    Task StopDiscoveryAsync();
    event EventHandler<DeviceInfo>? DeviceDiscovered;
    event EventHandler<string>? DeviceLost;
}