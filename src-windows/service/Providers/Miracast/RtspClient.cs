using System.Net.Sockets;
using System.Text;

namespace Gnd.Windows.Service.Providers.Miracast;

public class RtspClient : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private string _sessionId = string.Empty;
    private int _cseq = 0;
    private bool _disposed = false;

    public async Task ConnectAsync(string host, int port)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(host, port);
        _stream = _client.GetStream();
        _cseq = 0;
    }

    public async Task<string> DescribeAsync(string url)
    {
        var request = BuildRequest("DESCRIBE", url);
        request.Headers["Accept"] = "application/sdp";

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"DESCRIBE failed: {response.StatusCode}");

        return response.Body;
    }

    public async Task<string> SetupAsync(string url, string track)
    {
        var request = BuildRequest("SETUP", url);
        request.Headers["Transport"] = "RTP/AVP/UDP;unicast;client_port=15555-15556";

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"SETUP failed: {response.StatusCode}");

        if (response.Headers.TryGetValue("Session", out var session))
        {
            _sessionId = session.Split(';')[0];
        }

        return response.Body;
    }

    public async Task<string> PlayAsync(string url)
    {
        var request = BuildRequest("PLAY", url);
        request.Headers["Range"] = "npt=0-";

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        if (response.StatusCode != 200)
            throw new RtspException($"PLAY failed: {response.StatusCode}");

        return response.Body;
    }

    public async Task<string> TeardownAsync(string url)
    {
        var request = BuildRequest("TEARDOWN", url);
        if (!string.IsNullOrEmpty(_sessionId))
        {
            request.Headers["Session"] = _sessionId;
        }

        await SendRequestAsync(request);
        var response = await ReceiveResponseAsync();

        return response.Body;
    }

    public async Task SendInterleavedDataAsync(byte[] data)
    {
        if (_stream == null)
            throw new InvalidOperationException("Not connected");

        // RTSP over TCP interleaving: 0x24 ( '$' ) + channel + length
        var header = new byte[4];
        header[0] = 0x24; // Magic byte for interleaving
        header[1] = 0x00; // Channel 0 (video)
        header[2] = (byte)((data.Length >> 8) & 0xFF);
        header[3] = (byte)(data.Length & 0xFF);

        var packet = new byte[header.Length + data.Length];
        Buffer.BlockCopy(header, 0, packet, 0, header.Length);
        Buffer.BlockCopy(data, 0, packet, header.Length, data.Length);

        await _stream.WriteAsync(packet);
        await _stream.FlushAsync();
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
        await _stream.WriteAsync(requestBytes);
        await _stream.FlushAsync();
    }

    private async Task<RtspResponse> ReceiveResponseAsync()
    {
        if (_stream == null)
            throw new InvalidOperationException("Not connected");

        var buffer = new byte[8192];
        var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
        var responseText = Encoding.ASCII.GetString(buffer, 0, bytesRead);

        return RtspResponse.Parse(responseText);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _stream?.Dispose();
            _client?.Dispose();
        }

        _disposed = true;
    }
}

public class RtspRequest
{
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Version { get; set; } = "RTSP/1.0";
    public Dictionary<string, string> Headers { get; set; } = new();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Method} {Url} {Version}");

        foreach (var header in Headers)
        {
            sb.AppendLine($"{header.Key}: {header.Value}");
        }

        sb.AppendLine();
        return sb.ToString();
    }
}

public class RtspResponse
{
    public int StatusCode { get; set; }
    public string StatusPhrase { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = string.Empty;

    public static RtspResponse Parse(string text)
    {
        var response = new RtspResponse();
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // Parse status line
        var statusLine = lines[0].Split(' ', 3);
        if (statusLine.Length >= 3)
        {
            response.StatusCode = int.Parse(statusLine[1]);
            response.StatusPhrase = statusLine[2];
        }

        // Parse headers and body
        var bodyStart = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                bodyStart = i + 1;
                break;
            }

            var colonIndex = lines[i].IndexOf(':');
            if (colonIndex > 0)
            {
                var name = lines[i].Substring(0, colonIndex).Trim();
                var value = lines[i].Substring(colonIndex + 1).Trim();
                response.Headers[name] = value;
            }
        }

        // Collect body
        if (bodyStart < lines.Length)
        {
            response.Body = string.Join("\n", lines.Skip(bodyStart));
        }

        return response;
    }
}

public class RtspException : Exception
{
    public RtspException(string message) : base(message)
    {
    }
}
