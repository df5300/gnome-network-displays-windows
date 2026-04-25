@echo off
REM Uninstall gnome-network-displays Windows Service
REM Requires Administrator privileges

echo ==========================================
echo gnome-network-displays Service Uninstaller
echo ==========================================

REM Check for administrator privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: This script requires Administrator privileges.
    echo Please right-click and select "Run as administrator"
    exit /b 1
)

REM Check if service exists
sc query GndService >nul 2>&1
if %errorlevel% neq 0 (
    echo Service does not exist. Nothing to uninstall.
    exit /b 0
)

REM Stop the service if running
echo Stopping service...
sc stop GndService
timeout /t 3 /nobreak >nul

REM Delete the service
echo Deleting service...
sc delete GndService

if %errorlevel% neq 0 (
    echo ERROR: Failed to delete service
    exit /b 1
)

echo.
echo ==========================================
echo Service uninstalled successfully!
echo ==========================================

exit /b 0
