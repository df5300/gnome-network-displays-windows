# Avalonia UI Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace WinUI 3 client with Avalonia UI, enabling cross-platform compilation on Linux

**Architecture:** Single Avalonia application replacing both client and stream WinUI 3 projects. Platform APIs use interfaces for future cross-platform support.

**Tech Stack:** Avalonia UI 11.x, CommunityToolkit.Mvvm (retained), gRPC, LibVLCSharp for video playback

---

## File Structure

### New Project Layout
```
src-windows/
├── gnome-network-displays.sln          # Updated
├── shared/                             # Unchanged
│   └── Platform/                      # Add interfaces
│       ├── IDeviceDiscovery.cs         # NEW
│       ├── IFirewallManager.cs         # NEW
│       └── IWLANManager.cs             # NEW
├── service/                           # Unchanged
├── client/                            # Complete rewrite
│   ├── gnome-network-displays.client.csproj  # Avalonia
│   ├── App.axaml                      # NEW
│   ├── App.axaml.cs                   # NEW
│   ├── MainWindow.axaml               # NEW
│   ├── MainWindow.axaml.cs            # NEW
│   ├── ViewModels/
│   │   ├── MainViewModel.cs           # MODIFY (existing)
│   │   ├── DeviceViewModel.cs          # MODIFY (existing)
│   │   └── PlayerViewModel.cs          # NEW
│   ├── Services/
│   │   ├── GrpcServiceClient.cs        # RETAIN (cleanup)
│   │   └── IServiceClient.cs           # RETAIN
│   └── Views/
│       ├── DeviceListView.axaml       # NEW
│       └── PlayerView.axaml            # NEW
└── stream/                            # DELETE (merged)
```

### Files to Delete
- `src-windows/stream/` - entire directory
- `src-windows/client/App.xaml` - WinUI version
- `src-windows/client/App.xaml.cs` - WinUI version
- `src-windows/client/MainWindow.xaml` - WinUI version
- `src-windows/client/MainWindow.xaml.cs` - WinUI version
- `src-windows/client/gnome-network-displays.client.csproj` - WinUI version

---

## Task 1: Create Avalonia Project

**Files:**
- Create: `src-windows/client/gnome-network-displays.client.csproj`

- [ ] **Step 1: Create new Avalonia csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Platforms>x64;ARM64</Platforms>
    <RuntimeIdentifiers>win-x64;linux-x64;osx-x64</RuntimeIdentifiers>
  </PropertyGroup>

  <ItemGroup>
    <AvaloniaXaml ItemGroup="${None}" />
    <Compile Update="**\*.axaml.cs" DependentUpon="%(Filename)" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.2.0" />
    <PackageReference Include="Avalonia.Desktop" Version="11.2.0" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.0" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="Grpc.Net.Client" Version="2.60.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="LibVLCSharp" Version="3.8.5" />
    <PackageReference Include="LibVLCSharp.Avalonia" Version="3.8.5" />
    <PackageReference Include="VideoLAN.LibVLC.Windows" Version="3.0.20" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\shared\gnome-network-displays.shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Protobuf Include="..\shared\Proto\gnd.proto" GrpcServices="Client" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Run restore to verify dependencies**

Run: `dotnet restore src-windows/client/gnome-network-displays.client.csproj`
Expected: No errors, packages restored

- [ ] **Step 3: Commit**

```bash
git add src-windows/client/gnome-network-displays.client.csproj
git commit -m "feat(client): start Avalonia migration - new csproj"
```

---

## Task 2: Create Avalonia App and MainWindow

**Files:**
- Create: `src-windows/client/App.axaml`
- Create: `src-windows/client/App.axaml.cs`
- Create: `src-windows/client/MainWindow.axaml`
- Create: `src-windows/client/MainWindow.axaml.cs`

- [ ] **Step 1: Create App.axaml**

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Gnd.Windows.Client.App">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>

  <Application.Resources>
    <ResourceDictionary>
      <SolidColorBrush x:Key="PrimaryBrush" Color="#0078D4" />
      <SolidColorBrush x:Key="BackgroundBrush" Color="#1A1A1A" />
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 2: Create App.axaml.cs**

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Gnd.Windows.Client.ViewModels;
using Gnd.Windows.Client.Services;

