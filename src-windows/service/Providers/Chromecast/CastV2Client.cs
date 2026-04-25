using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Gnd.Windows.Service.Providers.Chromecast;

/// <summary>
/// Google Cast v2 Protocol client for Chromecast communication
/// </summary>
public class CastV2Client : IDisposable
{
    private ClientWebSocket? _connection;
    private string _deviceIp = string.Empty;
    private int _port;
    private string _transportId = string.Empty;
    private string _sessionId = string.Empty;
    private bool _disposed;
    private CancellationTokenSource? _receiveCts;

    // Cast channel IDs
    public const string SenderId = "sender-0";
    public const string ReceiverId = "receiver-0";

    public event EventHandler<CastMessage>? MessageReceived;
    public event EventHandler<string>? Error;
    public event EventHandler? Disconnected;

    public bool IsConnected => _connection?.State == WebSocketState.Open;
    public string DeviceIp => _deviceIp;
    public string TransportId => _transportId;

    /// <summary>
    /// Connect to a Chromecast device
    /// </summary>
    public async Task ConnectAsync(string deviceIp, int port = 8009)
    {
        if (IsConnected)
            return;

        _deviceIp = deviceIp;
        _port = port;

        System.Diagnostics.Debug.WriteLine($"Connecting to Chromecast at {deviceIp}:{port}");

        _connection = new ClientWebSocket();
        _connection.Options.AddSubProtocol("google.cast.protocol");
        _connection.Options.RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true; // Allow self-signed certs

        var uri = new Uri($"wss://{deviceIp}:{port}/connection");

        try
        {
            await _connection.ConnectAsync(uri, CancellationToken.None);
            System.Diagnostics.Debug.WriteLine("WebSocket connected");

            // Perform CASTV2 handshake
            await PerformConnectHandshakeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to connect: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Perform CASTV2 protocol handshake
    /// </summary>
    private async Task PerformConnectHandshakeAsync()
    {
        System.Diagnostics.Debug.WriteLine("Performing CASTV2 handshake");

        // Step 1: Send auth challenge response (for newer devices)
        var authMessage = new CastMessage
        {
            SourceId = SenderId,
            DestinationId = ReceiverId,
            Namespace = CastNamespaces.DeviceAuth,
            Type = CastMessageType.kBindTransportClient,
            Payload = "{}"
        };
        await SendAsync(authMessage);

        // Small delay for device response
        await Task.Delay(100);

        // Step 2: Connect to receiver
        var connectMessage = new CastMessage
        {
            SourceId = SenderId,
            DestinationId = ReceiverId,
            Namespace = CastNamespaces.Connection,
            Type = CastMessageType.kConnected,
            Payload = "{}"
        };
        await SendAsync(connectMessage);

        System.Diagnostics.Debug.WriteLine("CASTV2 handshake completed");

        // Start receive loop
        _receiveCts = new CancellationTokenSource();
        _ = ReceiveLoopAsync(_receiveCts.Token);
    }

    /// <summary>
    /// Receive loop for incoming messages
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];

        try
        {
            while (!cancellationToken.IsCancellationRequested && _connection?.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _connection.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        System.Diagnostics.Debug.WriteLine("WebSocket closed by device");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                    {
                        var messageData = new byte[result.Count];
                        Buffer.BlockCopy(buffer, 0, messageData, 0, result.Count);

                        try
                        {
                            var message = CastMessage.FromProto(messageData);
                            System.Diagnostics.Debug.WriteLine($"Received CastMessage: {message.Namespace}/{message.Type}");
                            MessageReceived?.Invoke(this, message);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to parse CastMessage: {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebSocket error: {ex.Message}");
                    break;
                }
            }
        }
        finally
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Send a CastMessage to the device
    /// </summary>
    public async Task<CastMessage> SendAsync(CastMessage message)
    {
        if (_connection?.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected to Chromecast");

        var data = message.ToProto();
        await _connection.SendAsync(
            new ArraySegment<byte>(data),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None);

        System.Diagnostics.Debug.WriteLine($"Sent CastMessage: {message.Namespace}/{message.Type}");

        return message;
    }

    /// <summary>
    /// Launch an app on the Chromecast
    /// </summary>
    public async Task<string> LaunchAppAsync(string appId)
    {
        var launchRequest = new CastMessage
        {
            SourceId = SenderId,
            DestinationId = ReceiverId,
            Namespace = CastNamespaces.Receiver,
            Type = CastMessageType.kLaunch,
            Payload = JsonPayload.Create(new { appId })
        };

        await SendAsync(launchRequest);

        // Wait for response with transport ID
        // In a full implementation, this would wait for kLaunchResponse
        await Task.Delay(500);

        return _transportId;
    }

    /// <summary>
    /// Connect to a specific transport (session)
    /// </summary>
    public async Task ConnectToTransportAsync(string transportId)
    {
        _transportId = transportId;

        var connectRequest = new CastMessage
        {
            SourceId = SenderId,
            DestinationId = transportId,
            Namespace = CastNamespaces.Connection,
            Type = CastMessageType.kConnected,
            Payload = "{}"
        };

        await SendAsync(connectRequest);
    }

    /// <summary>
    /// Load media for playback
    /// </summary>
    public async Task LoadMediaAsync(string mediaUrl, string mimeType, bool autoPlay = true, int? duration = null)
    {
        if (string.IsNullOrEmpty(_transportId))
            throw new InvalidOperationException("Not connected to a transport");

        var mediaRequest = new CastMessage
        {
            SourceId = SenderId,
            DestinationId = _transportId,
            Namespace = CastNamespaces.Media,
            Type = CastMessageType.kLoad,
            Payload = JsonPayload.Create(new
            {
                autoplay = autoPlay,
                currentTime = 0,
                activeTrackIds = Array.Empty<int>(),
                media = new
                {
                    contentId = mediaUrl,
                    streamType = "LIVE", // or "BUFFERED"
                    contentType = mimeType,
                    metadata = new { }
                }
            })
        };

        await SendAsync(mediaRequest);
    }

    /// <summary>
    /// Send play command
    /// </summary>
    public async Task PlayAsync()
    {
        await SendMediaCommandAsync("PLAY");
    }

    /// <summary>
    /// Send pause command
    /// </summary>
    public async Task PauseAsync()
    {
        await SendMediaCommandAsync("PAUSE");
    }

    /// <summary>
    /// Send stop command
    /// </summary>
    public async Task StopAsync()
    {
        await SendMediaCommandAsync("STOP");
    }

    /// <summary>
    /// Seek to position
    /// </summary>
    public async Task SeekAsync(double positionSeconds)
    {
        var seekRequest = new CastMessage
        {
            SourceId = SenderId,
            DestinationId = _transportId,
            Namespace = CastNamespaces.Media,
            Type = CastMessageType.kSeek,
            Payload = JsonPayload.Create(new
            {
                currentTime = positionSeconds,
                resumeState = "PLAYBACK_START"
            })
        };

        await SendAsync(seekRequest);
    }

    /// <summary>
    /// Set volume
    /// </summary>
    public async Task SetVolumeAsync(double volume)
    {
        var volumeRequest = new CastMessage
        {
            SourceId = SenderId,
            DestinationId = ReceiverId,
            Namespace = CastNamespaces.Media,
            Type = CastMessageType.kVolume,
            Payload = JsonPayload.Create(new
            {
                volume = Math.Clamp(volume, 0, 1)
            })
        };

        await SendAsync(volumeRequest);
    }

    private async Task SendMediaCommandAsync(string command)
    {
        if (string.IsNullOrEmpty(_transportId))
            throw new InvalidOperationException("Not connected to a transport");

        var request = new CastMessage
        {
            SourceId = SenderId,
            DestinationId = _transportId,
            Namespace = CastNamespaces.Media,
            Type = CastMessageType.kMediaCommand,
            Payload = JsonPayload.Create(new { type = command })
        };

        await SendAsync(request);
    }

    /// <summary>
    /// Get device status
    /// </summary>
    public async Task<string> GetStatusAsync()
    {
        var statusRequest = new CastMessage
        {
            SourceId = SenderId,
            DestinationId = ReceiverId,
            Namespace = CastNamespaces.Receiver,
            Type = CastMessageType.kGetStatus,
            Payload = "{}"
        };

        await SendAsync(statusRequest);
        await Task.Delay(200); // Wait for response

        return "{}"; // Placeholder - would return parsed status
    }

    /// <summary>
    /// Disconnect from the Chromecast
    /// </summary>
    public async Task DisconnectAsync()
    {
        _receiveCts?.Cancel();

        if (_connection?.State == WebSocketState.Open)
        {
            try
            {
                await _connection.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing WebSocket: {ex.Message}");
            }
        }

        Cleanup();
    }

    private void Cleanup()
    {
        _receiveCts?.Dispose();
        _receiveCts = null;
        _connection?.Dispose();
        _connection = null;
        _transportId = string.Empty;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// JSON payload helper
/// </summary>
public static class JsonPayload
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Create(object data) =>
        JsonSerializer.Serialize(data, Options);

    public static T? Parse<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, Options);
}
