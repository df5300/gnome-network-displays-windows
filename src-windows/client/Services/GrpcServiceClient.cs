using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Core;
using Gnd.Windows.Shared;
using Gnd.Windows.Grpc;
using SharedDeviceInfo = Gnd.Windows.Shared.DeviceInfo;
using SharedDeviceType = Gnd.Windows.Shared.DeviceType;
using SharedDeviceState = Gnd.Windows.Shared.DeviceState;

namespace Gnd.Windows.Client.Services;

public class GrpcServiceClient : IDisposable
{
    private readonly GndService.GndServiceClient _client;
    private readonly GrpcChannel _channel;
    private readonly ILogger<GrpcServiceClient> _logger;
    private bool _disposed;

    public event EventHandler<SharedDeviceInfo>? DeviceDiscovered;
    public event EventHandler<string>? DeviceLost;

    public GrpcServiceClient(string serverAddress = "http://localhost:5050")
    {
        _logger = LoggerFactory.CreateLogger<GrpcServiceClient>();

        // Configure gRPC channel
        var handler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
        };

        _channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
        {
            HttpHandler = handler,
            Credentials = ChannelCredentials.Insecure
        });

        _client = new GndService.GndServiceClient(_channel);
    }

    public async Task<List<SharedDeviceInfo>> GetDevicesAsync()
    {
        try
        {
            var request = new GetDevicesRequest();
            var response = await _client.GetDevicesAsync(request);

            var devices = new List<SharedDeviceInfo>();
            foreach (var device in response.Devices)
            {
                devices.Add(ConvertDevice(device));
            }

            _logger.LogInformation("Got {Count} devices", devices.Count);
            return devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get devices");
            return new List<SharedDeviceInfo>();
        }
    }

    public async Task ConnectAsync(string deviceId)
    {
        try
        {
            var request = new ConnectRequest { DeviceId = deviceId };
            var response = await _client.ConnectAsync(request);

            if (!response.Success)
            {
                throw new Exception(response.ErrorMessage);
            }

            _logger.LogInformation("Connected to device {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task DisconnectAsync(string deviceId)
    {
        try
        {
            var request = new DisconnectRequest { DeviceId = deviceId };
            var response = await _client.DisconnectAsync(request);

            if (!response.Success)
            {
                throw new Exception(response.ErrorMessage);
            }

            _logger.LogInformation("Disconnected from device {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task StartDiscoveryAsync()
    {
        try
        {
            var request = new StartDiscoveryRequest { ForceRefresh = true };
            var response = await _client.StartDiscoveryAsync(request);

            if (!response.Success)
            {
                throw new Exception(response.ErrorMessage);
            }

            _logger.LogInformation("Discovery started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start discovery");
            throw;
        }
    }

    public async Task StopDiscoveryAsync()
    {
        try
        {
            var request = new StopDiscoveryRequest();
            var response = await _client.StopDiscoveryAsync(request);

            if (!response.Success)
            {
                throw new Exception("Failed to stop discovery");
            }

            _logger.LogInformation("Discovery stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop discovery");
            throw;
        }
    }

    public async Task StartStreamAsync(string deviceId, VideoCodec videoCodec = VideoCodec.H264, AudioCodec audioCodec = AudioCodec.Aac)
    {
        try
        {
            var request = new StartStreamRequest
            {
                DeviceId = deviceId,
                PreferredVideoCodec = videoCodec,
                PreferredAudioCodec = audioCodec,
                Transport = new TransportConfig
                {
                    Type = TransportConfig.Types.TransportType.Tcp,
                    LocalIp = "0.0.0.0",
                    VideoPort = 0,
                    AudioPort = 0
                }
            };

            var response = await _client.StartStreamAsync(request);

            if (!response.Success)
            {
                throw new Exception(response.ErrorMessage);
            }

            _logger.LogInformation("Stream started for device {DeviceId}, URL: {Url}", deviceId, response.StreamUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start stream for device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task StopStreamAsync(string deviceId)
    {
        try
        {
            var request = new StopStreamRequest { DeviceId = deviceId };
            var response = await _client.StopStreamAsync(request);

            if (!response.Success)
            {
                throw new Exception(response.ErrorMessage);
            }

            _logger.LogInformation("Stream stopped for device {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop stream for device {DeviceId}", deviceId);
            throw;
        }
    }

    /// <summary>
    /// Subscribe to device events (device found, lost, state changes)
    /// </summary>
    public async Task SubscribeToEventsAsync()
    {
        try
        {
            var request = new StreamEventsRequest
            {
                Events = { StreamEventsRequest.Types.DeviceEventType.EventTypeDeviceFound,
                          StreamEventsRequest.Types.DeviceEventType.EventTypeDeviceLost,
                          StreamEventsRequest.Types.DeviceEventType.EventTypeStateChanged }
            };

            using var call = _client.StreamEvents(request);

            try
            {
                await foreach (var deviceEvent in call.ResponseStream.ReadAllAsync())
                {
                    switch (deviceEvent.Type)
                    {
                        case DeviceEvent.Types.DeviceEventType.EventTypeDeviceFound:
                            DeviceDiscovered?.Invoke(this, ConvertDevice(deviceEvent.Device));
                            break;
                        case DeviceEvent.Types.DeviceEventType.EventTypeDeviceLost:
                            DeviceLost?.Invoke(this, deviceEvent.DeviceId);
                            break;
                    }
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                _logger.LogInformation("Event subscription cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to events");
        }
    }

    private static SharedDeviceInfo ConvertDevice(Gnd.Windows.Grpc.DeviceInfo grpcDevice)
    {
        return new SharedDeviceInfo
        {
            Id = grpcDevice.Id,
            Name = grpcDevice.Name,
            IpAddress = grpcDevice.IpAddress,
            Port = grpcDevice.Port,
            Type = grpcDevice.Type switch
            {
                Gnd.Windows.Grpc.DeviceType.Miracast => SharedDeviceType.Miracast,
                Gnd.Windows.Grpc.DeviceType.Chromecast => SharedDeviceType.Chromecast,
                _ => SharedDeviceType.Miracast
            },
            State = grpcDevice.State switch
            {
                Gnd.Windows.Grpc.DeviceState.Available => SharedDeviceState.Available,
                Gnd.Windows.Grpc.DeviceState.Connecting => SharedDeviceState.Connecting,
                Gnd.Windows.Grpc.DeviceState.Connected => SharedDeviceState.Connected,
                Gnd.Windows.Grpc.DeviceState.Streaming => SharedDeviceState.Streaming,
                Gnd.Windows.Grpc.DeviceState.Error => SharedDeviceState.Error,
                _ => SharedDeviceState.Available
            }
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _channel.ShutdownAsync().Wait();
            _disposed = true;
        }
    }
}

// Logger interface for cross-platform compatibility
public interface ILogger<T>
{
    void LogInformation(string message, params object[] args);
    void LogError(Exception ex, string message, params object[] args);
    void LogWarning(string message, params object[] args);
}

public class LoggerFactory
{
    public static ILogger<T> CreateLogger<T>() => new ConsoleLogger<T>();
}

public class ConsoleLogger<T> : ILogger<T>
{
    public void LogInformation(string message, params object[] args) =>
        System.Console.WriteLine($"[INFO] {string.Format(message, args)}");
    public void LogError(Exception ex, string message, params object[] args) =>
        System.Console.WriteLine($"[ERROR] {string.Format(message, args)}: {ex.Message}");
    public void LogWarning(string message, params object[] args) =>
        System.Console.WriteLine($"[WARN] {string.Format(message, args)}");
}