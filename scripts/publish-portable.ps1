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

Write-Host "Portable build:" $publishDir
