$sampleRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$samplePlayer = Join-Path $sampleRepo '.local/art-photo-samples/Builds/Windows/CatanArtSamples-v001.exe'
if (!(Test-Path -LiteralPath $samplePlayer)) { & (Join-Path $PSScriptRoot 'Build-PhotoSamples.ps1') }
Start-Process -FilePath $samplePlayer -WorkingDirectory (Split-Path $samplePlayer)
