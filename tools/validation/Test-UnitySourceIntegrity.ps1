param([string]$ProjectPath = (Join-Path $PSScriptRoot '../../Unity/Catan'))
$ErrorActionPreference = 'Stop'
$integrityProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$integrityExtensions = @('.meta', '.unity', '.prefab', '.mat', '.asset', '.cs', '.json')
$integrityCount = 0
$integrityDamaged = @(
    foreach ($integrityFolder in @('Assets', 'ProjectSettings', 'Packages')) {
        foreach ($integrityFile in Get-ChildItem -LiteralPath (Join-Path $integrityProject $integrityFolder) -Recurse -File) {
            if ($integrityFile.Extension -notin $integrityExtensions) { continue }
            $integrityBytes = [IO.File]::ReadAllBytes($integrityFile.FullName)
            $integrityCount++
            if ($integrityBytes.Length -eq 0 -or [Array]::IndexOf($integrityBytes, [byte]0) -ge 0) {
                $integrityFile.FullName
            }
        }
    }
)
if ($integrityDamaged.Count -gt 0) {
    throw ("Unity 源文件为空或含 NUL 字节，停止构建以免丢失贴图／材质引用：`n" + ($integrityDamaged -join "`n"))
}
Write-Host "Unity 源文件完整性检查通过：$integrityCount 个文本配置／脚本。"
