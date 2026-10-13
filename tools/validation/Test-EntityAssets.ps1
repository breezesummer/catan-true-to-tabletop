$ErrorActionPreference = 'Stop'
$entityRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$entityPlayer = Join-Path $entityRepo '.local/art-pipeline/Builds/Windows/CatanEntityAssets-v001.exe'
$entityEvidence = Join-Path $entityRepo 'docs/verification/entity-conversion-v001'
$entityLog = Join-Path $entityRepo '.local/art-pipeline/logs/player-verification.log'
New-Item -ItemType Directory -Force -Path $entityEvidence,(Split-Path $entityLog) | Out-Null
$entityArgs = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--entity-verify',('"'+$entityEvidence+'"'),'-logFile',('"'+$entityLog+'"'))
$entityProcess = Start-Process -FilePath $entityPlayer -ArgumentList $entityArgs -WindowStyle Hidden -PassThru
$entityDeadline = [DateTime]::UtcNow.AddMinutes(10)
while (!$entityProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $entityDeadline) { Stop-Process -Id $entityProcess.Id; throw "Entity player timed out: $entityLog" }
}
if ($entityProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $entityLog -Pattern 'CATAN_ENTITY_PLAYER_OK' -Quiet)) { throw "Entity player failed: $entityLog" }
if (Select-String -LiteralPath $entityLog -Pattern 'Exception:|NullReferenceException|MissingReferenceException|CATAN_ENTITY_PLAYER_FAILED' -Quiet) { throw "Entity runtime errors: $entityLog" }
Write-Host 'CATAN_ENTITY_WINDOWS_VERIFIED'
