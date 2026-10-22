param([string]$OutputDirectory='art/exports/audio-v003',[switch]$InstallUnity)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$audioRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
New-Item -ItemType Directory -Path $audioRoot -Force | Out-Null
$modelText=Get-Content -LiteralPath (Join-Path $projectRoot 'Unity/Catan/Assets/Scripts/TabletopAudioModel.cs') -Raw
$enumText=[regex]::Match($modelText,'public enum TabletopAudioCue\s*\{[^}]+\}').Value
$synthPath=Join-Path $projectRoot 'Unity/Catan/Assets/Scripts/TabletopAudioSynth.cs'
$synthText=(Get-Content -LiteralPath $synthPath -Raw) -replace '^using System;',''
if(-not ('TabletopAudioSynth' -as [type])){Add-Type -TypeDefinition ('using System;'+[Environment]::NewLine+$enumText+[Environment]::NewLine+$synthText)}
$waveSource=@"
using System;
using System.IO;
using System.Text;
public static class TabletopAudioWave {
    public static void Write(string file,float[] samples){
        using(var writer=new BinaryWriter(File.Create(file))){
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(24000);writer.Write(48000);writer.Write((short)2);writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
            foreach(float sample in samples)writer.Write((short)Math.Round(Math.Max(-1.0,Math.Min(1.0,sample))*32767.0));
        }
    }
    public static float[] PrepareVoice(string file){
        byte[] raw=File.ReadAllBytes(file);int offset=12,start=0,length=0;
        while(offset+8<=raw.Length){int count=BitConverter.ToInt32(raw,offset+4);if(Encoding.ASCII.GetString(raw,offset,4)=="data"){start=offset+8;length=count/2;break;}offset+=8+count+(count%2);}
        if(start==0||length<2400)throw new InvalidDataException("未找到有效语音PCM数据");
        var data=new float[length];float peak=0;int first=length,last=0;
        for(int i=0;i<length;i++){data[i]=BitConverter.ToInt16(raw,start+i*2)/32767f;peak=Math.Max(peak,Math.Abs(data[i]));if(Math.Abs(data[i])>.006){first=Math.Min(first,i);last=i;}}
        if(peak<.01)throw new InvalidDataException("语音完全静音");
        first=Math.Max(0,first-1920);last=Math.Min(length-1,last+2880);var result=new float[last-first+1];
        for(int i=0;i<result.Length;i++)result[i]=data[first+i]*(.65f/peak)*(float)Math.Min(1,Math.Min(i,result.Length-1-i)/120.0);
        return result;
    }
    public static double[] Statistics(float[] data){double peak=0,energy=0;foreach(float s in data){peak=Math.Max(peak,Math.Abs(s));energy+=s*s;}return new[]{peak,Math.Sqrt(energy/data.Length)};}
}
"@
if(-not ('TabletopAudioWave' -as [type])){Add-Type -TypeDefinition $waveSource}
$entries=[Collections.Generic.List[object]]::new()
function Export-Audio([string]$Relative,[single[]]$Samples,[string]$Kind,[string]$Caption){
    $file=Join-Path $audioRoot $Relative
    New-Item -ItemType Directory -Path (Split-Path $file) -Force|Out-Null
    [TabletopAudioWave]::Write($file,$Samples);$stats=[TabletopAudioWave]::Statistics($Samples)
    $entries.Add(@{kind=$Kind;caption=$Caption;file=$Relative;samples=$Samples.Length;seconds=$Samples.Length/24000.0;peak=$stats[0];rms=$stats[1];sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()})
}
foreach($cue in [Enum]::GetValues([TabletopAudioCue])){Export-Audio ('Effects/'+$cue+'.wav') ([TabletopAudioSynth]::Render($cue)) 'effect' $cue.ToString()}
$themes=@('晨光港湾：海浪与海鸟','金色麦田：微风与田鸟','林间溪谷：溪流与林鸟','雪山城堡：山间风声','月夜灯港：夜风与虫鸣')
for($i=0;$i -lt 5;$i++){Export-Audio ('Ambience/Theme'+$i+'.wav') ([TabletopAudioSynth]::RenderAmbience($i)) 'ambience' $themes[$i]}
Export-Audio 'Music/SeaBreezeHome.wav' ([TabletopAudioSynth]::RenderMusic()) 'music' '原创循环配乐：海风归途'
$sourceRoot=Join-Path $projectRoot 'art/source/audio-v003/voices'
$voiceManifest=Get-Content -LiteralPath (Join-Path $sourceRoot 'manifest.json') -Raw|ConvertFrom-Json
foreach($voice in $voiceManifest.lines){
    $relative=$voice.language+'/'+$voice.file+'.wav';$source=Join-Path $sourceRoot $relative
    if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $voice.sha256){throw ('语音原件校验失败：'+$relative)}
    Export-Audio ('Voices/'+$relative) ([TabletopAudioWave]::PrepareVoice($source)) 'voice' $voice.text
}
$manifest=@{recipeVersion=[TabletopAudioSynth]::RecipeVersion;format='PCM16 mono 24000 Hz';origin='原创柔和拟音与配乐；Brandon Song 的 CC0 真人短应答与战呼，授权见 assets/references/audio/adventurer-v001/README.md';source='Unity/Catan/Assets/Scripts/TabletopAudioSynth.cs';sourceSha256=(Get-FileHash -LiteralPath $synthPath -Algorithm SHA256).Hash.ToLowerInvariant();files=$entries.ToArray()}
$manifest|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $audioRoot 'manifest.json') -Encoding UTF8
if($InstallUnity){
    $unityRoot=Join-Path $projectRoot 'Unity/Catan/Assets/Resources/TabletopAudio/v003'
    foreach($entry in $entries){$destination=Join-Path $unityRoot $entry.file;New-Item -ItemType Directory -Path (Split-Path $destination) -Force|Out-Null;Copy-Item -LiteralPath (Join-Path $audioRoot $entry.file) -Destination $destination -Force}
}
Write-Output ('已导出 '+$entries.Count+' 个可用WAV与来源清单：'+$audioRoot)