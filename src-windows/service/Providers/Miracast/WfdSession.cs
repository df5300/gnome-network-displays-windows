namespace Gnd.Windows.Service.Providers.Miracast;

public class WfdSession
{
    public WfdSessionState State { get; private set; }

    public event EventHandler<byte[]>? RtpPacketReceived;

    private readonly RtspClient _rtspClient;
    private WfdDevice? _device;
    private RtspCapabilities? _capabilities;
    private TransportConfig? _transportConfig;

    public WfdSession()
    {
        _rtspClient = new RtspClient();
        State = WfdSessionState.Idle;
    }

    public async Task<RtspCapabilities> NegotiateCapabilitiesAsync(WfdDevice device)
    {
        _device = device;
        State = WfdSessionState.Negotiating;

        try
        {
            await _rtspClient.ConnectAsync(device.IpAddress, device.Port);
            var sessionUrl = $"rtsp://{device.IpAddress}:{device.Port}/wfd1.0/stream";
            var sdp = await _rtspClient.DescribeAsync(sessionUrl);

            _capabilities = ParseSdpCapabilities(sdp);
            State = WfdSessionState.Negotiating;
            return _capabilities;
        }
        catch (Exception)
        {
            State = WfdSessionState.Error;
            throw;
        }
    }

    public async Task SetupTransportAsync(TransportConfig config)
    {
        if (_device == null || _capabilities == null)
            throw new InvalidOperationException("Must negotiate capabilities first");

        _transportConfig = config;
        State = WfdSessionState.Connecting;

        try
        {
            var sessionUrl = $"rtsp://{_device.IpAddress}:{_device.Port}/wfd1.0/stream";
            var transport = $"RTP/AVP/UDP;unicast;client_port={config.VideoPort}-{config.AudioPort}";
            await _rtspClient.SetupAsync(sessionUrl, $"streamid=0/");
            State = WfdSessionState.Connecting;
        }
        catch (Exception)
        {
            State = WfdSessionState.Error;
            throw;
        }
    }

    public async Task StartPlaybackAsync()
    {
        if (_device == null)
            throw new InvalidOperationException("Must setup transport first");

        State = WfdSessionState.Streaming;

        try
        {
            var sessionUrl = $"rtsp://{_device.IpAddress}:{_device.Port}/wfd1.0/stream";
            await _rtspClient.PlayAsync(sessionUrl);
        }
        catch (Exception)
        {
            State = WfdSessionState.Error;
            throw;
        }
    }

    public async Task TeardownAsync()
    {
        if (_device == null)
            return;

        try
        {
            var sessionUrl = $"rtsp://{_device.IpAddress}:{_device.Port}/wfd1.0/stream";
            await _rtspClient.TeardownAsync(sessionUrl);
        }
        finally
        {
            State = WfdSessionState.Teardown;
            _rtspClient.Dispose();
        }
    }

    public void OnRtpPacketReceived(byte[] data)
    {
        RtpPacketReceived?.Invoke(this, data);
    }

    private RtspCapabilities ParseSdpCapabilities(string sdp)
    {
        var capabilities = new RtspCapabilities();

        var lines = sdp.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("m=video"))
            {
                capabilities.VideoCodecs = ParseVideoCodecs(line);
            }
            else if (line.StartsWith("m=audio"))
            {
                capabilities.AudioCodecs = ParseAudioCodecs(line);
            }
            else if (line.Contains("a=rtpmap"))
            {
                ParseRtpmap(line, capabilities);
            }
            else if (line.Contains("a=fmtp"))
            {
                ParseFmtp(line, capabilities);
            }
        }

        if (capabilities.SupportedResolution == null)
        {
            capabilities.SupportedResolution = "1920x1080";
        }

        return capabilities;
    }

    private List<VideoCodec> ParseVideoCodecs(string mLine)
    {
        var codecs = new List<VideoCodec>();
        var parts = mLine.Split(' ');
        if (parts.Length >= 4)
        {
            for (int i = 3; i < parts.Length; i++)
            {
                var codec = parts[i].Split('/')[0];
                if (codec == "96")
                    codecs.Add(VideoCodec.H264);
                else if (codec == "97")
                    codecs.Add(VideoCodec.H265);
            }
        }
        return codecs;
    }

    private List<AudioCodec> ParseAudioCodecs(string mLine)
    {
        var codecs = new List<AudioCodec>();
        var parts = mLine.Split(' ');
        if (parts.Length >= 4)
        {
            for (int i = 3; i < parts.Length; i++)
            {
                var codec = parts[i].Split('/')[0];
                if (codec == "98")
                    codecs.Add(AudioCodec.AAC);
                else if (codec == "0")
                    codecs.Add(AudioCodec.LPCM);
            }
        }
        return codecs;
    }

    private void ParseRtpmap(string line, RtspCapabilities capabilities)
    {
        // Parse RTP map for codec details
    }

    private void ParseFmtp(string line, RtspCapabilities capabilities)
    {
        // Parse format parameters for resolution, profile, etc.
    }
}

public enum WfdSessionState
{
    Idle,
    Discovering,
    Negotiating,
    Connecting,
    Streaming,
    Teardown,
    Error
}

public class RtspCapabilities
{
    public List<VideoCodec> VideoCodecs { get; set; } = new();
    public List<AudioCodec> AudioCodecs { get; set; } = new();
    public string SupportedResolution { get; set; } = "1920x1080";
}

public class TransportConfig
{
    public string LocalIp { get; set; } = string.Empty;
    public int VideoPort { get; set; }
    public int AudioPort { get; set; }
    public TransportType Type { get; set; }
}

public enum VideoCodec
{
    H264,
    H265
}

public enum AudioCodec
{
    AAC,
    LPCM
}

public enum TransportType
{
    UDP,
    TCP
}
