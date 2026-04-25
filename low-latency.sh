#!/bin/bash
# 低延迟优化配置

export NETWORK_DISPLAYS_VIDEO_WIDTH=1280
export NETWORK_DISPLAYS_VIDEO_HEIGHT=720
export NETWORK_DISPLAYS_VIDEO_FRAMERATE=30
export NETWORK_DISPLAYS_AUTO_CONNECT=1

# PipeWire 低延迟配置
export PIPEWIRE_LATENCY="128/48000"

# 运行
exec /home/df530/projects/gnome-network-displays/build/src/gnome-network-displays
