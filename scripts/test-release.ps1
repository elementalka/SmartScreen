param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipDotnetTests,
    [switch]$KeepSmokeInstall
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "SmartScreen Portable"
$setupDir = Join-Path $artifactsDir "SmartScreen Setup"
$smokeRoot = Join-Path $env:TEMP "SmartScreenReleaseAcceptance-$([Guid]::NewGuid().ToString('N'))"

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Label
    )

    Assert-True (Test-Path -LiteralPath $Path) "$Label was not found: $Path"
}

function Assert-PathMissing {
    param(
        [string]$Path,
        [string]$Label
    )

    Assert-True (-not (Test-Path -LiteralPath $Path)) "$Label must not exist: $Path"
}

function Test-ChecksumManifest {
    param(
        [string]$Root
    )

    $manifest = Join-Path $Root "checksums.sha256"
    Assert-PathExists $manifest "Checksum manifest"

    $entries = Get-Content -Encoding UTF8 -Path $manifest | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    Assert-True ($entries.Count -gt 0) "Checksum manifest is empty."

    foreach ($entry in $entries) {
        if ($entry -notmatch "^([a-f0-9]{64})\s{2}(.+)$") {
            throw "Invalid checksum line: $entry"
        }

        $expectedHash = $Matches[1]
        $relativePath = $Matches[2]
        $filePath = Join-Path $Root $relativePath

        Assert-PathExists $filePath "Checksum file"

        $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $filePath).Hash.ToLowerInvariant()
        Assert-True ($actualHash -eq $expectedHash) "Checksum mismatch for $relativePath."
    }
}

function Test-PortableArtifact {
    param(
        [string]$Root
    )

    $requiredFiles = @(
        "SmartScreen.exe",
        "SmartScreen.ico",
        "SmartScreen.App.exe",
        "SmartScreen.App.dll",
        "SmartScreen.Application.dll",
        "SmartScreen.Domain.dll",
        "SmartScreen.Infrastructure.dll",
        "config\ai-providers.json",
        "config\appsettings.json",
        "config\hotkeys.json",
        "config\prompts.json",
        "config\secrets.local.example.json",
        "localization\uk-UA.json",
        "localization\en-US.json",
        "themes\themes.json",
        "release.json",
        "checksums.sha256"
    )

    foreach ($relativePath in $requiredFiles) {
        Assert-PathExists (Join-Path $Root $relativePath) "Portable artifact file"
    }

    foreach ($directory in @("config", "logs", "screenshots", "localization", "themes")) {
        Assert-PathExists (Join-Path $Root $directory) "Portable artifact directory"
    }

    Assert-PathMissing (Join-Path $Root "config\secrets.local.json") "Local secrets file"
    Test-ChecksumManifest -Root $Root
}

function Test-SetupArtifact {
    param(
        [string]$Root
    )

    foreach ($relativePath in @("Install SmartScreen.ps1", "README.txt", "SmartScreen.ico", "checksums.sha256")) {
        Assert-PathExists (Join-Path $Root $relativePath) "Setup artifact file"
    }

    Test-ChecksumManifest -Root $Root
}

function Test-InstallSmoke {
    param(
        [string]$Root
    )

    $installDir = Join-Path $Root "app"
    $startMenuDir = Join-Path $Root "start-menu"
    $desktopDir = Join-Path $Root "desktop"
    $startupDir = Join-Path $Root "startup"

    & (Join-Path $setupDir "Install SmartScreen.ps1") `
        -SkipPublish `
        -InstallDir $installDir `
        -StartMenuDirectory $startMenuDir `
        -DesktopDirectory $desktopDir `
        -StartupDirectory $startupDir `
        -CreateDesktopShortcut `
        -CreateStartupShortcut

    $requiredPaths = @(
        (Join-Path $installDir "SmartScreen.exe"),
        (Join-Path $installDir "checksums.sha256"),
        (Join-Path $installDir "install.json"),
        (Join-Path $installDir "uninstall.ps1"),
        (Join-Path $installDir "config\appsettings.json"),
        (Join-Path $installDir "localization\uk-UA.json"),
        (Join-Path $installDir "themes\themes.json"),
        (Join-Path $startMenuDir "SmartScreen.lnk"),
        (Join-Path $startMenuDir "SmartScreen Settings.lnk"),
        (Join-Path $startMenuDir "Uninstall SmartScreen.lnk"),
        (Join-Path $desktopDir "SmartScreen.lnk"),
        (Join-Path $startupDir "SmartScreen.lnk")
    )

    foreach ($path in $requiredPaths) {
        Assert-PathExists $path "Installed smoke file"
    }

    $metadata = Get-Content -Raw -Path (Join-Path $installDir "install.json") | ConvertFrom-Json
    Assert-True ([string]$metadata.InstallDir -eq $installDir) "Install metadata has an unexpected install directory."
    Assert-True ([string]$metadata.StartupShortcut -eq (Join-Path $startupDir "SmartScreen.lnk")) "Install metadata has an unexpected startup shortcut."

    & (Join-Path $installDir "uninstall.ps1") -KeepData

    Assert-PathMissing (Join-Path $installDir "SmartScreen.exe") "Uninstalled executable"
    Assert-PathMissing (Join-Path $installDir "SmartScreen.App.exe") "Uninstalled app host"
    Assert-PathMissing (Join-Path $startMenuDir "SmartScreen.lnk") "Uninstalled Start Menu shortcut"
    Assert-PathMissing (Join-Path $desktopDir "SmartScreen.lnk") "Uninstalled desktop shortcut"
    Assert-PathMissing (Join-Path $startupDir "SmartScreen.lnk") "Uninstalled startup shortcut"
    Assert-PathExists (Join-Path $installDir "config") "Kept portable config directory"
}

try {
    if (-not $SkipDotnetTests) {
        dotnet test (Join-Path $repoRoot "SmartScreen.sln")
    }

    & (Join-Path $PSScriptRoot "publish-portable.ps1") -Configuration $Configuration -Runtime $Runtime

    Test-PortableArtifact -Root $publishDir
    Test-SetupArtifact -Root $setupDir
    Test-InstallSmoke -Root $smokeRoot

    Write-Host "Release acceptance passed."
}
finally {
    if (-not $KeepSmokeInstall -and (Test-Path -LiteralPath $smokeRoot)) {
        $resolvedSmokeRoot = (Resolve-Path -LiteralPath $smokeRoot).Path
        $resolvedTemp = (Resolve-Path -LiteralPath $env:TEMP).Path
        $leaf = Split-Path -Leaf $resolvedSmokeRoot

        if ($resolvedSmokeRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -and
            $leaf.StartsWith("SmartScreenReleaseAcceptance-", [StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $resolvedSmokeRoot -Recurse -Force
        }
        else {
            Write-Warning "Smoke directory was not removed because it failed the safety check: $resolvedSmokeRoot"
        }
    }
}