namespace Gnd.Windows.Client;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var serviceClient = new GrpcServiceClient("http://localhost:5050");
            var mainViewModel = new MainViewModel(serviceClient);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 3: Create MainWindow.axaml**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Gnd.Windows.Client.ViewModels"
        x:Class="Gnd.Windows.Client.MainWindow"
        Title="Gnome Network Displays"
        Width="800"
        Height="600"
        Background="{DynamicResource BackgroundBrush}">

  <Design.DataContext>
    <vm:MainViewModel />
  </Design.DataContext>

  <DockPanel>
    <!-- Header -->
    <Border DockPanel.Dock="Top" Background="#2D2D2D" Padding="16,8">
      <TextBlock Text="Network Displays" FontSize="20" FontWeight="Bold" Foreground="White" />
    </Border>

    <!-- Main Content -->
    <Grid Margin="16">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" MinWidth="300" />
        <ColumnDefinition Width="8" />
        <ColumnDefinition Width="*" MinWidth="300" />
      </Grid.ColumnDefinitions>

      <!-- Device List Panel -->
      <Border Grid.Column="0" Background="#2D2D2D" CornerRadius="8" Padding="16">
        <DockPanel>
          <TextBlock DockPanel.Dock="Top" Text="Available Devices" FontSize="16" FontWeight="SemiBold" Foreground="White" Margin="0,0,0,16" />

          <!-- Action Buttons -->
          <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,16,0,0">
            <Button Content="Refresh" Command="{Binding RefreshDevicesCommand}" Margin="0,0,8,0" />
            <Button Content="Start Discovery" Command="{Binding StartDiscoveryCommand}" />
          </StackPanel>

          <!-- Device List -->
          <ListBox ItemsSource="{Binding Devices}" SelectedItem="{Binding SelectedDevice}" Background="Transparent">
            <ListBox.ItemTemplate>
              <DataTemplate x:DataType="vm:DeviceViewModel">
                <Border Padding="8" Margin="0,4">
                  <Grid ColumnDefinitions="Auto,*,Auto">
                    <TextBlock Grid.Column="0" Text="📺" FontSize="24" VerticalAlignment="Center" Margin="0,0,12,0" />
                    <StackPanel Grid.Column="1" VerticalAlignment="Center">
                      <TextBlock Text="{Binding Name}" FontWeight="SemiBold" Foreground="White" />
                      <TextBlock Text="{Binding TypeDisplay}" Opacity="0.7" Foreground="#AAAAAA" />
                    </StackPanel>
                    <Button Grid.Column="2" Content="{Binding ConnectButtonText}" Command="{Binding ConnectCommand}" />
                  </Grid>
                </Border>
              </DataTemplate>
            </ListBox.ItemTemplate>
          </ListBox>
        </DockPanel>
      </Border>

      <!-- Splitter -->
      <GridSplitter Grid.Column="1" Background="Transparent" />

      <!-- Player Panel -->
      <Border Grid.Column="2" Background="#2D2D2D" CornerRadius="8" Padding="16">
        <DockPanel>
          <TextBlock DockPanel.Dock="Top" Text="Video Player" FontSize="16" FontWeight="SemiBold" Foreground="White" Margin="0,0,0,16" />

          <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,16,0,0">
            <Button Content="Play" Command="{Binding PlayerViewModel.PlayCommand}" Margin="0,0,8,0" />
            <Button Content="Pause" Command="{Binding PlayerViewModel.PauseCommand}" Margin="0,0,8,0" />
            <Button Content="Stop" Command="{Binding PlayerViewModel.StopCommand}" />
          </StackPanel>

          <!-- Video View Container -->
          <Border Background="Black" CornerRadius="4">
            <Panel>
              <TextBlock Text="No video playing" Foreground="#666666" HorizontalAlignment="Center" VerticalAlignment="Center" />
              <ContentControl x:Name="VideoView" />
            </Panel>
          </Border>
        </DockPanel>
      </Border>
    </Grid>
  </DockPanel>
