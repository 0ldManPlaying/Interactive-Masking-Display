# Builds the InteractiveMask MSI installer.
#
#   .\build-installer.ps1                -> publishes Release builds and produces
#                                            build\publish\InteractiveMask-1.0.0.msi
#   .\build-installer.ps1 -Version 1.2.0 -> stamps the version into the MSI metadata
#
# Requires:
#   - .NET SDK 9 (or compatible)
#   - WiX SDK is fetched automatically via NuGet on first build (no manual install)
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Version = '1.0.0',
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$publishRoot      = Join-Path $PSScriptRoot 'build\publish'
$displayPublish   = Join-Path $publishRoot   'Display'
$webHostPublish   = Join-Path $publishRoot   'WebHost'
$installerOut     = Join-Path $publishRoot   ''

if (Test-Path $publishRoot) { Remove-Item $publishRoot -Recurse -Force }
New-Item -ItemType Directory -Path $publishRoot | Out-Null

Write-Host "==> Publishing Display ($Configuration / $Rid)" -ForegroundColor Cyan
dotnet publish .\src\InteractiveMask.Display\InteractiveMask.Display.csproj `
    -c $Configuration -r $Rid --self-contained false -o $displayPublish `
    -p:PublishSingleFile=false `
    -p:Platform=x64

Write-Host "==> Publishing WebHost ($Configuration / $Rid)" -ForegroundColor Cyan
dotnet publish .\src\InteractiveMask.WebHost\InteractiveMask.WebHost.csproj `
    -c $Configuration -r $Rid --self-contained false -o $webHostPublish `
    -p:PublishSingleFile=false `
    -p:Platform=x64

# Drop dev-only secrets so they never end up in the installer.
$localSettings = Join-Path $displayPublish 'appsettings.Local.json'
if (Test-Path $localSettings) { Remove-Item $localSettings -Force }

Write-Host "==> Building MSI (version $Version)" -ForegroundColor Cyan
dotnet build .\src\InteractiveMask.Setup\InteractiveMask.Setup.wixproj `
    -c $Configuration `
    -p:Version=$Version `
    -p:DisplayPublishDir=$displayPublish `
    -p:WebHostPublishDir=$webHostPublish

$msi = Get-ChildItem -Path .\src\InteractiveMask.Setup\bin -Recurse -Filter "InteractiveMask-$Version.msi" |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msi) { throw "MSI not found under src\InteractiveMask.Setup\bin" }

Copy-Item $msi.FullName -Destination $publishRoot -Force
Write-Host "==> Done. Installer at: $($publishRoot)\$($msi.Name)" -ForegroundColor Green
