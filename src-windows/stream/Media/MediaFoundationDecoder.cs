using System.Runtime.InteropServices;
using Vortice.MediaFoundation;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace GnomeNetworkDisplays.Stream.Media;

public class MediaFoundationDecoder : IDisposable
{
    private IMFMediaSession? _mediaSession;
    private IMFMediaSource? _mediaSource;
    private IMFVideoDisplayControl? _videoDisplayControl;
    private IMFTopology? _currentTopology;

    private ID3D11Device? _d3dDevice;
    private ID3D11DeviceContext? _d3dContext;

    private TransportStreamReceiver? _tsReceiver;
    private bool _isPaused;
    private bool _isInitialized;

    // Buffers for decoded frames
    private readonly object _lock = new();
    private IntPtr _currentVideoTexturePtr;
    private long _currentPosition;

    public event Action<IntPtr, int, int>? OnVideoFrameAvailable;
    public event Action<string>? OnError;

    public bool IsInitialized => _isInitialized;

    public void Initialize(TransportStreamReceiver tsReceiver)
    {
        _tsReceiver = tsReceiver;

        // Initialize Media Foundation
        int hr = MFExtern.MFStartup(MFVersion.Version_2_0, MFStartup.Full);
        if (hr != 0)
        {
            // Try version 1_0 if 2_0 fails
            hr = MFExtern.MFStartup(MFVersion.Version_1_0, MFStartup.Full);
            if (hr != 0)
            {
                throw new InvalidOperationException($"Media Foundation startup failed: 0x{hr:X8}");
            }
            Console.WriteLine("Using Media Foundation version 1.0");
        }
        else
        {
            Console.WriteLine("Media Foundation initialized (v2.0)");
        }

        // Subscribe to TS packets
        _tsReceiver.OnTsPacketReceived += ProcessTsPacket;

        _isInitialized = true;
    }

    public void CreateTopology(IMFMediaSource source, IntPtr renderTarget)
    {
        // Create presentation descriptor
        source.CreatePresentationDescriptor(out var presentationDescriptor);

        // Get number of streams
        presentationDescriptor.GetStreamDescriptorCount(out int streamCount);

        var topology = MFExtern.CreateTopology();

        for (int i = 0; i < streamCount; i++)
        {
            presentationDescriptor.GetStreamDescriptorByIndex(i, out bool selected, out var streamDescriptor);

            if (!selected)
            {
                streamDescriptor.Dispose();
                continue;
            }

            // Get media type handler
            streamDescriptor.MediaTypeHandler(out var mediaTypeHandler);
            mediaTypeHandler.GetMajorType(out var majorType);

            if (majorType == MediaTypes.MFMediaType_Video)
            {
                // Create video branch
                AddVideoBranch(topology, source, streamDescriptor, renderTarget);
            }
            else if (majorType == MediaTypes.MFMediaType_Audio)
            {
                // Create audio branch
                AddAudioBranch(topology, source, streamDescriptor);
            }

            mediaTypeHandler.Dispose();
            streamDescriptor.Dispose();
        }

        presentationDescriptor.Dispose();

        _currentTopology = topology;
    }

    private void AddVideoBranch(IMFTopology topology, IMFMediaSource source,
        IMFStreamDescriptor streamDescriptor, IntPtr renderTarget)
    {
        // Get the node for the source
        var sourceNode = MFExtern.CreateSourceStreamNode(
            source,
            presentationDescriptor: null!, // Will be set when adding to session
            streamDescriptor);

        // Create output node for video renderer
        var rendererActivate = MFExtern.MFCreateVideoRendererActivate(renderTarget, IntPtr.Zero);
        rendererActivate.ActivateObject(typeof(IMFVideoRenderer).GUID, out var renderer);
        rendererActivate.Dispose();

        var sinkNode = MFExtern.CreateOutputNode(
            renderer,
            streamDescriptor);

        // Add nodes to topology
        topology.AddNode(sourceNode);
        topology.AddNode(sinkNode);

        // Connect nodes
        topology.ConnectOutputNode(sourceNode, 0, sinkNode, 0);

        sourceNode.Dispose();
        sinkNode.Dispose();
        renderer.Dispose();
    }

    private void AddAudioBranch(IMFTopology topology, IMFMediaSource source,
        IMFStreamDescriptor streamDescriptor)
    {
        // Similar to video but for audio
        var sourceNode = MFExtern.CreateSourceStreamNode(
            source,
            presentationDescriptor: null!,
            streamDescriptor);

        // Create audio renderer
        var audioRenderer = MFExtern.MFCreateAudioRendererActivate();

        audioRenderer.ActivateObject(typeof(IMFAudioRenderer).GUID, out var audioSink);
        audioRenderer.Dispose();

        var sinkNode = MFExtern.CreateOutputNode(
            audioSink,
            streamDescriptor);

        topology.AddNode(sourceNode);
        topology.AddNode(sinkNode);

        topology.ConnectOutputNode(sourceNode, 0, sinkNode, 0);

        sourceNode.Dispose();
        sinkNode.Dispose();
        audioSink.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_mediaSession == null)
        {
            // Create media session
            var attributes = MFExtern.MFCreateAttributes<IMFAttributes>();
            attributes.Set(MFAttribute. MF_ATTRIBUTE_MF, 1);
            attributes.Set(MFAttribute.MF_MF_SESSION_SENDABLE, 1);

            _mediaSession = MFExtern.MFCreateSession(attributes);
            attributes.Dispose();

            // Set up event handlers
            _mediaSession.BeginGetEvent(MediaSessionEventCallback, null);
        }