</Window>
```

- [ ] **Step 4: Create MainWindow.axaml.cs**

```csharp
using Avalonia.Controls;
using Gnd.Windows.Client.ViewModels;

namespace Gnd.Windows.Client;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainViewModel vm)
        {
            // Connect video view to player
            if (vm.PlayerViewModel != null)
            {
                VideoView.Content = vm.PlayerViewModel.VideoView;
            }
        }
    }
}
```

- [ ] **Step 5: Verify build**

Run: `dotnet build src-windows/client/gnome-network-displays.client.csproj -c Debug`
Expected: Build succeeds (warnings ok)

- [ ] **Step 6: Commit**

```bash
git add src-windows/client/App.axaml src-windows/client/App.axaml.cs
git add src-windows/client/MainWindow.axaml src-windows/client/MainWindow.axaml.cs
git commit -m "feat(client): add Avalonia App and MainWindow"
```

---

## Task 3: Migrate ViewModels

**Files:**
- Modify: `src-windows/client/ViewModels/DeviceViewModel.cs`
- Modify: `src-windows/client/ViewModels/MainViewModel.cs`
- Create: `src-windows/client/ViewModels/PlayerViewModel.cs`

- [ ] **Step 1: Create PlayerViewModel.cs**

```csharp
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;

namespace Gnd.Windows.Client.ViewModels;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private bool _disposed;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _statusText = "Stopped";

    [ObservableProperty]
    private VideoView? _videoView;

    public PlayerViewModel()
    {
        Core.Initialize();

        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);

        _mediaPlayer.Playing += (s, e) =>
        {
            IsPlaying = true;
            IsPaused = false;
            StatusText = "Playing";
        };

        _mediaPlayer.Paused += (s, e) =>
        {
            IsPlaying = false;
            IsPaused = true;
            StatusText = "Paused";
        };

        _mediaPlayer.Stopped += (s, e) =>
        {
            IsPlaying = false;
            IsPaused = false;
            StatusText = "Stopped";
        };

        // Create video view for rendering
        VideoView = new VideoView
        {
            MediaPlayer = _mediaPlayer
        };
    }

    [RelayCommand]
    private void Play()
    {
        if (_mediaPlayer != null && !IsPlaying)
        {
            _mediaPlayer.Play();
        }
    }

    [RelayCommand]
    private void Pause()
    {
        if (_mediaPlayer != null && IsPlaying)
        {
            _mediaPlayer.Pause();
        }
    }

    [RelayCommand]
    private void Stop()
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Stop();
        }
    }

    public async Task PlayUrlAsync(string url)
    {
        if (_libVLC == null || _mediaPlayer == null)
            return;

        Stop();

        _currentMedia?.Dispose();
        _currentMedia = new Media(_libVLC, new Uri(url), FromType.FromLocation);

        _mediaPlayer.Media = _currentMedia;
        _mediaPlayer.Play();

        StatusText = $"Connecting to {url}...";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _currentMedia?.Dispose();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            _disposed = true;
        }
    }
}
```

- [ ] **Step 2: Update DeviceViewModel.cs**

```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Gnd.Windows.Client.Services;

