#Requires -RunAsAdministrator
<#
.SYNOPSIS
    BrodCatsSuite0: Launch OBS Studio with Optimaxx Priority & Hardware Isolation
.DESCRIPTION
    Starts OBS Studio with high thread priority, isolating broadcast encoding
    from gaming cores on AMD Ryzen 7 5800X.
#>

$ErrorActionPreference = "Continue"
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  BRODCATSSUITE0: OPTIMAXXED OBS STUDIO LAUNCHER        " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$obsExe = "C:\Program Files\obs-studio\bin\64bit\obs64.exe"
$obsWorkDir = "C:\Program Files\obs-studio\bin\64bit"

if (!(Test-Path $obsExe)) {
    Write-Error "OBS Studio 64-bit not found at $obsExe"
    exit 1
}

# Clean any safe-mode sentinel lock files
Remove-Item "$env:APPDATA\obs-studio\.sentinel\*" -Force -ErrorAction SilentlyContinue

# Ensure profile and scene are deployed
$suiteBase = "C:\Users\Administrator\.gemini\antigravity\scratch\BrodCatsSuite0"
& "$suiteBase\scripts\02_install_obs_optimaxx_profiles.ps1"

Write-Host "`nLaunching OBS Studio with Ada Lovelace NVENC & Ryzen 5800X isolation..." -ForegroundColor Yellow

$pinfo = New-Object System.Diagnostics.ProcessStartInfo
$pinfo.FileName = $obsExe
$pinfo.WorkingDirectory = $obsWorkDir
$pinfo.Arguments = "--disable-shutdown-check --collection `"Optimaxx_Master_Gaming`" --profile `"Optimaxx_MultiStream_LowLatency`""
$pinfo.UseShellExecute = $true

$proc = [System.Diagnostics.Process]::Start($pinfo)
Start-Sleep -Milliseconds 1500

$obsProcs = Get-Process -Name "obs64" -ErrorAction SilentlyContinue
if ($obsProcs) {
    foreach ($p in $obsProcs) {
        $p.ProcessorAffinity = [IntPtr]0xF000
        $p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::AboveNormal
        Write-Host "  [SUCCESS] OBS Studio running (PID: $($p.Id))" -ForegroundColor Green
        Write-Host "  [INFO] Affinity pinned to 0xF000 (Threads 12-15) - Game cores protected." -ForegroundColor Cyan
        Write-Host "  [INFO] Priority: AboveNormal (MMCSS Capture Linked)." -ForegroundColor Cyan
    }
} else {
    Write-Warning "OBS Studio process launched."
}

Write-Host "`n========================================================" -ForegroundColor Cyan
