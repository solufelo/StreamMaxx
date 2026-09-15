#Requires -RunAsAdministrator
<#
.SYNOPSIS
    BrodCatsSuite0: MediaMTX Zero-Copy Multi-Stream Relay Launcher
.DESCRIPTION
    Dynamically configures and launches the high-performance Go multi-stream relay,
    pinning it to Ryzen 5800X cores 6-7 (threads 12-15).
#>

$ErrorActionPreference = "Continue"
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  BRODCATSSUITE0: ZERO-COPY MULTI-STREAM RELAY ENGINE   " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$suiteBase = "C:\Users\Administrator\.gemini\antigravity\scratch\BrodCatsSuite0"
$mtxExe = "$suiteBase\bin\mediamtx.exe"
$destJsonPath = "$suiteBase\config\destinations.json"
$mtxYmlPath = "$suiteBase\config\mediamtx.yml"

if (!(Test-Path $mtxExe)) {
    Write-Error "mediamtx.exe not found at $mtxExe"
    exit 1
}

# Stop any running instances first
Get-Process -Name "mediamtx" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# Generate mediamtx.yml based on destinations.json
Write-Host "`n[1] Synchronizing active streaming destinations..." -ForegroundColor Yellow
$destData = Get-Content -Path $destJsonPath -Raw | ConvertFrom-Json

$forwardList = @()
foreach ($key in $destData.destinations.PSObject.Properties.Name) {
    $item = $destData.destinations.$key
    if ($item.enabled -and $item.stream_key -and $item.stream_key.Trim() -ne "") {
        $serverUrl = $item.server.TrimEnd('/')
        $forwardUrl = "$serverUrl/$($item.stream_key)"
        $forwardList += "      - dest: $forwardUrl"
        Write-Host "  [+] Active Destination: $($item.name) -> $($item.server)" -ForegroundColor Green
    }
}

$forwardSection = ""
if ($forwardList.Count -gt 0) {
    $forwardSection = "    forward:`n" + ($forwardList -join "`n")
} else {
    $forwardSection = "    forward: []"
    Write-Host "  [INFO] No stream keys configured yet. Relay operating in standby loopback mode." -ForegroundColor Gray
}

$ymlContent = @"
# BrodCatsSuite0 High-Performance MediaMTX Master Relay Configuration
api: true
apiAddress: 127.0.0.1:9997

rtmp: true
rtmpAddress: 127.0.0.1:1935
rtmpEncryption: "no"

rtsp: false
hls: false
webrtc: false
srt: true
srtAddress: 127.0.0.1:8890

readTimeout: 10s
writeTimeout: 10s

paths:
  live/master:
$forwardSection
    runOnInit: ""
    runOnDemand: ""
"@

# Write UTF-8 WITHOUT BOM
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($mtxYmlPath, $ymlContent, $utf8NoBom)
Write-Host "  [OK] Generated dynamic $mtxYmlPath (UTF-8 No-BOM)" -ForegroundColor Green

# 2. Launch MediaMTX with CPU affinity
Write-Host "`n[2] Launching MediaMTX Relay Engine..." -ForegroundColor Yellow
$proc = Start-Process -FilePath $mtxExe -ArgumentList "`"$mtxYmlPath`"" -WorkingDirectory "$suiteBase\bin" -WindowStyle Hidden -PassThru
Start-Sleep -Milliseconds 1200

if ($proc -and !$proc.HasExited) {
    # Pin to Ryzen 5800X Cores 6-7 (Threads 12-15)
    $proc.ProcessorAffinity = [IntPtr]0xF000
    $proc.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High
    Write-Host "  [SUCCESS] MediaMTX Relay running (PID: $($proc.Id)) on Affinity 0xF000 (High Priority)." -ForegroundColor Green
    Write-Host "  [INFO] Local Master Ingest: rtmp://127.0.0.1:1935/live/master" -ForegroundColor Cyan
    Write-Host "  [INFO] MediaMTX Telemetry API: http://127.0.0.1:9997" -ForegroundColor Cyan
} else {
    Write-Error "Failed to start MediaMTX relay process."
}

Write-Host "`n========================================================" -ForegroundColor Cyan
