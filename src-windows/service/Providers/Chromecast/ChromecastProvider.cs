using System.Text.Json;
using Gnd.Windows.Shared;

namespace Gnd.Windows.Service.Providers.Chromecast;

public class ChromecastProvider : IDisposable
{
    private readonly CastV2Client _client;
    private readonly ChromecastDiscovery _discovery;
    private ChromecastDevice? _connectedDevice;
    private string? _currentSessionId;
    private bool _disposed;

    public event EventHandler<CastMessage>? MessageReceived;

    public ChromecastProvider()
    {
        _client = new CastV2Client();
        _discovery = new ChromecastDiscovery();

        _client.MessageReceived += (s, msg) => MessageReceived?.Invoke(s, msg);
    }

    public async Task<ChromecastDevice> ConnectAsync(string deviceId)
    {
        var device = _discovery.GetDevices().FirstOrDefault(d => d.Id == deviceId)
            ?? throw new ArgumentException($"Device {deviceId} not found");

        await _client.ConnectAsync(device.IpAddress, device.Port);

        // Launch the default media receiver
        await _client.LaunchAppAsync("CC1AD845"); // Default Media Receiver

        _connectedDevice = device;
        return device;
    }

    public async Task LaunchMediaAsync(string sessionId, string mediaUrl)
    {
        if (_connectedDevice == null)
            throw new InvalidOperationException("Not connected to a device");

        _currentSessionId = sessionId;

        var loadRequest = new CastMessage
        {
            SourceId = "sender-0",
            DestinationId = sessionId,
            Namespace = CastNamespaces.Media,
            Type = CastMessageType.kLaunch,
            Payload = JsonPayload.Create(new
            {
                media = new
                {
                    contentId = mediaUrl,
                    streamType = "BUFFERED",
                    contentType = "video/mp4"
                },
                autoplay = true,
                currentTime = 0
            })
        };

        await _client.SendAsync(loadRequest);
    }

    public async Task StopMediaAsync(string sessionId)
    {
        var stopRequest = new CastMessage
        {
            SourceId = "sender-0",
            DestinationId = sessionId,
            Namespace = CastNamespaces.Media,
            Type = CastMessageType.kStopped,
            Payload = "{}"
        };

        await _client.SendAsync(stopRequest);
    }

    public async Task<MediaStatus> GetMediaStatusAsync(string sessionId)
    {
        var statusRequest = new CastMessage
        {
            SourceId = "sender-0",
            DestinationId = sessionId,
            Namespace = CastNamespaces.Media,
            Type = CastMessageType.kGetStatus,
            Payload = "{}"
        };

        await _client.SendAsync(statusRequest);

        // Note: In a real implementation, we would wait for the response
        // For now, return a placeholder status
        return new MediaStatus
        {
            SessionId = sessionId,
            PlayerState = MediaPlayerState.Idle
        };
    }

    public IEnumerable<DeviceInfo> GetAvailableDevices()
    {
        return _discovery.GetDevices().Select(d => new DeviceInfo
        {
            Id = d.Id,
            Name = d.Name,
            IpAddress = d.IpAddress,
            Type = DeviceType.Chromecast,
            State = _connectedDevice?.Id == d.Id ? DeviceState.Connected : DeviceState.Available
        });
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
            _client.Dispose();
        }

        _disposed = true;
    }
}
