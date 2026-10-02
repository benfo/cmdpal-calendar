<#
.SYNOPSIS
  Builds signed MSIX packages for x64 and ARM64 into ./artifacts, plus the public signing certificate.
#>
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $CertificatePath,
    [Parameter(Mandatory)] [string] $CertificatePassword,
    [string[]] $Platforms = @('x64', 'ARM64')
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$manifest = Join-Path $root 'CmdPalCalendar\Package.appxmanifest'
$packageVersion = if ($Version.Split('.').Count -eq 3) { "$Version.0" } else { $Version }

function Find-SignTool {
    $packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget\packages' }
    $tool = Get-ChildItem (Join-Path $packages 'microsoft.windows.sdk.cpp') -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq 'x64' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $tool) { throw 'signtool.exe not found; restore the solution first (dotnet restore).' }
    $tool.FullName
}

dotnet restore (Join-Path $root 'CmdPalCalendar\CmdPalCalendar.csproj') -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
$signTool = Find-SignTool
$originalManifest = [IO.File]::ReadAllBytes($manifest)
Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $artifacts | Out-Null

try {
    $stamped = [IO.File]::ReadAllText($manifest) -replace '(<Identity[^>]*\sVersion=")[^"]+(")', "`${1}$packageVersion`${2}"
    [IO.File]::WriteAllText($manifest, $stamped, [Text.UTF8Encoding]::new($true))

    foreach ($platform in $Platforms) {
        $arch = $platform.ToLowerInvariant()
        $packageDir = Join-Path $artifacts "build-$arch"

        dotnet publish (Join-Path $root 'CmdPalCalendar\CmdPalCalendar.csproj') -c Release -p:Platform=$platform -r "win-$arch" `
            -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false -p:AppxPackageDir="$packageDir\"
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $platform." }

        $msix = Get-ChildItem $packageDir -Recurse -Filter '*.msix' | Select-Object -First 1
        $target = Join-Path $artifacts "CmdPalCalendar_$arch.msix"
        Move-Item $msix.FullName $target

        & $signTool sign /fd SHA256 /f $CertificatePath /p $CertificatePassword $target
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $platform." }

        Remove-Item $packageDir -Recurse -Force
    }

    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath, $CertificatePassword)
    [IO.File]::WriteAllBytes((Join-Path $artifacts 'CmdPalCalendar.cer'), $certificate.Export('Cert'))
}
finally {
    [IO.File]::WriteAllBytes($manifest, $originalManifest)
}

Get-ChildItem $artifacts | Select-Object Name, Length
