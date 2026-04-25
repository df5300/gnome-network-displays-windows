# Windows Port Design: gnome-network-displays

## Overview

Port gnome-network-displays from Linux/GNOME to Windows with full feature parity (Wi-Fi Display + Chromecast).

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Windows System                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐     IPC (Named Pipes)    ┌──────────────┐ │
│  │  WinUI 3     │◄────────────────────────►│ Windows     │ │
│  │  GUI Client  │                         │ Service     │ │
│  └──────────────┘                         └──────┬───────┘ │
│                                                   │         │
│                                      ┌────────────┼────────┐│
│                                      │            │        ││
│                                      ▼            ▼        ││
│                              ┌───────────┐  ┌───────────┐  ││
│                              │ Miracast  │  │Chromecast │  ││
│                              │ Provider  │  │ Provider  │  ││
│                              └─────┬─────┘  └─────┬─────┘  ││
│                                    │              │        ││
│                              ┌─────┴──────────────┴─────┐  ││
│                              │    Device Registry       │  ││
│                              │  (Wi-Fi Direct + WS)    │  ││
│                              └──────────────────────────┘  ││
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  ││
│  │           Stream Renderer Process                      │  ││
│  │  - RTSP Client → Media Foundation → WinUI 3 Window    │  ││
│  └──────────────────────────────────────────────────────┘  ││
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

## Component Mapping

| Linux (Original) | Windows (Target) |
|-----------------|-----------------|
| GTK4 + libadwaita | WinUI 3 (Windows App SDK) |
| GSettings | Windows Registry / JSON config |
| NetworkManager + Wi-Fi P2P | Wi-Fi Direct API + WlanAPI |
| Avahi (mDNS) | Windows Sockets + Bonjour SDK |
| libportal | Windows Shell COM APIs |
| firewalld | Windows Firewall API (COM) |
| PipeWire | Windows Media Foundation |
| PulseAudio | Windows Audio (WASAPI) |
| GDBus | Named Pipes + gRPC |
| GStreamer RTSP | Windows Media Foundation RTSP |

## Subsystems

### 1. Service Framework (Windows Service)
- Service entry point, install/uninstall
- Named pipe server for IPC
- Lifetime management
- **Files**: `service/ServiceMain.cs`, `service/IpcServer.cs`

### 2. Device Discovery Layer
- Wi-Fi Direct device enumeration (WFD API)
- mDNS/Bonjour discovery
- Device registry management
- **Files**: `shared/Discovery/`, `service/DeviceRegistry.cs`

### 3. Miracast Provider
- WFD session management
- RTSP client for capability negotiation
- MPEG-TS over UDP transport
- **Files**: `service/Miracast/`

### 4. Chromecast Provider
- DIAL discovery
- CASTV2 protocol over WebSocket
- Application launch/control
- **Files**: `service/Chromecast/`

### 5. Stream Renderer Process
- Standalone executable
- Receives stream via named pipe from service
- Media Foundation decoding
- WinUI 3 rendering
- **Files**: `stream/Program.cs`, `stream/Renderer.cs`

### 6. WinUI 3 Client
- Device list UI
- Connection management
- Settings page
- **Files**: `client/`

### 7. System Integration
- Firewall rule management
- Audio device selection
- Notification toasts
- **Files**: `shared/SystemIntegration/`

## IPC Protocol

```csharp
// Named pipe messages (JSON)
interface IpcMessage {
    string Action;      // "Discover", "Connect", "Disconnect", "StreamStart"
    string Payload;     // JSON serialized data
    string RequestId;   // For correlation
}

// Examples:
// Client → Service: { "Action": "Discover", "RequestId": "123" }
// Service → Client: { "Action": "DeviceFound", "Payload": {...}, "RequestId": "123" }
```

## Build System

- **IDE**: Visual Studio 2022 / VS Code
- **Framework**: .NET 8.0 (LTS)
- **UI**: Windows App SDK 1.5+
- **Package**: MSIX for distribution
- **Dependencies**: vcpkg for C++ media libs if needed

## File Structure

```
src-windows/
├── client/                    # WinUI 3 GUI Application
│   ├── MainWindow.xaml
│   ├── ViewModels/
│   └── Services/
├── service/                   # Windows Service
│   ├── ServiceMain.cs
│   ├── IpcServer.cs
│   ├── Providers/
│   │   ├── Miracast/
│   │   └── Chromecast/
│   └── Discovery/
├── stream/                    # Stream Renderer Process
│   ├── Program.cs
│   └── Renderer.cs
└── shared/                    # Shared contracts
    ├── IpcMessages.cs
    └── Contracts/
```

## Key Windows APIs

| Function | Windows API |
|----------|-------------|
| Wi-Fi Direct | `WlanDeleteProfile`, `WFDOpenHandle` (WlanAPI.h) |
| mDNS/Bonjour | Apple's Bonjour SDK for Windows |
| Firewall | `INetFwMgr` COM interface |
| Audio | `IMMDeviceEnumerator` (WASAPI) |
| Media | `IMFMediaSession` (Media Foundation) |
| Notifications | `Windows.UI.Notifications` |

## Implementation Order

1. **Service Framework** - Build and test basic service + IPC
2. **Device Discovery** - Wi-Fi Direct enumeration
3. **Miracast Provider** - Core WFD/RTSP implementation
4. **Stream Renderer** - Media Foundation + WinUI rendering
5. **Chromecast Provider** - CASTV2 protocol
6. **WinUI Client** - Full GUI implementation
7. **System Integration** - Firewall, audio, notifications

## Testing Strategy

- Unit tests for each provider
- Integration tests with real devices (if available)
- Mock devices for CI/CD
- Windows VM testing (Azure VMs or local Hyper-V)

## Status

- [x] Design completed
- [ ] Service framework
- [ ] Device discovery
- [ ] Miracast provider
- [ ] Stream renderer
- [ ] Chromecast provider
- [ ] WinUI client
- [ ] System integration
