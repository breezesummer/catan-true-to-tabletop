param(
    [Parameter(Mandatory=$true)][string]$BuildRoot,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [string]$Endpoint='https://127.0.0.1:47831',
    [string]$FingerprintFile,
    [string]$RuleSet='combined',
    [int]$MinimumTurn=10
)
$ErrorActionPreference='Stop'
$profileRoot=[IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $profileRoot | Out-Null
$profilePlayer=& (Join-Path $PSScriptRoot '../development/Find-GamePlayer.ps1') -BuildRoot $BuildRoot
$profileConfig=Join-Path $profileRoot 'configuration.json'
$profileFingerprint=if($FingerprintFile){(Get-Content -LiteralPath $FingerprintFile -Raw).Trim()}else{''}
@{Endpoint=$Endpoint;CertificateFingerprint=$profileFingerprint;RuleSet=$RuleSet;ScenarioId=$(if($RuleSet -eq 'combined'){'through-the-desert'}else{'base'});OutputDirectory=$profileRoot;PlayerCount=3;MinimumTurn=$MinimumTurn;TimeoutSeconds=420;SkipLanDiscovery=$true} |
    ConvertTo-Json | Set-Content -LiteralPath $profileConfig -Encoding UTF8
$profileLog=Join-Path $profileRoot 'player.log'
$profileProcess=Start-Process -FilePath $profilePlayer -WindowStyle Hidden -PassThru -ArgumentList @(
    '-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',
    '--m7-verify',('"'+$profileConfig+'"'),'--frame-profile',('"'+$profileRoot+'"'),'-logFile',('"'+$profileLog+'"'))
try {
    $profileDeadline=[DateTime]::UtcNow.AddSeconds(450)
    while(!$profileProcess.WaitForExit(1000)){
        if([DateTime]::UtcNow -gt $profileDeadline){Stop-Process -Id $profileProcess.Id;throw '联机帧采样超时。'}
    }
    if($profileProcess.ExitCode -ne 0 -or !(Test-Path (Join-Path $profileRoot 'frames.csv'))){throw "联机帧采样失败，请检查 $profileLog"}
    Write-Host "联机帧采样完成：$profileRoot"
} finally {$profileProcess.Dispose()}
