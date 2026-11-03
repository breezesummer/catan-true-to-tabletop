$ErrorActionPreference='Stop'
$cardRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cardSource=Join-Path $cardRepo 'Unity/Catan/Assets/Art/CardRedraw/v002'
$cardTarget=Join-Path $cardRepo 'Unity/Catan/Assets/Resources/CardRedraw/v002'
$cardIds=@('alchemy','crane','engineering','invention','irrigation','medicine','mining','printing','road-building','smithing','commercial-harbor','guild-dues','merchant','merchant-fleet','resource-monopoly','trade-monopoly','diplomacy','espionage','encouragement','intrigue','taxation','constitution','treason','wedding','sabotage','base-knight','base-road-building','base-year-of-plenty','base-monopoly','base-victory-point','cloth','coin','paper')
$cardMissing=@($cardIds | Where-Object {!(Test-Path -LiteralPath (Join-Path $cardSource ($_.ToString()+'.png')))})
if($cardMissing.Count){throw ('重绘素材尚未齐备：'+($cardMissing -join '、'))}
New-Item -ItemType Directory -Force $cardTarget | Out-Null
$cardHashes=@{}
$cardInventory=@(foreach($id in $cardIds){
    $source=Join-Path $cardSource ($id+'.png')
    $target=Join-Path $cardTarget ($id+'.png')
    Copy-Item -LiteralPath $source -Destination $target -Force
    $sourceHash=(Get-FileHash -LiteralPath $source).Hash
    if($cardHashes.ContainsKey($sourceHash)){throw ('不同牌面使用同一文件：'+$id+' / '+$cardHashes[$sourceHash])}
    $cardHashes[$sourceHash]=$id
    if((Get-FileHash -LiteralPath $target).Hash -ne $sourceHash){throw ('素材复制不一致：'+$id)}
    [pscustomobject]@{id=$id;source=('Unity/Catan/Assets/Art/CardRedraw/v002/'+$id+'.png');resource=('CardRedraw/v002/'+$id);sha256=$sourceHash}
})
$cardInventory | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 (Join-Path $cardRepo 'art/recipes/development-commodity-redraw-v002/素材清单.json')
Write-Host '30种发展牌与3种商品牌已接入Resources。'
