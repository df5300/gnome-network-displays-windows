using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Gnd.Windows.Shared;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Gnd.Windows.Service;

/// <summary>
/// gRPC service implementation with Named Pipe fallback for Windows Service compatibility.
/// Routes messages to device providers (Miracast, Chromecast).
/// </summary>
public class IpcServer
{
    private readonly ILogger<IpcServer> _logger;
    private readonly Dictionary<string, Func<IpcMessage, Task<IpcMessage>>> _handlers = new();
    private CancellationTokenSource? _cts;
    private Task? _grpcServerTask;
    private Task? _namedPipeServerTask;

    private const int GrpcPort = 5050;
    private const string NamedPipeName = "gnome-network-displays-ipc";

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

        // Start Named Pipe server (primary for Windows Service reliability)
        _namedPipeServerTask = RunNamedPipeServerAsync(_cts.Token);

        // Start gRPC server (for WinUI 3 client communication)
        _grpcServerTask = RunGrpcServerAsync(_cts.Token);

        await Task.WhenAny(_namedPipeServerTask, _grpcServerTask);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        if (_grpcServerTask != null)
        {
            await Task.WhenAny(_grpcServerTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        if (_namedPipeServerTask != null)
        {
            await Task.WhenAny(_namedPipeServerTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    private async Task RunGrpcServerAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting gRPC server on port {Port}", GrpcPort);

        var server = new Grpc.Core.Server
        {
            Services = { IpcService.BindService(new GrpcIpcService(this)) },
            Ports = { new ServerPort("localhost", GrpcPort, ServerCredentials.Insecure) }
        };

        server.Start();
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
            await server.ShutdownAsync();
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

    private Task<IpcMessage> HandleDiscover(IpcMessage message)
    {
        // TODO: Implement device discovery via providers
        _logger.LogInformation("Discover requested - providers not yet implemented");
        return Task.FromResult(CreateSuccessResponse(message, "Discover started"));
    }

    private Task<IpcMessage> HandleGetDevices(IpcMessage message)
    {
        // TODO: Return list of discovered devices
        var devices = new List<DeviceInfo>();
        var payload = JsonSerializer.Serialize(devices);
        return Task.FromResult(CreateSuccessResponse(message, payload));
    }

    private Task<IpcMessage> HandleConnect(IpcMessage message)
    {
        // TODO: Connect to device
        return Task.FromResult(CreateSuccessResponse(message, "Connected"));
    }

    private Task<IpcMessage> HandleDisconnect(IpcMessage message)
    {
        // TODO: Disconnect from device
        return Task.FromResult(CreateSuccessResponse(message, "Disconnected"));
    }

    private Task<IpcMessage> HandleStreamStart(IpcMessage message)
    {
        // TODO: Start streaming
        return Task.FromResult(CreateSuccessResponse(message, "Streaming started"));
    }

    private Task<IpcMessage> HandleStreamStop(IpcMessage message)
    {
        // TODO: Stop streaming
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
}

/// <summary>
/// gRPC service implementation.
/// </summary>
public class IpcService : Ipc.IpcBase
{
    private readonly IpcServer _server;

    public IpcService(IpcServer server)
    {
        _server = server;
    }

    public override async Task<IpcResponse> SendMessage(IpcRequest request, ServerCallContext context)
    {
        var message = new IpcMessage
        {
            RequestId = request.RequestId,
            Action = Enum.Parse<IpcAction>(request.Action),
            Payload = request.Payload
        };

        var response = await _server.ProcessMessageAsync(message);

        return new IpcResponse
        {
            RequestId = response.RequestId,
            Action = response.Action.ToString(),
            Payload = response.Payload
        };
    }
}

// Placeholder for gRPC generated code
public class Ipc
{
    public class IpcBase
    {
        public virtual Task<IpcResponse> SendMessage(IpcRequest request, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ""));
        }
    }

    public class IpcClient
    {
        public IpcClient(Channel channel) { }
        public virtual Task<IpcResponse> SendMessage(IpcRequest request, CallOptions? options = null) { throw new RpcException(new Status(StatusCode.Unimplemented, "")); }
    }

    public class IpcRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }

    public class IpcResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }
}
