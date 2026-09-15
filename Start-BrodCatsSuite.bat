@echo off
TITLE BrodCatsSuite0 - Optimaxx Multi-Platform Broadcast Launcher
COLOR 0B

echo ========================================================
echo   BRODCATSSUITE0 // OPTIMAXX BROADCAST INITIALIZER
echo   Target: AMD Ryzen 7 5800X + NVIDIA GeForce RTX 4060 Ti
echo ========================================================
echo.

:: 1. Apply Network QoS and Affinity
echo [*] Applying System QoS (DSCP 46) & Ryzen Thread Isolation...
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\01_streaming_qos_and_affinity.ps1"

:: 2. Ensure OBS Profiles are Synced
echo.
echo [*] Syncing Optimaxx OBS Studio Profiles...
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\02_install_obs_optimaxx_profiles.ps1"

:: 3. Launch MediaMTX Zero-Copy Relay
echo.
echo [*] Initializing Zero-Copy Multi-Stream Fanout Relay...
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\03_run_mediamtx_relay.ps1"

:: 4. Start Telemetry Dashboard in background if not running
echo.
echo [*] Starting Telemetry Dashboard on http://127.0.0.1:8999...
powershell -Command "$p = Get-Process python -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like '*server.py*' }; if (!$p) { Start-Process python -ArgumentList '%~dp0app\server.py' -WorkingDirectory '%~dp0app' -WindowStyle Hidden }"

:: 5. Launch OBS Studio
echo.
echo [*] Launching OBS Studio with Optimaxx Affinity (Cores 6-7)...
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\04_launch_obs_optimaxx.ps1"

:: 6. Open Control Dashboard in Default Browser
echo.
echo [*] Opening BrodCats Dashboard in browser...
start http://127.0.0.1:8999

echo.
echo ========================================================
echo   BRODCATSSUITE0 IS FULLY OPERATIONAL!
echo   Stream to: rtmp://127.0.0.1:1935/live/master
echo   Dashboard: http://127.0.0.1:8999
echo ========================================================
echo.
pause
