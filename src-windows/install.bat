@echo off
REM Install gnome-network-displays as a Windows Service
REM Requires Administrator privileges

echo ==========================================
echo gnome-network-displays Service Installer
echo ==========================================

REM Check for administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: This script requires Administrator privileges.
    echo Please right-click and select "Run as administrator"
    exit /b 1
)

REM Get the directory where the script is located
set SERVICE_DIR=%~dp0
set SERVICE_EXE=%SERVICE_DIR%service\bin\Release\net8.0-windows10.0.19041.0\gnome-network-displays.service.exe

REM Check if service already exists
sc query GndService >nul 2>&1
if %errorlevel% equ 0 (
    echo Service already exists. Removing old service...
    sc stop GndService
    sc delete GndService
    timeout /t 2 /nobreak >nul
)

REM Create the service
echo Creating Windows Service...
sc create GndService binPath= "%SERVICE_EXE%" DisplayName= "GND Service" start= demand

if %errorlevel% neq 0 (
    echo ERROR: Failed to create service
    exit /b 1
)

REM Set service description
sc description GndService "GNOME Network Displays - Wi-Fi Display and Chromecast service"

REM Configure recovery options
sc failure GndService actions= restart/60000/restart/60000/restart/60000 reset= 86400

echo.
echo ==========================================
echo Service installed successfully!
echo ==========================================
echo.
echo To start the service:
echo   sc start GndService
echo.
echo To configure automatic startup:
echo   sc config GndService start= auto
echo.
echo To uninstall:
echo   uninstall.bat
echo.

exit /b 0
