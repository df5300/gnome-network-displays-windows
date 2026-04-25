namespace Gnd.Windows.Service.Providers.Miracast;

public class WfdDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 7236;
    public WfdDeviceCapabilities Capabilities { get; set; } = new();

    public WfdDevice()
    {
    }

    public WfdDevice(string id, string name, string ipAddress, int port = 7236)
    {
        Id = id;
        Name = name;
        IpAddress = ipAddress;
        Port = port;
    }
}

public class WfdDeviceCapabilities
{
    public bool SupportsVideo { get; set; } = true;
    public bool SupportsAudio { get; set; } = true;
    public List<VideoCodec> SupportedVideoCodecs { get; set; } = new() { VideoCodec.H264 };
    public List<Resolution> SupportedResolutions { get; set; } = new()
    {
        new Resolution { Width = 1920, Height = 1080, RefreshRate = 30 },
        new Resolution { Width = 1280, Height = 720, RefreshRate = 60 }
    };
}

public class Resolution
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }

    public override string ToString() => $"{Width}x{Height}@{RefreshRate}";
}
