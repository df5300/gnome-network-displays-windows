@echo off
REM Build script for gnome-network-displays Windows port
REM Requires .NET 8.0 SDK and Visual Studio 2022

echo ==========================================
echo gnome-network-displays Windows Build Script
echo ==========================================

REM Check for .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found. Please install .NET 8.0 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)

REM Check .NET version
for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo Using .NET version: %DOTNET_VERSION%

REM Restore packages
echo.
echo Restoring NuGet packages...
dotnet restore gnome-network-displays.sln
if errorlevel 1 (
    echo ERROR: Package restore failed
    exit /b 1
)

REM Build
echo.
echo Building solution...
dotnet build gnome-network-displays.sln -c Debug
if errorlevel 1 (
    echo ERROR: Build failed
    exit /b 1
)

echo.
echo ==========================================
echo Build completed successfully!
echo ==========================================
echo.
echo Output binaries:
echo   Service:  service\bin\Debug\net8.0-windows10.0.19041.0\gnome-network-displays.service.exe
echo   Client:   client\bin\Debug\net8.0-windows10.0.19041.0\gnome-network-displays.client.exe
echo   Stream:   stream\bin\Debug\net8.0-windows10.0.19041.0\gnome-network-displays.stream.exe
echo.

REM Optional: Run tests if any exist
if exist "test\*" (
    echo Running tests...
    dotnet test gnome-network-displays.sln
)

exit /b 0
