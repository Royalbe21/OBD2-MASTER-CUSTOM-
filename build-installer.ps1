$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$ProjectPath = Join-Path $ProjectRoot "src\TacomaDiag\TacomaDiag.csproj"
$PublishDir = Join-Path $ProjectRoot "publish\win-x64"
$InstallerDir = Join-Path $ProjectRoot "installer"
$InstallerOutputDir = Join-Path $InstallerDir "output"
$InnoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

function Assert-InProjectPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolvedProject = [System.IO.Path]::GetFullPath($ProjectRoot)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedProject, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside project: $resolvedPath"
    }
}

if (-not (Test-Path -LiteralPath $InnoCompiler)) {
    throw "Inno Setup compiler not found at $InnoCompiler"
}

Assert-InProjectPath -Path $PublishDir
Assert-InProjectPath -Path $InstallerOutputDir

if (Test-Path -LiteralPath $PublishDir) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force
}

if (Test-Path -LiteralPath $InstallerOutputDir) {
    Remove-Item -LiteralPath $InstallerOutputDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $InstallerOutputDir | Out-Null

dotnet publish $ProjectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    -p:PublishSingleFile=false `
    --output $PublishDir

Push-Location $InstallerDir
try {
    & $InnoCompiler "TacomaDiag.iss"
}
finally {
    Pop-Location
}

$setupPath = Join-Path $InstallerOutputDir "TacomaDiagSetup.exe"
Write-Host "Installer created: $setupPath"
