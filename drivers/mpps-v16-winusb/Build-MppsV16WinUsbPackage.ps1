[CmdletBinding()]
param(
    [switch]$CreateTestCertificate
)

$ErrorActionPreference = 'Stop'

$driverRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$infPath = Join-Path $driverRoot 'MppsV16WinUsb.inf'
$catPath = Join-Path $driverRoot 'MppsV16WinUsb.cat'
$certificateName = 'TacomaDiag MPPS V16 WinUSB Test Certificate'

function Find-Tool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $fromPath = Get-Command $Name -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $match = Get-ChildItem -LiteralPath $kitsRoot -Recurse -Filter $Name -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($match) {
            return $match.FullName
        }
    }

    return $null
}

if (-not (Test-Path $infPath)) {
    throw "Missing INF file: $infPath"
}

$inf2Cat = Find-Tool 'inf2cat.exe'
if (-not $inf2Cat) {
    throw "inf2cat.exe was not found. Install the Windows Driver Kit, then rerun this script."
}

& $inf2Cat /driver:"$driverRoot" /os:10_X64,10_RS5_X64,10_VB_X64,10_GE_X64
if ($LASTEXITCODE -ne 0) {
    throw "inf2cat failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $catPath)) {
    throw "inf2cat finished but did not create $catPath"
}

if (-not $CreateTestCertificate) {
    Write-Host "Created catalog: $catPath"
    Write-Host "Catalog is not signed. Sign it before installing on Windows 11."
    exit 0
}

$signtool = Find-Tool 'signtool.exe'
if (-not $signtool) {
    throw "signtool.exe was not found. Install the Windows Driver Kit, then rerun this script."
}

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq "CN=$certificateName" } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $cert) {
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=$certificateName" `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyExportPolicy Exportable `
        -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(3)
}

$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'CurrentUser')
$trustedPublisherStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('TrustedPublisher', 'CurrentUser')
$rootStore.Open('ReadWrite')
$trustedPublisherStore.Open('ReadWrite')
$rootStore.Add($cert)
$trustedPublisherStore.Add($cert)
$rootStore.Close()
$trustedPublisherStore.Close()

& $signtool sign /v /fd SHA256 /sha1 $cert.Thumbprint "$catPath"
if ($LASTEXITCODE -ne 0) {
    throw "signtool failed with exit code $LASTEXITCODE"
}

Write-Host "Created and test-signed catalog: $catPath"
Write-Host "Windows may still require test-signing mode or a Microsoft-signed catalog for installation."
