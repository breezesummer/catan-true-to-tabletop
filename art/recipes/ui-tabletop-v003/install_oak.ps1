param()
$ErrorActionPreference='Stop'
$oakRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$oakSource=Join-Path $oakRepo 'art/source/ui-tabletop-v003/oak-table-albedo-v001.png'
$oakExpected='E6133B0A6B36618739A10E791E0D01F9A21D77B105716FD8B7144D661974124C'
if((Get-FileHash -LiteralPath $oakSource -Algorithm SHA256).Hash -ne $oakExpected){throw '橡木原始生成图校验不符，停止导入。'}
$oakResources=Join-Path $oakRepo 'Unity/Catan/Assets/Resources'
function Get-OakGuid([string]$name){
    $oakHasher=[Security.Cryptography.SHA256]::Create()
    try{return [BitConverter]::ToString($oakHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes('tabletop-ui-v003/'+$name))).Replace('-','').ToLowerInvariant().Substring(0,32)}
    finally{$oakHasher.Dispose()}
}
foreach($oakFolder in @('TabletopUI','TabletopUI/v003')){
    $oakFolderPath=Join-Path $oakResources $oakFolder
    New-Item -ItemType Directory -Force -Path $oakFolderPath | Out-Null
    $oakGuid=Get-OakGuid $oakFolder
    @('fileFormatVersion: 2',"guid: $oakGuid",'folderAsset: yes','DefaultImporter:','  externalObjects: {}','  userData:','  assetBundleName:','  assetBundleVariant:') | Set-Content -LiteralPath ($oakFolderPath+'.meta') -Encoding utf8
}
$oakTarget=Join-Path $oakResources 'TabletopUI/v003/oak-table-albedo.png'
Copy-Item -LiteralPath $oakSource -Destination $oakTarget
$oakMeta=Get-Content -LiteralPath (Join-Path $oakResources 'PhotoSamples/v001/wood-front.png.meta') -Raw -Encoding UTF8
$oakMeta=$oakMeta -replace '(?m)^guid: .+$',('guid: '+(Get-OakGuid 'oak-table-albedo.png'))
$oakMeta=$oakMeta -replace 'enableMipMap: 0','enableMipMap: 1' -replace '(?m)^    aniso: .+$','    aniso: 8'
$oakMeta=$oakMeta -replace '(?m)^    wrapU: .+$','    wrapU: 1' -replace '(?m)^    wrapV: .+$','    wrapV: 1'
$oakMeta=$oakMeta -replace 'maxTextureSize: 1024','maxTextureSize: 2048'
($oakMeta -replace '(?m)[ \t]+$','').TrimEnd() | Set-Content -LiteralPath ($oakTarget+'.meta') -Encoding utf8
Write-Host '橡木位图已按 v003 配方导入；桌面网格由场景代码生成。'
