# Builds the InteractiveMask MSI installer.
#
#   .\build-installer.ps1                                -> build\publish\InteractiveMask-1.0.0.msi
#   .\build-installer.ps1 -Version 1.2.0                 -> stamps version into MSI metadata
#   .\build-installer.ps1 -Version 1.0.4 -Sign `
#       -CertSubject 'IDIS Nederland BV'                 -> signs all PE files + the MSI
#                                                          using the cert in CurrentUser\My
#                                                          (SafeNet token must be plugged in)
#
# Requires:
#   - .NET SDK 9 (or compatible)
#   - WiX SDK is fetched automatically via NuGet on first build
#   - For -Sign: Windows 10/11 SDK (signtool.exe) + a code-signing cert
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Version = '1.0.0',
    [string]$Rid = 'win-x64',
    [switch]$Sign,
    [string]$CertSubject,
    [string]$CertThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$publishRoot      = Join-Path $PSScriptRoot 'build\publish'
$displayPublish   = Join-Path $publishRoot   'Display'
$webHostPublish   = Join-Path $publishRoot   'WebHost'
$signScript       = Join-Path $PSScriptRoot 'tools\sign-build.ps1'

if ($Sign -and -not $CertSubject -and -not $CertThumbprint) {
    throw "-Sign requires either -CertSubject or -CertThumbprint"
}

if (Test-Path $publishRoot) { Remove-Item $publishRoot -Recurse -Force }
New-Item -ItemType Directory -Path $publishRoot | Out-Null

Write-Host "==> Publishing Display ($Configuration / $Rid, self-contained)" -ForegroundColor Cyan
# Self-contained = bundle the .NET 9 + WindowsDesktop runtime alongside the app
# so the installer works on any clean Windows x64 PC. Adds ~80 MB to the MSI but
# removes the runtime install dependency (zorginstelling deploys, SCCM/Intune).
dotnet publish .\src\InteractiveMask.Display\InteractiveMask.Display.csproj `
    -c $Configuration -r $Rid --self-contained true -o $displayPublish `
    -p:PublishSingleFile=false `
    -p:Platform=x64

Write-Host "==> Publishing WebHost ($Configuration / $Rid, self-contained)" -ForegroundColor Cyan
dotnet publish .\src\InteractiveMask.WebHost\InteractiveMask.WebHost.csproj `
    -c $Configuration -r $Rid --self-contained true -o $webHostPublish `
    -p:PublishSingleFile=false `
    -p:Platform=x64

# Drop dev-only secrets so they never end up in the installer.
$localSettings = Join-Path $displayPublish 'appsettings.Local.json'
if (Test-Path $localSettings) { Remove-Item $localSettings -Force }

# Sign all our own EXE/DLLs BEFORE WiX harvest so the cab inside the MSI
# contains signed files. Skip Microsoft / IDIS DLLs (they're already signed by
# their vendors; re-signing them is unnecessary and would fail anyway because
# we don't own the cert chain). The pattern matches anything starting with
# "InteractiveMask." which covers Display, WebHost, Ipc, Gdk.
if ($Sign) {
    Write-Host "==> Signing application binaries" -ForegroundColor Cyan
    $ourBinaries = @()
    $ourBinaries += Get-ChildItem -Path $displayPublish -Recurse -Include 'InteractiveMask.*.exe','InteractiveMask.*.dll' | ForEach-Object FullName
    $ourBinaries += Get-ChildItem -Path $webHostPublish -Recurse -Include 'InteractiveMask.*.exe','InteractiveMask.*.dll' | ForEach-Object FullName

    & $signScript -Files $ourBinaries `
                  -CertSubject $CertSubject `
                  -CertThumbprint $CertThumbprint `
                  -TimestampUrl $TimestampUrl
}

Write-Host "==> Building MSI (version $Version)" -ForegroundColor Cyan
dotnet build .\src\InteractiveMask.Setup\InteractiveMask.Setup.wixproj `
    -c $Configuration `
    -p:Version=$Version

$msi = Get-ChildItem -Path .\src\InteractiveMask.Setup\bin -Recurse -Filter "InteractiveMask-$Version.msi" |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msi) { throw "MSI not found under src\InteractiveMask.Setup\bin" }

Copy-Item $msi.FullName -Destination $publishRoot -Force
$finalMsi = Join-Path $publishRoot $msi.Name

# Sign the MSI itself last — must be after WiX wraps the cab, otherwise the
# signature is invalidated. Re-signing the MSI doesn't change the embedded
# signed files; they were signed in the previous step.
if ($Sign) {
    Write-Host "==> Signing MSI" -ForegroundColor Cyan
    & $signScript -Files @($finalMsi) `
                  -CertSubject $CertSubject `
                  -CertThumbprint $CertThumbprint `
                  -TimestampUrl $TimestampUrl
}

Write-Host "==> Done. Installer at: $finalMsi" -ForegroundColor Green
