using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gnd.Windows.Shared;

namespace Gnd.Windows.Client.ViewModels;

public partial class DeviceViewModel : ObservableObject
{
    private readonly DeviceInfo _deviceInfo;

    public string Id => _deviceInfo.Id;
    public string Name => _deviceInfo.Name;
    public string Address => _deviceInfo.IpAddress;
    public DeviceType Type => _deviceInfo.Type;

    [ObservableProperty]
    private DeviceState _state;

    [ObservableProperty]
    private bool _canConnect = true;

    public string TypeDisplay => Type switch
    {
        DeviceType.Miracast => "Wireless Display (Miracast)",
        DeviceType.Chromecast => "Chromecast",
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
        _canConnect = State != DeviceState.Connecting;
    }

    [RelayCommand]
    private async Task Connect()
    {
        // Connection logic handled by MainViewModel
        await Task.Yield();
    }
}