# Authenticode signing helper for the InteractiveMask build pipeline.
#
# Designed for SafeNet Authentication Client EV-token workflows: the cert lives
# in CurrentUser\My once the USB token is plugged in and unlocked. signtool
# finds it via /n "<Subject CN>" or /sha1 <thumbprint>; the SafeNet client
# handles the PIN prompt (one-time per session if single-logon mode is on).
#
# Usage:
#   .\sign-build.ps1 -CertSubject "IDIS Nederland BV" -Files @('a.exe','b.dll')
#   .\sign-build.ps1 -CertThumbprint <40-hex> -Files (Get-ChildItem build\publish -Recurse -Include *.exe,*.dll).FullName
#
# Conventions:
#   - Always SHA-256 file digest + SHA-256 timestamp digest (no SHA-1 dual-sign).
#   - Timestamp URL defaults to DigiCert. Override via -TimestampUrl for Sectigo/GlobalSign.
#   - Files that are already validly signed (correct subject + valid chain)
#     are skipped unless -Force is set.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string[]]$Files,
    [string]$CertSubject,
    [string]$CertThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Find-Signtool {
    # Prefer the newest signtool.exe under "Program Files (x86)\Windows Kits\10\bin"
    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (-not (Test-Path $kitsRoot)) { throw "Windows SDK not found under $kitsRoot. Install the Windows 10/11 SDK." }
    $candidate = Get-ChildItem -Path $kitsRoot -Recurse -Filter 'signtool.exe' -ErrorAction SilentlyContinue |
                 Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
                 Sort-Object FullName -Descending |
                 Select-Object -First 1
    if (-not $candidate) { throw "signtool.exe not found under $kitsRoot." }
    return $candidate.FullName
}

function Test-AlreadySigned {
    param([string]$Path)
    try {
        $sig = Get-AuthenticodeSignature -FilePath $Path -ErrorAction Stop
        return $sig.Status -eq 'Valid'
    }
    catch { return $false }
}

if (-not $CertSubject -and -not $CertThumbprint) {
    throw "Provide either -CertSubject or -CertThumbprint (CertSubject preferred for SafeNet)."
}

$signtool = Find-Signtool
Write-Host "==> signtool: $signtool" -ForegroundColor DarkGray

$signArgs = @('sign', '/fd', 'sha256', '/td', 'sha256', '/tr', $TimestampUrl)
if ($CertSubject)    { $signArgs += @('/n',    $CertSubject) }
if ($CertThumbprint) { $signArgs += @('/sha1', $CertThumbprint) }

$signed   = 0
$skipped  = 0
$failed   = @()

foreach ($file in $Files) {
    if (-not (Test-Path $file)) {
        Write-Warning "skip (missing): $file"
        continue
    }
    if (-not $Force -and (Test-AlreadySigned $file)) {
        $skipped++
        continue
    }

    Write-Host "  sign: $file" -ForegroundColor DarkGray
    & $signtool @signArgs $file
    if ($LASTEXITCODE -ne 0) {
        $failed += $file
    } else {
        $signed++
    }
}

Write-Host ("==> signed {0}, skipped {1}, failed {2}" -f $signed, $skipped, $failed.Count) -ForegroundColor Green
if ($failed.Count -gt 0) {
    $failed | ForEach-Object { Write-Host "    FAILED: $_" -ForegroundColor Red }
    throw "$($failed.Count) file(s) failed to sign"
}
