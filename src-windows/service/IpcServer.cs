using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Gnd.Windows.Shared;
using Gnd.Windows.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;

using GrpcDeviceInfo = Gnd.Windows.Grpc.DeviceInfo;
using GrpcDeviceState = Gnd.Windows.Grpc.DeviceState;

namespace Gnd.Windows.Service;

/// <summary>
/// IPC Server managing both gRPC and Named Pipe communication.
/// Routes messages to device providers (Miracast, Chromecast).
/// </summary>
public class IpcServer : IDisposable
{
    private readonly ILogger<IpcServer> _logger;
    private readonly Dictionary<string, Func<IpcMessage, Task<IpcMessage>>> _handlers = new();
    private CancellationTokenSource? _cts;
    private Server? _grpcServer;
    private Task? _namedPipeServerTask;
    private bool _disposed;

    public const int GrpcPort = 5050;
    public const string NamedPipeName = "gnome-network-displays-ipc";

    // Events for device state changes
    public event EventHandler<DeviceEventArgs>? DeviceFound;
    public event EventHandler<DeviceEventArgs>? DeviceLost;
    public event EventHandler<DeviceEventArgs>? DeviceStateChanged;

    public IpcServer(ILogger<IpcServer> logger)
    {
        _logger = logger;
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        _handlers[IpcAction.Discover.ToString()] = HandleDiscover;
        _handlers[IpcAction.GetDevices.ToString()] = HandleGetDevices;
        _handlers[IpcAction.Connect.ToString()] = HandleConnect;
        _handlers[IpcAction.Disconnect.ToString()] = HandleDisconnect;
        _handlers[IpcAction.StreamStart.ToString()] = HandleStreamStart;
        _handlers[IpcAction.StreamStop.ToString()] = HandleStreamStop;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start Named Pipe server (for internal service communication)
        _namedPipeServerTask = RunNamedPipeServerAsync(_cts.Token);

        // Note: gRPC server is started via UseGrpcWeb() in Program.cs
        // This is for direct gRPC access if needed

        _logger.LogInformation("IPC Server started");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IPC Server stopping");
        _cts?.Cancel();

        if (_grpcServer != null)
        {
            await _grpcServer.ShutdownAsync();
        }

        if (_namedPipeServerTask != null)
        {
            await Task.WhenAny(_namedPipeServerTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    /// <summary>
    /// Start the gRPC server directly (alternative to using Kestrel)
    /// </summary>
    public async Task StartGrpcServerAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting gRPC server on port {Port}", GrpcPort);

        _grpcServer = new Grpc.Core.Server
        {
            Services =
            {
                GndService.BindService(new GndGrpcService(
                    LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<GndGrpcService>(),
                    null!))
            },
            Ports = { new ServerPort("localhost", GrpcPort, ServerCredentials.Insecure) }
        };

        _grpcServer.Start();
        _logger.LogInformation("gRPC server started on port {Port}", GrpcPort);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("gRPC server shutting down");
        }
        finally
        {
            await _grpcServer.ShutdownAsync();
        }
    }

    private async Task RunNamedPipeServerAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Named Pipe server: {PipeName}", NamedPipeName);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    NamedPipeName,
                    PipeDirection.InOut,
                    NamedPipeServer.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);
                _logger.LogInformation("Client connected via Named Pipe");

                _ = HandleNamedPipeClientAsync(server, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Named Pipe server");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task HandleNamedPipeClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(server, leaveOpen: true);
            using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

            while (!cancellationToken.IsCancellationRequested && server.IsConnected)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) break;

                var request = JsonSerializer.Deserialize<IpcMessage>(line);
                if (request != null)
                {
                    var response = await ProcessMessageAsync(request);
                    var responseJson = JsonSerializer.Serialize(response);
                    await writer.WriteLineAsync(responseJson);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Named Pipe client");
        }
    }

    public async Task<IpcMessage> ProcessMessageAsync(IpcMessage message)
    {
        _logger.LogInformation("Processing IPC message: {Action} (RequestId: {RequestId})",
            message.Action, message.RequestId);

        if (_handlers.TryGetValue(message.Action.ToString(), out var handler))
        {
            try
            {
                return await handler(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {Action}", message.Action);
                return CreateErrorResponse(message, ex.Message);
            }
        }

        return CreateErrorResponse(message, $"Unknown action: {message.Action}");
    }

    // Handler implementations - these would normally interact with providers
    private Task<IpcMessage> HandleDiscover(IpcMessage message)
    {
        _logger.LogInformation("Discover requested");
        // TODO: Start discovery via providers
        DeviceFound?.Invoke(this, new DeviceEventArgs { Device = CreateStubDevice() });
        return Task.FromResult(CreateSuccessResponse(message, "Discovery started"));
    }

    private Task<IpcMessage> HandleGetDevices(IpcMessage message)
    {
        _logger.LogInformation("GetDevices requested");
        // TODO: Return list of discovered devices from providers
        var devices = new List<DeviceInfo> { CreateStubDevice() };
        var payload = JsonSerializer.Serialize(devices);
        return Task.FromResult(CreateSuccessResponse(message, payload));
    }

    private Task<IpcMessage> HandleConnect(IpcMessage message)
    {
        _logger.LogInformation("Connect requested");
        // TODO: Connect via appropriate provider (Miracast or Chromecast)
        return Task.FromResult(CreateSuccessResponse(message, "Connected"));
    }

    private Task<IpcMessage> HandleDisconnect(IpcMessage message)
    {
        _logger.LogInformation("Disconnect requested");
        // TODO: Disconnect via provider
        return Task.FromResult(CreateSuccessResponse(message, "Disconnected"));
    }

    private Task<IpcMessage> HandleStreamStart(IpcMessage message)
    {
        _logger.LogInformation("StreamStart requested");
        // TODO: Start stream via provider
        return Task.FromResult(CreateSuccessResponse(message, "Streaming started"));
    }

    private Task<IpcMessage> HandleStreamStop(IpcMessage message)
    {
        _logger.LogInformation("StreamStop requested");
        // TODO: Stop stream via provider
        return Task.FromResult(CreateSuccessResponse(message, "Streaming stopped"));
    }

    private static IpcMessage CreateSuccessResponse(IpcMessage request, string payload)
    {
        return new IpcMessage
        {
            RequestId = request.RequestId,
            Action = request.Action,
            Payload = payload
        };
    }

    private static IpcMessage CreateErrorResponse(IpcMessage request, string error)
    {
        return new IpcMessage
        {
            RequestId = request.RequestId,
            Action = request.Action,
            Payload = JsonSerializer.Serialize(new { Error = error })
        };
    }

    private static DeviceInfo CreateStubDevice()
    {
        return new DeviceInfo
        {
            Id = "stub-" + Guid.NewGuid().ToString("N")[..8],
            Name = "Stub Device",
            IpAddress = "192.168.1.100",
            Type = DeviceType.Miracast,
            State = DeviceState.Available
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts?.Cancel();
            _grpcServer?.ShutdownAsync().Wait();
            _disposed = true;
        }
    }
}

public class DeviceEventArgs : EventArgs
{
    public required GrpcDeviceInfo Device { get; init; }
    public string? Message { get; init; }
    public GrpcDeviceState? OldState { get; init; }
    public GrpcDeviceState? NewState { get; init; }
}
