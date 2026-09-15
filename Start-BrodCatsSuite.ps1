#Requires -RunAsAdministrator
<#
.SYNOPSIS
    BrodCatsSuite0 Master Launcher
.DESCRIPTION
    One-click launcher to initialize network QoS, Ryzen affinity, MediaMTX relay,
    dashboard server, and launch OBS Studio with hardware isolation.
#>

$ErrorActionPreference = "Continue"
$suiteDir = $PSScriptRoot

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  BRODCATSSUITE0 // MASTER LAUNCHER                     " -ForegroundColor Cyan
Write-Host "  Target: AMD Ryzen 7 5800X + NVIDIA GeForce RTX 4060 Ti" -ForegroundColor DarkCyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Apply System & Network QoS
& "$suiteDir\scripts\01_streaming_qos_and_affinity.ps1"

# 2. Deploy Profiles
& "$suiteDir\scripts\02_install_obs_optimaxx_profiles.ps1"

# 3. Start MediaMTX Relay
& "$suiteDir\scripts\03_run_mediamtx_relay.ps1"

# 4. Start Dashboard Server
$serverRunning = Get-Process python -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*server.py*" }
if (!$serverRunning) {
    Start-Process -FilePath "python" -ArgumentList "`"$suiteDir\app\server.py`"" -WorkingDirectory "$suiteDir\app" -WindowStyle Hidden
    Write-Host "[OK] Dashboard server started on http://127.0.0.1:8999" -ForegroundColor Green
} else {
    Write-Host "[OK] Dashboard server already active on http://127.0.0.1:8999" -ForegroundColor Green
}

# 5. Launch OBS Studio
& "$suiteDir\scripts\04_launch_obs_optimaxx.ps1"

# 6. Open Dashboard
Start-Process "http://127.0.0.1:8999"

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "  BRODCATSSUITE0 OPERATIONAL!" -ForegroundColor Green
Write-Host "  Relay Master Ingest: rtmp://127.0.0.1:1935/live/master" -ForegroundColor Cyan
Write-Host "  Dashboard:           http://127.0.0.1:8999" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
