using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Gnd.Windows.Grpc;

namespace Gnd.Windows.Client;

public class LocalGrpcServer : BackgroundService
{
    private readonly ILogger<LocalGrpcServer> _logger;
    private readonly GndGrpcServiceImpl _grpcService;
    private readonly int _port;
    private Server? _server;

    public LocalGrpcServer(ILogger<LocalGrpcServer> logger, int port = 5050)
    {
        _logger = logger;
        _port = port;
        _grpcService = new GndGrpcServiceImpl(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting local gRPC server on port {Port}", _port);

        _server = new Server
        {
            Services = { GndService.BindService(_grpcService) },
            Ports = { new ServerPort("localhost", _port, ServerCredentials.Insecure) }
        };

        _server.Start();
        _logger.LogInformation("Local gRPC server started on localhost:{Port}", _port);

        // Wait until cancelled
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping local gRPC server");
        if (_server != null)
        {
            await _server.ShutdownAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}

public class GndGrpcServiceImpl : GndService.GndServiceBase
{
    private readonly ILogger _logger;

    public GndGrpcServiceImpl(ILogger logger)
    {
        _logger = logger;
    }

    public override Task<Gnd.Windows.Grpc.StartDiscoveryResponse> StartDiscovery(
        Gnd.Windows.Grpc.StartDiscoveryRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StartDiscovery requested");
        return Task.FromResult(new Gnd.Windows.Grpc.StartDiscoveryResponse { Success = true });
    }

    public override Task<Gnd.Windows.Grpc.StopDiscoveryResponse> StopDiscovery(
        Gnd.Windows.Grpc.StopDiscoveryRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StopDiscovery requested");
        return Task.FromResult(new Gnd.Windows.Grpc.StopDiscoveryResponse { Success = true });
    }

    public override Task<Gnd.Windows.Grpc.GetDevicesResponse> GetDevices(
        Gnd.Windows.Grpc.GetDevicesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("GetDevices requested");

        var response = new Gnd.Windows.Grpc.GetDevicesResponse();
        response.Devices.Add(new Gnd.Windows.Grpc.DeviceInfo
        {
            Id = "stub-device",
            Name = "Stub Device",
            IpAddress = "192.168.1.100",
            Port = 7236,
            Type = Gnd.Windows.Grpc.DeviceType.Miracast,
            State = Gnd.Windows.Grpc.DeviceState.Available
        });

        return Task.FromResult(response);
    }

    public override Task<Gnd.Windows.Grpc.ConnectResponse> Connect(
        Gnd.Windows.Grpc.ConnectRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Connect requested for device {DeviceId}", request.DeviceId);

        return Task.FromResult(new Gnd.Windows.Grpc.ConnectResponse
        {
            Success = true,
            Capabilities = new Gnd.Windows.Grpc.DeviceCapabilities
            {
                SupportsVideo = true,
                SupportsAudio = true
            }
        });
    }

    public override Task<Gnd.Windows.Grpc.DisconnectResponse> Disconnect(
        Gnd.Windows.Grpc.DisconnectRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Disconnect requested");
        return Task.FromResult(new Gnd.Windows.Grpc.DisconnectResponse { Success = true });
    }

    public override Task<Gnd.Windows.Grpc.StartStreamResponse> StartStream(
        Gnd.Windows.Grpc.StartStreamRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StartStream requested");
        return Task.FromResult(new Gnd.Windows.Grpc.StartStreamResponse
        {
            Success = true,
            StreamUrl = "pipe://localhost/stream"
        });
    }

    public override Task<Gnd.Windows.Grpc.StopStreamResponse> StopStream(
        Gnd.Windows.Grpc.StopStreamRequest request, ServerCallContext context)
    {
        _logger.LogInformation("StopStream requested");
        return Task.FromResult(new Gnd.Windows.Grpc.StopStreamResponse { Success = true });
    }
}
