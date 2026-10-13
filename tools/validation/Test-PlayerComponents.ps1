$ErrorActionPreference = 'Stop'
$componentRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$componentPlayer = Join-Path $componentRepo '.local/art-pipeline/Builds/Windows/CatanPlayerComponents-v002.exe'
$componentEvidence = Join-Path $componentRepo 'docs/verification/entity-conversion-v002'
$componentLogs = Join-Path $componentRepo '.local/art-pipeline/logs-v002'
New-Item -ItemType Directory -Force -Path $componentEvidence,$componentLogs | Out-Null
foreach ($componentMode in @('entity','player-components')) {
    $componentLog = Join-Path $componentLogs "$componentMode-verification.log"
    $componentArgs = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',"--$componentMode-verify",('"'+$componentEvidence+'"'),'-logFile',('"'+$componentLog+'"'))
    $componentProcess = Start-Process -FilePath $componentPlayer -ArgumentList $componentArgs -WindowStyle Hidden -PassThru
    $componentDeadline = [DateTime]::UtcNow.AddMinutes(10)
    while (!$componentProcess.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $componentDeadline) { Stop-Process -Id $componentProcess.Id; throw "Player timed out: $componentLog" }
    }
    $componentMarker = if ($componentMode -eq 'entity') { 'CATAN_ENTITY_PLAYER_OK' } else { 'CATAN_PLAYER_COMPONENTS_OK' }
    if ($componentProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $componentLog -Pattern $componentMarker -Quiet)) { throw "Player failed: $componentLog" }
    if (Select-String -LiteralPath $componentLog -Pattern 'Exception:|NullReferenceException|MissingReferenceException|CATAN_ENTITY_PLAYER_FAILED' -Quiet) { throw "Player runtime errors: $componentLog" }
}
$componentGame = Join-Path $componentRepo '.local/m6/Builds/Windows/CatanM6.exe'
# Authoritative smoke saves stay in ignored local storage. Only public success metadata is published.
$componentSmoke = Join-Path $componentRepo '.local/art-pipeline/m6-smoke-v002'
New-Item -ItemType Directory -Force -Path $componentSmoke | Out-Null
foreach ($componentStage in @('save','load')) {
    $componentLog = Join-Path $componentLogs "m6-$componentStage.log"
    $componentArgs = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',"--m6-verify-$componentStage",('"'+$componentSmoke+'"'),'-logFile',('"'+$componentLog+'"'))
    $componentProcess = Start-Process -FilePath $componentGame -ArgumentList $componentArgs -WindowStyle Hidden -PassThru
    $componentDeadline = [DateTime]::UtcNow.AddMinutes(5)
    while (!$componentProcess.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $componentDeadline) { Stop-Process -Id $componentProcess.Id; throw "Game smoke timed out: $componentLog" }
    }
    $componentMarker = "CATAN_M6_PLAYER_$($componentStage.ToUpperInvariant())_OK"
    if ($componentProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $componentLog -Pattern $componentMarker -Quiet)) { throw "Game smoke failed: $componentLog" }
    if (Select-String -LiteralPath $componentLog -Pattern 'Exception:|CATAN_M6_PLAYER_VERIFY_FAILED' -Quiet) { throw "Game smoke runtime error: $componentLog" }
}
[ordered]@{status='passed'; rules=@('base','seafarers','cities-knights','combined'); independentProcesses=2; saveRestore=$true; continuedAi=$true; hiddenSaveLocation='.local/art-pipeline/m6-smoke-v002'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $componentEvidence 'm6-smoke.json') -Encoding utf8
Write-Host 'CATAN_PLAYER_COMPONENT_WINDOWS_VERIFIED'
