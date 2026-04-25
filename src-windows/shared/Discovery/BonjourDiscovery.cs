using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Gnd.Windows.Shared.Platform;

namespace Gnd.Windows.Shared.Discovery;

public class BonjourDiscovery : IDeviceDiscovery, IDisposable {
    private IntPtr _serviceRef;
    private bool _disposed;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, DeviceInfo> _devices = new();
    private Task? _discoveryTask;
    private GCHandle _callbackHandle;

    public event EventHandler<DeviceDiscoveredEventArgs>? DeviceFound;
    public event EventHandler<string>? DeviceLost;

    public async Task StartDiscoveryAsync(CancellationToken ct) {
        if (_serviceRef != IntPtr.Zero) {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        uint result = DnsSdApi.DNSServiceDiscover(
            out _serviceRef,
            DnsSdApi.kDNSServiceFlagsBrowseDomains,
            0,
            "_googlecast._tcp",
            "",
            "local.",
            false,
            OnDnsServiceDiscovery,
            IntPtr.Zero);

        if (result != DnsSdApi.kDNSServiceErr_NoError) {
            throw new InvalidOperationException($"Failed to start Bonjour discovery: {result}");
        }

        _discoveryTask = ProcessResultsAsync(_cts.Token);
        await Task.CompletedTask;
    }

    private void OnDnsServiceDiscovery(
        uint flags,
        uint interfaceIndex,
        uint errorCode,
        string serviceName,
        string regType,
        string domain,
        IntPtr context) {
        if (errorCode != DnsSdApi.kDNSServiceErr_NoError) {
            return;
        }

        bool added = (flags & DnsSdApi.kDNSServiceFlagsAdd) != 0;
        string fullName = $"{serviceName}.{regType}.{domain}";

        if (added) {
            ResolveAndAddDevice(serviceName, domain);
        } else {
            if (_devices.TryRemove(serviceName, out _)) {
                DeviceLost?.Invoke(this, serviceName);
            }
        }
    }

    private async void ResolveAndAddDevice(string serviceName, string domain) {
        try {
            // In production, use DNSServiceResolve to get the actual IP/port
            var device = new DeviceInfo {
                Id = serviceName,
                Name = ParseDeviceName(serviceName),
                Type = DeviceType.Chromecast,
                Properties = new Dictionary<string, string> {
                    ["domain"] = domain
                }
            };

            if (_devices.TryAdd(serviceName, device)) {
                DeviceFound?.Invoke(this, new DeviceDiscoveredEventArgs { Device = device });
            }
        } catch {
            // Log error
        }
    }

    private string ParseDeviceName(string serviceName) {
        // Parse Chromecast device name from service name
        // e.g., "Chromecast-ABC123" -> "Chromecast"
        if (serviceName.StartsWith("Chromecast-")) {
            return serviceName.Substring(11);
        }
        return serviceName;
    }

    private async Task ProcessResultsAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested && _serviceRef != IntPtr.Zero) {
            try {
                uint result = DnsSdApi.DNSServiceProcessResult(_serviceRef);
                if (result != DnsSdApi.kDNSServiceErr_NoError) {
                    await Task.Delay(1000, ct);
                }
            } catch (OperationCanceledException) {
                break;
            } catch {
                await Task.Delay(1000, ct);
            }
        }
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

        if (_serviceRef != IntPtr.Zero) {
            DnsSdApi.DNSServiceRefDestroy(_serviceRef);
            _serviceRef = IntPtr.Zero;
        }

        if (_callbackHandle.IsAllocated) {
            _callbackHandle.Free();
        }
    }

    public void Dispose() {
        if (!_disposed) {
            _cts?.Cancel();
            if (_serviceRef != IntPtr.Zero) {
                DnsSdApi.DNSServiceRefDestroy(_serviceRef);
                _serviceRef = IntPtr.Zero;
            }
            if (_callbackHandle.IsAllocated) {
                _callbackHandle.Free();
            }
            _cts?.Dispose();
            _disposed = true;
        }
    }
}