<#
.SYNOPSIS
  Builds the extension and registers it with Windows so Command Palette can load it.
  Requires Developer Mode (Settings > System > For developers).
#>
param(
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Debug',
    [ValidateSet('x64', 'ARM64')] [string] $Platform = $(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' })
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$rid = if ($Platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }

# Stop a running instance first: it locks the files in the output folder.
Get-Process CmdPalCalendar -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet build "$root\CmdPalCalendar.sln" -c $Configuration -p:Platform=$Platform
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$manifest = Join-Path $root "CmdPalCalendar\bin\$Platform\$Configuration\net10.0-windows10.0.26100.0\$rid\AppxManifest.xml"
if (-not (Test-Path $manifest)) { throw "AppxManifest.xml not found at $manifest" }

try {
    Add-AppxPackage -Register $manifest -ForceUpdateFromAnyVersion
}
catch {
    # 0x80073CFB: in Developer Mode a manifest change can't be re-registered over the old package.
    # Remove it and register fresh, keeping settings, which live in the package's LocalState.
    if ($_.Exception.HResult -ne 0x80073CFB -and $_.Exception.Message -notmatch '0x80073CFB') { throw }

    $existing = Get-AppxPackage CmdPalCalendar
    $localState = Join-Path $env:LOCALAPPDATA "Packages\$($existing.PackageFamilyName)\LocalState"
    $backup = Join-Path ([IO.Path]::GetTempPath()) "CmdPalCalendar-LocalState"
    Remove-Item $backup -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $localState) { Copy-Item $localState $backup -Recurse }

    Write-Host "Manifest changed; reinstalling (settings and cache are preserved)." -ForegroundColor Yellow
    Remove-AppxPackage $existing.PackageFullName
    Add-AppxPackage -Register $manifest

    if (Test-Path $backup) {
        $newLocalState = Join-Path $env:LOCALAPPDATA "Packages\$((Get-AppxPackage CmdPalCalendar).PackageFamilyName)\LocalState"
        New-Item -ItemType Directory -Force $newLocalState | Out-Null
        Copy-Item "$backup\*" $newLocalState -Recurse -Force
        Remove-Item $backup -Recurse -Force
    }
}
Get-AppxPackage CmdPalCalendar | Select-Object Name, Version, InstallLocation | Format-List

Write-Host "Registered. In Command Palette, run 'Reload' (Reload Command Palette extensions)." -ForegroundColor Green
