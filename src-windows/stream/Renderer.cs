using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.MediaFoundation;
using GnomeNetworkDisplays.Stream.Media;

namespace GnomeNetworkDisplays.Stream;

/// <summary>
/// Video renderer window using WinUI 3 and Direct3D
/// </summary>
public sealed class RendererWindow : Microsoft.UI.Xaml.Window
{
    private MediaFoundationDecoder? _decoder;
    private ID3D11Device? _d3dDevice;
    private ID3D11DeviceContext? _d3dContext;
    private IDXGISwapChain1? _swapChain;
    private ID3D11Texture2D? _renderTarget;
    private ID3D11VideoDevice? _videoDevice;
    private ID3D11VideoContext? _videoContext;
    private bool _isInitialized;
    private bool _isVisible;
    private readonly object _lock = new();

    private Canvas? _videoCanvas;
    private Image? _videoImage;
    private SoftwareBitmap? _currentFrame;

    public RendererWindow()
    {
        InitializeWindow();
    }

    private void InitializeWindow()
    {
        Title = "Network Displays - Streaming";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);

        // Create main grid
        var mainGrid = new Grid();
        mainGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Black);
        Content = mainGrid;

        // Create video canvas
        _videoCanvas = new Canvas
        {
            Name = "VideoCanvas",
            Background = new SolidColorBrush(Microsoft.UI.Colors.Black)
        };
        mainGrid.Children.Add(_videoCanvas);

        // Create video image (for SoftwareBitmap rendering)
        _videoImage = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center
        };
        _videoCanvas.Children.Add(_videoImage);

        Closed += OnClosed;
    }

    public async Task InitializeAsync(MediaFoundationDecoder decoder, CancellationToken cancellationToken)
    {
        _decoder = decoder;
        _decoder.OnVideoFrame += OnVideoFrame;

        await Task.Run(() =>
        {
            InitializeD3D();
            _isInitialized = true;
        }, cancellationToken);
    }

    private void InitializeD3D()
    {
        try
        {
            // Create D3D11 device with video support
            var deviceFlags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport;

            var result = D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                deviceFlags,
                new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 },
                out _d3dDevice,
                out _d3dContext,
                out var featureLevel);

            if (result.Failure || _d3dDevice == null || _d3dContext == null)
            {
                System.Diagnostics.Debug.WriteLine($"D3D11 device creation failed: {result}");
                return;
            }

            // Get video device/context if available
            _videoDevice = _d3dDevice.QueryInterface<ID3D11VideoDevice>();
            _videoContext = _d3dContext.QueryInterface<ID3D11VideoContext>();

            System.Diagnostics.Debug.WriteLine($"D3D11 Device created with feature level: {featureLevel}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing D3D: {ex.Message}");
        }
    }

    public void Show()
    {
        _isVisible = true;
        Activate();
    }

    public void Hide()
    {
        _isVisible = false;
    }

    private void OnVideoFrame(object? sender, VideoFrameEventArgs e)
    {
        if (!_isInitialized || !_isVisible)
            return;

        try
        {
            // Convert the video frame to SoftwareBitmap for display
            var softwareBitmap = e.Frame as SoftwareBitmap;
            if (softwareBitmap != null && _videoImage != null)
            {
                // Update UI on UI thread
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    try
                    {
                        var bitmapSource = new Microsoft.UI.Xaml.Media.Imaging.SoftwareBitmapSource();
                        bitmapSource.SetBitmapAsync(softwareBitmap).AsTask().Wait();
                        _videoImage.Source = bitmapSource;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error displaying frame: {ex.Message}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling video frame: {ex.Message}");
        }
    }

    public void PresentFrame(ID3D11Texture2D frame)
    {
        if (!_isInitialized || !_isVisible || _d3dContext == null)
            return;

        lock (_lock)
        {
            try
            {
                // Copy the frame to render target
                if (_renderTarget != null)
                {
                    _d3dContext.CopyResource(_renderTarget, frame);
                }

                // Present
                if (_swapChain != null)
                {
                    _swapChain.Present(1, PresentFlags.None);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error presenting frame: {ex.Message}");
            }
        }
    }

    public void UpdateVideoSize(int width, int height)
    {
        if (_videoCanvas == null)
            return;

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            if (_videoCanvas != null)
            {
                _videoCanvas.Width = width;
                _videoCanvas.Height = height;
            }
        });
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Cleanup();
    }

    private void Cleanup()
    {
        _isInitialized = false;

        _decoder?.Stop();

        _renderTarget?.Dispose();
        _swapChain?.Dispose();
        _videoContext?.Dispose();
        _videoDevice?.Dispose();
        _d3dContext?.Dispose();
        _d3dDevice?.Dispose();
        _currentFrame?.Dispose();
    }
}

/// <summary>
/// Event args for video frame callback
/// </summary>
public class VideoFrameEventArgs : EventArgs
{
    public object? Frame { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long Timestamp { get; set; }
}
