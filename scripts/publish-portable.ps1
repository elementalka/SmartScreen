param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "SmartScreen Portable"
$setupDir = Join-Path $artifactsDir "SmartScreen Setup"

function Reset-ArtifactDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $artifactsFullPath = [System.IO.Path]::GetFullPath($artifactsDir)
    $targetFullPath = [System.IO.Path]::GetFullPath($Path)

    if (-not $targetFullPath.StartsWith($artifactsFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset a directory outside artifacts: $targetFullPath"
    }

    if (Test-Path -LiteralPath $targetFullPath) {
        Remove-Item -LiteralPath $targetFullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $targetFullPath | Out-Null
}

function Remove-ArtifactDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $artifactsFullPath = [System.IO.Path]::GetFullPath($artifactsDir)
    $targetFullPath = [System.IO.Path]::GetFullPath($Path)

    if (-not $targetFullPath.StartsWith($artifactsFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside artifacts: $targetFullPath"
    }

    if (Test-Path -LiteralPath $targetFullPath) {
        Remove-Item -LiteralPath $targetFullPath -Recurse -Force
    }
}

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
Remove-ArtifactDirectory -Path (Join-Path $artifactsDir "SmartScreen-$Runtime")
Reset-ArtifactDirectory -Path $publishDir
Reset-ArtifactDirectory -Path $setupDir

dotnet publish `
    (Join-Path $repoRoot "src\SmartScreen.App\SmartScreen.App.csproj") `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained false `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -o $publishDir

foreach ($directory in @("config", "logs", "screenshots", "localization", "themes")) {
    New-Item -ItemType Directory -Force -Path (Join-Path $publishDir $directory) | Out-Null
}

Copy-Item -Path (Join-Path $repoRoot "localization\*.json") `
    -Destination (Join-Path $publishDir "localization") `
    -Force

Copy-Item -Path (Join-Path $repoRoot "themes\*") `
    -Destination (Join-Path $publishDir "themes") `
    -Recurse `
    -Force

Get-ChildItem -Path (Join-Path $repoRoot "config") -File |
    Where-Object { $_.Name -notlike "secrets.local.json" -and $_.Name -notlike "*.broken-*" } |
    Copy-Item -Destination (Join-Path $publishDir "config") -Force

$appHost = Join-Path $publishDir "SmartScreen.App.exe"
if (Test-Path -LiteralPath $appHost) {
    Copy-Item -LiteralPath $appHost -Destination (Join-Path $publishDir "SmartScreen.exe") -Force
}

Copy-Item `
    -Path (Join-Path $repoRoot "src\SmartScreen.App\Resources\SmartScreen.ico") `
    -Destination (Join-Path $publishDir "SmartScreen.ico") `
    -Force

$releaseInfo = [ordered]@{
    name = "SmartScreen"
    package = "Portable"
    runtime = $Runtime
    configuration = $Configuration
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
}

$releaseInfo |
    ConvertTo-Json |
    Set-Content -Encoding UTF8 -Path (Join-Path $publishDir "release.json")

$checksumPath = Join-Path $publishDir "checksums.sha256"
Get-ChildItem -Path $publishDir -File -Recurse |
    Where-Object { $_.FullName -ne $checksumPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($publishDir.Length).TrimStart("\")
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
        "$($hash.Hash.ToLowerInvariant())  $relativePath"
    } |
    Set-Content -Encoding UTF8 -Path $checksumPath

Copy-Item `
    -Path (Join-Path $PSScriptRoot "install-portable.ps1") `
    -Destination (Join-Path $setupDir "Install SmartScreen.ps1") `
    -Force

Copy-Item `
    -Path (Join-Path $repoRoot "src\SmartScreen.App\Resources\SmartScreen.ico") `
    -Destination (Join-Path $setupDir "SmartScreen.ico") `
    -Force

@"
SmartScreen Setup

Run:
  powershell -ExecutionPolicy Bypass -File ".\Install SmartScreen.ps1"

Optional:
  powershell -ExecutionPolicy Bypass -File ".\Install SmartScreen.ps1" -CreateDesktopShortcut -CreateStartupShortcut

The setup script expects the sibling folder "SmartScreen Portable" to be next to this setup folder.
Default install location:
  %LOCALAPPDATA%\Programs\SmartScreen
"@ | Set-Content -Encoding UTF8 -Path (Join-Path $setupDir "README.txt")

$setupChecksumPath = Join-Path $setupDir "checksums.sha256"
Get-ChildItem -Path $setupDir -File -Recurse |
    Where-Object { $_.FullName -ne $setupChecksumPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($setupDir.Length).TrimStart("\")
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
        "$($hash.Hash.ToLowerInvariant())  $relativePath"
    } |
    Set-Content -Encoding UTF8 -Path $setupChecksumPath

Write-Host "Portable build:" $publishDir
Write-Host "Checksum manifest:" $checksumPath
Write-Host "Setup package:" $setupDir
Write-Host "Setup checksum manifest:" $setupChecksumPath
