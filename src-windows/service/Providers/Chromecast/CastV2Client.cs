using System.Net.WebSockets;
using System.Text;

namespace Gnd.Windows.Service.Providers.Chromecast;

public class CastV2Client : IDisposable
{
    private ClientWebSocket? _connection;
    private string _deviceIp = string.Empty;
    private int _port;
    private string _transportId = string.Empty;
    private string _sessionId = string.Empty;
    private bool _disposed;
    private CancellationTokenSource? _receiveCts;

    public event EventHandler<CastMessage>? MessageReceived;

    public bool IsConnected => _connection?.State == WebSocketState.Open;

    public async Task ConnectAsync(string deviceIp, int port = 8009)
    {
        _deviceIp = deviceIp;
        _port = port;

        _connection = new ClientWebSocket();
        _connection.Options.AddSubProtocol("google.cast.protocol");

        var uri = new Uri($"wss://{deviceIp}:{port}/connection");
        await _connection.ConnectAsync(uri, CancellationToken.None);

        // Perform CASTV2 handshake
        await PerformConnectHandshakeAsync();
    }

    private async Task PerformConnectHandshakeAsync()
    {
        // Step 1: Send device auth challenge response
        var authRequest = new CastMessage
        {
            SourceId = "sender-0",
            DestinationId = "receiver-0",
            Namespace = CastNamespaces.DeviceAuth,
            Type = CastMessageType.kBindTransportClient,
            Payload = "{}"
        };

        await SendAsync(authRequest);

        // Step 2: Connect to the transport
        var connectMessage = new CastMessage
        {
            SourceId = "sender-0",
            DestinationId = "receiver-0",
            Namespace = CastNamespaces.Connection,
            Type = CastMessageType.kConnected,
            Payload = "{}"
        };

        await SendAsync(connectMessage);

        // Step 3: Start heartbeat
        _receiveCts = new CancellationTokenSource();
        _ = ReceiveLoopAsync(_receiveCts.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested && _connection?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _connection.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await DisconnectAsync();
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var messageData = new byte[result.Count];
                    Buffer.BlockCopy(buffer, 0, messageData, 0, result.Count);

                    var message = CastMessage.FromProto(messageData);
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException)
            {
                break;
            }
        }
    }

    public async Task<CastMessage> SendAsync(CastMessage message)
    {
        if (_connection?.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected to Chromecast");

        var data = message.ToProto();
        await _connection.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);

        return message;
    }

    public async Task LaunchAppAsync(string appId)
    {
        var launchRequest = new CastMessage
        {
            SourceId = "sender-0",
            DestinationId = "receiver-0",
            Namespace = CastNamespaces.Receiver,
            Type = CastMessageType.kLaunch,
            Payload = JsonPayload.Create(new { appId })
        };

        await SendAsync(launchRequest);
    }

    public async Task ConnectAsync(string transportId)
    {
        _transportId = transportId;

        var connectRequest = new CastMessage
        {
            SourceId = "sender-0",
            DestinationId = transportId,
            Namespace = CastNamespaces.Connection,
            Type = CastMessageType.kConnected,
            Payload = "{}"
        };

        await SendAsync(connectRequest);
    }

    public async Task DisconnectAsync()
    {
        if (_connection?.State == WebSocketState.Open)
        {
            try
            {
                await _connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch
            {
                // Ignore close errors
            }
        }

        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _connection?.Dispose();
        _connection = null;
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
            _receiveCts?.Cancel();
            _receiveCts?.Dispose();
            _connection?.Dispose();
        }

        _disposed = true;
    }
}

public static class JsonPayload
{
    public static string Create(object data)
    {
        return System.Text.Json.JsonSerializer.Serialize(data);
    }

    public static T? Parse<T>(string json) where T : class
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }
}
