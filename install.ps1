<#
.SYNOPSIS
  Installs or updates Calendar for Command Palette from the latest GitHub release.
  Run in PowerShell (it asks for administrator rights):
    irm https://github.com/benfo/cmdpal-calendar/releases/latest/download/install.ps1 | iex
#>
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$release = 'https://github.com/benfo/cmdpal-calendar/releases/latest/download'
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host 'Asking for administrator rights to trust the signing certificate...'
    Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-NoExit', '-Command', "irm $release/install.ps1 | iex"
    return
}

function Get-PackageVersion([string] $msix) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($msix)
    try {
        $reader = [IO.StreamReader]::new($zip.GetEntry('AppxManifest.xml').Open())
        try { ([xml]$reader.ReadToEnd()).Package.Identity.Version } finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }
}

$arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$work = Join-Path ([IO.Path]::GetTempPath()) 'CmdPalCalendar-install'
New-Item -ItemType Directory -Force $work | Out-Null

try {
    $certificate = Join-Path $work 'CmdPalCalendar.cer'
    $package = Join-Path $work "CmdPalCalendar_$arch.msix"

    Write-Host 'Downloading Calendar for Command Palette...'
    Invoke-WebRequest "$release/CmdPalCalendar.cer" -OutFile $certificate -UseBasicParsing
    Invoke-WebRequest "$release/CmdPalCalendar_$arch.msix" -OutFile $package -UseBasicParsing

    $latest = Get-PackageVersion $package
    $current = (Get-AppxPackage CmdPalCalendar).Version
    if ($current -eq $latest) {
        Write-Host "Calendar for Command Palette $latest is already installed and up to date." -ForegroundColor Green
        return
    }

    Write-Host 'Trusting the signing certificate...'
    Import-Certificate -FilePath $certificate -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

    Write-Host 'Installing...'
    Get-Process CmdPalCalendar -ErrorAction SilentlyContinue | Stop-Process -Force
    Add-AppxPackage -Path $package -ForceUpdateFromAnyVersion

    $message = if ($current) { "Updated Calendar for Command Palette from $current to $latest." } else { "Installed Calendar for Command Palette $latest." }
    Write-Host $message -ForegroundColor Green

    if (-not (Get-AppxPackage Microsoft.CommandPalette)) {
        Write-Host 'Command Palette was not found. Install PowerToys (https://aka.ms/installpowertoys), then open Command Palette.' -ForegroundColor Yellow
    }
    else {
        Write-Host "Open Command Palette and run 'Reload', then pick 'Calendar'." -ForegroundColor Green
    }
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
