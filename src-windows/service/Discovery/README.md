# Device Discovery Architecture

## Overview

The device discovery subsystem provides a unified interface for discovering Wi-Fi Display (Miracast/Wi-Fi Direct) and Chromecast devices on the local network.

## Architecture Layers

### 1. Shared Layer (shared/Discovery/)

Low-level protocol implementations that implement the IDeviceDiscovery interface:

- **IDeviceDiscovery.cs** - Core interface defining discovery contract
- **WiFiDirectDiscovery.cs** - Wi-Fi Direct device discovery using Windows WFD API
- **BonjourDiscovery.cs** - mDNS/Bonjour discovery for Chromecast devices
- **DeviceDiscoveryAggregator.cs** - Combines multiple discovery sources

### 2. Platform Layer (shared/Platform/)

Native API P/Invoke bindings:

- **WlanApi.cs** - Windows Wlanapi.h bindings for Wi-Fi operations
- **DnsSdApi.cs** - Bonjour DNS-SD API bindings

### 3. Service Layer (service/Discovery/)

High-level providers wrapping shared implementations:

- **WiFiDirectProvider.cs** - High-level Wi-Fi Direct device management
- **ChromecastProvider.cs** - Chromecast discovery and control

## Usage

```csharp
// Create aggregator with multiple sources
var aggregator = new DeviceDiscoveryAggregator(
    new WiFiDirectDiscovery(),
    new BonjourDiscovery()
);

aggregator.DeviceFound += (s, e) => {
    Console.WriteLine($"Found: {e.Device.Name} ({e.Device.Type})");
};

aggregator.DeviceLost += (s, id) => {
    Console.WriteLine($"Lost: {id}");
};

await aggregator.StartDiscoveryAsync(CancellationToken.None);

// Later, stop discovery
await aggregator.StopDiscoveryAsync();
```

## Device Types

- **WiFiDirect** - Wi-Fi Display (Miracast) sinks
- **Chromecast** - Google Cast compatible devices

## Platform Requirements

- Windows 10/11 with Wi-Fi Direct support
- Bonjour SDK for Windows (for Chromecast discovery)
- .NET 8.0 Runtime
