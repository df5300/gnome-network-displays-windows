namespace Gnd.Windows.Service;

public class ServiceConfiguration
{
    public GndServiceSettings GndService { get; set; } = new();
    public MiracastSettings Miracast { get; set; } = new();
    public ChromecastSettings Chromecast { get; set; } = new();
    public FirewallSettings Firewall { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
}

public class GndServiceSettings
{
    public int GrpcPort { get; set; } = 5050;
    public string NamedPipeName { get; set; } = "gnome-network-displays-ipc";
    public int DiscoveryIntervalMs { get; set; } = 5000;
    public bool AutoReconnect { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
}

public class MiracastSettings
{
    public string DefaultVideoCodec { get; set; } = "H264";
    public string DefaultAudioCodec { get; set; } = "AAC";
    public string DefaultResolution { get; set; } = "1920x1080";
    public int DefaultFramerate { get; set; } = 30;
    public UdpPortRangeSettings UdpPortRange { get; set; } = new();
}

public class UdpPortRangeSettings
{
    public int Min { get; set; } = 5000;
    public int Max { get; set; } = 6000;
}

public class ChromecastSettings
{
    public string DefaultMediaCodec { get; set; } = "H264";
    public string DefaultResolution { get; set; } = "1920x1080";
    public string TransportProtocol { get; set; } = "HTTPS";
}

public class FirewallSettings
{
    public List<FirewallRule> RequiredRules { get; set; } = new();
}

public class FirewallRule
{
    public string Name { get; set; } = "";
    public int Port { get; set; }
    public string Protocol { get; set; } = "";
    public string Direction { get; set; } = "";
}

public class LoggingSettings
{
    public string Level { get; set; } = "Information";
    public string FilePath { get; set; } = "logs/gnd-service.log";
    public int MaxFileSizeMb { get; set; } = 10;
    public int MaxRetainedFiles { get; set; } = 5;
}