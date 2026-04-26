using System;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gnd.Windows.Client.ViewModels;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _mediaPlayer;
    private bool _disposed;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private VideoView? _videoView;

    public MediaPlayer MediaPlayer => _mediaPlayer;

    public PlayerViewModel()
    {
        // Initialize LibVLC
        Core.Initialize();

        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);

        // Subscribe to media player events
        _mediaPlayer.Playing += OnPlaying;
        _mediaPlayer.Paused += OnPaused;
        _mediaPlayer.Stopped += OnStopped;
        _mediaPlayer.EndReached += OnEndReached;
        _mediaPlayer.EncounteredError += OnError;
    }

    public void AttachVideoView(VideoView videoView)
    {
        VideoView = videoView;
        videoView.MediaPlayer = _mediaPlayer;
    }

    private void OnPlaying(object? sender, EventArgs e)
    {
        IsPlaying = true;
        IsPaused = false;
        StatusText = "Playing";
    }

    private void OnPaused(object? sender, EventArgs e)
    {
        IsPlaying = false;
        IsPaused = true;
        StatusText = "Paused";
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        IsPlaying = false;
        IsPaused = false;
        StatusText = "Stopped";
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        IsPlaying = false;
        IsPaused = false;
        StatusText = "Playback ended";
    }

    private void OnError(object? sender, EventArgs e)
    {
        IsPlaying = false;
        IsPaused = false;
        StatusText = "Playback error";
    }

    [RelayCommand]
    private void Play()
    {
        if (_mediaPlayer.CanPlay)
        {
            _mediaPlayer.Play();
        }
    }

    [RelayCommand]
    private void Pause()
    {
        if (_mediaPlayer.CanPause)
        {
            _mediaPlayer.Pause();
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _mediaPlayer.Stop();
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
            StatusText = "Loading...";
            IsPlaying = false;
            IsPaused = false;

            using var media = new Media(_libVLC, new Uri(url), FromType.FromLocation);

            // Wait for media to be ready
            await media.Parse(MediaParseOptions.ParseLocal);

            _mediaPlayer.Media = media;
            _mediaPlayer.Play();

            StatusText = $"Playing: {url}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            IsPlaying = false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _mediaPlayer.Playing -= OnPlaying;
            _mediaPlayer.Paused -= OnPaused;
            _mediaPlayer.Stopped -= OnStopped;
            _mediaPlayer.EndReached -= OnEndReached;
            _mediaPlayer.EncounteredError -= OnError;

            _mediaPlayer.Stop();
            _mediaPlayer.Dispose();
            _libVLC.Dispose();

            _disposed = true;
        }
    }
}
