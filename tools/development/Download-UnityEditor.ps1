# Run with PowerShell 7. Download/resume only; installation is a separate step.
$ErrorActionPreference = 'Stop'
$url = 'https://download.unity3d.com/download_unity/e1dba0a9aba4/Windows64EditorInstaller/UnitySetup64-6000.3.25f1.exe'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$downloads = Join-Path $projectRoot '.local\development\downloads'
$target = Join-Path $downloads 'UnitySetup64-6000.3.25f1.exe'
$total = [long]4201959888
$parts = Join-Path $downloads 'Unity-6000.3.25f1-parts'
New-Item -ItemType Directory -Path $parts -Force | Out-Null
$offset = if (Test-Path $target) { (Get-Item $target).Length } else { 0L }
if ($offset -gt $total) { throw 'Existing installer is larger than the official download.' }
$ranges = @()
for ($start = $offset; $start -lt $total; $start += 64MB) {
    $ranges += [pscustomobject]@{ Start = $start; End = [Math]::Min($start + 64MB - 1, $total - 1) }
}
$ranges | ForEach-Object -ThrottleLimit 4 -Parallel {
    $ErrorActionPreference = 'Stop'
    $chunk = Join-Path $using:parts ("{0:D12}-{1:D12}.part" -f $_.Start, $_.End)
    $length = $_.End - $_.Start + 1
    if (!(Test-Path $chunk) -or (Get-Item $chunk).Length -ne $length) {
        & curl.exe -4 --fail --location --connect-timeout 15 --max-time 240 `
            --retry 5 --retry-all-errors --retry-max-time 900 --silent --show-error `
            --range ("{0}-{1}" -f $_.Start, $_.End) --output $chunk $using:url
        if ($LASTEXITCODE -ne 0) { throw "Chunk download failed: $chunk" }
        if ((Get-Item $chunk).Length -ne $length) { throw "Incorrect range length: $chunk" }
    }
    Write-Host ("Downloaded range {0}-{1}" -f $_.Start, $_.End)
}
# Check every part before appending any bytes to the existing prefix.
foreach ($range in $ranges) {
    $chunk = Join-Path $parts ("{0:D12}-{1:D12}.part" -f $range.Start, $range.End)
    if (!(Test-Path $chunk) -or (Get-Item $chunk).Length -ne $range.End - $range.Start + 1) {
        throw "Missing or incomplete chunk: $chunk"
    }
}
$destination = [IO.File]::Open($target, [IO.FileMode]::Append)
try {
    foreach ($range in $ranges) {
        $chunk = Join-Path $parts ("{0:D12}-{1:D12}.part" -f $range.Start, $range.End)
        $source = [IO.File]::OpenRead($chunk)
        try { $source.CopyTo($destination) } finally { $source.Dispose() }
    }
} finally { $destination.Dispose() }
if ((Get-FileHash $target -Algorithm MD5).Hash -ne 'F9ECE8BE816F34BD301DE62DFF1D4111') {
    throw 'Checksum does not match the official Unity release metadata.'
}
$signature = Get-AuthenticodeSignature $target
if ($signature.Status -ne 'Valid') { throw "Installer signature is not valid: $($signature.Status)" }
Write-Host 'PASS: complete Unity installer, official checksum and Authenticode signature.'
