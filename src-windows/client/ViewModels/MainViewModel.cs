using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gnd.Windows.Client.Services;

namespace Gnd.Windows.Client.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;

    [ObservableProperty]
    private ObservableCollection<DeviceViewModel> _devices = new();

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isDiscovering;

    public MainViewModel(IServiceClient serviceClient)
    {
        _serviceClient = serviceClient;
        _serviceClient.DeviceDiscovered += OnDeviceDiscovered;
        _serviceClient.DeviceLost += OnDeviceLost;
    }

    private void OnDeviceDiscovered(object? sender, DeviceInfo e)
    {
        // Dispatch to UI thread
        App.Current?.DispatcherQueue?.TryEnqueue(() =>
        {
            // Check if device already exists
            foreach (var device in Devices)
            {
                if (device.Id == e.Id)
                    return;
            }
            Devices.Add(new DeviceViewModel(e));
        });
    }

    private void OnDeviceLost(object? sender, string e)
    {
        App.Current?.DispatcherQueue?.TryEnqueue(() =>
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
        });
    }

    [RelayCommand]
    private async Task RefreshDevices()
    {
        StatusText = "Refreshing devices...";
        try
        {
            var devices = await _serviceClient.GetDevicesAsync();
            await App.Current!.DispatcherQueue!.TryEnqueueAsync(() =>
            {
                Devices.Clear();
                foreach (var d in devices)
                {
                    Devices.Add(new DeviceViewModel(d));
                }
                StatusText = $"Found {devices.Count} device(s)";
            });
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
        device.State = DeviceState.Connecting;

        try
        {
            await _serviceClient.ConnectAsync(device.Id);
            device.State = DeviceState.Connected;
            StatusText = $"Connected to {device.Name}";
        }
        catch (Exception ex)
        {
            device.State = DeviceState.Error;
            StatusText = $"Failed to connect: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }
}