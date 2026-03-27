param(
    [string]$AppName = "KunQiongBrowser",
    [string]$MinMacOS = "12.0",
    [string]$AppVersion = "1.0.0",
    [switch]$Portable = $true
)

$ErrorActionPreference = "Stop"

if (-not $IsMacOS) {
    throw "This script must run on macOS."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$shellScript = Join-Path $PSScriptRoot "build-macos-native.sh"

if (!(Test-Path $shellScript)) {
    throw "Script not found: $shellScript"
}

$env:APP_NAME = $AppName
$env:MIN_MACOS = $MinMacOS
$env:APP_VERSION = $AppVersion
$env:PORTABLE_MODE = if ($Portable) { "1" } else { "0" }

& bash $shellScript
