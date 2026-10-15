param(
    [string]$OutputDirectory='.local/audio-v001',
    [string]$RebuildDirectory='.local/audio-v001-rebuild',
    [string]$ReportPath='docs/verification/menu-audio-v001/audio-assets.json'
)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$audioRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
$rebuildRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot $RebuildDirectory))
$inspectionSource=@'
using System;
using System.IO;
using System.Text;
public sealed class TabletopAudioPcmInspection {
    public int Samples, SampleRate, Channels, Bits;
    public double Peak, Rms;
    public bool EndpointsSilent;
    public static TabletopAudioPcmInspection Read(string file) {
        byte[] data=File.ReadAllBytes(file);
        if(data.Length<44 || Encoding.ASCII.GetString(data,0,4)!="RIFF" || Encoding.ASCII.GetString(data,8,4)!="WAVE" ||
            Encoding.ASCII.GetString(data,36,4)!="data") throw new InvalidDataException("Unsupported PCM WAV header");
        var r=new TabletopAudioPcmInspection {Channels=BitConverter.ToInt16(data,22),SampleRate=BitConverter.ToInt32(data,24),Bits=BitConverter.ToInt16(data,34)};
        if(r.Channels!=1||r.Bits!=16||r.SampleRate!=24000||BitConverter.ToInt32(data,40)!=data.Length-44) throw new InvalidDataException("Invalid format or length");
        r.Samples=(data.Length-44)/2;double energy=0;
        for(int i=0;i<r.Samples;i++) {double v=BitConverter.ToInt16(data,44+i*2)/32767.0;r.Peak=Math.Max(r.Peak,Math.Abs(v));energy+=v*v;}
        r.Rms=Math.Sqrt(energy/r.Samples);
        r.EndpointsSilent=BitConverter.ToInt16(data,44)==0&&BitConverter.ToInt16(data,data.Length-2)==0;
        return r;
    }
}
'@
if(-not ('TabletopAudioPcmInspection' -as [type])){Add-Type -TypeDefinition $inspectionSource}
$manifest=Get-Content -LiteralPath (Join-Path $audioRoot 'manifest.json') -Raw|ConvertFrom-Json
$entries=@($manifest.effects)+@($manifest.ambience)
$checks=@()
foreach($entry in $entries){
    $file=Join-Path $audioRoot $entry.file
    $wave=[TabletopAudioPcmInspection]::Read($file)
    $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    $rebuildHash=(Get-FileHash -LiteralPath (Join-Path $rebuildRoot $entry.file) -Algorithm SHA256).Hash.ToLowerInvariant()
    $passed=$wave.Samples -eq $entry.samples -and $wave.Rms -gt .0005 -and $wave.Peak -le .701 -and $wave.EndpointsSilent -and $hash -eq $entry.sha256 -and $hash -eq $rebuildHash
    if(-not $passed){throw ('PCM解码、清单或干净重建验证失败：'+$entry.file)}
    $checks+=@{file=$entry.file;samples=$wave.Samples;sampleRate=$wave.SampleRate;channels=$wave.Channels;bits=$wave.Bits;decodedPeak=$wave.Peak;decodedRms=$wave.Rms;endpointsSilent=$wave.EndpointsSilent;sha256=$hash;cleanRebuildIdentical=$true;passed=$true}
}
if(($checks.sha256|Select-Object -Unique).Count -ne 36){throw '36个音频文件应各自具有唯一声纹'}
$result=@{recipeVersion=$manifest.recipeVersion;clientDate='2026-10-16';passed=$true;effectCount=35;ambienceCount=1;decodedFiles=$checks.Count;cleanRebuildIdentical=$true;checks=$checks;limitations=@('未录制操作系统实际输出；此处不是人工听感验收','Unity运行时使用同源浮点片段，Windows实际导入/运行由主任务验证','安卓真机音量、耳机/扬声器与长局听感待验')}
$report=[IO.Path]::GetFullPath((Join-Path $projectRoot $ReportPath));New-Item -ItemType Directory -Path (Split-Path $report) -Force|Out-Null
$result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $report -Encoding UTF8
Write-Output ('音频验证通过：35个音效+1个环境WAV，均非静音、格式/峰值正确、哈希唯一、干净重建一致。'+$report)
