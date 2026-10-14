param([switch]$VerifyPlayer,[string]$OutputRoot='.local/m6-entity-v005')
$ErrorActionPreference='Stop'
$markerRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$markerProject=Join-Path $markerRepo 'Unity/Catan'
$markerPreserved=@{}
$markerBackup=Join-Path $markerRepo ($OutputRoot+'/workspace-preserved/'+[Guid]::NewGuid().ToString('N'))
# 构建场景与渲染配置只用于导出；保留开工时工作区中的已有设置和场景字节。
foreach($markerRelative in @('Assets/M6.unity','Assets/M1Pipeline.asset','Assets/UniversalRenderPipelineGlobalSettings.asset','ProjectSettings/GraphicsSettings.asset','ProjectSettings/ProjectSettings.asset')) {
    $markerPath=Join-Path $markerProject $markerRelative
    if(Test-Path -LiteralPath $markerPath){
        $markerPreserved[$markerPath]=[IO.File]::ReadAllBytes($markerPath)
        $markerBackupPath=Join-Path $markerBackup $markerRelative
        New-Item -ItemType Directory -Force -Path (Split-Path $markerBackupPath) | Out-Null
        [IO.File]::WriteAllBytes($markerBackupPath,$markerPreserved[$markerPath])
    }
}
try {
    & (Join-Path $PSScriptRoot 'Build-M6.ps1') -OutputRoot $OutputRoot -VerifyPlayer:$VerifyPlayer -ImportTabletopMarkers
}
finally {
    foreach($markerEntry in $markerPreserved.GetEnumerator()) {
        # 内容相同不重写：Unity 可能仍映射未改变的设置文件。
        if(Test-Path -LiteralPath $markerEntry.Key){
            if([Convert]::ToBase64String([IO.File]::ReadAllBytes($markerEntry.Key)) -ceq [Convert]::ToBase64String($markerEntry.Value)){continue}
        }
        [IO.File]::WriteAllBytes($markerEntry.Key,$markerEntry.Value)
    }
}
