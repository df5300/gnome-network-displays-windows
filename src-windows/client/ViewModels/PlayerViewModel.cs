using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gnd.Windows.Client.ViewModels;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _currentUrl = "";

    [ObservableProperty]
    private Bitmap? _currentFrame;

    [ObservableProperty]
    private bool _hasVideo;

    public PlayerViewModel()
    {
    }

    [RelayCommand]
    private void Play()
    {
        if (!string.IsNullOrEmpty(CurrentUrl))
        {
            IsPlaying = true;
            IsPaused = false;
            StatusText = $"Playing: {CurrentUrl}";
        }
    }

    [RelayCommand]
    private void Pause()
    {
        if (IsPlaying)
        {
            IsPaused = true;
            IsPlaying = false;
            StatusText = "Paused";
        }
    }

    [RelayCommand]
    private void Stop()
    {
        IsPlaying = false;
        IsPaused = false;
        HasVideo = false;
        CurrentFrame = null;
        StatusText = "Stopped";
    }

    [RelayCommand]
    private async Task PlayUrlAsync(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            StatusText = "No URL provided";
            return;
        }

        try
        {
            StatusText = $"Connecting to {url}...";
            CurrentUrl = url;

            // For now, just show connected state
            // Actual video rendering requires RTSP stream handling and decoding
            // which will be implemented separately
            HasVideo = true;
            IsPlaying = true;
            StatusText = $"Connected to stream (URL: {url})";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            IsPlaying = false;
            HasVideo = false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CurrentFrame?.Dispose();
            _disposed = true;
        }
    }
}
