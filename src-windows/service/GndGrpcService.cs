using Grpc.Core;
using Gnd.Windows.Shared;
using Gnd.Windows.Shared.Discovery;
using Microsoft.Extensions.Logging;
using GndGrpc = Gnd.Windows.Grpc;
using ILoggerInterface = Microsoft.Extensions.Logging.ILogger;

namespace Gnd.Windows.Service;

/// <summary>
/// gRPC service implementation for GND
/// </summary>
public class GndGrpcService : GndGrpc.GndService.GndServiceBase
{
    private readonly ILoggerInterface<GndGrpcService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public GndGrpcService(ILoggerInterface<GndGrpcService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public override async Task<StartDiscoveryResponse> StartDiscovery(StartDiscoveryRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StartDiscovery requested, force_refresh={ForceRefresh}", request.ForceRefresh);

        var response = new StartDiscoveryResponse();

        try
        {
            // TODO: Get discovery service from DI and start discovery
            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start discovery");
            response.Success = false;
            response.ErrorMessage = ex.Message;
        }

        return response;
    }

    public override async Task<StopDiscoveryResponse> StopDiscovery(StopDiscoveryRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StopDiscovery requested");

        var response = new StopDiscoveryResponse();

        try
        {
            // TODO: Get discovery service from DI and stop discovery
            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop discovery");
            response.Success = false;
        }

        return response;
    }

    public override async Task<GetDevicesResponse> GetDevices(GetDevicesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("GetDevices requested");

        var response = new GetDevicesResponse();

        try
        {
            // TODO: Get device list from discovery service
            // For now, return empty list
            response.Devices.Add(new DeviceInfo
            {
                Id = "stub-device",
                Name = "Stub Device",
                IpAddress = "192.168.1.100",
                Port = 7236,
                Type = DeviceType.DeviceTypeMiracast,
                State = DeviceState.DeviceStateAvailable
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get devices");
        }

        return response;
    }

    public override async Task<ConnectResponse> Connect(ConnectRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Connect requested for device {DeviceId}", request.DeviceId);

        var response = new ConnectResponse();

        try
        {
            // TODO: Get provider and connect to device
            response.Success = true;
            response.Capabilities = new DeviceCapabilities
            {
                SupportsVideo = true,
                SupportsAudio = true
            };
            response.Capabilities.SupportedVideoCodecs.Add(VideoCodec.VideoCodecH264);
            response.Capabilities.SupportedAudioCodecs.Add(AudioCodec.AudioCodecAac);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to device {DeviceId}", request.DeviceId);
            response.Success = false;
            response.ErrorMessage = ex.Message;
        }

        return response;
    }

    public override async Task<DisconnectResponse> Disconnect(DisconnectRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Disconnect requested for device {DeviceId}", request.DeviceId);

        var response = new DisconnectResponse();

        try
        {
            // TODO: Get provider and disconnect
            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect device {DeviceId}", request.DeviceId);
            response.Success = false;
            response.ErrorMessage = ex.Message;
        }

        return response;
    }

    public override async Task<StartStreamResponse> StartStream(StartStreamRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StartStream requested for device {DeviceId}", request.DeviceId);

        var response = new StartStreamResponse();

        try
        {
            // TODO: Start streaming
            response.Success = true;
            response.StreamUrl = $"pipe://localhost/gnd-stream-{request.DeviceId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start stream for device {DeviceId}", request.DeviceId);
            response.Success = false;
            response.ErrorMessage = ex.Message;
        }

        return response;
    }

    public override async Task<StopStreamResponse> StopStream(StopStreamRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StopStream requested for device {DeviceId}", request.DeviceId);

        var response = new StopStreamResponse();

        try
        {
            // TODO: Stop streaming
            response.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop stream for device {DeviceId}", request.DeviceId);
            response.Success = false;
            response.ErrorMessage = ex.Message;
        }

        return response;
    }

    public override async Task StreamEvents(StreamEventsRequest request, IServerStreamWriter<DeviceEvent> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("StreamEvents started");

        try
        {
            // Send initial device list
            var devicesResponse = await GetDevices(new GetDevicesRequest(), context);
            foreach (var device in devicesResponse.Devices)
            {
                await responseStream.WriteAsync(new DeviceEvent
                {
                    Type = DeviceEvent.EventTypeDeviceFound,
                    DeviceId = device.Id,
                    Device = device
                });
            }

            // Keep connection open until cancelled
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, context.CancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("StreamEvents cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StreamEvents error");
        }
    }
}
