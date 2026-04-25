# gnome-network-displays for Windows

A Windows port of gnome-network-displays, implementing Wi-Fi Display (Miracast) and Chromecast support using native Windows APIs.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Windows System                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐     IPC (gRPC/Named Pipes)  ┌───────────┐│
│  │  WinUI 3     │◄─────────────────────────────►│  Windows  ││
│  │  Client      │                             │  Service  ││
│  └──────────────┘                             └─────┬─────┘│
│                                                     │       │
│                                      ┌──────────────┼──────┐│
│                                      │              │      ││
│                                      ▼              ▼      ││
│                              ┌───────────┐  ┌───────────┐ ││
│                              │ Miracast  │  │Chromecast │ ││
│                              │ Provider  │  │ Provider  │ ││
│                              └─────┬─────┘  └─────┬─────┘ ││
│                                    │              │       ││
│                              ┌─────┴──────────────┴─────┐ ││
│                              │   Device Discovery       │ ││
│                              │  (Wi-Fi Direct + WS)     │ ││
│                              └──────────────────────────┘ ││
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  ││
│  │           Stream Renderer Process                      │  ││
│  │  RTSP Client → Media Foundation → WinUI 3 Window      │  ││
│  └──────────────────────────────────────────────────────┘  ││
└──────────────────────────────────────────────────────────────┘
```

## Components

| Component | Description | Technology |
|-----------|-------------|------------|
| **Client** | GUI Application | WinUI 3 + MVVM |
| **Service** | Background Service | .NET 8 Worker Service |
| **Stream** | Media Renderer | Media Foundation + WinUI 3 |
| **Shared** | Common Code | Discovery, IPC, Platform APIs |

## Requirements

- **Windows 10 19041+** (Windows 11 recommended)
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** (optional, for IDE support)
- Wi-Fi adapter supporting Wi-Fi Direct (for Miracast)
- Network connectivity

## Building

### Prerequisites

1. Install .NET 8.0 SDK:
   ```powershell
   winget install Microsoft.DotNet.SDK.8
   ```

2. Restart your terminal/VS Code

### Build Commands

```powershell
# Navigate to project directory
cd src-windows

# Restore packages
dotnet restore

# Build Debug
dotnet build -c Debug

# Build Release
dotnet build -c Release

# Or use the build script
.\build.bat
```

### Output Binaries

After building:
- `service/bin/Debug/net8.0-windows10.0.19041.0/gnome-network-displays.service.exe`
- `client/bin/Debug/net8.0-windows10.0.19041.0/gnome-network-displays.client.exe`
- `stream/bin/Debug/net8.0-windows10.0.19041.0/gnome-network-displays.stream.exe`

## Running

### 1. Install the Service

```powershell
# Run as Administrator
sc create GndService binPath= "C:\path\to\gnome-network-displays.service.exe"
sc start GndService
```

### 2. Launch the Client

```powershell
# Run the client (can be done as normal user)
.\gnome-network-displays.client.exe
```

### 3. Command Line Options

```powershell
# Client options
.\gnome-network-displays.client.exe --help

# Service options
.\gnome-network-displays.service.exe --help
```

## Project Structure

```
src-windows/
├── gnome-network-displays.sln          # Solution file
│
├── shared/                            # Shared library
│   ├── IpcMessages.cs                 # IPC message definitions
│   ├── Discovery/                     # Device discovery interfaces
│   ├── Platform/                      # Native API P/Invoke
│   └── SystemIntegration/             # Windows integration
│       ├── WindowsFirewall.cs         # Firewall management
│       ├── WindowsAudio.cs            # Audio device management
│       └── WindowsNotifications.cs     # Toast notifications
│
├── service/                           # Windows Service
│   ├── Program.cs                     # Entry point
│   ├── ServiceMain.cs                 # Service implementation
│   ├── IpcServer.cs                   # gRPC + Named Pipe IPC
│   ├── Discovery/                     # High-level discovery
│   └── Providers/                     # Protocol providers
│       ├── Miracast/                  # Wi-Fi Display
│       └── Chromecast/                # Chromecast
│
├── client/                            # WinUI 3 GUI
│   ├── App.xaml                       # Application definition
│   ├── MainWindow.xaml                # Main window UI
│   ├── ViewModels/                    # MVVM ViewModels
│   └── Services/                      # Service client
│
└── stream/                            # Stream Renderer
    ├── Program.cs                     # Entry point
    ├── Renderer.cs                    # WinUI 3 renderer
    ├── NamedPipeClient.cs             # IPC client
    └── Media/                         # Media handling
        ├── RtspClient.cs              # RTSP client
        ├── TransportStreamReceiver.cs # MPEG-TS receiver
        └── MediaFoundationDecoder.cs  # Media Foundation
```

## Development

### Setting up Visual Studio

1. Open `gnome-network-displays.sln` in Visual Studio 2022
2. Select "Windows" as the target platform
3. Set startup project to `client`
4. Press F5 to build and run

### Adding New Protocol Providers

1. Create provider class in `service/Providers/`
2. Implement `IDeviceProvider` interface
3. Register in `IpcServer.cs`

### Native API Bindings

Platform-specific code goes in `shared/Platform/`:
- `WlanApi.cs` - Wi-Fi Direct API (Wlanapi.h)
- `DnsSdApi.cs` - DNS Service Discovery (dns_sd.h)

## Known Limitations

1. **Bonjour SDK Required** - Chromecast discovery requires Bonjour SDK for Windows
2. **Wi-Fi Direct** - Requires compatible Wi-Fi adapter
3. **Firewall Rules** - May need manual configuration for first run

## Troubleshooting

### Service won't start

Check the Windows Event Viewer:
```powershell
Get-EventLog -LogName Application -Source "GndService" -Newest 50
```

### Device not discovered

1. Ensure Wi-Fi is enabled
2. Check if device supports Miracast
3. Verify firewall allows the application

### Build errors

Ensure you have .NET 8.0 SDK installed (not just runtime):
```powershell
dotnet --list-sdks
```

## Contributing

This is a port of the GNOME Network Displays project. For the original project, see:
- https://gitlab.gnome.org/GNOME/gnome-network-displays

## License

Same as the original project - GPLv3+
