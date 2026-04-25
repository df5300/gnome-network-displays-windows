namespace Gnd.Windows.Shared;

// IPC Message types for client ↔ service communication
public enum IpcAction {
    Discover,
    Connect,
    Disconnect,
    GetDevices,
    StreamStart,
    StreamStop
}

public class IpcMessage {
    public string RequestId { get; set; } = string.Empty;
    public IpcAction Action { get; set; }
    public string Payload { get; set; } = string.Empty;
}

public class DeviceInfo {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
    public DeviceState State { get; set; }
}

public enum DeviceType { Miracast, Chromecast }
public enum DeviceState { Available, Connecting, Connected, Streaming, Error }
