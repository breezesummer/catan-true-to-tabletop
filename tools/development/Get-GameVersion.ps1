$ErrorActionPreference='Stop'
$versionRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$versionText=Get-Content -LiteralPath (Join-Path $versionRepo 'AGENTS.md') -Raw
$versionMatch=[regex]::Match($versionText,'版本号：内测版(\d+\.\d+(?:\.\d+)?)')
if(!$versionMatch.Success){throw 'AGENTS.md 缺少有效游戏版本号。'}
$versionValue=$versionMatch.Groups[1].Value
$versionSource=Get-Content -LiteralPath (Join-Path $versionRepo 'Unity/Catan/Assets/Scripts/M6App.cs') -Raw
if($versionSource -notmatch ('BuildVersion\s*=\s*"'+[regex]::Escape($versionValue)+'"')){throw 'AGENTS.md 与客户端版本号不一致。'}
$versionValue
