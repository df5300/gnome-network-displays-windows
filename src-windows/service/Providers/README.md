# Protocol Providers

## Miracast (Wi-Fi Display)

Miracast is a Wi-Fi Display standard based on the Wi-Fi Display Specification. It uses:

### RTSP for Capability Negotiation
- **DESCRIBE**: Get device capabilities (video/audio codecs, resolutions)
- **SETUP**: Configure transport (UDP ports for RTP/RTCP)
- **PLAY**: Start streaming
- **PAUSE/TEARDOWN**: Control streaming

### Transport
- **MPEG-TS over UDP** for media transport
- **RTP/RTCP** for packetization and control
- **H.264/H.265** video codecs
- **AAC/LPCM** audio codecs

### Key Components
- `WfdDiscovery`: Discovers WFD devices via mDNS and UDP broadcast
- `WfdSession`: Manages the WFD session state and RTSP negotiation
- `RtspClient`: Low-level RTSP protocol client
- `MiracastProvider`: High-level provider integrating discovery and sessions

## Chromecast

Chromecast uses the Google Cast protocol v2 for device communication:

### DIAL for App Discovery
- **DIAL** (DIAL Instant Access Language) for app discovery and launch
- Default Media Receiver app ID: `CC1AD845`

### CASTV2 over WebSocket
- **CONNECT**: Initial handshake and transport binding
- **LOAD**: Launch media app and load content
- **PLAY/PAUSE/STOP**: Media control
- **STATUS**: Get playback state

### Namespaces
- `urn:x-cast:com.google.cast.tp.deviceauth`: Device authentication
- `urn:x-cast:com.google.cast.socket`: Connection management
- `urn:x-cast:com.google.cast.heartbeat`: Keep-alive heartbeat
- `urn:x-cast:com.google.cast.receiver`: Receiver control
- `urn:x-cast:com.google.cast.media`: Media playback control

### Transport
- **WebSocket** over TLS (wss://) on port 8009
- **HTTPS** for media streaming
- **MPEG-DASH** or progressive download for media

### Key Components
- `ChromecastDiscovery`: Discovers Cast devices via mDNS
- `CastV2Client`: Low-level CASTV2 protocol client
- `ChromecastDevice`: Device representation with capabilities
- `ChromecastProvider`: High-level provider integrating all components
