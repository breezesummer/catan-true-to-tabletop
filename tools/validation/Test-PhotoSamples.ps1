$ErrorActionPreference = 'Stop'
$sampleRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$samplePlayer = Join-Path $sampleRepo '.local/art-photo-samples/Builds/Windows/CatanArtSamples-v001.exe'
$sampleEvidence = Join-Path $sampleRepo '.local/art-photo-samples/verification'
$sampleLogs = Join-Path $sampleRepo '.local/art-photo-samples/logs'
New-Item -ItemType Directory -Force -Path $sampleEvidence,$sampleLogs | Out-Null
foreach ($sampleMode in @('art','save','load')) {
    $sampleFlag = switch ($sampleMode) { 'art' {'--art-verify'} 'save' {'--m6-verify-save'} 'load' {'--m6-verify-load'} }
    $sampleLog = Join-Path $sampleLogs "player-$sampleMode.log"
    $sampleArgs = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',$sampleFlag,('"'+$sampleEvidence+'"'),'-logFile',('"'+$sampleLog+'"'))
    $sampleProcess = Start-Process -FilePath $samplePlayer -ArgumentList $sampleArgs -WindowStyle Hidden -PassThru
    $sampleDeadline = [DateTime]::UtcNow.AddMinutes(3)
    while (!$sampleProcess.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $sampleDeadline) { Stop-Process -Id $sampleProcess.Id; throw "Player $sampleMode timed out: $sampleLog" }
    }
    $sampleMarker = switch ($sampleMode) { 'art' {'CATAN_PHOTO_PLAYER_OK'} 'save' {'CATAN_M6_PLAYER_SAVE_OK'} 'load' {'CATAN_M6_PLAYER_LOAD_OK'} }
    if ($sampleProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $sampleLog -Pattern $sampleMarker -Quiet)) { throw "Player $sampleMode failed: $sampleLog" }
    if (Select-String -LiteralPath $sampleLog -Pattern 'Exception:|NullReferenceException|MissingReferenceException|CATAN_PHOTO_PLAYER_FAILED|Failed to capture screen shot' -Quiet) { throw "Runtime errors: $sampleLog" }
    Write-Host "CATAN_PHOTO_RUNTIME_$($sampleMode.ToUpper())_OK"
}
