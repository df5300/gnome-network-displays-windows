# Stream Renderer

Receives and renders RTSP streams from Miracast/Chromecast sources.

## Overview

The Stream Renderer is a standalone Windows process that handles media decoding and rendering. It communicates with the main Gnome Network Displays service via named pipes and uses Windows Media Foundation for hardware-accelerated video decoding.

## Building

```bash
dotnet restore
dotnet build
```

### Requirements

- .NET 8.0 SDK
- Windows 10 19041 (May 2020 Update) or later
- Windows App SDK (for WinUI 3 support)

## Usage

```bash
gnome-network-displays.stream.exe --pipe \\.\pipe\gnd-stream --device-id xxx --device-name "My Device"
```

### Command Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `--pipe` | Named pipe name for IPC | `gnd-stream` |
| `--device-id` | Device identifier | empty |
| `--device-name` | Human-readable device name | `Unknown Device` |
| `--stream-url` | Initial stream URL to play | none |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Main Service                            │
│                    (Named Pipe Server)                       │
└──────────────────────────┬────────────────────────────────────┘
                           │ Commands / Status
                           │ (Named Pipe)
┌──────────────────────────▼────────────────────────────────────┐
│                    Stream Renderer Process                    │
├──────────────────────────────────────────────────────────────┤
│  NamedPipeClient    │  Receives commands from main service  │
├──────────────────────────────────────────────────────────────┤
│  RtspClient         │  RTSP DESCRIBE/SETUP/PLAY handling    │
│  TransportStreamReceiver │ MPEG-TS over UDP                 │
├──────────────────────────────────────────────────────────────┤
│  MediaFoundationDecoder │ IMFMediaSource / Decode H.264/265 │
├──────────────────────────────────────────────────────────────┤
│  RendererWindow     │  WinUI 3 SwapChainPanel rendering     │
└──────────────────────────────────────────────────────────────┘
```

## IPC Protocol

### Commands (Main Service → Stream Renderer)

```json
{ "Action": "StartStream", "Url": "rtsp://192.168.1.100:8554/stream" }
{ "Action": "StopStream" }
{ "Action": "PauseStream" }
{ "Action": "ResumeStream" }
{ "Action": "Shutdown" }
```

### Status Updates (Stream Renderer → Main Service)

```json
{ "Status": "Started", "DeviceId": "xxx", "Message": "Stream renderer ready" }
{ "Status": "Connecting", "Message": "Connecting to rtsp://..." }
{ "Status": "Playing", "Position": 1234 }
{ "Status": "Paused", "Position": 1234 }
{ "Status": "Stopped", "Message": "Stream stopped" }
{ "Status": "Error", "Message": "Connection failed: timeout" }
```

## Supported Stream Types

- **RTSP/RTP**: TCP interleaved (`RTP/AVP/TCP;interleaved=X`) or UDP
- **MPEG-TS over UDP**: For network-based streaming
- **HTTP Tunneled RTSP**: For NAT traversal

## Video Codecs

- H.264 (AVC) - Primary video codec
- H.265 (HEVC) - Supported if hardware acceleration available
- AAC - Audio codec

## Media Foundation Pipeline

```
IMFMediaSource
    │
    ├── Video Branch
    │   └── H.264/H.265 Decoder → IMFVideoDisplayControl → SwapChainPanel
    │
    └── Audio Branch
        └── AAC Decoder → IMFAudioRenderer → Audio Output
```

## Implementation Notes

### WinUI 3 Integration

The renderer uses `SwapChainPanel` for hardware-accelerated rendering:

```csharp
var device = D3D11Helpers.CreateDeviceAndContext(out ID3D11Device device, ...);
var surface = Media.CreateSurfaceFromDHTexture(texture, true);
VideoSurface.SetSwapChain(surface);
```

### Named Pipe Protocol

Messages use a simple length-prefixed JSON format:
- 4 bytes: message length (big-endian)
- N bytes: UTF-8 JSON payload

### Stream Processing

1. RTSP handshake (DESCRIBE → SETUP → PLAY)
2. RTP packets received via TCP interleaved or UDP
3. TS packets extracted from RTP payloads
4. PSI tables (PAT/PMT) parsed to identify streams
5. H.264/H.265 NAL units extracted from PES payloads
6. Frames decoded via Media Foundation
7. Decoded frames rendered via D3D11 SwapChainPanel

## Debugging

Enable verbose output by running with debug logging:

```powershell
$env:LOG_LEVEL = "Debug"
.\gnome-network-displays.stream.exe --pipe \\.\pipe\gnd-stream
```

## Known Limitations

- RTSP over HTTP tunneling requires server support
- Hardware acceleration depends on GPU driver support
- Some RTSP servers may require authentication (not yet implemented)

## License

Same as gnome-network-displays project.