        if (_currentTopology != null)
        {
            await _mediaSession.SetTopologyAsync(_currentTopology);
            await _mediaSession.StartAsync(Guid.Empty, null);
        }

        _isPaused = false;
    }

    public void Pause()
    {
        if (_mediaSession != null && !_isPaused)
        {
            _mediaSession.Pause();
            _isPaused = true;
        }
    }

    public void Resume()
    {
        if (_mediaSession != null && _isPaused)
        {
            _mediaSession.Start(Guid.Empty, null);
            _isPaused = false;
        }
    }

    public void Stop()
    {
        if (_mediaSession != null)
        {
            _mediaSession.Stop();
            _isPaused = false;
        }
    }

    private void ProcessTsPacket(byte[] packet)
    {
        // Parse PID from TS packet
        ushort pid = (ushort)(((packet[1] & 0x1F) << 8) | packet[2]);

        if (_tsReceiver?.CurrentPmt != null)
        {
            var videoStream = _tsReceiver.CurrentPmt.Streams
                .FirstOrDefault(s => s.StreamType == 0x1b || s.StreamType == 0x24); // H.264 or H.265

            if (videoStream != null && pid == videoStream.ElementaryPid)
            {
                ProcessVideoPacket(packet);
            }
        }
    }

    private void ProcessVideoPacket(byte[] packet)
    {
        lock (_lock)
        {
            // Extract PES payload from TS packet
            // In a full implementation, this would:
            // 1. Reassemble TS packets into PES packets
            // 2. Extract NAL units from the PES payload
            // 3. Feed NAL units to the H.264/H.265 decoder

            // For now, pass raw access units to the decoder
            if (_mediaSource == null)
            {
                // No active source - log
                return;
            }
        }
    }

    public IntPtr GetVideoSurface()
    {
        return _currentVideoTexturePtr;
    }

    public long GetCurrentPosition()
    {
        if (_mediaSession != null)
        {
            try
            {
                _mediaSession.GetClock(out var clock);
                clock.GetCorrelatedTime(0, out var position);
                clock.Dispose();
                return position;
            }
            catch
            {
                // Ignore clock errors
            }
        }
        return _currentPosition;
    }

    private void MediaSessionEventCallback(IMFMediaEvent mediaEvent, IntPtr callbackState)
    {
        mediaEvent.GetType(out var eventType);
        mediaEvent.GetStatus(out var status);

        Console.WriteLine($"Media session event: {eventType}, status: 0x{status:X8}");

        switch (eventType)
        {
            case MediaEventTypes.MESessionStarted:
                Console.WriteLine("Session started");
                break;

            case MediaEventTypes.MESessionPaused:
                Console.WriteLine("Session paused");
                break;

            case MediaEventTypes.MESessionStopped:
                Console.WriteLine("Session stopped");
                break;

            case MediaEventTypes.MESessionEnded:
                Console.WriteLine("Session ended");
                OnError?.Invoke("Stream ended");
                break;

            case MediaEventTypes.MEError:
                Console.WriteLine($"Media error: 0x{status:X8}");
                OnError?.Invoke($"Media error: 0x{status:X8}");
                break;

            case MediaEventTypes.MEQualityAdvise:
                Console.WriteLine("Quality advise event");
                break;
        }

        mediaEvent.Dispose();
    }

    private void CleanupTopology()
    {
        if (_currentTopology != null)
        {
            // Disconnect all nodes
            try
            {
                _currentTopology.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
            _currentTopology = null;
        }
    }

    public void Dispose()
    {
        CleanupTopology();

        if (_mediaSession != null)
        {
            try
            {
                _mediaSession.Close();
                _mediaSession.Shutdown();
                _mediaSession.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
            _mediaSession = null;
        }

        if (_tsReceiver != null)
        {
            _tsReceiver.OnTsPacketReceived -= ProcessTsPacket;
            _tsReceiver = null;
        }

        _d3dContext?.Dispose();
        _d3dDevice?.Dispose();

        try
        {
            MFExtern.MFShutdown();
        }
        catch
        {
            // Ignore shutdown errors
        }

        _isInitialized = false;
    }
}

// Extension methods for Media Foundation interop
internal static class MFExtensions
{
    public static T ActivateObject<T>(this IMFActivate activate, Guid riid) where T : class
    {
        activate.ActivateObject(riid, out var unk);
        try
        {
            return unk.QueryInterface<T>();
        }
        finally
        {
            Marshal.Release(unk);
        }
    }
}

// Custom attributes for Media Foundation
internal static class MFAttribute
{
    public static readonly Guid MF_ATTRIBUTE_MF = new("45维亚-8C27-4373-8F1D-08C6DC5A82E5");
    public static readonly Guid MF_MF_SESSION_SENDABLE = new("ad8c8f8-1b6c-4580-8168-66e0dcde919e");
}
