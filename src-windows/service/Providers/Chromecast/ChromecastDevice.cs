namespace Gnd.Windows.Service.Providers.Chromecast;

public class ChromecastDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 8009;
    public List<string> SupportedApps { get; set; } = new();
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;

    public ChromecastDevice()
    {
    }

    public ChromecastDevice(string id, string name, string ipAddress)
    {
        Id = id;
        Name = name;
        IpAddress = ipAddress;
    }
}

public class MediaStatus
{
    public string SessionId { get; set; } = string.Empty;
    public string MediaUrl { get; set; } = string.Empty;
    public MediaPlayerState PlayerState { get; set; } = MediaPlayerState.Idle;
    public double CurrentTime { get; set; }
    public double Duration { get; set; }
    public int Volume { get; set; } = 100;
    public bool IsMuted { get; set; }
}

public enum MediaPlayerState
{
    Idle,
    Loading,
    Playing,
    Paused,
    Buffering
}
