# Build a multi-size .ico from a set of PNG files. ICO format embeds raw PNG
# bytes per size for >= 32px (Vista+ supported). Each ICONDIRENTRY is 16 bytes.
# Use: pwsh build-ico.ps1 -OutputPath foo.ico -Pngs @("16.png","32.png",...)
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$OutputPath,
    [Parameter(Mandatory)] [string[]]$Pngs
)

$entries = foreach ($path in $Pngs) {
    if (-not (Test-Path $path)) { throw "missing: $path" }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    # PNG: width is bytes 16..19 big-endian, height 20..23.
    $w = [int]$bytes[16]*16777216 + [int]$bytes[17]*65536 + [int]$bytes[18]*256 + [int]$bytes[19]
    $h = [int]$bytes[20]*16777216 + [int]$bytes[21]*65536 + [int]$bytes[22]*256 + [int]$bytes[23]
    [pscustomobject]@{ W=$w; H=$h; Bytes=$bytes }
}

$count = $entries.Count
$header = New-Object byte[] (6 + 16*$count)
[byte[]]@(0,0,1,0) | ForEach-Object { $i=0 } { $header[$i]=$_; $i++ }   # reserved + type
$header[4] = $count -band 0xff
$header[5] = ($count -shr 8) -band 0xff

$dataOffset = 6 + 16*$count
$entryIdx = 6
$dataBlobs = New-Object System.Collections.Generic.List[byte[]]

foreach ($e in $entries) {
    $size = $e.Bytes.Length
    # ICONDIRENTRY:
    #   bWidth (1)   - 0 = 256
    #   bHeight (1)  - 0 = 256
    #   bColorCount (1) - 0 (truecolor)
    #   bReserved (1) - 0
    #   wPlanes (2)  - 1
    #   wBitCount (2)- 32
    #   dwBytesInRes (4)
    #   dwImageOffset (4)
    $header[$entryIdx + 0] = if ($e.W -ge 256) { 0 } else { $e.W }
    $header[$entryIdx + 1] = if ($e.H -ge 256) { 0 } else { $e.H }
    $header[$entryIdx + 2] = 0
    $header[$entryIdx + 3] = 0
    $header[$entryIdx + 4] = 1
    $header[$entryIdx + 5] = 0
    $header[$entryIdx + 6] = 32
    $header[$entryIdx + 7] = 0
    $header[$entryIdx + 8]  =  $size        -band 0xff
    $header[$entryIdx + 9]  = ($size -shr 8) -band 0xff
    $header[$entryIdx + 10] = ($size -shr 16) -band 0xff
    $header[$entryIdx + 11] = ($size -shr 24) -band 0xff
    $header[$entryIdx + 12] =  $dataOffset        -band 0xff
    $header[$entryIdx + 13] = ($dataOffset -shr 8) -band 0xff
    $header[$entryIdx + 14] = ($dataOffset -shr 16) -band 0xff
    $header[$entryIdx + 15] = ($dataOffset -shr 24) -band 0xff
    $entryIdx += 16
    $dataOffset += $size
    $dataBlobs.Add($e.Bytes)
}

$fs = [System.IO.File]::Create($OutputPath)
try {
    $fs.Write($header, 0, $header.Length)
    foreach ($blob in $dataBlobs) {
        $fs.Write($blob, 0, $blob.Length)
    }
}
finally {
    $fs.Dispose()
}

Write-Host "Wrote $OutputPath ($count sizes, $((Get-Item $OutputPath).Length) bytes)"
