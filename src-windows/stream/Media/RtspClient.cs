using System.Net.Sockets;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace GnomeNetworkDisplays.Stream.Media;

public class RtspClient : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private HttpClient? _httpClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    private string? _sessionId;
    private string? _baseUrl;
    private int _cseq = 1;
    private readonly Dictionary<string, string> _sessionHeaders = new();

    // RTP state
    private int _videoChannel = 0;
    private int _audioChannel = 2;
    private int _videoRtpPort = 0;
    private int _audioRtpPort = 0;
    private bool _useInterleaved = true;

    // Callbacks
    public event Action<byte[]>? OnRtpPacketReceived;
    public event Action<string>? OnStatusChanged;

    public bool IsConnected { get; private set; }

    public async Task ConnectAsync(string url, CancellationToken cancellationToken)
    {
        _baseUrl = url;
        var uri = new Uri(url);

        int port = uri.Port > 0 ? uri.Port : 554;
        string host = uri.Host;

        Console.WriteLine($"Connecting to RTSP server: {host}:{port}");

        // Check if this is an HTTP tunneled RTSP (like via a proxy)
        bool useHttp = uri.Query.Contains("gateway", StringComparison.OrdinalIgnoreCase) ||
                       uri.Scheme == "rtsp-over-http";

        if (useHttp)
        {
            _httpClient = new HttpClient();
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes("anonymous:anonymous"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);
        }
        else
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port, cancellationToken);
            _networkStream = _tcpClient.GetStream();
            _reader = new StreamReader(_networkStream, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(_networkStream, Encoding.UTF8, leaveOpen: true)
            {
                AutoFlush = true
            };
        }

        IsConnected = true;
        OnStatusChanged?.Invoke("Connected");
    }

    public async Task<string> SendDescribeAsync(CancellationToken cancellationToken)
    {
        var response = await SendRtspCommandAsync($"DESCRIBE {_baseUrl} RTSP/1.0\r\n" +
            $"Accept: application/sdp\r\n" +
            $"CSeq: {_cseq++}\r\n" +
            $"\r\n", cancellationToken);

        // Parse SDP content
        if (response.Contains("200 OK"))
        {
            // Extract SDP body after \r\n\r\n
            int bodyStart = response.IndexOf("\r\n\r\n") + 4;
            if (bodyStart > 4 && bodyStart < response.Length)
            {
                string sdp = response.Substring(bodyStart);
                ParseSdp(sdp);
                return sdp;
            }
        }

        throw new InvalidOperationException($"DESCRIBE failed: {response}");
    }

    public async Task SetupVideoAsync(CancellationToken cancellationToken)
    {
        // Determine transport - prefer interleaved (RTP over RTSP)
        string transport = _useInterleaved
            ? $"RTP/AVP/TCP;interleaved={_videoChannel}"
            : $"RTP/AVP;unicast;client_port={_videoRtpPort}-{_audioRtpPort}";

        var response = await SendRtspCommandAsync($"SETUP {_baseUrl}/trackID=0 RTSP/1.0\r\n" +
            $"Transport: {transport}\r\n" +
            $"CSeq: {_cseq++}\r\n" +
            $"User-Agent: GnomeNetworkDisplays\r\n" +
            $"{GetSessionHeader()}" +
            $"\r\n", cancellationToken);

        ParseSessionHeader(response);
    }

    public async Task SetupAudioAsync(CancellationToken cancellationToken)
    {
        string transport = _useInterleaved
            ? $"RTP/AVP/TCP;interleaved={_audioChannel}"
            : $"RTP/AVP;unicast;client_port={_videoRtpPort + 1}-{_audioRtpPort + 1}";

        var response = await SendRtspCommandAsync($"SETUP {_baseUrl}/trackID=1 RTSP/1.0\r\n" +
            $"Transport: {transport}\r\n" +
            $"CSeq: {_cseq++}\r\n" +
            $"User-Agent: GnomeNetworkDisplays\r\n" +
            $"{GetSessionHeader()}" +
            $"\r\n", cancellationToken);

        ParseSessionHeader(response);
    }

    public async Task PlayAsync(CancellationToken cancellationToken)
    {
        var response = await SendRtspCommandAsync($"PLAY {_baseUrl} RTSP/1.0\r\n" +
            $"Range: npt=0.000-\r\n" +
            $"CSeq: {_cseq++}\r\n" +
            $"User-Agent: GnomeNetworkDisplays\r\n" +
            $"{GetSessionHeader()}" +
            $"\r\n", cancellationToken);

        if (response.Contains("200 OK"))
        {
            OnStatusChanged?.Invoke("Playing");
        }
        else
        {
            throw new InvalidOperationException($"PLAY failed: {response}");
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken)
    {
        var response = await SendRtspCommandAsync($"PAUSE {_baseUrl} RTSP/1.0\r\n" +
            $"CSeq: {_cseq++}\r\n" +
            $"User-Agent: GnomeNetworkDisplays\r\n" +
            $"{GetSessionHeader()}" +
            $"\r\n", cancellationToken);

        OnStatusChanged?.Invoke("Paused");
    }

    public async Task TeardownAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SendRtspCommandAsync($"TEARDOWN {_baseUrl} RTSP/1.0\r\n" +
                $"CSeq: {_cseq++}\r\n" +
                $"User-Agent: GnomeNetworkDisplays\r\n" +
                $"{GetSessionHeader()}" +
                $"\r\n", cancellationToken);
        }
        catch
        {
            // Ignore errors during teardown
        }

        OnStatusChanged?.Invoke("Stopped");
    }

    public void Disconnect()
    {
        _sessionId = null;
        _sessionHeaders.Clear();
        IsConnected = false;

        _reader?.Dispose();
        _writer?.Dispose();
        _networkStream?.Dispose();
        _tcpClient?.Dispose();
        _httpClient?.Dispose();

        _reader = null;
        _writer = null;
        _networkStream = null;
        _tcpClient = null;
        _httpClient = null;
    }

    private async Task<string> SendRtspCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (_httpClient != null)
        {
            // HTTP tunneling mode
            var content = new StringContent(command, Encoding.UTF8, "application/x-rtsp-string");
            var response = await _httpClient.PostAsync(_baseUrl!, content, cancellationToken);
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        else if (_writer != null && _reader != null)
        {
            await _writer.WriteAsync(command, cancellationToken);
            await _writer.FlushAsync(cancellationToken);

            var response = await ReadResponseAsync(_reader, cancellationToken);
            return response;
        }
        else
        {
            throw new InvalidOperationException("Not connected");
        }
    }

    private async Task<string> ReadResponseAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            response.AppendLine(line);

            // End of headers
            if (string.IsNullOrEmpty(line))
            {
                // Check for interleaved RTP data
                // This is a simplified check - real implementation would
                // look for $ prefix with channel and length
                break;
            }

            // Check if this is the end of a 200 OK response
            if (line.StartsWith("RTP-Info:") || line.StartsWith("Server:"))
            {
                // Continue reading
            }
        }

        return response.ToString();
    }

    public async Task<int> ReadInterleavedDataAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_networkStream == null)
            return 0;

        // Read while we get RTP data (starts with $)
        int timeout = 1000;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            // Peek to see if there's data
            if (_networkStream.DataAvailable)
            {
                int bytesRead = await _networkStream.ReadAsync(buffer, offset, count, cts.Token);

                // Check for RTP over RTSP interleaved format: $ ch len data...
                if (bytesRead > 4 && buffer[offset] == 0x24) // '$' marker
                {
                    int channel = buffer[offset + 1];
                    int length = (buffer[offset + 2] << 8) | buffer[offset + 3];

                    if (length <= bytesRead - 4)
                    {
                        // Valid RTP packet
                        OnRtpPacketReceived?.Invoke(buffer.Take(4 + length).ToArray());
                        return 4 + length;
                    }
                }

                return bytesRead;
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void ParseSdp(string sdp)
    {
        // Parse basic SDP to extract media info
        // In production, use a proper SDP parser

        Console.WriteLine("Parsing SDP...");
        Console.WriteLine(sdp);

        // Look for media descriptions
        var mediaMatches = Regex.Matches(sdp, "m=(\\w+)\\s+(\\d+)\\s+(\\w+)\\s+(\\d+)");
        foreach (Match match in mediaMatches)
        {
            string mediaType = match.Groups[1].Value;
            Console.WriteLine($"Media: {mediaType}");
        }

        // Look for connection info
        var connMatch = Regex.Match(sdp, "c=IN IP4 (\\S+)");
        if (connMatch.Success)
        {
            Console.WriteLine($"Connection: {connMatch.Groups[1].Value}");
        }
    }

    private void ParseSessionHeader(string response)
    {
        var match = Regex.Match(response, @"Session:\s*(\S+)(?:;timeout=(\d+))?", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            _sessionId = match.Groups[1].Value;
            _sessionHeaders["Session"] = _sessionId;

            if (match.Groups[2].Success)
            {
                _sessionHeaders["Timeout"] = match.Groups[2].Value;
            }

            Console.WriteLine($"Session: {_sessionId}");
        }

        // Parse Transport header for port info
        var transportMatch = Regex.Match(response, @"server_port=(\d+)-(\d+)", RegexOptions.IgnoreCase);
        if (transportMatch.Success)
        {
            _videoRtpPort = int.Parse(transportMatch.Groups[1].Value);
            _audioRtpPort = int.Parse(transportMatch.Groups[2].Value);
            Console.WriteLine($"RTP ports: {_videoRtpPort}-{_audioRtpPort}");
        }
    }

    private string GetSessionHeader()
    {
        if (string.IsNullOrEmpty(_sessionId))
            return "";
        return $"Session: {_sessionId}\r\n";
    }

    public void Dispose()
    {
        Disconnect();
    }
}

