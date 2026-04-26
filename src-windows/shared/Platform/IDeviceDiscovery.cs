namespace Gnd.Windows.Shared.Platform;

public interface IDeviceDiscovery
{
    Task StartAsync();
    Task StopAsync();
    event EventHandler<DeviceInfo>? DeviceFound;
    event EventHandler<string>? DeviceLost;
    bool IsRunning { get; }
}

public class DeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
}

public enum DeviceType
{
    Unknown,
    Miracast,
    Chromecast
}