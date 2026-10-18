param([ValidateRange(1,20)][int]$Keep=5)
$ErrorActionPreference='Stop'
$retentionRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$retentionRoot=[IO.Path]::GetFullPath((Join-Path $retentionRepo 'game version'))
if(!(Test-Path -LiteralPath $retentionRoot)){return}
# 仅管理按现行约定命名的游戏版本；开发工具、资料缓存和历史验收目录不属于此集合。
$retentionBuilds=@(Get-ChildItem -LiteralPath $retentionRoot -Directory | Where-Object {$_.Name -match '^Catan\d+\.\d+\.\d+-.+'} | Sort-Object @{Expression={[version]([regex]::Match($_.Name,'^Catan(\d+\.\d+\.\d+)-').Groups[1].Value)};Descending=$true},@{Expression={$_.LastWriteTimeUtc};Descending=$true})
$retentionRemoved=@()
foreach($retentionDirectory in @($retentionBuilds | Select-Object -Skip $Keep))
{
    $retentionTarget=[IO.Path]::GetFullPath($retentionDirectory.FullName)
    if([IO.Path]::GetDirectoryName($retentionTarget) -ne $retentionRoot -or ($retentionDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw '版本清理目标必须是项目 game version 下的实际版本目录。'}
    if((Test-Path -LiteralPath (Join-Path $retentionTarget 'release')) -or (Test-Path -LiteralPath (Join-Path $retentionTarget '正式发布.txt'))){continue}
    Remove-Item -LiteralPath $retentionTarget -Recurse -Force
    $retentionRemoved+=$retentionDirectory.Name
}
[pscustomobject]@{保留上限=$Keep;版本数=$retentionBuilds.Count;已清理=$retentionRemoved}
