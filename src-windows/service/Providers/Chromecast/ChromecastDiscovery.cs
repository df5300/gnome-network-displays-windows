using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Gnd.Windows.Service.Providers.Chromecast;

public class ChromecastDiscovery
{
    private readonly List<ChromecastDevice> _devices = new();
    private readonly object _lock = new();
    private bool _isDiscovering;

    private const int CastPort = 8009;

    public async Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        if (_isDiscovering)
            return;

        _isDiscovering = true;
        _devices.Clear();

        try
        {
            // Chromecast devices announce themselves via mDNS
            await DiscoverViaMdnsAsync(cancellationToken);

            // Also scan common subnets for Cast devices
            await ScanNetworkAsync(cancellationToken);
        }
        finally
        {
            _isDiscovering = false;
        }
    }

    private async Task DiscoverViaMdnsAsync(CancellationToken cancellationToken)
    {
        // Note: In production, use a proper mDNS library
        // Chromecast devices broadcast _googlecast._tcp
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

    private async Task ScanNetworkAsync(CancellationToken cancellationToken)
    {
        // Also try connecting to common Chromecast ports
        var tasks = new List<Task>();
        for (int i = 1; i < 255; i++)
        {
            var subnet = GetLocalIpAddress().Substring(0, GetLocalIpAddress().LastIndexOf('.'));
            var ip = $"{subnet}.{i}";
            tasks.Add(Task.Run(() => ProbeCastPortAsync(ip), cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProbeDeviceAsync(string ip)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            await client.ConnectAsync(IPAddress.Parse(ip), CastPort, cts.Token);

            var device = await GetDeviceInfoAsync(ip);
            if (device != null)
            {
                lock (_lock)
                {
                    if (!_devices.Any(d => d.IpAddress == ip))
                    {
                        _devices.Add(device);
                    }
                }
            }
        }
        catch
        {
            // Device not available
        }
    }

    private async Task ProbeCastPortAsync(string ip)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            await client.ConnectAsync(IPAddress.Parse(ip), CastPort, cts.Token);

            var device = new ChromecastDevice
            {
                Id = $"cast-{ip}",
                Name = $"Chromecast ({ip})",
                IpAddress = ip,
                Port = CastPort
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
            // Port not open
        }
    }

    private async Task<ChromecastDevice?> GetDeviceInfoAsync(string ip)
    {
        // Try to get device info via HTTP
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);

            var response = await httpClient.GetStringAsync($"http://{ip}:8008/setup/eureka_info");
            var info = JsonPayload.Parse<EurekaInfo>(response);

            if (info != null)
            {
                return new ChromecastDevice
                {
                    Id = info.DeviceId ?? $"cast-{ip}",
                    Name = info.Name ?? $"Chromecast ({ip})",
                    IpAddress = ip,
                    Port = CastPort,
                    Model = info.Model ?? string.Empty,
                    Manufacturer = info.Manufacturer ?? string.Empty
                };
            }
        }
        catch
        {
            // Could not get device info, return basic device
        }

        return new ChromecastDevice
        {
            Id = $"cast-{ip}",
            Name = $"Chromecast ({ip})",
            IpAddress = ip,
            Port = CastPort
        };
    }

    public IEnumerable<ChromecastDevice> GetDevices()
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

    private class EurekaInfo
    {
        public string? DeviceId { get; set; }
        public string? Name { get; set; }
        public string? Model { get; set; }
        public string? Manufacturer { get; set; }
    }
}
