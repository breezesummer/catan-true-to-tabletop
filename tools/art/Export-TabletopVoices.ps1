param([string]$SourceDirectory='art/source/audio-v002/voices')
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$voiceRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot $SourceDirectory))
Add-Type -AssemblyName System.Speech
$speech=[System.Speech.Synthesis.SpeechSynthesizer]::new()
$format=[System.Speech.AudioFormat.SpeechAudioFormatInfo]::new(24000,[System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,[System.Speech.AudioFormat.AudioChannel]::Mono)
$lines=@(
    @{language='zh';voice='Microsoft Kangkang';file='KnightRecruit01';text='听候您的差遣。'},
    @{language='zh';voice='Microsoft Kangkang';file='KnightRecruit02';text='悉听遵命！'},
    @{language='zh';voice='Microsoft Kangkang';file='KnightActivate01';text='发饷啦！'},
    @{language='zh';voice='Microsoft Kangkang';file='KnightActivate02';text='我办事，您放心。'},
    @{language='en';voice='Microsoft Zira Desktop';file='KnightRecruit01';text='As you wish, my lord.'},
    @{language='en';voice='Microsoft Zira Desktop';file='KnightRecruit02';text='At your command, my lord.'},
    @{language='en';voice='Microsoft Zira Desktop';file='KnightActivate01';text='Our wages have arrived!'},
    @{language='en';voice='Microsoft Zira Desktop';file='KnightActivate02';text='Leave it to me, my lord.'}
)
try {
    foreach($line in $lines){
        $directory=Join-Path $voiceRoot $line.language
        New-Item -ItemType Directory -Path $directory -Force|Out-Null
        $file=Join-Path $directory ($line.file+'.wav')
        $speech.SelectVoice($line.voice);$speech.Rate=-1;$speech.Volume=100
        $speech.SetOutputToWaveFile($file,$format);$speech.Speak($line.text);$speech.SetOutputToNull()
        $line.sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}finally{$speech.Dispose()}
@{version='audio-v002';origin='本机 Windows System.Speech 离线文字转语音；原创台词；无商业游戏采样';format='PCM16 mono 24000 Hz';rebuild='相同语音引擎版本可重新合成；原始合成WAV随配方保留，发行导出以这些原始文件为准';lines=$lines}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $voiceRoot 'manifest.json') -Encoding UTF8
Write-Output ('已生成8句中英语音原件：'+$voiceRoot)
