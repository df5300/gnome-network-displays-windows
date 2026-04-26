# Avalonia UI 迁移设计

## 概述

将 `src-windows` 中的 WinUI 3 应用（client + stream）迁移到 Avalonia UI，实现跨平台编译。

**现状：**
- `client` - WinUI 3 GUI
- `stream` - WinUI 3 独立渲染进程
- 均依赖 Windows 专属 XamlCompiler

**目标：**
- 单一 Avalonia UI 应用
- 合并视频渲染到主窗口
- 平台 API 通过接口抽象

---

## 架构设计

### 项目结构

```
src-windows/
├── gnome-network-displays.sln
│
├── shared/                          # 保持不变
│   └── Platform/                    # 抽象接口
│       ├── IWiFiDirectDiscovery.cs
│       ├── IFirewallManager.cs
│       └── ...
│
├── service/                         # 保持不变
│   └── ...
│
├── client/                          # 重写为 Avalonia
│   ├── gnome-network-displays.client.csproj  # Avalonia 项目
│   ├── App.axaml
│   ├── MainWindow.axaml
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   ├── DeviceViewModel.cs
│   │   └── PlayerViewModel.cs       # 新增：视频播放控制
│   ├── Services/
│   │   └── GrpcServiceClient.cs    # 复用现有
│   └── Views/
│       ├── MainWindow.axaml
│       ├── DeviceListView.axaml
│       └── PlayerView.axaml         # 新增：视频渲染区域
│
└── stream/                          # 删除，功能合并到 client
```

### 平台 API 抽象

```csharp
// IWiFiDirectDiscovery.cs
public interface IWiFiDirectDiscovery
{
    Task StartDiscoveryAsync();
    Task StopDiscoveryAsync();
    event EventHandler<Device> DeviceFound;
    event EventHandler<string> DeviceLost;
}

// IFirewallManager.cs
public interface IFirewallManager
{
    Task<bool> AddRuleAsync(string name, int port);
    Task<bool> RemoveRuleAsync(string name);
}

// 在 shared 项目中创建接口，Windows 实现放在 service 或 client 中
```

### IPC 通信（保持不变）

- client → service: gRPC
- 视频流: RTSP client 内置于 client

---

## 实现步骤

### Step 1: 创建 Avalonia 项目

```
cd src-windows
dotnet new avalonia.app -n client -o client
```

修改 csproj:
```xml
<Project Sdk="Avalonia.App/0.10">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <!-- 多平台支持 -->
    <RuntimeIdentifiers>win-x64;linux-x64;osx-x64</RuntimeIdentifiers>
  </PropertyGroup>
</Project>
```

### Step 2: 迁移 UI

| WinUI 3 | Avalonia |
|---------|----------|
| `Page` | `UserControl` |
| `Grid` | `Grid` |
| `ListView` | `ListBox` |
| `TextBlock` | `TextBlock` |
| `Button` | `Button` |
| `Microsoft.UI.Xaml.Window` | `Window` |
| `SoftwareBitmap` | `WriteableBitmap` |

主要 ViewModels:
- `MainViewModel` - 应用状态、导航
- `DeviceViewModel` - 单个设备
- `PlayerViewModel` - 播放控制

### Step 3: 合并 stream 功能

`PlayerViewModel` 包含:
- RTSP 连接管理
- 视频帧解码（使用 libVLC 或 FFmpeg 自动播放）
- `WriteableBitmap` 渲染到 Image 控件

### Step 4: 平台 API 实现

暂时只实现 Windows 版本，接口在 shared，实现在 client：

```
shared/Platform/
├── IWiFiDirectDiscovery.cs
├── IFirewallManager.cs
└── IDeviceDiscovery.cs

client/Platform/Windows/
├── WiFiDirectDiscovery.cs
└── FirewallManager.cs
```

---

## 待删除文件

- `src-windows/stream/` 整个目录
- `src-windows/client/` 下所有 WinUI 3 文件（除了 Services/GrpcServiceClient.cs 复用）

---

## 风险与限制

1. **Wi-Fi Direct**: Windows 实现需要 WlanApi P/Invoke
2. **视频解码**: 需要跨平台解码库（LibVLC.Sharp 或 FFmpeg.AutoGen）
3. **Chromecast**: Bonjour 依赖 macOS/Linux 可能不同

---

## 成功标准

1. `dotnet build -c Debug` 在 Linux 成功
2. client 应用启动显示设备列表
3. 可以连接到 service 并发现设备
