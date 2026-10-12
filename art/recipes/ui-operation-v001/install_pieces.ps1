param([string]$ExportDirectory = '')
$ErrorActionPreference = 'Stop'
$pieceRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (!$ExportDirectory) { $ExportDirectory = Join-Path $pieceRepo 'art/exports/ui-operation-v001/pieces' }
$pieceManifest = Get-Content -LiteralPath (Join-Path $ExportDirectory 'manifest.json') -Raw | ConvertFrom-Json
$pieceResources = Join-Path $pieceRepo 'Unity/Catan/Assets/Resources'
$pieceTemplate = Get-Content -LiteralPath (Join-Path $pieceResources 'PhotoSamples/v001/wood-front.png.meta') -Raw
function Get-PieceGuid([string]$name) {
    $pieceHasher = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($pieceHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes('operation-ui-v001/' + $name))).Replace('-', '').ToLowerInvariant().Substring(0,32) }
    finally { $pieceHasher.Dispose() }
}
foreach ($pieceFolder in @('OperationUI', 'OperationUI/v001', 'OperationUI/v001/pieces')) {
    $pieceFolderPath = Join-Path $pieceResources $pieceFolder
    New-Item -ItemType Directory -Path $pieceFolderPath -Force | Out-Null
    $pieceGuid = Get-PieceGuid $pieceFolder
    @("fileFormatVersion: 2", "guid: $pieceGuid", 'folderAsset: yes', 'DefaultImporter:', '  externalObjects: {}', '  userData: ', '  assetBundleName: ', '  assetBundleVariant: ') | Set-Content -LiteralPath ($pieceFolderPath + '.meta') -Encoding utf8
}
foreach ($piece in $pieceManifest.assets) {
    $pieceSource = Join-Path $ExportDirectory $piece.file
    $pieceHash = (Get-FileHash -LiteralPath $pieceSource -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($pieceHash -ne $piece.sha256) { throw "Thumbnail hash mismatch: $($piece.file)" }
    $pieceTarget = Join-Path $pieceResources ('OperationUI/v001/pieces/' + $piece.file)
    Copy-Item -LiteralPath $pieceSource -Destination $pieceTarget
    $pieceGuid = Get-PieceGuid $piece.file
    $pieceMeta = $pieceTemplate -replace '(?m)^guid: .+$', ('guid: ' + $pieceGuid)
    $pieceMeta = $pieceMeta -replace 'enableMipMap: 1', 'enableMipMap: 0' -replace 'maxTextureSize: 2048', 'maxTextureSize: 256'
    $pieceMeta = $pieceMeta -replace 'filterMode: 2', 'filterMode: 1' -replace 'alphaIsTransparency: 0', 'alphaIsTransparency: 1'
    $pieceMeta = $pieceMeta -replace 'nPOTScale: 1', 'nPOTScale: 0' -replace 'textureCompression: 1', 'textureCompression: 0'
    $pieceMeta = $pieceMeta -replace 'isReadable: 0', 'isReadable: 1'
    $pieceMeta | Set-Content -LiteralPath ($pieceTarget + '.meta') -Encoding utf8
}
Write-Output "OPERATION_PIECES_INSTALLED $($pieceManifest.assets.Count)"
