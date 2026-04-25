# WinUI 3 Client

A WinUI 3 desktop application for GNOME Network Displays on Windows. This client connects to the Windows Service via gRPC/IPC and provides a user interface for device discovery and streaming.

## Overview

The GNOME Network Displays Windows port consists of:
- **Service**: A Windows Service handling device discovery and streaming
- **Client**: A WinUI 3 desktop application for user interaction
- **Shared**: Protobuf/gRPC definitions for IPC communication

## Building

```bash
cd src-windows/client
dotnet build
```

## Running

```bash
dotnet run
```

Note: The gnome-network-displays Service must be running for the client to function.

## Requirements

- Windows 10 19041+ (or Windows 11)
- .NET 8.0 Runtime
- The gnome-network-displays Service must be running

## Architecture

- **WinUI 3**: Modern Windows UI framework
- **MVVM Pattern**: Using CommunityToolkit.Mvvm for clean separation
- **gRPC/IPC**: Communication with the Windows Service
- **Device Discovery**: Discovers Cast-compatible devices on the network
- **Streaming**: Initiates and controls media streaming sessions

## Project Structure

```
client/
├── App.xaml(.cs)           # Application entry point
├── MainWindow.xaml(.cs)    # Main window with navigation
├── ViewModels/
│   ├── MainViewModel.cs    # Main window view model
│   └── DeviceViewModel.cs  # Device item view model
├── Services/
│   ├── IServiceClient.cs   # Service client interface
│   └── GrpcServiceClient.cs # gRPC implementation
├── app.manifest            # Application manifest
└── gnome-network-displays.client.csproj
```

## Features

- Device discovery and listing
- Device connection management
- Real-time device status updates
- Settings configuration
- Status notifications