param(
    [string]$OutputDirectory='art/exports/audio-v003',
    [string]$RebuildDirectory='.local/audio-v003-rebuild',
    [string]$ReportPath='docs/verification/feedback-20261018/audio-assets.json'
)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$audioRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
$rebuildRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot $RebuildDirectory))
$inspectionSource=@"
using System;
using System.IO;
using System.Text;
public sealed class TabletopAudioPcmInspection {
    public int Samples, SampleRate, Channels, Bits;
    public double Peak, Rms, Seam;
    public bool EndpointsSilent;
    public static TabletopAudioPcmInspection Read(string file) {
        byte[] data=File.ReadAllBytes(file);
        if(data.Length<44 || Encoding.ASCII.GetString(data,0,4)!="RIFF" || Encoding.ASCII.GetString(data,8,4)!="WAVE" ||
            Encoding.ASCII.GetString(data,36,4)!="data") throw new InvalidDataException("不支持的PCM文件头");
        var r=new TabletopAudioPcmInspection {Channels=BitConverter.ToInt16(data,22),SampleRate=BitConverter.ToInt32(data,24),Bits=BitConverter.ToInt16(data,34)};
        if(r.Channels!=1||r.Bits!=16||r.SampleRate!=24000||BitConverter.ToInt32(data,40)!=data.Length-44) throw new InvalidDataException("音频格式或长度错误");
        r.Samples=(data.Length-44)/2;double energy=0;
        for(int i=0;i<r.Samples;i++) {double v=BitConverter.ToInt16(data,44+i*2)/32767.0;r.Peak=Math.Max(r.Peak,Math.Abs(v));energy+=v*v;}
        r.Rms=Math.Sqrt(energy/r.Samples);
        r.EndpointsSilent=BitConverter.ToInt16(data,44)==0&&BitConverter.ToInt16(data,data.Length-2)==0;
        r.Seam=Math.Abs(BitConverter.ToInt16(data,44)-BitConverter.ToInt16(data,data.Length-2))/32767.0;
        return r;
    }
}
"@
if(-not ('TabletopAudioPcmInspection' -as [type])){Add-Type -TypeDefinition $inspectionSource}
$manifest=Get-Content -LiteralPath (Join-Path $audioRoot 'manifest.json') -Raw|ConvertFrom-Json
$checks=@()
foreach($entry in $manifest.files){
    $file=Join-Path $audioRoot $entry.file;$wave=[TabletopAudioPcmInspection]::Read($file)
    $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    $rebuildHash=(Get-FileHash -LiteralPath (Join-Path $rebuildRoot $entry.file) -Algorithm SHA256).Hash.ToLowerInvariant()
    $runtime=Join-Path $projectRoot ('Unity/Catan/Assets/Resources/TabletopAudio/v003/'+$entry.file)
    $runtimeHash=(Get-FileHash -LiteralPath $runtime -Algorithm SHA256).Hash.ToLowerInvariant()
    $importSettings=Get-Content -LiteralPath ($runtime+'.meta') -Raw
    $pcmImport=$importSettings -match 'compressionFormat: 0' -and $importSettings -notmatch 'compressionFormat: [1-9]' -and $importSettings -match 'loadType: 0' -and $importSettings -match 'sampleRateSetting: 0' -and $importSettings -match 'preloadAudioData: 1'
    $seamValid=if($entry.kind -eq 'music'){$wave.Seam -lt .01}else{$wave.EndpointsSilent}
    $passed=$wave.Samples -eq $entry.samples -and $wave.Rms -gt .0005 -and $wave.Peak -le .701 -and $seamValid -and $hash -eq $entry.sha256 -and $hash -eq $rebuildHash -and $hash -eq $runtimeHash -and $pcmImport
    if(-not $passed){throw ('PCM解码、清单、安装资源或干净重建验证失败：'+$entry.file)}
    $checks+=@{file=$entry.file;kind=$entry.kind;samples=$wave.Samples;sampleRate=$wave.SampleRate;channels=$wave.Channels;bits=$wave.Bits;decodedPeak=$wave.Peak;decodedRms=$wave.Rms;endpointsSilent=$wave.EndpointsSilent;loopSeam=$wave.Seam;sha256=$hash;cleanRebuildIdentical=$true;installedResourceIdentical=$true;losslessPcmImport=$pcmImport;passed=$true}
}
if(($checks.sha256|Select-Object -Unique).Count -ne $checks.Count){throw '音频文件存在重复声纹'}
$effects=@($checks|Where-Object kind -eq 'effect');$ambiences=@($checks|Where-Object kind -eq 'ambience');$voices=@($checks|Where-Object kind -eq 'voice');$music=@($checks|Where-Object kind -eq 'music')
if($effects.Count -ne 39 -or $ambiences.Count -ne 5 -or $voices.Count -ne 8 -or $music.Count -ne 1){throw '音频清单数量不符'}
$result=@{recipeVersion=$manifest.recipeVersion;clientDate='2026-10-18';passed=$true;effectCount=$effects.Count;ambienceCount=$ambiences.Count;voiceCount=$voices.Count;musicCount=$music.Count;decodedFiles=$checks.Count;cleanRebuildIdentical=$true;theoreticalMixPeakUpperBound=.98;checks=$checks;limitations=@('程序拟音与原创配乐，配合Brandon Song的CC0真人短语音；未使用商业游戏采样','未录制操作系统实际输出；此处不是人工听感验收','Windows实际导入/运行由主任务验证','安卓真机音量、耳机/扬声器与长局听感待验')}
$report=[IO.Path]::GetFullPath((Join-Path $projectRoot $ReportPath));New-Item -ItemType Directory -Path (Split-Path $report) -Force|Out-Null
$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $report -Encoding UTF8
Write-Output ('音频验证通过：39个音效+5个环境+8条真人短语音+1首配乐；PCM有效、峰值/循环接缝正确、干净重建与安装资源一致。'+$report)
