param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PortableDir = "",
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\SmartScreen"),
    [string]$StartMenuDirectory = (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\SmartScreen"),
    [string]$StartupDirectory = (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup"),
    [string]$DesktopDirectory = [Environment]::GetFolderPath("DesktopDirectory"),
    [switch]$NoStartMenuShortcut,
    [switch]$CreateDesktopShortcut,
    [switch]$CreateStartupShortcut,
    [switch]$SkipPublish,
    [switch]$Launch
)

$ErrorActionPreference = "Stop"

$scriptParent = Split-Path -Parent $PSScriptRoot
$repoRoot = if (Test-Path -LiteralPath (Join-Path $scriptParent "SmartScreen.sln")) {
    $scriptParent
} else {
    $null
}

if ([string]::IsNullOrWhiteSpace($PortableDir)) {
    $artifactRoot = if ($repoRoot) {
        Join-Path $repoRoot "artifacts"
    } else {
        $scriptParent
    }

    $PortableDir = Join-Path $artifactRoot "SmartScreen Portable"
}

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
$metadataPath = Join-Path $installRoot "install.json"
$metadata = $null

if (Test-Path -LiteralPath $metadataPath) {
    $metadata = Get-Content -Raw -Path $metadataPath | ConvertFrom-Json
}

$startMenuDirectory = if ($metadata -and $metadata.StartMenuDirectory) {
    [string]$metadata.StartMenuDirectory
} else {
    Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\SmartScreen"
}

$desktopShortcut = if ($metadata -and $metadata.DesktopShortcut) {
    [string]$metadata.DesktopShortcut
} else {
    Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "SmartScreen.lnk"
}

$startupShortcut = if ($metadata -and $metadata.StartupShortcut) {
    [string]$metadata.StartupShortcut
} else {
    Join-Path (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup") "SmartScreen.lnk"
}

if (Test-Path -LiteralPath $startMenuDirectory) {
    Remove-Item -LiteralPath $startMenuDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $desktopShortcut) {
    Remove-Item -LiteralPath $desktopShortcut -Force
}

if (Test-Path -LiteralPath $startupShortcut) {
    Remove-Item -LiteralPath $startupShortcut -Force
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

function Resolve-SmartScreenSourceExe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory
    )

    $preferredExe = Join-Path $Directory "SmartScreen.exe"
    if (Test-Path -LiteralPath $preferredExe) {
        return $preferredExe
    }

    $fallbackExe = Join-Path $Directory "SmartScreen.App.exe"
    if (Test-Path -LiteralPath $fallbackExe) {
        return $fallbackExe
    }

    return $preferredExe
}

if (-not $SkipPublish) {
    if ($repoRoot) {
        & (Join-Path $PSScriptRoot "publish-portable.ps1") -Configuration $Configuration -Runtime $Runtime
    }
    elseif (Test-Path -LiteralPath (Resolve-SmartScreenSourceExe -Directory $PortableDir)) {
        Write-Host "Using sibling portable build:" $PortableDir
    }
    else {
        throw "Publishing is only available from the source repository. Use -SkipPublish when running from the setup package."
    }
}

$sourceExe = Resolve-SmartScreenSourceExe -Directory $PortableDir
if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Portable build was not found: $sourceExe. Keep the setup folder next to 'SmartScreen Portable' or pass -PortableDir."
}

$targetExe = Join-Path $InstallDir (Split-Path -Leaf $sourceExe)

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $PortableDir "*") -Destination $InstallDir -Recurse -Force

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

if ($CreateStartupShortcut) {
    New-SmartScreenShortcut `
        -Path (Join-Path $StartupDirectory "SmartScreen.lnk") `
        -TargetPath $targetExe `
        -Description "Start SmartScreen with Windows"
}

$installMetadata = [ordered]@{
    InstallDir = $InstallDir
    StartMenuDirectory = if (-not $NoStartMenuShortcut) { $StartMenuDirectory } else { $null }
    DesktopShortcut = if ($CreateDesktopShortcut) { Join-Path $DesktopDirectory "SmartScreen.lnk" } else { $null }
    StartupShortcut = if ($CreateStartupShortcut) { Join-Path $StartupDirectory "SmartScreen.lnk" } else { $null }
}

$installMetadata |
    ConvertTo-Json |
    Set-Content -Encoding UTF8 -Path (Join-Path $InstallDir "install.json")

Write-Host "SmartScreen installed:" $InstallDir
if (-not $NoStartMenuShortcut) {
    Write-Host "Start Menu shortcuts:" $StartMenuDirectory
}

if ($CreateDesktopShortcut) {
    Write-Host "Desktop shortcut:" (Join-Path $DesktopDirectory "SmartScreen.lnk")
}

if ($CreateStartupShortcut) {
    Write-Host "Startup shortcut:" (Join-Path $StartupDirectory "SmartScreen.lnk")
}

if ($Launch) {
    Start-Process -FilePath $targetExe
}
