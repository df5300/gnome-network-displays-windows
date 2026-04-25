using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GnomeNetworkDisplays.Stream;

public class NamedPipeClient : IDisposable
{
    private PipeClient? _pipeClient;
    private CancellationTokenSource? _receiveCts;
    private readonly ILogger<NamedPipeClient>? _logger;

    public NamedPipeClient(ILogger<NamedPipeClient>? logger = null)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string pipeName, CancellationToken cancellationToken)
    {
        try
        {
            var fullPipeName = pipeName.StartsWith(@"\\.\pipe\")
                ? pipeName
                : $@"\\.\pipe\{pipeName}";

            Console.WriteLine($"Connecting to pipe: {fullPipeName}");

            _pipeClient = new PipeClient(fullPipeName, PipeDirection.InOut, 1024 * 1024); // 1MB buffer

            await _pipeClient.ConnectAsync(5000, cancellationToken); // 5 second timeout

            Console.WriteLine("Connected to named pipe");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to pipe: {ex.Message}");
            return false;
        }
    }

    public async Task SendStatusAsync(StreamStatus status, CancellationToken cancellationToken)
    {
        if (_pipeClient == null || !_pipeClient.IsConnected)
        {
            Console.WriteLine("Cannot send status: not connected");
            return;
        }

        try
        {
            string json = JsonSerializer.Serialize(status, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await SendMessageAsync(new PipeMessage
            {
                Type = "Status",
                Content = json
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending status: {ex.Message}");
        }
    }

    public async Task SendPositionUpdateAsync(long position, CancellationToken cancellationToken)
    {
        if (_pipeClient == null || !_pipeClient.IsConnected)
            return;

        await SendStatusAsync(new StreamStatus
        {
            Status = "Playing",
            Position = position
        }, cancellationToken);
    }

    public async Task ReceiveCommandsAsync(
        Func<StreamCommand, Task> commandHandler,
        CancellationToken cancellationToken)
    {
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        while (!_receiveCts.Token.IsCancellationRequested)
        {
            try
            {
                var message = await ReceiveMessageAsync(_receiveCts.Token);

                if (message == null)
                {
                    // Connection closed
                    Console.WriteLine("Pipe connection closed");
                    break;
                }

                if (message.Type == "Command")
                {
                    var command = JsonSerializer.Deserialize<StreamCommand>(
                        message.Content,
                        new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });

                    if (command != null)
                    {
                        await commandHandler(command);
                    }
                }
                else if (message.Type == "Ping")
                {
                    // Respond to keepalive
                    await SendMessageAsync(new PipeMessage { Type = "Pong" }, _receiveCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving command: {ex.Message}");
                await Task.Delay(1000, _receiveCts.Token); // Wait before retrying
            }
        }
    }

    private async Task SendMessageAsync(PipeMessage message, CancellationToken cancellationToken)
    {
        if (_pipeClient == null)
            return;

        try
        {
            string json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] lengthBytes = BitConverter.GetBytes(data.Length);

            var buffer = new byte[4 + data.Length];
            Buffer.BlockCopy(lengthBytes, 0, buffer, 0, 4);
            Buffer.BlockCopy(data, 0, buffer, 4, data.Length);

            await _pipeClient.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            await _pipeClient.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    private async Task<PipeMessage?> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        if (_pipeClient == null)
            return null;

        try
        {
            // Read 4-byte length prefix
            byte[] lengthBuffer = new byte[4];
            int bytesRead = await _pipeClient.ReadAsync(lengthBuffer, 0, 4, cancellationToken);

            if (bytesRead == 0)
                return null;

            if (bytesRead < 4)
            {
                Console.WriteLine("Incomplete length header received");
                return null;
            }

            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

            if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) // Max 10MB
            {
                Console.WriteLine($"Invalid message length: {messageLength}");
                return null;
            }

            // Read message body
            byte[] dataBuffer = new byte[messageLength];
            int totalRead = 0;

            while (totalRead < messageLength)
            {
                bytesRead = await _pipeClient.ReadAsync(
                    dataBuffer,
                    totalRead,
                    messageLength - totalRead,
                    cancellationToken);

                if (bytesRead == 0)
                    return null;

                totalRead += bytesRead;
            }

            string json = Encoding.UTF8.GetString(dataBuffer, 0, dataBuffer.Length);

            return JsonSerializer.Deserialize<PipeMessage>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error receiving message: {ex.Message}");
            return null;
        }
    }

    public void Disconnect()
    {
        _receiveCts?.Cancel();
        _pipeClient?.Dispose();
        _pipeClient = null;
    }

    public bool IsConnected => _pipeClient?.IsConnected ?? false;

    public void Dispose()
    {
        Disconnect();
        _receiveCts?.Dispose();
    }
}

public class PipeMessage
{
    public string Type { get; set; } = "";
    public string? Content { get; set; }
}

// Thin wrapper around NamedPipeClientStream for simpler API
public class PipeClient : IDisposable
{
    private readonly NamedPipeClientStream _pipeStream;
    private readonly int _bufferSize;
    private bool _isConnected;

    public PipeClient(string pipeName, PipeDirection direction, int bufferSize)
    {
        _bufferSize = bufferSize;
        _pipeStream = new NamedPipeClientStream(
            ".",
            pipeName,
            direction,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough);

        _pipeStream.ReadBufferSize = bufferSize;
        _pipeStream.WriteBufferSize = bufferSize;
    }

    public bool IsConnected => _isConnected;

    public Task ConnectAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        return _pipeStream.ConnectAsync(timeoutMs, cancellationToken).ContinueWith(t =>
        {
            _isConnected = _pipeStream.IsConnected;
            return t;
        }, cancellationToken).Unwrap();
    }

    public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _pipeStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        return _pipeStream.FlushAsync(cancellationToken);
    }

    public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _pipeStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public void Dispose()
    {
        _pipeStream.Dispose();
    }
}
