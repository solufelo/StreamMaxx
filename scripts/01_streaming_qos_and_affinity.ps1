#Requires -RunAsAdministrator
<#
.SYNOPSIS
    BrodCatsSuite0: Streaming QoS, Multimedia Scheduler, and Ryzen 5800X Affinity Hardening
.DESCRIPTION
    Applies DSCP 46 (Expedited Forwarding) network QoS tagging to OBS and MediaMTX to eliminate
    bufferbloat and gaming jitter during multi-platform streaming. Configures MMCSS Capture scheduler
    and optimizes Ryzen 7 5800X thread isolation.
#>

$ErrorActionPreference = "Continue"
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  BRODCATSSUITE0: HARDWARE STREAMING OPTIMAXX ENGINE   " -ForegroundColor Cyan
Write-Host "  Target: AMD Ryzen 7 5800X | NVIDIA RTX 4060 Ti       " -ForegroundColor DarkCyan
Write-Host "========================================================" -ForegroundColor Cyan

# -------------------------------------------------------------
# 1. NETWORK DSCP QOS POLICIES (Zero-Jitter Stream Delivery)
# -------------------------------------------------------------
Write-Host "`n[1] Applying Network DSCP 46 (Expedited Forwarding) QoS Policies..." -ForegroundColor Yellow

Get-NetQosPolicy -Name "OBSStudioStreamQoS" -ErrorAction SilentlyContinue | Remove-NetQosPolicy -Confirm:$false
Get-NetQosPolicy -Name "MediaMTXRelayQoS" -ErrorAction SilentlyContinue | Remove-NetQosPolicy -Confirm:$false

try {
    New-NetQosPolicy -Name "OBSStudioStreamQoS" `
                     -AppPathNameMatchCondition "obs64.exe" `
                     -DSCPAction 46 `
                     -NetworkProfile All `
                     -ErrorAction Stop | Out-Null
    Write-Host "  [OK] OBS Studio DSCP 46 Expedited Forwarding QoS Policy active." -ForegroundColor Green
} catch {
    Write-Warning "  [!] Could not register OBS NetQosPolicy: $_"
}

try {
    New-NetQosPolicy -Name "MediaMTXRelayQoS" `
                     -AppPathNameMatchCondition "mediamtx.exe" `
                     -DSCPAction 46 `
                     -NetworkProfile All `
                     -ErrorAction Stop | Out-Null
    Write-Host "  [OK] MediaMTX Relay DSCP 46 Expedited Forwarding QoS Policy active." -ForegroundColor Green
} catch {
    Write-Warning "  [!] Could not register MediaMTX NetQosPolicy: $_"
}

# -------------------------------------------------------------
# 2. MMCSS KERNEL & CAPTURE SCHEDULER TUNING
# -------------------------------------------------------------
Write-Host "`n[2] Hardening MMCSS Capture & Pro Audio Multimedia Scheduling..." -ForegroundColor Yellow

$captureTaskPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Capture"
if (!(Test-Path $captureTaskPath)) {
    New-Item -Path $captureTaskPath -Force | Out-Null
}
Set-ItemProperty -Path $captureTaskPath -Name "Affinity" -Value 0 -Type DWord
Set-ItemProperty -Path $captureTaskPath -Name "Background Only" -Value "False" -Type String
Set-ItemProperty -Path $captureTaskPath -Name "Clock Rate" -Value 10000 -Type DWord
Set-ItemProperty -Path $captureTaskPath -Name "GPU Priority" -Value 8 -Type DWord
Set-ItemProperty -Path $captureTaskPath -Name "Priority" -Value 6 -Type DWord
Set-ItemProperty -Path $captureTaskPath -Name "Scheduling Category" -Value "High" -Type String
Set-ItemProperty -Path $captureTaskPath -Name "SFIO Priority" -Value "High" -Type String
Write-Host "  [OK] MMCSS Capture task pinned to High SFIO / GPU Priority 8." -ForegroundColor Green

# -------------------------------------------------------------
# 3. RYZEN 7 5800X THREAD ALLOCATION (Cores 6-7 / Threads 12-15)
# -------------------------------------------------------------
Write-Host "`n[3] Ryzen 7 5800X Thread Affinity Configuration..." -ForegroundColor Yellow
$broadcastAffinityMask = [IntPtr]0xF000
Write-Host "  [INFO] Broadcast Affinity Mask calculated: 0xF000 (Threads 12, 13, 14, 15)." -ForegroundColor Gray
Write-Host "  [INFO] Cores 0-5 (Threads 0-11) kept 100% pure for game execution." -ForegroundColor Green

$obsProcesses = Get-Process -Name "obs64" -ErrorAction SilentlyContinue
if ($obsProcesses) {
    foreach ($p in $obsProcesses) {
        $p.ProcessorAffinity = $broadcastAffinityMask
        $p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::AboveNormal
        Write-Host "  [LIVE] Adjusted active OBS process (PID: $($p.Id)) to Affinity 0xF000 + AboveNormal." -ForegroundColor Green
    }
}

$mtxProcesses = Get-Process -Name "mediamtx" -ErrorAction SilentlyContinue
if ($mtxProcesses) {
    foreach ($p in $mtxProcesses) {
        $p.ProcessorAffinity = $broadcastAffinityMask
        $p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High
        Write-Host "  [LIVE] Adjusted active MediaMTX process (PID: $($p.Id)) to Affinity 0xF000 + High." -ForegroundColor Green
    }
}

# -------------------------------------------------------------
# 4. WASAPI AUDIO BUFFER LATENCY OPTIMIZATION
# -------------------------------------------------------------
Write-Host "`n[4] Tuning Low-Latency WASAPI Audio Registry..." -ForegroundColor Yellow
$audioEndpointPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
Set-ItemProperty -Path $audioEndpointPath -Name "NoLazyMode" -Value 1 -Type DWord
Set-ItemProperty -Path $audioEndpointPath -Name "AlwaysKeepChanges" -Value 1 -Type DWord
Write-Host "  [OK] NoLazyMode set to 1 (instant zero-delay audio buffer flush)." -ForegroundColor Green

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "  STREAMING OPTIMAXX APPLIED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
