using System.Runtime.InteropServices;
using Vortice.MediaFoundation;
using Vortice.Direct3D11;

namespace GnomeNetworkDisplays.Stream.Media;

/// <summary>
/// Media Foundation based video/audio decoder for RTSP streams
/// </summary>
public class MediaFoundationDecoder : IDisposable
{
    private IMFMediaSession? _mediaSession;
    private IMFMediaSource? _mediaSource;
    private IMFTopology? _currentTopology;
    private ID3D11Device? _d3dDevice;
    private ID3D11DeviceContext? _d3dContext;
    private TransportStreamReceiver? _tsReceiver;
    private bool _isRunning;
    private bool _disposed;

    // Video frame callback
    public event EventHandler<VideoFrameEventArgs>? OnVideoFrame;

    // Error callback
    public event EventHandler<string>? OnError;

    public bool IsInitialized { get; private set; }
    public bool IsRunning => _isRunning;

    public MediaFoundationDecoder()
    {
    }

    /// <summary>
    /// Initialize the decoder with a transport stream receiver
    /// </summary>
    public void Initialize(TransportStreamReceiver tsReceiver)
    {
        _tsReceiver = tsReceiver;
        _tsReceiver.OnTsPacketReceived += HandleTsPacket;

        // Initialize Media Foundation
        try
        {
            int hr = MFExtern.MFStartup(MFVersion.Version_2_0, MFStartup.Full);
            if (hr != 0)
            {
                hr = MFExtern.MFStartup(MFVersion.Version_1_0, MFStartup.Full);
                if (hr != 0)
                {
                    throw new InvalidOperationException($"Media Foundation startup failed: 0x{hr:X8}");
                }
            }

            // Create D3D device for video rendering
            InitializeD3D();

            IsInitialized = true;
            System.Diagnostics.Debug.WriteLine("MediaFoundationDecoder initialized");
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, $"Failed to initialize MediaFoundationDecoder: {ex.Message}");
            throw;
        }
    }

    private void InitializeD3D()
    {
        var deviceFlags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport;

        var result = D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            deviceFlags,
            new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 },
            out _d3dDevice,
            out _d3dContext,
            out _);

        if (result.Failure || _d3dDevice == null || _d3dContext == null)
        {
            throw new InvalidOperationException($"D3D11 device creation failed: {result}");
        }

        System.Diagnostics.Debug.WriteLine("D3D11 device created for video decoding");
    }

    /// <summary>
    /// Start decoding
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Decoder not initialized");

        if (_isRunning)
            return;

        _isRunning = true;

        try
        {
            // Start the TS receiver
            await _tsReceiver!.StartAsync(cancellationToken);

            System.Diagnostics.Debug.WriteLine("MediaFoundationDecoder started");
        }
        catch (Exception ex)
        {
            _isRunning = false;
            OnError?.Invoke(this, $"Failed to start decoder: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Stop decoding
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;
        _tsReceiver?.Stop();

        System.Diagnostics.Debug.WriteLine("MediaFoundationDecoder stopped");
    }

    /// <summary>
    /// Handle incoming TS packets
    /// </summary>
    private void HandleTsPacket(byte[] packet)
    {
        if (!_isRunning)
            return;

        try
        {
            // Parse the TS packet PID
            ushort pid = (ushort)(((packet[1] & 0x1F) << 8) | packet[2]);

            // Check if it's a video PID (H.264 = 0x1b, H.265 = 0x24)
            if (_tsReceiver?.CurrentPmt != null)
            {
                var videoStream = _tsReceiver.CurrentPmt.Streams
                    .FirstOrDefault(s => s.StreamType == 0x1b || s.StreamType == 0x24);

                if (videoStream != null && pid == videoStream.ElementaryPid)
                {
                    ProcessVideoPacket(packet);
                }

                var audioStream = _tsReceiver.CurrentPmt.Streams
                    .FirstOrDefault(s => s.StreamType == 0x0f || s.StreamType == 0x11); // AAC or MP4

                if (audioStream != null && pid == audioStream.ElementaryPid)
                {
                    ProcessAudioPacket(packet);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing TS packet: {ex.Message}");
        }
    }

    private void ProcessVideoPacket(byte[] packet)
    {
        // In a complete implementation, this would:
        // 1. Reassemble TS packets into PES packets
        // 2. Extract NAL units from PES payload
        // 3. Feed to H.264/H.265 decoder
        // 4. Output decoded frames via OnVideoFrame event

        // For now, this is a placeholder
        // A full implementation would use MFCreateSample() and MFTransform
    }

    private void ProcessAudioPacket(byte[] packet)
    {
        // Similar to video - extract and decode audio
    }

    /// <summary>
    /// Create a topology for playing the stream
    /// </summary>
    public void CreateTopology()
    {
        // Create media session
        var attributes = MFExtern.MFCreateAttributes<IMFAttributes>();
        attributes.Set(MFAttributes.MF_SESSION_CONTENT_PROTECTION_MANAGER, Guid.Empty);

        _mediaSession = MFExtern.MFCreateSession(attributes);
        attributes.Dispose();

        // Set up session event handler
        _mediaSession.BeginGetEvent(SessionEventCallback, null);

        System.Diagnostics.Debug.WriteLine("Media session created");
    }

    private void SessionEventCallback(IMFMediaEvent mediaEvent, IntPtr callbackState)
    {
        try
        {
            mediaEvent.GetEventType(out var eventType);
            mediaEvent.GetStatus(out var status);

            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine($"Session error: 0x{status:X8}");
                OnError?.Invoke(this, $"Media session error: 0x{status:X8}");
                return;
            }

            switch (eventType)
            {
                case MediaEventTypes.MESessionStarted:
                    System.Diagnostics.Debug.WriteLine("Session started");
                    break;

                case MediaEventTypes.MESessionPaused:
                    System.Diagnostics.Debug.WriteLine("Session paused");
                    break;

                case MediaEventTypes.MESessionStopped:
                    System.Diagnostics.Debug.WriteLine("Session stopped");
                    break;

                case MediaEventTypes.MESessionEnded:
                    System.Diagnostics.Debug.WriteLine("Session ended");
                    _isRunning = false;
                    OnError?.Invoke(this, "Stream ended");
                    break;

                case MediaEventTypes.MEError:
                    System.Diagnostics.Debug.WriteLine("Media error");
                    OnError?.Invoke(this, "Media error");
                    break;

                default:
                    System.Diagnostics.Debug.WriteLine($"Session event: {eventType}");
                    break;
            }
        }
        finally
        {
            mediaEvent.Dispose();
        }
    }

    /// <summary>
    /// Get the D3D device for video rendering
    /// </summary>
    public ID3D11Device? GetD3DDevice() => _d3dDevice;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _isRunning = false;

        Stop();

        if (_tsReceiver != null)
        {
            _tsReceiver.OnTsPacketReceived -= HandleTsPacket;
        }

        if (_currentTopology != null)
        {
            try { _currentTopology.Dispose(); } catch { }
            _currentTopology = null;
        }

        if (_mediaSession != null)
        {
            try
            {
                _mediaSession.Close();
                _mediaSession.Shutdown();
                _mediaSession.Dispose();
            }
            catch { }
            _mediaSession = null;
        }

        _d3dContext?.Dispose();
        _d3dDevice?.Dispose();

        try
        {
            MFExtern.MFShutdown();
        }
        catch { }

        System.Diagnostics.Debug.WriteLine("MediaFoundationDecoder disposed");
    }
}

/// <summary>
/// Event args for video frame events
/// </summary>
public class VideoFrameEventArgs : EventArgs
{
    public required object Frame { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long Timestamp { get; init; }
}