// RTP packet structure
public class RtpPacket
{
    public byte Version { get; set; }
    public byte Padding { get; set; }
    public byte Extension { get; set; }
    public byte CsrcCount { get; set; }
    public byte Marker { get; set; }
    public byte PayloadType { get; set; }
    public ushort SequenceNumber { get; set; }
    public uint Timestamp { get; set; }
    public uint Ssrc { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    public static RtpPacket Parse(byte[] data, int offset, int length)
    {
        if (length < 12)
            throw new ArgumentException("Invalid RTP packet - too short");

        var packet = new RtpPacket
        {
            Version = (byte)((data[offset] >> 6) & 0x03),
            Padding = (byte)((data[offset] >> 5) & 0x01),
            Extension = (byte)((data[offset] >> 4) & 0x01),
            CsrcCount = (byte)(data[offset] & 0x0F),
            Marker = (byte)((data[offset + 1] >> 7) & 0x01),
            PayloadType = (byte)(data[offset + 1] & 0x7F),
            SequenceNumber = (ushort)((data[offset + 2] << 8) | data[offset + 3]),
            Timestamp = (uint)((data[offset + 4] << 24) | (data[offset + 5] << 16) |
                              (data[offset + 6] << 8) | data[offset + 7]),
            Ssrc = (uint)((data[offset + 8] << 24) | (data[offset + 9] << 16) |
                         (data[offset + 10] << 8) | data[offset + 11])
        };

        int headerLength = 12 + (packet.CsrcCount * 4);

        // Check for extension
        if (packet.Extension == 1 && length >= headerLength + 4)
        {
            ushort extLength = (ushort)((data[offset + headerLength + 2] << 8) |
                                        data[offset + headerLength + 3]);
            headerLength += 4 + (extLength * 4);
        }

        int payloadLength = length - headerLength;
        if (packet.Padding == 1 && payloadLength > 0)
        {
            int padLength = data[offset + length - 1];
            payloadLength -= padLength;
        }

        packet.Payload = new byte[payloadLength];
        Array.Copy(data, offset + headerLength, packet.Payload, 0, payloadLength);

        return packet;
    }
}
