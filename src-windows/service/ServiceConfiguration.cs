{
  "GndService": {
    "GrpcPort": 5050,
    "NamedPipeName": "gnome-network-displays-ipc",
    "DiscoveryIntervalMs": 5000,
    "AutoReconnect": true,
    "MaxRetryAttempts": 3
  },
  "Miracast": {
    "DefaultVideoCodec": "H264",
    "DefaultAudioCodec": "AAC",
    "DefaultResolution": "1920x1080",
    "DefaultFramerate": 30,
    "UdpPortRange": {
      "Min": 5000,
      "Max": 6000
    }
  },
  "Chromecast": {
    "DefaultMediaCodec": "H264",
    "DefaultResolution": "1920x1080",
    "TransportProtocol": "HTTPS"
  },
  "Firewall": {
    "RequiredRules": [
      {
        "Name": "GND - RTSP",
        "Port": 7236,
        "Protocol": "UDP",
        "Direction": "Inbound"
      },
      {
        "Name": "GND - HTTP",
        "Port": 7237,
        "Protocol": "TCP",
        "Direction": "Inbound"
      }
    ]
  },
  "Logging": {
    "Level": "Information",
    "FilePath": "logs/gnd-service.log",
    "MaxFileSizeMb": 10,
    "MaxRetainedFiles": 5
  }
}
