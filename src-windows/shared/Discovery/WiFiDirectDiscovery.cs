using System.Collections.Concurrent;
using Gnd.Windows.Shared.Platform;

namespace Gnd.Windows.Shared.Discovery;

public class WiFiDirectDiscovery : IDeviceDiscovery, IDisposable {
    private IntPtr _wlanHandle;
    private uint _negotiatedVersion;
    private bool _disposed;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, DeviceInfo> _devices = new();
    private Task? _discoveryTask;

    public event EventHandler<DeviceDiscoveredEventArgs>? DeviceFound;
    public event EventHandler<string>? DeviceLost;

    public async Task StartDiscoveryAsync(CancellationToken ct) {
        if (_wlanHandle != IntPtr.Zero) {
            return;
        }

        uint result = WlanApi.WlanOpenHandle(
            WlanApi.WLAN_API_VERSION,
            IntPtr.Zero,
            out _negotiatedVersion,
            out _wlanHandle);

        if (result != 0) {
            throw new InvalidOperationException($"Failed to open WLAN handle: {result}");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _discoveryTask = DiscoverDevicesAsync(_cts.Token);
        await Task.CompletedTask;
    }

    private async Task DiscoverDevicesAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested && _wlanHandle != IntPtr.Zero) {
            try {
                EnumerateWfdDevices();
                await Task.Delay(5000, ct);
            } catch (OperationCanceledException) {
                break;
            } catch {
                // Continue discovery attempts
                await Task.Delay(5000, ct);
            }
        }
    }

    private void EnumerateWfdDevices() {
        var interfaces = GetWlanInterfaces();
        var currentDeviceIds = new HashSet<string>();

        foreach (var iface in interfaces) {
            IntPtr networkList = IntPtr.Zero;
            try {
                uint result = WlanApi.WlanGetAvailableNetworkList(
                    _wlanHandle,
                    ref iface,
                    0,
                    IntPtr.Zero,
                    out networkList);

                if (result == 0 && networkList != IntPtr.Zero) {
                    // Parse network list and identify WFD devices
                    // Note: Full WFD enumeration requires additional Windows APIs
                    // This is a simplified implementation
                    var wfdDevices = ParseWfdNetworks(networkList);
                    foreach (var device in wfdDevices) {
                        currentDeviceIds.Add(device.Id);
                        if (_devices.TryAdd(device.Id, device)) {
                            DeviceFound?.Invoke(this, new DeviceDiscoveredEventArgs { Device = device });
                        }
                    }
                }
            } finally {
                if (networkList != IntPtr.Zero) {
                    WlanApi.WlanFreeMemory(networkList);
                }
            }
        }

        // Check for lost devices
        foreach (var removedId in _devices.Keys.Except(currentDeviceIds)) {
            if (_devices.TryRemove(removedId, out _)) {
                DeviceLost?.Invoke(this, removedId);
            }
        }
    }

    private List<DeviceInfo> ParseWfdNetworks(IntPtr networkList) {
        var devices = new List<DeviceInfo>();
        // Wi-Fi Direct device enumeration implementation
        // In production, this would parse the actual WFD IE structures
        // and filter for devices with WFD capability
        return devices;
    }

    private List<Guid> GetWlanInterfaces() {
        var interfaces = new List<Guid>();
        // Enumerate WLAN interfaces - simplified for Windows Desktop
        // Full implementation requires WlanEnumInterfaces
        return interfaces;
    }

    public async Task StopDiscoveryAsync() {
        _cts?.Cancel();
        if (_discoveryTask != null) {
            try {
                await _discoveryTask.WaitAsync(TimeSpan.FromSeconds(5));
            } catch (OperationCanceledException) {
                // Expected
            } catch (TimeoutException) {
                // Task did not complete in time
            }
        }
        CloseWlanHandle();
    }

    private void CloseWlanHandle() {
        if (_wlanHandle != IntPtr.Zero) {
            WlanApi.WlanCloseHandle(_wlanHandle, IntPtr.Zero);
            _wlanHandle = IntPtr.Zero;
        }
    }

    public void Dispose() {
        if (!_disposed) {
            _cts?.Cancel();
            CloseWlanHandle();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}