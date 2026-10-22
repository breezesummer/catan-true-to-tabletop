param([Parameter(Mandatory=$true)][string]$RunDirectory,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
function Distribution($Rows,[string]$Column){
    $values=@($Rows|Where-Object {$_.$Column -ne '' -and [double]$_.$Column -ge 0}|ForEach-Object {[double]$_.$Column}|Sort-Object)
    if(!$values.Count){return @{count=0;p50=$null;p95=$null;p99=$null;max=$null}}
    return @{count=$values.Count;p50=$values[[Math]::Ceiling($values.Count*.5)-1];p95=$values[[Math]::Ceiling($values.Count*.95)-1];p99=$values[[Math]::Ceiling($values.Count*.99)-1];max=$values[-1]}
}
$result=foreach($case in @('ordinary','maximum','crowded','fog')){
    $dir=Join-Path $RunDirectory $case
    $summary=Get-Content (Join-Path $dir 'summary.json') -Raw|ConvertFrom-Json
    $rows=@(Import-Csv (Join-Path $dir 'frames.csv'))
    if($rows.Count -ne $summary.sampleCount -or !$summary.authorityUnchanged){throw '采样完整性检查失败'}
    $metrics=[ordered]@{}
    foreach($column in @('frameMs','cpuMs','cpuMainMs','cpuRenderMs','presentWaitMs','gpuMs','gcAllocatedBytes','mainThreadAllocatedBytes','guiMs','gameUpdateMs')){$metrics[$column]=Distribution $rows $column}
    $prep=Get-Content (Join-Path $dir 'preparation.json') -Raw|ConvertFrom-Json
    [ordered]@{scenario=$case;device=$summary.device;count=$rows.Count;metrics=$metrics;preparation=$prep;gcCollections=$summary.gcCollections;gcAllocatedBytesTotal=$summary.gcAllocatedBytesTotal;gcBytesPerSecond=$(if($summary.gcRecorderAvailable){$summary.gcAllocatedBytesTotal/$summary.sampleSeconds}else{$null});census=$summary.sceneCensus;drawCalls=$summary.drawCalls;triangles=$summary.triangles;cpuPeaks=@($rows|Sort-Object {[double]$_.cpuMs} -Descending|Select-Object -First 5);firstFrames=@($rows|Select-Object -First 8)}
}
$result|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $Output -Encoding utf8
