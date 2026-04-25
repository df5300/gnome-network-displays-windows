using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Gnd.Windows.Service.Providers.Miracast;

/// <summary>
/// RTSP client for Wi-Fi Display (Miracast) protocol
/// Implements WFD (Wi-Fi Display) specific RTSP messages
/// </summary>
public class RtspClient : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private string _sessionId = string.Empty;
    private int _cseq = 0;
    private bool _disposed;
    private int _videoPort;
    private int _audioPort;

    // WFD-specific state
    public string WfdDeviceInfo { get; private set; } = string.Empty;
    public RtspCapabilities? Capabilities { get; private set; }

    public event EventHandler<byte[]>? OnRtpPacketReceived;
    public event EventHandler<string>? OnError;

    /// <summary>
    /// Connect to RTSP server
    /// </summary>
    public async Task ConnectAsync(string host, int port)
    {
        _client = new TcpClient();
        _client.SendTimeout = 5000;
        _client.ReceiveTimeout = 10000;

        await _client.ConnectAsync(host, port);
        _stream = _client.GetStream();
        _cseq = 0;

        System.Diagnostics.Debug.WriteLine($"RTSP connected to {host}:{port}");
    }

    /// <summary>
    /// Send M1 - Device Discovery
    /// </summary>
    public async Task<string> SendM1Async()
    {
        var request = BuildRequest("OPTIONS", "*");
        request.Headers["Require"] = "wfd";

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"M1 (OPTIONS) failed: {response.StatusCode}");

        return response.StatusPhrase;
    }

    /// <summary>
    /// Send M2 - Device Capability Discovery
    /// </summary>
    public async Task<RtspCapabilities> SendM2Async(string wfdTrigger)
    {
        var request = BuildRequest("GET_PARAMETER", "*");
        request.Headers["Content-Type"] = "text/parameters";
        request.Body = wfdTrigger;

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"M2 (GET_PARAMETER) failed: {response.StatusCode}");

        WfdDeviceInfo = response.Body;
        Capabilities = ParseWfdCapabilities(response.Body);

        return Capabilities;
    }

    /// <summary>
    /// Send M3 - RTSP Setup via M3 Message
    /// </summary>
    public async Task SetupTransportAsync(int videoPort, int audioPort)
    {
        _videoPort = videoPort;
        _audioPort = audioPort;

        var request = BuildRequest("SETUP", "rtsp://localhost/streamid=0");
        request.Headers["Transport"] = $"RTP/AVP/UDP;unicast;client_port={videoPort}-{audioPort}";
        request.Headers["Conference"] = "0";

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"SETUP failed: {response.StatusCode}");

        if (response.Headers.TryGetValue("Session", out var session))
        {
            _sessionId = session.Split(';')[0].Trim();
        }
    }

    /// <summary>
    /// Send M4 - Play
    /// </summary>
    public async Task SendM4Async()
    {
        var request = BuildRequest("PLAY", "*");
        if (!string.IsNullOrEmpty(_sessionId))
        {
            request.Headers["Session"] = _sessionId;
        }

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"M4 (PLAY) failed: {response.StatusCode}");
    }

    /// <summary>
    /// Send M5 - TEARDOWN
    /// </summary>
    public async Task SendM5Async()
    {
        var request = BuildRequest("TEARDOWN", "*");
        if (!string.IsNullOrEmpty(_sessionId))
        {
            request.Headers["Session"] = _sessionId;
        }

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        // TEARDOWN might not return 200
        System.Diagnostics.Debug.WriteLine($"TEARDOWN response: {response.StatusCode}");
    }

    /// <summary>
    /// Standard RTSP DESCRIBE
    /// </summary>
    public async Task<SdpDocument> DescribeAsync(string url)
    {
        var request = BuildRequest("DESCRIBE", url);
        request.Headers["Accept"] = "application/sdp";

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"DESCRIBE failed: {response.StatusCode}");

        return SdpDocument.Parse(response.Body);
    }

    /// <summary>
    /// Standard RTSP SETUP for a track
    /// </summary>
    public async Task SetupTrackAsync(string url, int trackIndex, int port)
    {
        var request = BuildRequest("SETUP", $"{url}/track{trackIndex}");
        request.Headers["Transport"] = $"RTP/AVP/UDP;unicast;client_port={port}-{port + 1}";

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"SETUP track{trackIndex} failed: {response.StatusCode}");

        if (response.Headers.TryGetValue("Session", out var session))
        {
            _sessionId = session.Split(';')[0].Trim();
        }
    }

    /// <summary>
    /// Standard RTSP PLAY
    /// </summary>
    public async Task PlayAsync(string url)
    {
        var request = BuildRequest("PLAY", url);
        request.Headers["Range"] = "npt=0-";
        if (!string.IsNullOrEmpty(_sessionId))
        {
            request.Headers["Session"] = _sessionId;
        }

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"PLAY failed: {response.StatusCode}");
    }

    /// <summary>
    /// Standard RTSP PAUSE
    /// </summary>
    public async Task PauseAsync(string url)
    {
        var request = BuildRequest("PAUSE", url);
        if (!string.IsNullOrEmpty(_sessionId))
        {
            request.Headers["Session"] = _sessionId;
        }

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"PAUSE failed: {response.StatusCode}");
    }

    /// <summary>
    /// Send RTSP over TCP interleaved data (RTP packets)
    /// </summary>
    public async Task SendInterleavedDataAsync(byte channel, byte[] data)
    {
        if (_stream == null)
            throw new InvalidOperationException("Not connected");

        // RTSP over TCP interleaving: '$' + channel + length (2 bytes) + data
        var header = new byte[4];
        header[0] = 0x24; // Magic byte for interleaving
        header[1] = channel;
        header[2] = (byte)((data.Length >> 8) & 0xFF);
        header[3] = (byte)(data.Length & 0xFF);

        var packet = new byte[header.Length + data.Length];
        Buffer.BlockCopy(header, 0, packet, 0, header.Length);
        Buffer.BlockCopy(data, 0, packet, header.Length, data.Length);

        await _stream.WriteAsync(packet);
        await _stream.FlushAsync();
    }

    /// <summary>
    /// Receive interleaved data (non-blocking check)
    /// </summary>
    public async Task<byte[]?> ReceiveInterleavedDataAsync()
    {
        if (_stream == null || !_stream.DataAvailable)
            return null;

        var header = new byte[4];
        var bytesRead = await _stream.ReadAsync(header, 0, 4);

        if (bytesRead != 4 || header[0] != 0x24)
            return null;

        int channel = header[1];
        int length = (header[2] << 8) | header[3];

        var data = new byte[length];
        bytesRead = await _stream.ReadAsync(data, 0, length);

        if (bytesRead != length)
            return null;

        return data;
    }

    private RtspRequest BuildRequest(string method, string url)
    {
        _cseq++;
        return new RtspRequest
        {
            Method = method,
            Url = url,
            Version = "RTSP/1.0",
            Headers = new Dictionary<string, string>
            {
                { "CSeq", _cseq.ToString() },
                { "User-Agent", "Gnd.Windows.Service/1.0" }
            }
        };
    }

    private async Task SendRequestAsync(RtspRequest request)
    {
        if (_stream == null)
            throw new InvalidOperationException("Not connected");

        var requestText = request.ToString();
        var requestBytes = Encoding.ASCII.GetBytes(requestText);

        System.Diagnostics.Debug.WriteLine($"RTSP Request:\n{requestText}");

        await _stream.WriteAsync(requestBytes);
        await _stream.FlushAsync();
    }

    private async Task<RtspResponse> ReceiveResponseAsync()
    {
        if (_stream == null)
            throw new InvalidOperationException("Not connected");

        // Read until we have a complete response
        var sb = new StringBuilder();
        var buffer = new byte[4096];

        while (true)
        {
            var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0)
                throw new RtspException("Connection closed by server");

            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

            var text = sb.ToString();
            // Check if we have a complete response (double CRLF marks end of headers)
            if (text.Contains("\r\n\r\n"))
                break;
        }

        var responseText = sb.ToString();
        System.Diagnostics.Debug.WriteLine($"RTSP Response:\n{responseText}");

        return RtspResponse.Parse(responseText);
    }

    private RtspCapabilities ParseWfdCapabilities(string wfdBody)
    {
        var capabilities = new RtspCapabilities();

        // Parse WFD capability lines
        var lines = wfdBody.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("wfd_video_formats:"))
            {
                capabilities.VideoFormats = ParseVideoFormats(line);
            }
            else if (line.StartsWith("wfd_audio_formats:"))
            {
                capabilities.AudioFormats = ParseAudioFormats(line);
            }
            else if (line.Contains("wfd_3d_video_formats"))
            {
                capabilities.Supports3DVideo = true;
            }
            else if (line.Contains("wfd_display_edid_supported"))
            {
                capabilities.SupportsDisplayEdid = true;
            }
            else if (line.Contains("wfd_coupled_sink_supported"))
            {
                capabilities.SupportsCoupledSink = true;
            }
        }

        return capabilities;
    }

    private List<VideoFormat> ParseVideoFormats(string line)
    {
        var formats = new List<VideoFormat>();
        // Parse WFD video format strings
        // Format: "wfd_video_formats: <format-list>"
        return formats;
    }

    private List<AudioFormat> ParseAudioFormats(string line)
    {
        var formats = new List<AudioFormat>();
        // Parse WFD audio format strings
        return formats;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stream?.Dispose();
            _client?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// RTSP request message
/// </summary>
public class RtspRequest
{
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Version { get; set; } = "RTSP/1.0";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = string.Empty;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Method} {Url} {Version}");

        foreach (var header in Headers)
        {
            sb.AppendLine($"{header.Key}: {header.Value}");
        }

        if (!string.IsNullOrEmpty(Body))
        {
            sb.AppendLine($"Content-Length: {Body.Length}");
            sb.AppendLine();
            sb.Append(Body);
        }
        else
        {
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

/// <summary>
/// RTSP response message
/// </summary>
public class RtspResponse
{
    public int StatusCode { get; set; }
    public string StatusPhrase { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = string.Empty;

    public static RtspResponse Parse(string text)
    {
        var response = new RtspResponse();
        var parts = text.Split(new[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
        var headers = parts[0];
        var body = parts.Length > 1 ? parts[1] : string.Empty;

        var lines = headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // Parse status line
        var statusLine = lines[0].Split(' ', 3);
        if (statusLine.Length >= 3)
        {
            response.StatusCode = int.Parse(statusLine[1]);
            response.StatusPhrase = statusLine[2];
        }

        // Parse headers
        for (int i = 1; i < lines.Length; i++)
        {
            var colonIndex = lines[i].IndexOf(':');
            if (colonIndex > 0)
            {
                var name = lines[i].Substring(0, colonIndex).Trim();
                var value = lines[i].Substring(colonIndex + 1).Trim();
                response.Headers[name] = value;
            }
        }

        response.Body = body.Trim();
        return response;
    }
}

/// <summary>
/// RTSP capabilities from WFD device
/// </summary>
public class RtspCapabilities
{
    public List<VideoFormat> VideoFormats { get; set; } = new();
    public List<AudioFormat> AudioFormats { get; set; } = new();
    public bool Supports3DVideo { get; set; }
    public bool SupportsDisplayEdid { get; set; }
    public bool SupportsCoupledSink { get; set; }
    public bool SupportsStandby { get; set; }
    public bool SupportsPresentation { get; set; }
    public bool SupportsI2C { get; set; }
    public bool SupportsHDCP { get; set; }
}

/// <summary>
/// Video format information
/// </summary>
public class VideoFormat
{
    public string Resolution { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Chroma { get; set; } = string.Empty;
    public int MaxFramerate { get; set; }
}

/// <summary>
/// Audio format information
/// </summary>
public class AudioFormat
{
    public string Type { get; set; } = string.Empty;
    public int Channels { get; set; }
    public int SampleRate { get; set; }
    public string BitDepth { get; set; } = string.Empty;
}

/// <summary>
/// SDP document from DESCRIBE response
/// </summary>
public class SdpDocument
{
    public string SessionName { get; set; } = string.Empty;
    public string SessionDescription { get; set; } = string.Empty;
    public List<MediaDescription> Media { get; set; } = new();

    public static SdpDocument Parse(string sdp)
    {
        var doc = new SdpDocument();
        var lines = sdp.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var currentMedia = (MediaDescription?)null;

        foreach (var line in lines)
        {
            if (line.StartsWith("v="))
                continue; // Version
            else if (line.StartsWith("s="))
                doc.SessionName = line.Substring(2);
            else if (line.StartsWith("c="))
                continue; // Connection info
            else if (line.StartsWith("m="))
            {
                currentMedia = new MediaDescription { Line = line };
                doc.Media.Add(currentMedia);
            }
            else if (currentMedia != null)
            {
                if (line.StartsWith("a="))
                    currentMedia.Attributes.Add(line.Substring(2));
            }
        }

        return doc;
    }
}

/// <summary>
/// Media description from SDP
/// </summary>
public class MediaDescription
{
    public string Line { get; set; } = string.Empty;
    public string MediaType => Line.StartsWith("m=") ? Line.Substring(2).Split(' ')[0] : string.Empty;
    public List<string> Attributes { get; set; } = new();
}

public class RtspException : Exception
{
    public RtspException(string message) : base(message) { }
}