namespace Gnd.Windows.Client.ViewModels;

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
}
```

- [ ] **Step 3: Update MainViewModel.cs**

```csharp
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
    private DeviceViewModel? _selectedDevice;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isDiscovering;

    public PlayerViewModel PlayerViewModel { get; } = new();

    public MainViewModel(IServiceClient serviceClient)
    {
        _serviceClient = serviceClient;
        _serviceClient.DeviceDiscovered += OnDeviceDiscovered;
        _serviceClient.DeviceLost += OnDeviceLost;
    }

    private void OnDeviceDiscovered(object? sender, DeviceInfo e)
    {
        // Note: Avalonia dispatches to UI thread automatically via data binding
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

            // Start stream playback
            await PlayerViewModel.PlayUrlAsync($"rtsp://{device.Address}:7236/wfd1.0");
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
```

- [ ] **Step 4: Verify build**

Run: `dotnet build src-windows/client/gnome-network-displays.client.csproj -c Debug`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add src-windows/client/ViewModels/
git commit -m "feat(client): migrate ViewModels to Avalonia with PlayerViewModel"
```

---

## Task 4: Clean Up and Delete Old Files

**Files:**
- Delete: `src-windows/stream/` (entire directory)
- Delete: `src-windows/client/App.xaml` (WinUI version)
- Delete: `src-windows/client/App.xaml.cs` (WinUI version)
- Delete: `src-windows/client/MainWindow.xaml` (WinUI version)
- Delete: `src-windows/client/MainWindow.xaml.cs` (WinUI version)

- [ ] **Step 1: Delete stream directory**

Run: `rm -rf src-windows/stream/`

- [ ] **Step 2: Delete WinUI client files**

Run: `rm -f src-windows/client/App.xaml src-windows/client/App.xaml.cs src-windows/client/MainWindow.xaml src-windows/client/MainWindow.xaml.cs`

- [ ] **Step 3: Update solution file**

The solution file references the old project. Update it to remove stream project references.

Run: `dotnet sln src-windows/gnome-network-displays.sln remove src-windows/stream/gnome-network-displays.stream.csproj`

- [ ] **Step 4: Verify clean build**

Run: `dotnet build src-windows/gnome-network-displays.sln -c Debug`
Expected: Build succeeds (warnings ok)

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(client): remove WinUI3 files and stream project, migrate to Avalonia"
```

---

## Task 5: Update Platform Interfaces (Future Proofing)

**Files:**
- Create: `src-windows/shared/Platform/IDeviceDiscovery.cs`
- Create: `src-windows/shared/Platform/IFirewallManager.cs`

- [ ] **Step 1: Create IDeviceDiscovery.cs**

```csharp
using System;
using System.Threading.Tasks;

namespace Gnd.Windows.Shared.Platform;

public interface IDeviceDiscovery
{
    Task StartAsync();
    Task StopAsync();
    event EventHandler<DeviceInfo>? DeviceFound;
    event EventHandler<string>? DeviceLost;
    bool IsRunning { get; }
}

public class DeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
}

public enum DeviceType
{
    Unknown,
    Miracast,
    Chromecast
}
```

- [ ] **Step 2: Create IFirewallManager.cs**

```csharp
using System.Threading.Tasks;

namespace Gnd.Windows.Shared.Platform;

public interface IFirewallManager
{
    Task<bool> AddRuleAsync(string name, int port, string protocol);
    Task<bool> RemoveRuleAsync(string name);
    Task<bool> IsEnabledAsync();
}
```

- [ ] **Step 3: Commit**

```bash
git add src-windows/shared/Platform/IDeviceDiscovery.cs src-windows/shared/Platform/IFirewallManager.cs
git commit -m "feat(shared): add platform abstraction interfaces"
```

---

## Task 6: Final Build Verification

- [ ] **Step 1: Full solution build**

Run: `dotnet build src-windows/gnome-network-displays.sln -c Debug`
Expected: All projects build successfully

- [ ] **Step 2: Test on Linux**

If you have a Linux environment available, verify the build works there too.

Run: `dotnet build src-windows/client/gnome-network-displays.client.csproj -c Debug -r linux-x64 --self-contained`
Expected: Builds successfully for Linux

- [ ] **Step 3: Commit final state**

```bash
git status
git commit -m "chore: Avalonia UI migration complete - cross-platform build enabled"
```

---

## Spec Coverage Checklist

- [x] Single Avalonia application replaces client + stream
- [x] Platform APIs abstracted via interfaces
- [x] MVVM pattern preserved (CommunityToolkit.Mvvm)
- [x] gRPC communication with service retained
- [x] Video playback via LibVLCSharp
- [x] Cross-platform build enabled (win-x64, linux-x64, osx-x64)

## Potential Issues

1. **LibVLC binary availability**: LibVLC needs platform-specific binaries. On Linux, install via system package manager or use `VideoLAN.LibVLC.*.runtime` packages.

2. **gRPC Named Pipes on Linux**: If using named pipes for IPC, may need adjustment (currently using localhost HTTP).

3. **Wi-Fi Direct**: Windows-only API. Discovery will only work on Windows initially.

---

## Execution Options

**Plan complete and saved to `docs/superpowers/plans/2026-04-26-avalonia-ui-migration.md`.**

Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
