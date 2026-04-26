@echo off
REM ==========================================
REM Build Script for GNOME Network Displays (Windows)
REM Requires: Windows 10/11, .NET 8 SDK
REM ==========================================

echo ==========================================
echo GNOME Network Displays - Windows Build
echo ==========================================

REM Check for .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found.
    echo Please install .NET 8 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)

REM Get .NET version
for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo Using .NET version: %DOTNET_VERSION%

REM Navigate to src-windows directory
cd /d "%~dp0src-windows" 2>nul
if errorlevel 1 (
    echo ERROR: src-windows directory not found
    exit /b 1
)

echo.
echo Restoring NuGet packages...
dotnet restore gnome-network-displays.sln
if errorlevel 1 (
    echo ERROR: Package restore failed
    exit /b 1
)

echo.
echo Building Debug configuration...
dotnet build gnome-network-displays.sln -c Debug
if errorlevel 1 (
    echo ERROR: Debug build failed
    exit /b 1
)

echo.
echo Building Release configuration...
dotnet build gnome-network-displays.sln -c Release -r win-x64 --self-contained
if errorlevel 1 (
    echo ERROR: Release build (win-x64) failed
    exit /b 1
)

REM Optional: Build for ARM64
echo.
set /p BUILD_ARM64="Build for ARM64 as well? (y/N): "
if /i "%BUILD_ARM64%"=="y" (
    echo Building ARM64...
    dotnet build gnome-network-displays.sln -c Release -r win-arm64 --self-contained
)

echo.
echo ==========================================
echo Build completed successfully!
echo ==========================================
echo.
echo Output binaries:
echo.
echo Service (Debug):
echo   src-windows\service\bin\Debug\net8.0-windows10.0.19041.0\gnome-network-displays.service.exe
echo.
echo Service (Release):
echo   src-windows\service\bin\Release\net8.0-windows10.0.19041.0\win-x64\gnome-network-displays.service.exe
echo.
echo Client (Debug):
echo   src-windows\client\bin\Debug\net8.0-windows10.0.19041.0\gnome-network-displays.client.exe
echo.
echo Stream Renderer (Debug):
echo   src-windows\stream\bin\Debug\net8.0-windows10.0.19041.0\gnome-network-displays.stream.exe
echo.

exit /b 0
