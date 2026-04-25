using System.Collections.Concurrent;
using Gnd.Windows.Shared;

namespace Gnd.Windows.Service.Providers.Miracast;

public class MiracastProvider
{
    private readonly WfdDiscovery _discovery;
    private readonly ConcurrentDictionary<string, WfdSession> _sessions = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _streamCancellation = new();

    public MiracastProvider()
    {
        _discovery = new WfdDiscovery();
    }

    public async Task<WfdSession> ConnectAsync(WfdDevice device)
    {
        var session = new WfdSession();

        // Negotiate capabilities
        await session.NegotiateCapabilitiesAsync(device);

        // Setup transport with default configuration
        var transport = new TransportConfig
        {
            LocalIp = GetLocalIpAddress(),
            VideoPort = 15555,
            AudioPort = 15556,
            Type = TransportType.UDP
        };

        await session.SetupTransportAsync(transport);
        await session.StartPlaybackAsync();

        _sessions[device.Id] = session;
        return session;
    }

    public async Task DisconnectAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            if (_streamCancellation.TryRemove(sessionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            await session.TeardownAsync();
        }
    }

    public async IAsyncEnumerable<byte[]> GetStreamAsync(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new ArgumentException($"Session {sessionId} not found");

        var cts = _streamCancellation.GetOrAdd(sessionId, _ => new CancellationTokenSource());
        var token = cts.Token;

        var receiveTcs = new TaskCompletionSource<byte[]>();
        session.RtpPacketReceived += (s, data) => receiveTcs.TrySetResult(data);

        while (!token.IsCancellationRequested)
        {
            using var registration = token.Register(() => receiveTcs.TrySetCanceled());

            try
            {
                var task = await Task.WhenAny(receiveTcs.Task, Task.Delay(Timeout.Infinite, token));
                if (task == receiveTcs.Task)
                {
                    var data = await receiveTcs.Task;
                    yield return data;
                }
                else
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public IEnumerable<DeviceInfo> GetAvailableDevices()
    {
        return _discovery.GetDevices().Select(d => new DeviceInfo
        {
            Id = d.Id,
            Name = d.Name,
            IpAddress = d.IpAddress,
            Type = DeviceType.Miracast,
            State = _sessions.ContainsKey(d.Id) ? DeviceState.Connected : DeviceState.Available
        });
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
            return endPoint?.Address.ToString() ?? "192.168.1.100";
        }
        catch
        {
            return "192.168.1.100";
        }
    }
}
