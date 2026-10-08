param(
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manifestPath = Join-Path $repoRoot 'assets/references/rules/v001/sources.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$cacheRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $manifest.cacheRoot))
$cachePrefix = $cacheRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

function Assert-ReferenceFile {
    param([string]$Path, [object]$Source)
    $file = Get-Item -LiteralPath $Path
    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($file.Length -ne $Source.byteLength -or $actualHash -ne $Source.sha256) {
        throw "Reference mismatch for $($Source.id). Preserved file: $Path. Expected $($Source.byteLength) bytes / $($Source.sha256); got $($file.Length) bytes / $actualHash. Review the source and register a new version; do not overwrite the pinned original."
    }
    Write-Output "VERIFIED $($Source.id): $($file.Length) bytes, SHA-256 $actualHash"
}

foreach ($source in $manifest.sources) {
    if ($source.sha256 -notmatch '^[0-9a-f]{64}$' -or $source.byteLength -le 0) {
        throw "Invalid locked metadata for $($source.id)."
    }
    $targetPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $source.cachePath))
    if (-not $targetPath.StartsWith($cachePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Reference path is outside the pinned cache: $targetPath"
    }
    if (Test-Path -LiteralPath $targetPath) {
        Assert-ReferenceFile -Path $targetPath -Source $source
        continue
    }
    if ($VerifyOnly) {
        throw "Reference missing: $targetPath. Run without -VerifyOnly to download the pinned source."
    }
    if ([Uri]$source.url -notmatch '^https://www\.catan\.com/') {
        throw "Unexpected reference host for $($source.id)."
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $targetPath) -Force | Out-Null
    $downloadPath = $targetPath + '.download-' + [Guid]::NewGuid().ToString('N')
    # Download separately. A network or hash failure retains this file for inspection.
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $source.url -OutFile $downloadPath
    Assert-ReferenceFile -Path $downloadPath -Source $source
    # Move without -Force: an existing pinned reference must never be replaced.
    Move-Item -LiteralPath $downloadPath -Destination $targetPath
}
