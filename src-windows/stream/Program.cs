using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using GnomeNetworkDisplays.Stream.Media;

namespace GnomeNetworkDisplays.Stream;

public class Program
{
    private static readonly CancellationTokenSource s_cts = new();

    public static async Task<int> Main(string[] args)
    {
        // Parse command line arguments
        var pipeNameOption = new Option<string>(
            name: "--pipe",
            description: "Named pipe name for IPC with main service",
            getDefaultValue: () => "gnd-stream");

        var deviceIdOption = new Option<string>(
            name: "--device-id",
            description: "Device identifier",
            getDefaultValue: () => "");

        var deviceNameOption = new Option<string>(
            name: "--device-name",
            description: "Human-readable device name",
            getDefaultValue: () => "Unknown Device");

        var streamUrlOption = new Option<string>(
            name: "--stream-url",
            description: "Initial stream URL (optional)",
            getDefaultValue: () => "");

        var rootCommand = new RootCommand("Gnome Network Displays Stream Renderer");
        rootCommand.AddOption(pipeNameOption);
        rootCommand.AddOption(deviceIdOption);
        rootCommand.AddOption(deviceNameOption);
        rootCommand.AddOption(streamUrlOption);

        ParseResult parseResult = rootCommand.Parse(args);

        string pipeName = parseResult.GetValue(pipeNameOption)!;
        string deviceId = parseResult.GetValue(deviceIdOption)!;
        string deviceName = parseResult.GetValue(deviceNameOption)!;
        string? initialStreamUrl = parseResult.GetValue(streamUrlOption);

        Console.WriteLine($"Stream Renderer starting...");
        Console.WriteLine($"  Pipe: {pipeName}");
        Console.WriteLine($"  Device: {deviceId} ({deviceName})");

        // Set up Ctrl+C handling
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("Shutdown requested...");
            s_cts.Cancel();
        };

