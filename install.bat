@echo off
TITLE StreamMaxx - Turn-Key 1-Click Installer
COLOR 0A

echo ================================================================
echo   STREAMMAXX // 1-CLICK TURNKEY ENVIRONMENT SETUP
echo   Zero-Copy Multi-Platform Broadcast Engine & Hardware Scheduler
echo ================================================================
echo.

:: 1. Check for Admin Rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Administrator privileges required. Elevating...
    powershell -Command "Start-Process '%~0' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

:: 2. Initialize destinations.json from template if missing
if not exist "config\destinations.json" (
    echo [*] Initializing config\destinations.json from template...
    copy "config\destinations.example.json" "config\destinations.json" >nul
)

:: 3. Check / Download MediaMTX if missing
if not exist "bin\mediamtx.exe" (
    echo [*] Downloading MediaMTX High-Performance Relay...
    powershell -Command "$url = 'https://github.com/bluenviron/mediamtx/releases/download/v1.21.0/mediamtx_v1.21.0_windows_amd64.zip'; $dest = 'bin\mtx.zip'; [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri $url -OutFile $dest; Expand-Archive -Path $dest -DestinationPath 'bin' -Force; Remove-Item $dest -Force"
)

:: 4. Build Native StreamMaxx.exe via built-in CSC compiler
echo [*] Compiling native StreamMaxx.exe desktop executable...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /win32manifest:"src\app.manifest" /win32icon:"src\app.ico" /reference:System.dll,System.Windows.Forms.dll,System.Drawing.dll /out:"StreamMaxx.exe" "src\StreamMaxxApp.cs"

:: 5. Provision OBS Profiles & Master Scene
echo [*] Deploying Hardware-Tuned OBS Profiles and Gaming Scenes...
powershell -ExecutionPolicy Bypass -File "scripts\02_install_obs_optimaxx_profiles.ps1"

:: 6. Apply Network QoS and Ryzen Affinity
echo [*] Hardening Network QoS (DSCP 46) and Ryzen 5800X Thread Affinity...
powershell -ExecutionPolicy Bypass -File "scripts\01_streaming_qos_and_affinity.ps1"

:: 7. Create Desktop Shortcut
echo [*] Creating Desktop Shortcut...
powershell -Command "$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut(\"$env:USERPROFILE\Desktop\StreamMaxx.lnk\"); $Shortcut.TargetPath = (Convert-Path 'StreamMaxx.exe'); $Shortcut.WorkingDirectory = (Convert-Path '.'); $Shortcut.IconLocation = (Convert-Path 'src\app.ico'); $Shortcut.Description = 'StreamMaxx - Zero-Copy Live Broadcast Hub'; $Shortcut.Save()"

echo.
echo ================================================================
echo   INSTALLATION COMPLETE! 
echo   Launch StreamMaxx from your desktop to broadcast.
echo ================================================================
echo.

start "" "StreamMaxx.exe"
pause
