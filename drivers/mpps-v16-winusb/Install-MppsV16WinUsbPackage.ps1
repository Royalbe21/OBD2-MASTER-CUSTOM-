[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$driverRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$infPath = Join-Path $driverRoot 'MppsV16WinUsb.inf'
$catPath = Join-Path $driverRoot 'MppsV16WinUsb.cat'
$hardwareId = 'USB\VID_1C43&PID_0500'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell prompt.'
}

if (-not (Test-Path $infPath)) {
    throw "Missing INF file: $infPath"
}

if (-not (Test-Path $catPath) -and -not $Force) {
    throw "Missing catalog file: $catPath. Build and sign the package first, or rerun with -Force to let pnputil report the signing error."
}

Write-Host "Current MPPS device state:"
Get-PnpDevice -PresentOnly |
    Where-Object { $_.InstanceId -like "$hardwareId*" -or $_.FriendlyName -like '*Amt Flash*' -or $_.FriendlyName -like '*MPPS*' } |
    Select-Object FriendlyName,Status,Problem,Class,Service,InstanceId |
    Format-List

Write-Host "Installing $infPath"
pnputil /add-driver "$infPath" /install
if ($LASTEXITCODE -ne 0) {
    throw "pnputil failed with exit code $LASTEXITCODE"
}

Write-Host "Replug the MPPS V16 adapter, then run .\Test-MppsV16Device.ps1"
