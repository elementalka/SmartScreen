param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\SmartScreen"),
    [string]$StartMenuDirectory = (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\SmartScreen"),
    [string]$DesktopDirectory = [Environment]::GetFolderPath("DesktopDirectory"),
    [switch]$NoStartMenuShortcut,
    [switch]$CreateDesktopShortcut,
    [switch]$SkipPublish,
    [switch]$Launch
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "artifacts\SmartScreen-$Runtime"
$sourceExe = Join-Path $publishDir "SmartScreen.App.exe"
$targetExe = Join-Path $InstallDir "SmartScreen.App.exe"

function New-SmartScreenShortcut {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$TargetPath,
        [string]$Arguments = "",
        [string]$Description = "SmartScreen"
    )

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $directory | Out-Null

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = $TargetPath
    $shortcut.Arguments = $Arguments
    $shortcut.WorkingDirectory = Split-Path -Parent $TargetPath
    $shortcut.Description = $Description
    $shortcut.IconLocation = "$TargetPath,0"
    $shortcut.Save()
}

function Write-UninstallScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    @'
param(
    [switch]$KeepData
)

$ErrorActionPreference = "Stop"

$installRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$startMenuDirectory = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\SmartScreen"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "SmartScreen.lnk"

if (Test-Path -LiteralPath $startMenuDirectory) {
    Remove-Item -LiteralPath $startMenuDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $desktopShortcut) {
    Remove-Item -LiteralPath $desktopShortcut -Force
}

if ($KeepData) {
    Get-ChildItem -LiteralPath $installRoot -File |
        Where-Object { $_.Name -ne "uninstall.ps1" } |
        Remove-Item -Force

    Write-Host "SmartScreen binaries removed. Portable data kept in:" $installRoot
    return
}

$parent = Split-Path -Parent $installRoot
$leaf = Split-Path -Leaf $installRoot
$deleteScript = Join-Path $env:TEMP "SmartScreen-uninstall-$([Guid]::NewGuid().ToString('N')).ps1"

@"
Start-Sleep -Milliseconds 500
`$target = Join-Path '$parent' '$leaf'
if (Test-Path -LiteralPath `$target) {
    Remove-Item -LiteralPath `$target -Recurse -Force
}
Remove-Item -LiteralPath `$MyInvocation.MyCommand.Path -Force
"@ | Set-Content -Encoding UTF8 -Path $deleteScript

Start-Process powershell -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$deleteScript`"" -WindowStyle Hidden
Write-Host "SmartScreen uninstall scheduled:" $installRoot
'@ | Set-Content -Encoding UTF8 -Path $Path
}

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "publish-portable.ps1") -Configuration $Configuration -Runtime $Runtime
}

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Portable build was not found: $sourceExe. Run scripts\publish-portable.ps1 first or remove -SkipPublish."
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $InstallDir -Recurse -Force

$uninstallScript = Join-Path $InstallDir "uninstall.ps1"
Write-UninstallScript -Path $uninstallScript

if (-not $NoStartMenuShortcut) {
    New-SmartScreenShortcut `
        -Path (Join-Path $StartMenuDirectory "SmartScreen.lnk") `
        -TargetPath $targetExe `
        -Description "SmartScreen screenshot tool"

    New-SmartScreenShortcut `
        -Path (Join-Path $StartMenuDirectory "SmartScreen Settings.lnk") `
        -TargetPath $targetExe `
        -Arguments "--settings" `
        -Description "Open SmartScreen settings"

    New-SmartScreenShortcut `
        -Path (Join-Path $StartMenuDirectory "Uninstall SmartScreen.lnk") `
        -TargetPath "powershell.exe" `
        -Arguments "-ExecutionPolicy Bypass -File `"$uninstallScript`"" `
        -Description "Uninstall SmartScreen"
}

if ($CreateDesktopShortcut) {
    New-SmartScreenShortcut `
        -Path (Join-Path $DesktopDirectory "SmartScreen.lnk") `
        -TargetPath $targetExe `
        -Description "SmartScreen screenshot tool"
}

Write-Host "SmartScreen installed:" $InstallDir
if (-not $NoStartMenuShortcut) {
    Write-Host "Start Menu shortcuts:" $StartMenuDirectory
}

if ($CreateDesktopShortcut) {
    Write-Host "Desktop shortcut:" (Join-Path $DesktopDirectory "SmartScreen.lnk")
}

if ($Launch) {
    Start-Process -FilePath $targetExe
}
