using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gnd.Windows.Shared;
using Gnd.Windows.Client.Services;
using SharedDeviceInfo = Gnd.Windows.Shared.DeviceInfo;
using SharedDeviceState = Gnd.Windows.Shared.DeviceState;

namespace Gnd.Windows.Client.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly GrpcServiceClient _serviceClient;

    [ObservableProperty]
    private ObservableCollection<DeviceViewModel> _devices = new();

    [ObservableProperty]
    private DeviceViewModel? _selectedDevice;

    [ObservableProperty]
    private PlayerViewModel _playerViewModel;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isDiscovering;

    public MainViewModel(GrpcServiceClient serviceClient)
    {
        _serviceClient = serviceClient;
        _serviceClient.DeviceDiscovered += OnDeviceDiscovered;
        _serviceClient.DeviceLost += OnDeviceLost;
        _playerViewModel = new PlayerViewModel();
    }

    private void OnDeviceDiscovered(object? sender, SharedDeviceInfo e)
    {
        // Check if device already exists
        foreach (var device in Devices)
        {
            if (device.Id == e.Id)
                return;
        }
        Devices.Add(new DeviceViewModel(e));
    }

    private void OnDeviceLost(object? sender, string e)
    {
        DeviceViewModel? deviceToRemove = null;
        foreach (var device in Devices)
        {
            if (device.Id == e)
            {
                deviceToRemove = device;
                break;
            }
        }
        if (deviceToRemove != null)
            Devices.Remove(deviceToRemove);
    }

    [RelayCommand]
    private async Task RefreshDevices()
    {
        StatusText = "Refreshing devices...";
        try
        {
            var devices = await _serviceClient.GetDevicesAsync();
            Devices.Clear();
            foreach (var d in devices)
            {
                Devices.Add(new DeviceViewModel(d));
            }
            StatusText = $"Found {devices.Count} device(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartDiscovery()
    {
        StatusText = "Starting discovery...";
        IsDiscovering = true;
        try
        {
            await _serviceClient.StartDiscoveryAsync();
            StatusText = "Discovery started";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            IsDiscovering = false;
        }
    }

    [RelayCommand]
    private async Task StopDiscovery()
    {
        try
        {
            await _serviceClient.StopDiscoveryAsync();
            IsDiscovering = false;
            StatusText = "Discovery stopped";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ConnectToDevice(DeviceViewModel? device)
    {
        if (device == null)
            return;

        IsConnecting = true;
        StatusText = $"Connecting to {device.Name}...";
        device.State = SharedDeviceState.Connecting;

        try
        {
            await _serviceClient.ConnectAsync(device.Id);
            device.State = SharedDeviceState.Connected;
            StatusText = $"Connected to {device.Name}";

            // Stream URL would be obtained and passed to PlayerViewModel
            // StartStreamAsync(device.Id) would be called here when needed
        }
        catch (Exception ex)
        {
            device.State = SharedDeviceState.Error;
            StatusText = $"Failed to connect: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }
}