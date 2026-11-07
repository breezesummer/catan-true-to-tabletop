param(
    [Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$Endpoint,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [switch]$GeneralSkins
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($OutputRoot)
if(Test-Path -LiteralPath $root){throw '验收目录须为新的目录。'}
New-Item -ItemType Directory -Path $root | Out-Null
$clients=@()
try {
    # 动皮验收四席分别绘制；道具压力验收只绘制主窗口，以隔离同机 GPU 争用。
    foreach($index in 1,2,3,0){
        $seatDir=(New-Item -ItemType Directory -Path (Join-Path $root "peer-$index")).FullName
        $configPath=Join-Path $seatDir 'config.json'
        @{Endpoint=$Endpoint;RuleSet='combined';ScenarioId='through-the-desert';PlayerCount=4;
            OutputDirectory=$seatDir;PeerIndex=$index;ReactionRunDirectory=$root;TimeoutSeconds=$(if($GeneralSkins){240}else{150});VerifySkinPlayback=[bool]$GeneralSkins
        } | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
        $arguments=@('--m7-verify',('"'+$configPath+'"'),'-logFile',('"'+(Join-Path $seatDir 'player.log')+'"'))
        if($index -eq 0 -or $GeneralSkins){
            $size=if($GeneralSkins){@('960','600')}else{@('1440','900')}
            $arguments+=@('-force-d3d11','-screen-fullscreen','0','-screen-width',$size[0],'-screen-height',$size[1])
            $clients+=Start-Process -FilePath $Player -ArgumentList $arguments -PassThru
        }else{
            $arguments+=@('-batchmode','-nographics')
            $clients+=Start-Process -FilePath $Player -ArgumentList $arguments -WindowStyle Hidden -PassThru
        }
    }
    $limit=[DateTime]::UtcNow.AddSeconds($(if($GeneralSkins){270}else{180}))
    while(@($clients | Where-Object {!$_.HasExited}).Count){
        if(@($clients | Where-Object {$_.HasExited -and $_.ExitCode -ne 0}).Count){throw '有席位验收失败，请查看各席日志。'}
        if([DateTime]::UtcNow -gt $limit){throw '验收超时，请查看各席日志。'}
        Start-Sleep -Seconds 1
    }
    $results=@(Get-ChildItem -Path $root -Recurse -Filter summary.json | ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json})
    if($results.Count -ne 4 -or @($results | Where-Object {$_.Result -ne 'passed'}).Count){throw '四席验收未全部完成。'}
    $results | Format-Table Result,Stage,Transport,HumanCommands,Revision
} finally {
    foreach($client in $clients){if(!$client.HasExited){Stop-Process -Id $client.Id};$client.Dispose()}
}
