# GNOME Network Displays Windows Service

A .NET 8 Windows Service that hosts device management logic for the GNOME Network Displays Windows port.

## Features

- .NET 8 Worker Service running as a Windows Service
- gRPC server for IPC communication with WinUI 3 client
- Named Pipes as backup IPC mechanism
- Service lifecycle management (Start, Stop, Install)

## Requirements

- .NET 8.0 SDK
- Windows 10 version 19041 (Build 19041) or later
- Windows App SDK for WinUI 3 integration

## Building

```bash
# From the service directory
cd src-windows/service
dotnet build
```

## Running (Development)

```bash
# Run as console application (not as a Windows Service)
dotnet run
```

## Installing as Windows Service

### Using sc.exe

```bash
# Create the service
sc create GndService binPath= "C:\path\to\gnome-network-displays.service.exe" DisplayName= "GNOME Network Displays"

# Start the service
sc start GndService

# Stop the service
sc stop GndService

# Delete the service
sc delete GndService
```

### Using PowerShell

```powershell
# Create the service
New-Service -Name GndService -BinaryPathName "C:\path\to\gnome-network-displays.service.exe" -DisplayName "GNOME Network Displays" -StartupType Automatic

# Start the service
Start-Service -Name GndService

# Stop the service
Stop-Service -Name GndService

# Remove the service
Remove-Service -Name GndService
```

## IPC Communication

### gRPC (Primary)

- Port: 5050
- Protocol: gRPC over HTTP/2
- Used by: WinUI 3 client

### Named Pipes (Fallback)

- Pipe Name: `gnome-network-displays-ipc`
- Direction: InOut
- Transport: Streamed JSON messages
- Used by: Legacy clients or when gRPC is unavailable

### Message Format

```json
{
  "RequestId": "uuid-string",
  "Action": "Discover|Connect|Disconnect|GetDevices|StreamStart|StreamStop",
  "Payload": "json-string"
}
```

## Project Structure

```
src-windows/
├── service/                    # Windows Service project
│   ├── gnome-network-displays.service.csproj
│   ├── Program.cs             # Entry point
│   ├── ServiceMain.cs         # Background service implementation
│   ├── IpcServer.cs           # gRPC and Named Pipe servers
│   ├── app.manifest           # Windows 10 compatibility manifest
│   └── README.md
└── shared/                    # Shared contracts
    ├── gnome-network-displays.shared.csproj
    └── IpcMessages.cs         # IPC message definitions
```

## Device Providers (To be implemented)

- **Miracast**: Wireless display mirroring
- **Chromecast**: Google Cast protocol support

## Notes

- The service currently runs with stub implementations for device providers
- Actual device discovery and streaming logic needs to be implemented
- Consider adding telemetry and logging for debugging
