#Requires -RunAsAdministrator
<#
.SYNOPSIS
    BrodCatsSuite0: Install Tuned Profiles and Scenes to OBS Studio AppData
.DESCRIPTION
    Provisions OBS Studio with hardware-tuned NVENC P6 Low-Latency and AV1 profiles,
    master gaming scene collection, and pre-configures global.ini.
#>

$ErrorActionPreference = "Continue"
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  BRODCATSSUITE0: OBS OPTIMAXX PROFILE INSTALLER       " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$suiteBase = "C:\Users\Administrator\.gemini\antigravity\scratch\BrodCatsSuite0"
$obsAppData = "$env:APPDATA\obs-studio"
$targetProfiles = "$obsAppData\basic\profiles"
$targetScenes = "$obsAppData\basic\scenes"

# 1. Create target directories
New-Item -Path $targetProfiles, $targetScenes -ItemType Directory -Force | Out-Null

# 2. Copy profiles
Write-Host "`n[1] Deploying Tuned OBS Profiles..." -ForegroundColor Yellow
$srcProfiles = "$suiteBase\config\obs_profiles"
Get-ChildItem -Path $srcProfiles -Directory | ForEach-Object {
    $dest = Join-Path $targetProfiles $_.Name
    Copy-Item -Path $_.FullName -Destination $dest -Recurse -Force
    Write-Host "  [OK] Profile installed: $($_.Name)" -ForegroundColor Green
}

# 3. Copy Scene Collections
Write-Host "`n[2] Deploying Master Gaming Scene Collection..." -ForegroundColor Yellow
$srcScenes = "$suiteBase\config\obs_scenes"
Get-ChildItem -Path $srcScenes -Filter "*.json" | ForEach-Object {
    $dest = Join-Path $targetScenes $_.Name
    Copy-Item -Path $_.FullName -Destination $dest -Force
    Write-Host "  [OK] Scene collection installed: $($_.Name)" -ForegroundColor Green
}

# 4. Configure global.ini
Write-Host "`n[3] Aligning OBS global.ini defaults..." -ForegroundColor Yellow
$globalIni = "$obsAppData\global.ini"

$iniContent = @"
[General]
LicenseAccepted=true
FirstRun=false
EnableAutoUpdates=false

[Basic]
Profile=Optimaxx_MultiStream_LowLatency
ProfileDir=Optimaxx_MultiStream_LowLatency
SceneCollection=Optimaxx_Master_Gaming
SceneCollectionFile=Optimaxx_Master_Gaming

[BasicWindow]
DocksState=
cx=1280
cy=720
posx=50
posy=50
"@

if (Test-Path $globalIni) {
    # Backup existing
    Copy-Item -Path $globalIni -Destination "$globalIni.bak" -Force
}
Set-Content -Path $globalIni -Value $iniContent -Encoding UTF8
Write-Host "  [OK] global.ini set default Profile: Optimaxx_MultiStream_LowLatency, Scene: Optimaxx_Master_Gaming" -ForegroundColor Green

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host "  OBS STUDIO OPTIMAXX PROVISIONING COMPLETE!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
