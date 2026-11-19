param([string]$SourceDirectory)
$ErrorActionPreference='Stop'
$cardRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cardTarget=Join-Path $cardRepo 'Unity/Catan/Assets/Resources/CardRedraw/v002'
$cardIds=@('alchemy','crane','engineering','invention','irrigation','medicine','mining','printing','road-building','smithing','commercial-harbor','guild-dues','merchant','merchant-fleet','resource-monopoly','trade-monopoly','diplomacy','espionage','encouragement','intrigue','taxation','constitution','treason','wedding','sabotage','base-knight','base-road-building','base-year-of-plenty','base-monopoly','base-victory-point','cloth','coin','paper')
if (!$SourceDirectory) {
    $legacySource = Join-Path $cardRepo 'Unity/Catan/Assets/Art/CardRedraw/v002'
    $SourceDirectory = if (Get-ChildItem -LiteralPath $legacySource -Filter '*.png' -File -ErrorAction SilentlyContinue) { $legacySource } else { $cardTarget }
}
$sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path
$cardMissing=@($cardIds | Where-Object {!(Test-Path -LiteralPath (Join-Path $sourceRoot ($_.ToString()+'.png')))})
if($cardMissing.Count){throw ('重绘素材尚未齐备：'+($cardMissing -join '、'))}
$cardHashes=@{}
$cardInventory=@(foreach($id in $cardIds){
    $source=Join-Path $sourceRoot ($id+'.png')
    $cardHash=(Get-FileHash -LiteralPath $source).Hash
    if($cardHashes.ContainsKey($cardHash)){throw ('不同牌面使用同一文件：'+$id+' / '+$cardHashes[$cardHash])}
    $cardHashes[$cardHash]=$id
    [pscustomobject]@{id=$id;source=('Unity/Catan/Assets/Resources/CardRedraw/v002/'+$id+'.png');resource=('CardRedraw/v002/'+$id);sha256=$cardHash}
})
if ($sourceRoot -ne $cardTarget) {
    New-Item -ItemType Directory -Force -Path $cardTarget | Out-Null
    foreach ($card in $cardInventory) {
        $name = $card.id + '.png'
        $target = Join-Path $cardTarget $name
        Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target).Hash -ne $card.sha256) { throw "牌面复制校验失败：$name" }
    }
}
$manifest = Join-Path $cardRepo 'art/recipes/development-commodity-redraw-v002/素材清单.json'
$json = ($cardInventory | ConvertTo-Json -Depth 4).Replace([Environment]::NewLine, [string][char]10) + [char]10
if (!(Test-Path -LiteralPath $manifest) -or [IO.File]::ReadAllText($manifest) -cne $json) {
    [IO.File]::WriteAllText($manifest, $json, [Text.UTF8Encoding]::new($false))
}
Write-Host '30种发展牌与3种商品牌已接入Resources。'
