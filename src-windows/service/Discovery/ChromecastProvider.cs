using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Gnd.Windows.Shared.Discovery;

namespace Gnd.Windows.Service.Discovery;

public class ChromecastProvider : IDisposable {
    private readonly BonjourDiscovery _discovery;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public event EventHandler<DeviceDiscoveredEventArgs>? DeviceFound => _discovery.DeviceFound;
    public event EventHandler<string>? DeviceLost => _discovery.DeviceLost;

    public ChromecastProvider() {
        _discovery = new BonjourDiscovery();
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task StartAsync(CancellationToken ct = default) {
        await _discovery.StartDiscoveryAsync(ct);
    }

    public async Task StopAsync() {
        await _discovery.StopDiscoveryAsync();
    }

    public async Task LaunchAppAsync(DeviceInfo device, string appId, CancellationToken ct = default) {
        if (device.Type != DeviceType.Chromecast) {
            throw new ArgumentException("Invalid device type", nameof(device));
        }

        string? ip = device.IpAddress ?? device.Properties.GetValueOrDefault("ip");
        if (string.IsNullOrEmpty(ip)) {
            throw new InvalidOperationException("Device IP address not available");
        }

        int port = device.Port ?? 8008;
        string url = $"http://{ip}:{port}/apps/{appId}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        try {
            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        } catch (HttpRequestException ex) {
            throw new InvalidOperationException($"Failed to launch app on Chromecast: {ex.Message}", ex);
        }
    }

    public async Task<ChromecastStatus> GetStatusAsync(DeviceInfo device, CancellationToken ct = default) {
        if (device.Type != DeviceType.Chromecast) {
            throw new ArgumentException("Invalid device type", nameof(device));
        }

        string? ip = device.IpAddress ?? device.Properties.GetValueOrDefault("ip");
        if (string.IsNullOrEmpty(ip)) {
            throw new InvalidOperationException("Device IP address not available");
        }

        int port = device.Port ?? 8008;
        string url = $"http://{ip}:{port}/setup/eureka_info";

        try {
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<ChromecastStatus>(json) ?? new ChromecastStatus();
        } catch (Exception ex) {
            return new ChromecastStatus { Error = ex.Message };
        }
    }

    public void Dispose() {
        if (!_disposed) {
            _discovery.Dispose();
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}

public class ChromecastStatus {
    public string? Name { get; set; }
    public string? Model { get; set; }
    public string? Error { get; set; }
}