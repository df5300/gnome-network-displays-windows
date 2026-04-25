#!/usr/bin/env bash
# Build script for gnome-network-displays Windows port (for WSL/Cross-compile)

set -e

echo "=========================================="
echo "gnome-network-displays Windows Build Script"
echo "=========================================="

# Check for dotnet
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found. Please install .NET 8.0 SDK"
    exit 1
fi

echo "Using .NET version: $(dotnet --version)"

# Restore
echo ""
echo "Restoring NuGet packages..."
dotnet restore gnome-network-displays.sln

# Build
echo ""
echo "Building solution..."
dotnet build gnome-network-displays.sln -c Debug

echo ""
echo "=========================================="
echo "Build completed successfully!"
echo "=========================================="
