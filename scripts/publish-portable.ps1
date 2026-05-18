param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "artifacts\SmartScreen-$Runtime"

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

Write-Host "Portable build:" $publishDir
Write-Host "Checksum manifest:" $checksumPath
