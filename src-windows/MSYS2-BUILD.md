# MSYS2 Windows 构建指南

本指南说明如何使用 MSYS2 在 Windows 上交叉编译 gnome-network-displays。

## 1. 安装 MSYS2

从 https://www.msys2.org/ 下载并安装 MSYS2。

## 2. 在 MSYS2 终端中安装构建工具

```bash
# 更新包数据库
pacman -Syu

# 安装 MinGW-w64 工具链
pacman -S --needed \
    mingw-w64-x86_64-toolchain \
    mingw-w64-x86_64-pkgconf \
    mingw-w64-x86_64-cmake \
    mingw-w64-x86_64-ninja \
    mingw-w64-x86_64-meson \
    base-devel

# 安装 GTK4 和相关库
pacman -S \
    mingw-w64-x86_64-gtk4 \
    mingw-w64-x86_64-gdk-pixbuf-2 \
    mingw-w64-x86_64-gstreamer \
    mingw-w64-x86_64-gst-plugins-base \
    mingw-w64-x86_64-gst-plugins-good \
    mingw-w64-x86_64-libnice \
    mingw-w64-x86_64-glib2 \
    mingw-w64-x86_64-json-glib \
    mingw-w64-x86_64-libsoup3 \
    mingw-w64-x86_64-libportal \
    mingw-w64-x86_64-networkmanager \
    mingw-w64-x86_64-avahi \
    mingw-w64-x86_64-pulseaudio

# 安装 protobuf 和其他依赖
pacman -S \
    mingw-w64-x86_64-protobuf-c \
    mingw-w64-x86_64-protobuf \
    mingw-w64-x86_64-libxml2
```

## 3. 设置环境变量

在 MSYS2 MinGW 64-bit 终端中：

```bash
# 设置 pkg-config 路径
export PKG_CONFIG_PATH="/mingw64/lib/pkgconfig:/mingw64/share/pkgconfig:$PKG_CONFIG_PATH"

# 验证 GTK4 已安装
pkg-config --modversion gtk4
```

## 4. 构建项目

```bash
# 进入源码目录
cd /path/to/gnome-network-displays

# 创建构建目录
mkdir build-mingw
cd build-mingw

# 配置 Meson（针对 Windows）
meson setup .. \
    --cross-file=mingw64 \
    --prefix=/mingw64

# 编译
ninja
```

## 5. 创建交叉编译文件

创建 `mingw64` 交叉编译配置文件：

```ini
# mingw64
[binaries]
c = 'x86_64-w64-mingw32-gcc'
cpp = 'x86_64-w64-mingw32-g++'
ar = 'x86_64-w64-mingw32-ar'
strip = 'x86_64-w64-mingw32-strip'
pkgconfig = 'x86_64-w64-mingw32-pkg-config'
exe_suffix = '.exe'

[host_machine]
system = 'windows'
cpu_family = 'x86_64'
cpu = 'x86_64'
endian = 'little'

[properties]
sys_root = '/usr/x86_64-w64-mingw32'
```

## 已知问题

1. **NetworkManager** - Windows 没有 NetworkManager，需要替代方案
2. **Avahi** - Windows 没有 Avahi，需要替代方案
3. **firewalld** - Windows 使用不同防火墙 API
4. **libportal** - Windows 使用不同桌面集成 API

这些问题需要为 Windows 编写替代实现。

## 替代方案

如果不需要完整的 Linux 功能集，可以只交叉编译核心功能：

```bash
# 只构建核心库（无 GUI）
meson setup .. -Dbuild_app=false -Dbuild_daemon=true
```
