param([Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$assemblySource=(Resolve-Path -LiteralPath $Source).Path
$assemblyTarget=[IO.Path]::GetFullPath($Destination)
$assemblyHash=(Get-FileHash -LiteralPath $assemblySource).Hash
if((Test-Path -LiteralPath $assemblyTarget) -and $assemblyHash -eq (Get-FileHash -LiteralPath $assemblyTarget).Hash){return}
# 原子替换允许尚未释放的程序集映射继续读取旧文件，不截断正在使用的 DLL。
$assemblyTemporary=$assemblyTarget+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
$assemblyBackup=$assemblyTarget+'.'+[Guid]::NewGuid().ToString('N')+'.bak'
try {
    Copy-Item -LiteralPath $assemblySource -Destination $assemblyTemporary
    if(Test-Path -LiteralPath $assemblyTarget){
        try{[IO.File]::Replace($assemblyTemporary,$assemblyTarget,$assemblyBackup)}
        catch{
            # Windows 可能已替换目标，仅旧映射的清理失败；以目标完整哈希确认结果。
            if(!(Test-Path -LiteralPath $assemblyTarget) -or (Get-FileHash -LiteralPath $assemblyTarget).Hash -ne $assemblyHash){throw}
        }
    }
    else{Move-Item -LiteralPath $assemblyTemporary -Destination $assemblyTarget}
    if((Get-FileHash -LiteralPath $assemblyTarget).Hash -ne $assemblyHash){throw '复制后的程序集哈希不一致。'}
} finally {
    if(Test-Path -LiteralPath $assemblyTemporary){Remove-Item -LiteralPath $assemblyTemporary}
    if(Test-Path -LiteralPath $assemblyBackup){Remove-Item -LiteralPath $assemblyBackup -ErrorAction SilentlyContinue}
}
