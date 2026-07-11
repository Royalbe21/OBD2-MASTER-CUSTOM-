[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$driverRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$infPath = Join-Path $driverRoot 'MppsV16WinUsb.inf'

if (-not (Test-Path $infPath)) {
    throw "Missing INF file: $infPath"
}

$inf = Get-Content -LiteralPath $infPath -Raw
$requiredPatterns = @(
    'USB\\VID_1C43&PID_0500',
    'Include\s*=\s*winusb\.inf',
    'Needs\s*=\s*WINUSB\.NT',
    'Needs\s*=\s*WINUSB\.NT\.Services',
    'Needs\s*=\s*WINUSB\.NT\.Wdf',
    'DeviceInterfaceGUIDs',
    'ClassGuid\s*=\s*\{88BAE032-5A81-49f0-BC3D-A4FF138216D6\}'
)

foreach ($pattern in $requiredPatterns) {
    if ($inf -notmatch $pattern) {
        throw "INF validation failed. Missing pattern: $pattern"
    }
}

Write-Host "INF validation passed: $infPath"
Write-Host "This package still needs a signed catalog before normal Windows 11 installation."
