param([string]$EvidenceVersion='entity-integration-v003',[switch]$ArtOnly)
$ErrorActionPreference='Stop'
$integrationRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$integrationPlayer=Join-Path $integrationRepo '.local/m6/Builds/Windows/CatanM6.exe'
$integrationOutput=Join-Path $integrationRepo "docs/verification/$EvidenceVersion"
$integrationLogs=Join-Path $integrationRepo '.local/art-integration-v003/logs'
$integrationSmoke=Join-Path $integrationRepo '.local/art-integration-v003/m6-smoke'
New-Item -ItemType Directory -Force $integrationOutput,$integrationLogs,$integrationSmoke | Out-Null
$integrationModes=if($ArtOnly){@('entity-integration')}else{@('entity-integration','player-components','m6-save','m6-load')}
foreach($integrationMode in $integrationModes){
    $integrationFlag=if($integrationMode -eq 'm6-save'){'--m6-verify-save'}elseif($integrationMode -eq 'm6-load'){'--m6-verify-load'}else{"--$integrationMode-verify"}
    $integrationTarget=if($integrationMode.StartsWith('m6-')){$integrationSmoke}else{$integrationOutput}
    $integrationLog=Join-Path $integrationLogs "$integrationMode.log"
    $integrationProcess=Start-Process -FilePath $integrationPlayer -ArgumentList @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',$integrationFlag,('"'+$integrationTarget+'"'),'-logFile',('"'+$integrationLog+'"')) -WindowStyle Hidden -PassThru
    $integrationDeadline=[DateTime]::UtcNow.AddMinutes(6)
    while(!$integrationProcess.WaitForExit(1000)){
        if([DateTime]::UtcNow -gt $integrationDeadline){Stop-Process -Id $integrationProcess.Id;throw "Verification timed out: $integrationLog"}
    }
    $integrationMarker=if($integrationMode -eq 'entity-integration'){'CATAN_ENTITY_INTEGRATION_OK'}elseif($integrationMode -eq 'player-components'){'CATAN_PLAYER_COMPONENTS_OK'}elseif($integrationMode -eq 'm6-save'){'CATAN_M6_PLAYER_SAVE_OK'}else{'CATAN_M6_PLAYER_LOAD_OK'}
    if($integrationProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $integrationLog -Pattern $integrationMarker -Quiet)){throw "Verification failed: $integrationLog"}
    if(Select-String -LiteralPath $integrationLog -Pattern 'Exception:|_FAILED:' -Quiet){throw "Runtime error: $integrationLog"}
    Write-Host "$integrationMode verified"
}
if(!$ArtOnly){[ordered]@{status='passed';ruleSets=@('base','seafarers','cities-knights','combined');independentProcesses=2;saveRestore=$true;continuedAi=$true;privateSaves='.local/art-integration-v003/m6-smoke'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $integrationOutput 'm6-smoke.json') -Encoding utf8}
Write-Host 'CATAN_ENTITY_INTEGRATION_VERIFIED'