        try
        {
            // Create the host builder
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<NamedPipeClient>();
                    services.AddSingleton<RendererWindow>();
                    services.AddSingleton<RtspClient>();
                    services.AddSingleton<TransportStreamReceiver>();
                    services.AddSingleton<MediaFoundationDecoder>();

                    // Configuration
                    services.AddSingleton(new StreamConfiguration
                    {
                        PipeName = pipeName,
                        DeviceId = deviceId,
                        DeviceName = deviceName,
                        InitialStreamUrl = initialStreamUrl
                    });
                })
                .Build();

            var pipeClient = host.Services.GetRequiredService<NamedPipeClient>();
            var renderer = host.Services.GetRequiredService<RendererWindow>();

            // Connect to the main service
            if (!await pipeClient.ConnectAsync(pipeName, s_cts.Token))
            {
                Console.WriteLine("Failed to connect to main service");
                return 1;
            }

            Console.WriteLine("Connected to main service");

            // Send startup notification
            await pipeClient.SendStatusAsync(new StreamStatus
            {
                Status = "Started",
                DeviceId = deviceId,
                Message = "Stream renderer ready"
            }, s_cts.Token);

            // If we have an initial stream URL, start streaming
            if (!string.IsNullOrEmpty(initialStreamUrl))
            {
                Console.WriteLine($"Starting initial stream: {initialStreamUrl}");
                await StartStreamAsync(host.Services, initialStreamUrl, s_cts.Token);
            }

            // Main loop - wait for commands
            await pipeClient.ReceiveCommandsAsync(async command =>
            {
                Console.WriteLine($"Received command: {command.Action}");

                switch (command.Action)
                {
                    case "StartStream":
                        await StartStreamAsync(host.Services, command.Url!, s_cts.Token);
                        break;

                    case "StopStream":
                        await StopStreamAsync(host.Services, s_cts.Token);
                        break;

                    case "PauseStream":
                        await PauseStreamAsync(host.Services, s_cts.Token);
                        break;

                    case "ResumeStream":
                        await ResumeStreamAsync(host.Services, s_cts.Token);
                        break;

                    case "Shutdown":
                        Console.WriteLine("Shutdown command received");
                        s_cts.Cancel();
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command.Action}");
                        break;
                }
            }, s_cts.Token);

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Stream renderer shutting down...");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
        finally
        {
            s_cts.Dispose();
        }
    }

    private static async Task StartStreamAsync(IServiceProvider services, string url, CancellationToken cancellationToken)
    {
        var rtspClient = services.GetRequiredService<RtspClient>();
        var tsReceiver = services.GetRequiredService<TransportStreamReceiver>();
        var decoder = services.GetRequiredService<MediaFoundationDecoder>();
        var renderer = services.GetRequiredService<RendererWindow>();
        var pipeClient = services.GetRequiredService<NamedPipeClient>();

        try
        {
            Console.WriteLine($"Connecting to stream: {url}");

            await pipeClient.SendStatusAsync(new StreamStatus
            {
                Status = "Connecting",
                Message = $"Connecting to {url}"
            }, cancellationToken);

            // Determine stream type and start receiving
            if (url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
            {
                await rtspClient.ConnectAsync(url, cancellationToken);
                rtspClient.OnRtpPacketReceived += (packet) =>
                {
                    tsReceiver.FeedPacket(packet);
                };
                await rtspClient.PlayAsync(cancellationToken);
            }
            else if (url.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(url);
                await tsReceiver.StartAsync(uri.Host, uri.Port, cancellationToken);
            }

            // Initialize decoder with TS receiver
            decoder.Initialize(tsReceiver);

            // Start rendering
            await renderer.InitializeAsync(decoder, cancellationToken);
            renderer.Show();

            await pipeClient.SendStatusAsync(new StreamStatus
            {
                Status = "Playing",
                Message = "Stream started"
            }, cancellationToken);

            Console.WriteLine("Stream started successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start stream: {ex.Message}");
            await pipeClient.SendStatusAsync(new StreamStatus
            {
                Status = "Error",
                Message = ex.Message
            }, cancellationToken);
        }
    }

    private static async Task StopStreamAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var rtspClient = services.GetRequiredService<RtspClient>();
        var tsReceiver = services.GetRequiredService<TransportStreamReceiver>();
        var renderer = services.GetRequiredService<RendererWindow>();
        var pipeClient = services.GetRequiredService<NamedPipeClient>();

        try
        {
            renderer.Hide();
            rtspClient.Disconnect();
            tsReceiver.Stop();
            await pipeClient.SendStatusAsync(new StreamStatus
            {
                Status = "Stopped",
                Message = "Stream stopped"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping stream: {ex.Message}");
        }
    }

    private static async Task PauseStreamAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var decoder = services.GetRequiredService<MediaFoundationDecoder>();
        var pipeClient = services.GetRequiredService<NamedPipeClient>();

        decoder.Pause();
        await pipeClient.SendStatusAsync(new StreamStatus
        {
            Status = "Paused",
            Message = "Stream paused"
        }, cancellationToken);
    }

    private static async Task ResumeStreamAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var decoder = services.GetRequiredService<MediaFoundationDecoder>();
        var pipeClient = services.GetRequiredService<NamedPipeClient>();

        decoder.Resume();
        await pipeClient.SendStatusAsync(new StreamStatus
        {
            Status = "Playing",
            Message = "Stream resumed"
        }, cancellationToken);
    }
}

public class StreamConfiguration
{
    public string PipeName { get; set; } = "gnd-stream";
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string? InitialStreamUrl { get; set; }
}

public class StreamCommand
{
    public string Action { get; set; } = "";
    public string? Url { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}

public class StreamStatus
{
    public string Status { get; set; } = "";
    public string? DeviceId { get; set; }
    public string? Message { get; set; }
    public long? Position { get; set; }
}
