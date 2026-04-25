using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gnd.Windows.Client.Services;

namespace Gnd.Windows.Client.ViewModels;

public enum DeviceType
{
    Unknown,
    Sink,
    Source
}

public enum DeviceState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public partial class DeviceViewModel : ObservableObject
{
    private readonly DeviceInfo _deviceInfo;

    public string Id => _deviceInfo.Id;
    public string Name => _deviceInfo.Name;
    public DeviceType Type => _deviceInfo.Type;
    public string Address => _deviceInfo.Address;

    [ObservableProperty]
    private DeviceState _state;

    [ObservableProperty]
    private bool _canConnect = true;

    public string TypeDisplay => Type switch
    {
        DeviceType.Sink => "Wireless Display (Sink)",
        DeviceType.Source => "Media Source",
        _ => "Unknown Device"
    };

    public string ConnectButtonText => State switch
    {
        DeviceState.Connected => "Disconnect",
        DeviceState.Connecting => "Connecting...",
        DeviceState.Error => "Retry",
        _ => "Connect"
    };

    public bool IsConnected => State == DeviceState.Connected;

    public DeviceViewModel(DeviceInfo deviceInfo)
    {
        _deviceInfo = deviceInfo;
        _state = deviceInfo.State;
        _canConnect = deviceInfo.IsConnectable && State != DeviceState.Connecting;
    }

    [RelayCommand]
    private async Task Connect()
    {
        // Connection logic handled by MainViewModel
        await Task.Yield();
    }
}