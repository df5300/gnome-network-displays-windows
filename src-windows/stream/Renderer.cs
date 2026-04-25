using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Vortice.Direct3D11;
using Vortice.DirectX;
using Vortice.DXGI;
using Vortice.MediaFoundation;
using GnomeNetworkDisplays.Stream.Media;

namespace GnomeNetworkDisplays.Stream;

public sealed class RendererWindow : Window
{
    private MediaFoundationDecoder? _decoder;
    private ID3D11Device? _d3dDevice;
    private ID3D11DeviceContext? _d3dContext;
    private IDXGISwapChain? _swapChain;
    private ID3D11Texture2D? _renderTarget;
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _vertexBuffer;
    private ID3D11Texture2D? _videoTexture;
    private ID3D11ShaderResourceView? _videoTextureView;
    private bool _isInitialized;
    private bool _isVisible;
    private readonly object _lock = new();

    public RendererWindow()
    {
        InitializeWindow();
    }

    private void InitializeWindow()
    {
        Title = "Stream Renderer";
        Width = 1280;
        Height = 720;
        WindowStyle = WindowStyle.Normal;
        ResizeMode = ResizeMode.CanResize;

        // Set up background
        var backgroundGrid = new Grid();
        backgroundGrid.Background = new SolidColorBrush(Colors.Black);
        Content = backgroundGrid;

        // Create video surface placeholder
        var videoSurface = new Canvas
        {
            Name = "VideoSurface",
            Background = new SolidColorBrush(Colors.Black)
        };
        backgroundGrid.Children.Add(videoSurface);

        Closed += OnClosed;
    }

    public async Task InitializeAsync(MediaFoundationDecoder decoder, CancellationToken cancellationToken)
    {
        _decoder = decoder;

        await Task.Run(() =>
        {
            InitializeD3D();
            InitializeShaders();
            SetupVideoTexture();
            _isInitialized = true;
        }, cancellationToken);
    }

    private void InitializeD3D()
    {
        // Create D3D11 device with hardware acceleration
        var deviceFlags = Vortice.Direct3D11.DeviceCreationFlags.BgraSupport |
                          Vortice.Direct3D11.DeviceCreationFlags.VideoSupport;

        var featureLevel = D3D11Helpers.CreateDeviceAndContext(
            out _d3dDevice,
            out _d3dContext,
            deviceFlags);

        if (_d3dDevice == null || _d3dContext == null)
        {
            throw new InvalidOperationException("Failed to create D3D11 device");
        }

        Console.WriteLine($"D3D11 Device created with feature level: {featureLevel}");
    }

    private void InitializeShaders()
    {
        if (_d3dDevice == null) return;

        // Simple vertex shader for fullscreen quad
        byte[] vertexShaderData = new byte[]
        {
            0x44, 0x42, 0x42, 0x43, // Magic number (DXBC)
            // Simplified - in production, use compiled shaders
        };

        // For now, use a basic pass-through approach
        // Full shader code would be compiled from HLSL

        Console.WriteLine("Shaders initialized (placeholder - use compiled shaders in production)");
    }

    private void SetupVideoTexture()
    {
        if (_d3dDevice == null) return;

        // Create video texture for decoded frames
        var textureDesc = new Texture2DDescription
        {
            Width = 1920,
            Height = 1080,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.NV12,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = Vortice.Direct3D11.BindFlags.ShaderResource,
            CpuAccessFlags = CpuAccessFlags.None,
            MiscFlags = Vortice.Direct3D11.ResourceMiscFlags.Shared
        };

        _videoTexture = _d3dDevice.CreateTexture2D(textureDesc);

        var srvDesc = new ShaderResourceViewDescription
        {
            Format = textureDesc.Format,
            Dimension = ShaderResourceViewDimension.Texture2D,
            Texture2D = new Texture2DSRV
            {
                MipLevels = 1,
                MostDetailedMip = 0
            }
        };

        _videoTextureView = _d3dDevice.CreateShaderResourceView(_videoTexture, srvDesc);

        Console.WriteLine("Video texture created");
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

    public void PresentFrame(ID3D11Texture2D frame)
    {
        if (!_isInitialized || !_isVisible || _d3dContext == null)
            return;

        lock (_lock)
        {
            // Copy the decoded frame to our video texture
            // This would involve color conversion from NV12/I420 to RGBA
            // and proper handling of the video frame

            // Present to screen via swap chain
            if (_swapChain != null)
            {
                _swapChain.Present(1, PresentFlags.None);
            }
        }
    }

    public void UpdateVideoTexture(IntPtr texturePtr, int width, int height)
    {
        if (!_isInitialized || _d3dDevice == null || _d3dContext == null)
            return;

        lock (_lock)
        {
            try
            {
                var texture = new ID3D11Texture2D(texturePtr);

                // Copy from source texture to our render target
                _d3dContext.CopyResource(_videoTexture, texture);

                // Present
                if (_swapChain != null)
                {
                    _swapChain.Present(0, PresentFlags.None);
                }

                texture.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating video texture: {ex.Message}");
            }
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Cleanup();
    }

    private void Cleanup()
    {
        _isInitialized = false;

        _vertexBuffer?.Dispose();
        _inputLayout?.Dispose();
        _pixelShader?.Dispose();
        _vertexShader?.Dispose();
        _videoTextureView?.Dispose();
        _videoTexture?.Dispose();
        _renderTarget?.Dispose();
        _swapChain?.Dispose();
        _d3dContext?.Dispose();
        _d3dDevice?.Dispose();
    }
}

// Helper class for D3D device creation
internal static class D3D11Helpers
{
    [DllImport("user32.dll", SetLastError = false)]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", SetLastError = false)]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = false)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    public static FeatureLevel CreateDeviceAndContext(
        out ID3D11Device? device,
        out ID3D11DeviceContext? context,
        Vortice.Direct3D11.DeviceCreationFlags flags = Vortice.Direct3D11.DeviceCreationFlags.BgraSupport)
    {
        device = null;
        context = null;

        try
        {
            var result = D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                flags,
                new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 },
                out device,
                out context,
                out var featureLevel);

            if (result.Failure)
            {
                Console.WriteLine($"D3D11 device creation failed: {result}");
                return FeatureLevel.Unknown;
            }

            return featureLevel;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception creating D3D11 device: {ex.Message}");
            return FeatureLevel.Unknown;
        }
    }
}

// SwapChainPanel for WinUI 3 - placeholder implementation
public class SwapChainPanel : Canvas
{
    public void SetSwapChain(IDXGISwapChain swapChain)
    {
        // This would integrate the swap chain with the XAML surface
        Console.WriteLine("Swap chain set on panel");
    }
}
