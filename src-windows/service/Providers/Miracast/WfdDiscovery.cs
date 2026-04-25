using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace Gnd.Windows.Service.Providers.Miracast;

public class WfdDiscovery
{
    private readonly List<WfdDevice> _devices = new();
    private readonly object _lock = new();
    private bool _isDiscovering;

    private const int WfdPort = 7236;
    private const string WfdServiceType = "_wfd._tcp";

    public async Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        if (_isDiscovering)
            return;

        _isDiscovering = true;
        _devices.Clear();

        try
        {
            // Discover via mDNS/DNS-SD
            await DiscoverViaMdnsAsync(cancellationToken);

            // Also listen for UDP broadcast probes
            await ListenForUdpBroadcastsAsync(cancellationToken);
        }
        finally
        {
            _isDiscovering = false;
        }
    }

    private async Task DiscoverViaMdnsAsync(CancellationToken cancellationToken)
    {
        // Note: In production, use a proper mDNS library like mDNSResponder or Avahi
        // For now, we'll simulate discovery by scanning common subnets
        var localIp = GetLocalIpAddress();
        var subnet = localIp.Substring(0, localIp.LastIndexOf('.'));

        var tasks = new List<Task>();
        for (int i = 1; i < 255; i++)
        {
            var ip = $"{subnet}.{i}";
            tasks.Add(Task.Run(() => ProbeDeviceAsync(ip), cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProbeDeviceAsync(string ip)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            await client.ConnectAsync(IPAddress.Parse(ip), WfdPort, cts.Token);

            var device = new WfdDevice
            {
                Id = $"wfd-{ip}",
                Name = $"WFD Device ({ip})",
                IpAddress = ip,
                Port = WfdPort
            };

            lock (_lock)
            {
                if (!_devices.Any(d => d.IpAddress == ip))
                {
                    _devices.Add(device);
                }
            }
        }
        catch
        {
            // Device not available or doesn't speak WFD
        }
    }

    private async Task ListenForUdpBroadcastsAsync(CancellationToken cancellationToken)
    {
        using var udpClient = new UdpClient(39190);
        udpClient.EnableBroadcast = true;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(cancellationToken);
                var message = Encoding.UTF8.GetString(result.Buffer);

                if (message.Contains("WFD"))
                {
                    var device = ParseWfdAnnouncement(message, result.RemoteEndPoint.Address.ToString());
                    if (device != null)
                    {
                        lock (_lock)
                        {
                            if (!_devices.Any(d => d.IpAddress == device.IpAddress))
                            {
                                _devices.Add(device);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Continue listening
            }
        }
    }

    private WfdDevice? ParseWfdAnnouncement(string message, string sourceIp)
    {
        // Parse WFD device announcement message
        // Format: wfd_device_type=<type> | wfd_service_ discovery=<svc> | ...
        var device = new WfdDevice
        {
            Id = $"wfd-{sourceIp}",
            Name = $"WFD Device ({sourceIp})",
            IpAddress = sourceIp,
            Port = WfdPort
        };

        if (message.Contains("wfd_device_type=1"))
        {
            device.Capabilities.SupportsVideo = true;
            device.Capabilities.SupportsAudio = true;
        }

        return device;
    }

    public IEnumerable<WfdDevice> GetDevices()
    {
        lock (_lock)
        {
            return _devices.ToList();
        }
    }

    public void StopDiscovery()
    {
        _isDiscovering = false;
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return "192.168.1.100";
    }
}
