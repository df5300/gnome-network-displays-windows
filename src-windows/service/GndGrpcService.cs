using Grpc.Core;
using Gnd.Windows.Shared;
using Gnd.Windows.Shared.Discovery;
using Microsoft.Extensions.Logging;
using GndGrpc = Gnd.Windows.Grpc;

namespace Gnd.Windows.Service;

/// <summary>
/// gRPC service implementation for GND
/// </summary>
public class GndGrpcService : GndGrpc.GndService.GndServiceBase
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public GndGrpcService(Microsoft.Extensions.Logging.ILogger logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public override async Task<GndGrpc.StartDiscoveryResponse> StartDiscovery(GndGrpc.StartDiscoveryRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StartDiscovery requested, force_refresh={ForceRefresh}", request.ForceRefresh);

        var response = new GndGrpc.StartDiscoveryResponse();

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

    public override async Task<GndGrpc.StopDiscoveryResponse> StopDiscovery(GndGrpc.StopDiscoveryRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StopDiscovery requested");

        var response = new GndGrpc.StopDiscoveryResponse();

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

    public override async Task<GndGrpc.GetDevicesResponse> GetDevices(GndGrpc.GetDevicesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("GetDevices requested");

        var response = new GndGrpc.GetDevicesResponse();

        try
        {
            // TODO: Get device list from discovery service
            // For now, return empty list
            response.Devices.Add(new GndGrpc.DeviceInfo
            {
                Id = "stub-device",
                Name = "Stub Device",
                IpAddress = "192.168.1.100",
                Port = 7236,
                Type = GndGrpc.DeviceType.DeviceTypeMiracast,
                State = GndGrpc.DeviceState.DeviceStateAvailable
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get devices");
        }

        return response;
    }

    public override async Task<GndGrpc.ConnectResponse> Connect(GndGrpc.ConnectRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Connect requested for device {DeviceId}", request.DeviceId);

        var response = new GndGrpc.ConnectResponse();

        try
        {
            // TODO: Get provider and connect to device
            response.Success = true;
            response.Capabilities = new GndGrpc.DeviceCapabilities
            {
                SupportsVideo = true,
                SupportsAudio = true
            };
            response.Capabilities.SupportedVideoCodecs.Add(GndGrpc.VideoCodec.VideoCodecH264);
            response.Capabilities.SupportedAudioCodecs.Add(GndGrpc.AudioCodec.AudioCodecAac);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to device {DeviceId}", request.DeviceId);
            response.Success = false;
            response.ErrorMessage = ex.Message;
        }

        return response;
    }

    public override async Task<GndGrpc.DisconnectResponse> Disconnect(GndGrpc.DisconnectRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Disconnect requested for device {DeviceId}", request.DeviceId);

        var response = new GndGrpc.DisconnectResponse();

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

    public override async Task<GndGrpc.StartStreamResponse> StartStream(GndGrpc.StartStreamRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StartStream requested for device {DeviceId}", request.DeviceId);

        var response = new GndGrpc.StartStreamResponse();

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

    public override async Task<GndGrpc.StopStreamResponse> StopStream(GndGrpc.StopStreamRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StopStream requested for device {DeviceId}", request.DeviceId);

        var response = new GndGrpc.StopStreamResponse();

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

    public override async Task StreamEvents(GndGrpc.StreamEventsRequest request, IServerStreamWriter<GndGrpc.DeviceEvent> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("StreamEvents started");

        try
        {
            // Send initial device list
            var devicesResponse = await GetDevices(new GndGrpc.GetDevicesRequest(), context);
            foreach (var device in devicesResponse.Devices)
            {
                await responseStream.WriteAsync(new GndGrpc.DeviceEvent
                {
                    Type = GndGrpc.DeviceEvent.DeviceEventType.EventTypeDeviceFound,
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
