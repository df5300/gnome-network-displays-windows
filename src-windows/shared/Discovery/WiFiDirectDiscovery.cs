using System.Collections.Concurrent;
using Gnd.Windows.Shared.Platform;

namespace Gnd.Windows.Shared.Discovery;

/// <summary>
/// Wi-Fi Direct device discovery using Windows WFD API
/// </summary>
public class WiFiDirectDiscovery : IDeviceDiscovery, IDisposable
{
    private WiFiDirectHelper? _wfdHelper;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, DeviceInfo> _devices = new();
    private Task? _discoveryTask;
    private bool _disposed;

    public event EventHandler<DeviceDiscoveredEventArgs>? DeviceFound;
    public event EventHandler<string>? DeviceLost;

    public async Task StartDiscoveryAsync(CancellationToken ct)
    {
        if (_wfdHelper != null)
            return;

        try
        {
            _wfdHelper = new WiFiDirectHelper();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize Wi-Fi Direct: {ex.Message}");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _discoveryTask = DiscoverDevicesAsync(_cts.Token);
        await Task.CompletedTask;
    }

    private async Task DiscoverDevicesAsync(CancellationToken ct)
    {
        System.Diagnostics.Debug.WriteLine("WiFiDirectDiscovery: Starting device discovery");

        while (!ct.IsCancellationRequested && _wfdHelper != null)
        {
            try
            {
                // Get interfaces and scan
                var interfaces = _wfdHelper.GetInterfaces();
                foreach (var iface in interfaces)
                {
                    try
                    {
                        // Trigger a scan for fresh data
                        _wfdHelper.Scan(iface.InterfaceGuid);

                        // Small delay to allow scan to start
                        await Task.Delay(500, ct);

                        // Get available networks
                        var networks = _wfdHelper.GetAvailableNetworks(iface.InterfaceGuid);
                        ProcessNetworks(iface.InterfaceGuid, networks);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error scanning interface {iface.InterfaceGuid}: {ex.Message}");
                    }
                }

                await Task.Delay(5000, ct); // Scan every 5 seconds
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discovery error: {ex.Message}");
                await Task.Delay(5000, ct);
            }
        }

        System.Diagnostics.Debug.WriteLine("WiFiDirectDiscovery: Discovery stopped");
    }

    private void ProcessNetworks(Guid interfaceGuid, List<WLAN_AVAILABLE_NETWORK> networks)
    {
        var currentDeviceIds = new HashSet<string>();

        foreach (var network in networks)
        {
            // Check if this is a Wi-Fi Direct network
            // WFD networks typically have specific naming patterns or capabilities
            if (IsWiFiDirectNetwork(network))
            {
                var device = CreateDeviceFromNetwork(network, interfaceGuid);
                if (device != null)
                {
                    currentDeviceIds.Add(device.Id);

                    if (_devices.TryAdd(device.Id, device))
                    {
                        System.Diagnostics.Debug.WriteLine($"WiFiDirectDiscovery: Found device {device.Name}");
                        DeviceFound?.Invoke(this, new DeviceDiscoveredEventArgs { Device = device });
                    }
                }
            }
        }

        // Check for lost devices
        foreach (var removedId in _devices.Keys.Except(currentDeviceIds))
        {
            if (_devices.TryRemove(removedId, out var removedDevice))
            {
                System.Diagnostics.Debug.WriteLine($"WiFiDirectDiscovery: Device lost {removedDevice.Name}");
                DeviceLost?.Invoke(this, removedId);
            }
        }
    }

    private bool IsWiFiDirectNetwork(WLAN_AVAILABLE_NETWORK network)
    {
        // Wi-Fi Direct networks often have specific characteristics:
        // 1. Network name starts with "DIRECT-" (Windows convention)
        // 2. Or has specific security settings
        // 3. Or has "Wi-Fi Direct" capability flag

        var networkName = network.strNetworkName ?? string.Empty;

        // Check for common WFD naming patterns
        if (networkName.StartsWith("DIRECT-", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for Hotspot2.0 / Passpoint (often used with WFD)
        if (network.AuthenticationAndCipher.AuthAlgo == DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA3 ||
            network.AuthenticationAndCipher.AuthAlgo == DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_WPA3_PSK)
            return true;

        // Check if it's an ad-hoc or infrastructure network that looks like WFD
        if (network.AuthenticationAndCipher.CipherAlgo != DOT11_CIPHER_ALGORITHM.DOT11_CIPHER_ALGO_NONE &&
            network.AuthenticationAndCipher.AuthAlgo != DOT11_AUTH_ALGORITHM.DOT11_AUTH_ALGO_80211_OPEN)
        {
            // Has security enabled - could be WFD
            // Additional heuristics could be applied here
        }

        return false;
    }

    private DeviceInfo? CreateDeviceFromNetwork(WLAN_AVAILABLE_NETWORK network, Guid interfaceGuid)
    {
        var networkName = network.strNetworkName ?? string.Empty;
        if (string.IsNullOrEmpty(networkName))
            return null;

        return new DeviceInfo
        {
            Id = $"wfd-{networkName.GetHashCode():X8}",
            Name = networkName,
            IpAddress = string.Empty, // WFD devices get IP after connection
            Type = DeviceType.Miracast,
            State = DeviceState.Available,
            Properties = new Dictionary<string, string>
            {
                ["interface"] = interfaceGuid.ToString(),
                ["signal"] = network.dwSignalQuality.ToString(),
                ["profile"] = network.strProfileName ?? string.Empty,
                ["security"] = $"{network.AuthenticationAndCipher.AuthAlgo}/{network.AuthenticationAndCipher.CipherAlgo}"
            }
        };
    }

    public async Task StopDiscoveryAsync()
    {
        System.Diagnostics.Debug.WriteLine("WiFiDirectDiscovery: Stopping discovery");

        _cts?.Cancel();

        if (_discoveryTask != null)
        {
            try
            {
                await _discoveryTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine("WiFiDirectDiscovery: Task did not complete in time");
            }
        }

        _wfdHelper?.Dispose();
        _wfdHelper = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts?.Cancel();
            _wfdHelper?.Dispose();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}
