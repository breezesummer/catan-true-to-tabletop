param(
    [string[]]$LogDirectory,
    [string]$OutputRoot = '.local/network-diagnostics',
    [int]$RecentHours = 24
)
$ErrorActionPreference = 'Stop'
if ($RecentHours -lt 1 -or $RecentHours -gt 720) { throw 'RecentHours 须为 1–720 小时。' }
$collectRoot = [IO.Path]::GetFullPath($OutputRoot)
$collectStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$collectDestination = Join-Path $collectRoot $collectStamp
if (!$LogDirectory) {
    $collectPlayerRoot = Join-Path $env:USERPROFILE 'AppData/LocalLow/CatanDevelopment'
    $LogDirectory = @(
        if (Test-Path -LiteralPath $collectPlayerRoot) {
            Get-ChildItem -LiteralPath $collectPlayerRoot -Directory -Filter 'Catan*' |
                ForEach-Object { Join-Path $_.FullName 'm7-logs' } |
                Where-Object { Test-Path -LiteralPath $_ -PathType Container }
        }
    )
}
$collectFiles = @()
$collectCutoff = [DateTime]::UtcNow.AddHours(-$RecentHours)
$collectSourceNumber = 0
foreach ($collectDirectory in $LogDirectory) {
    if (!(Test-Path -LiteralPath $collectDirectory -PathType Container)) { continue }
    $collectSourceNumber++
    # 只复制诊断 JSONL；凭据、存档、Player.log 和其他文件均不在收集范围。
    foreach ($collectFile in Get-ChildItem -LiteralPath $collectDirectory -File -Filter '*.jsonl') {
        if ($collectFile.LastWriteTimeUtc -lt $collectCutoff -or ($collectFile.Attributes -band [IO.FileAttributes]::ReparsePoint)) { continue }
        $collectSourceFolder = Join-Path $collectDestination ('source-' + $collectSourceNumber)
        New-Item -ItemType Directory -Force -Path $collectSourceFolder | Out-Null
        $collectTarget = Join-Path $collectSourceFolder $collectFile.Name
        # 允许游戏继续写日志；最后一行可能未写完，分析工具会统计并跳过截断行。
        $collectInput = [IO.FileStream]::new($collectFile.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        try {
            $collectOutput = [IO.FileStream]::new($collectTarget, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
            try { $collectInput.CopyTo($collectOutput) } finally { $collectOutput.Dispose() }
        } finally { $collectInput.Dispose() }
        $collectFiles += [ordered]@{ file = ('source-' + $collectSourceNumber + '/' + $collectFile.Name); bytes = (Get-Item -LiteralPath $collectTarget).Length; modifiedUtc = $collectFile.LastWriteTimeUtc.ToString('O') }
    }
}
if ($collectFiles.Count -eq 0) { throw '没有找到指定时段的通信诊断日志。请先使用包含诊断功能的新构建进行联机。' }
[ordered]@{ format = 1; collectedUtc = [DateTime]::UtcNow.ToString('O'); recentHours = $RecentHours; note = '仅包含通信诊断 JSONL；未收集凭据、存档或原始网络正文。'; files = $collectFiles } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $collectDestination 'manifest.json') -Encoding utf8
$collectArchive = $collectDestination + '.zip'
Compress-Archive -LiteralPath $collectDestination -DestinationPath $collectArchive
Write-Host "已收集 $($collectFiles.Count) 个诊断日志：$collectArchive"
