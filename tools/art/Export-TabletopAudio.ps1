param([string]$OutputDirectory='.local/audio-v001')
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$audioRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
New-Item -ItemType Directory -Path $audioRoot -Force | Out-Null
$modelText=Get-Content -LiteralPath (Join-Path $projectRoot 'Unity/Catan/Assets/Scripts/TabletopAudioModel.cs') -Raw
$enumText=[regex]::Match($modelText,'public enum TabletopAudioCue\s*\{[^}]+\}').Value
$synthPath=Join-Path $projectRoot 'Unity/Catan/Assets/Scripts/TabletopAudioSynth.cs'
$synthText=(Get-Content -LiteralPath $synthPath -Raw) -replace '^using System;',''
if(-not ('TabletopAudioSynth' -as [type])){Add-Type -TypeDefinition ('using System;'+[Environment]::NewLine+$enumText+[Environment]::NewLine+$synthText)}
function Write-PcmWave([string]$File,[single[]]$Samples){
    $stream=[IO.File]::Create($File)
    $writer=[IO.BinaryWriter]::new($stream)
    try{
        $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'));$writer.Write([int](36+$Samples.Length*2));$writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
        $writer.Write([int]16);$writer.Write([int16]1);$writer.Write([int16]1);$writer.Write([int]24000);$writer.Write([int]48000);$writer.Write([int16]2);$writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes('data'));$writer.Write([int]($Samples.Length*2))
        # 显式 double 避免 PowerShell 把 Math.Min(1, float) 绑定至整数重载而静音。
        foreach($sample in $Samples){$writer.Write([int16]([Math]::Round([Math]::Max([double]-1,[Math]::Min([double]1,[double]$sample))*32767.0)))}
    }finally{$writer.Dispose();$stream.Dispose()}
}
$entries=@()
foreach($cue in [Enum]::GetValues([TabletopAudioCue])){
    $samples=[TabletopAudioSynth]::Render($cue)
    $file=Join-Path $audioRoot ($cue.ToString()+'.wav');Write-PcmWave $file $samples
    $peak=0.0;$energy=0.0
    foreach($sample in $samples){$peak=[Math]::Max($peak,[Math]::Abs($sample));$energy+=$sample*$sample}
    $entries+=@{cue=$cue.ToString();file=[IO.Path]::GetFileName($file);samples=$samples.Length;seconds=$samples.Length/24000.0;peak=$peak;rms=[Math]::Sqrt($energy/$samples.Length);sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$environment=[TabletopAudioSynth]::RenderAmbience();$ambiencePath=Join-Path $audioRoot 'PastoralAmbience.wav';Write-PcmWave $ambiencePath $environment
if(($entries.sha256|Select-Object -Unique).Count -ne $entries.Count){throw '音效 WAV 校验值重复，导出失败。'}
$manifest=@{recipeVersion=[TabletopAudioSynth]::RecipeVersion;format='PCM16 mono 24000 Hz';origin='原创确定性程序合成；不使用外部录音或游戏规则随机状态';source='Unity/Catan/Assets/Scripts/TabletopAudioSynth.cs';sourceSha256=(Get-FileHash -LiteralPath $synthPath -Algorithm SHA256).Hash.ToLowerInvariant();effects=$entries;ambience=@{file='PastoralAmbience.wav';samples=$environment.Length;seconds=$environment.Length/24000.0;sha256=(Get-FileHash -LiteralPath $ambiencePath -Algorithm SHA256).Hash.ToLowerInvariant()}}
$manifest|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $audioRoot 'manifest.json') -Encoding UTF8
Write-Output ('已导出 '+$entries.Length+' 种音效与田园环境循环：'+$audioRoot)
