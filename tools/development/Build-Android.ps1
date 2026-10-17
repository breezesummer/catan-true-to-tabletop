param(
    [string]$OutputRoot = '.local/Catan1.6.3-AudioVisual',
    [switch]$Arm64Only
)
$androidBuildClock = [Diagnostics.Stopwatch]::StartNew()
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$androidRepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$androidProject = Join-Path $androidRepoRoot 'Unity\Catan'
& (Join-Path $PSScriptRoot '../validation/Test-UnitySourceIntegrity.ps1') -ProjectPath $androidProject
$androidOutputRoot = [IO.Path]::GetFullPath((Join-Path $androidRepoRoot $OutputRoot))
$androidLogs = Join-Path $androidOutputRoot 'logs'
$androidPlayerRoot = Join-Path (Split-Path $toolchain.unity) 'Data\PlaybackEngines\AndroidPlayer'
$androidKeytool = Join-Path $androidPlayerRoot 'OpenJDK\bin\keytool.exe'
if (!(Test-Path -LiteralPath $androidKeytool)) { throw '缺少固定 Unity Android 工具链，请先安装 Android Build Support、SDK、NDK 与 OpenJDK。' }
New-Item -ItemType Directory -Force -Path $androidLogs,(Join-Path $androidProject 'Assets\Plugins') | Out-Null

& $toolchain.dotnet build (Join-Path $androidRepoRoot 'src\Catan.AI\Catan.AI.csproj') -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw '共用规则与 AI 程序集构建失败。' }
foreach ($androidAssemblyName in @('Catan.Core','Catan.AI')) {
    $androidAssemblySource = Join-Path $androidRepoRoot "src\$androidAssemblyName\bin\Release\netstandard2.1\$androidAssemblyName.dll"
    $androidAssemblyTarget = Join-Path $androidProject "Assets\Plugins\$androidAssemblyName.dll"
    if (!(Test-Path -LiteralPath $androidAssemblyTarget) -or (Get-FileHash -LiteralPath $androidAssemblySource).Hash -ne (Get-FileHash -LiteralPath $androidAssemblyTarget).Hash) {
        Copy-Item -LiteralPath $androidAssemblySource -Destination $androidAssemblyTarget -Force
    }
}

# 稳定的内测签名支持覆盖安装。口令只用本机 Windows DPAPI 加密保存，不写入仓库或命令行。
$androidCredentialRoot = Join-Path $androidRepoRoot '.local\development\credentials\android'
$androidKeystore = Join-Path $androidCredentialRoot 'catan-internal.keystore'
$androidPasswordFile = Join-Path $androidCredentialRoot 'keystore-password.dpapi'
New-Item -ItemType Directory -Force $androidCredentialRoot | Out-Null
$androidHasKeystore = Test-Path -LiteralPath $androidKeystore
$androidHasPassword = Test-Path -LiteralPath $androidPasswordFile
if ($androidHasKeystore -ne $androidHasPassword) {
    throw 'Android 签名密钥与本机加密口令必须同时存在；检测到孤立文件，停止以免生成不同签名。'
}
if (!(Test-Path -LiteralPath $androidPasswordFile)) {
    $androidRandomBytes = New-Object byte[] 32
    $androidRandom = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $androidRandom.GetBytes($androidRandomBytes) } finally { $androidRandom.Dispose() }
    $androidNewPassword = [Convert]::ToBase64String($androidRandomBytes)
    ConvertTo-SecureString -String $androidNewPassword -AsPlainText -Force | ConvertFrom-SecureString |
        Set-Content -LiteralPath $androidPasswordFile -Encoding ASCII
    $androidNewPassword = $null
}
$androidSecurePassword = (Get-Content -LiteralPath $androidPasswordFile -Raw).Trim() | ConvertTo-SecureString
$androidPassword = [Net.NetworkCredential]::new('', $androidSecurePassword).Password
$androidPreviousKeystore = $env:CATAN_ANDROID_KEYSTORE
$androidPreviousPassword = $env:CATAN_ANDROID_KEYSTORE_PASS
$androidPreviousGradleHome = $env:GRADLE_USER_HOME
try {
    $env:CATAN_ANDROID_KEYSTORE = $androidKeystore
    $env:CATAN_ANDROID_KEYSTORE_PASS = $androidPassword
    $env:GRADLE_USER_HOME = Join-Path $androidRepoRoot '.local\development\caches\gradle'
    New-Item -ItemType Directory -Force $env:GRADLE_USER_HOME | Out-Null
    if (!(Test-Path -LiteralPath $androidKeystore)) {
        & $androidKeytool -genkeypair -keystore $androidKeystore -storetype JKS -alias catan-internal `
            -storepass:env CATAN_ANDROID_KEYSTORE_PASS -keypass:env CATAN_ANDROID_KEYSTORE_PASS `
            -keyalg RSA -keysize 2048 -validity 10000 -dname 'CN=Catan Internal Playtest, O=CatanDevelopment, C=CN' -noprompt
        if ($LASTEXITCODE -ne 0) { throw 'Android 内测签名创建失败。' }
    }
    $androidUnityArguments = @('-batchmode','-nographics','-quit','-projectPath',$androidProject,
        '-buildTarget','Android','-executeMethod','M6Build.BuildAndroid','--m6-output-root',$androidOutputRoot,
        '-logFile',(Join-Path $androidLogs 'unity-android-build.log'))
    if ($Arm64Only) { $androidUnityArguments += '--m6-android-arm64-only' }
    & $toolchain.unity @androidUnityArguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Unity Android 构建失败，请检查 $androidLogs/unity-android-build.log。" }
    if (!(Select-String -LiteralPath (Join-Path $androidLogs 'unity-android-build.log') -Pattern 'CATAN_M6_ANDROID_BUILD_OK' -Quiet)) {
        throw '未找到 Android 构建成功标记。'
    }
    $androidFileStem='Catan'+(& (Join-Path $PSScriptRoot 'Get-GameVersion.ps1'))
    $androidApk = Join-Path $androidOutputRoot "Builds/Android/$androidFileStem.apk"
    if (!(Test-Path -LiteralPath $androidApk)) { throw '未找到构建后的 APK。' }
    $androidApkSigner = Join-Path $androidPlayerRoot 'SDK\build-tools\36.0.0\apksigner.bat'
    $androidSavedJavaHome = $env:JAVA_HOME
    try {
        $env:JAVA_HOME = Join-Path $androidPlayerRoot 'OpenJDK'
        $androidSignatureReport = & $androidApkSigner verify --verbose --print-certs $androidApk 2>&1
        if ($LASTEXITCODE -ne 0) { throw 'Android APK 签名验证失败。' }
        $androidSignatureReport | Set-Content -LiteralPath (Join-Path $androidLogs 'android-apk-signature.txt') -Encoding UTF8
        $androidInfoFile = Join-Path $androidOutputRoot 'Builds\Android\build-info.json'
        $androidInfo = Get-Content -LiteralPath $androidInfoFile -Raw | ConvertFrom-Json
        $androidSignatureDigest = [regex]::Match(($androidSignatureReport -join "`n"),'Signer #1 certificate SHA-256 digest:\s*([a-fA-F0-9]+)')
        if (!$androidSignatureDigest.Success) { throw '未找到 Android APK 的签名摘要。' }
        $androidInfo | Add-Member -NotePropertyName signingCertificateSha256 -NotePropertyValue $androidSignatureDigest.Groups[1].Value.ToLowerInvariant() -Force
        $androidInfo | Add-Member -NotePropertyName apkSha256 -NotePropertyValue (Get-FileHash -LiteralPath $androidApk -Algorithm SHA256).Hash.ToLowerInvariant() -Force
        $androidInfo | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $androidInfoFile -Encoding UTF8
    } finally { $env:JAVA_HOME = $androidSavedJavaHome }
    Write-Host ('Android APK 已生成并验证签名，耗时 {0:N1} 秒：{1}' -f $androidBuildClock.Elapsed.TotalSeconds,$androidApk)
} finally {
    $env:CATAN_ANDROID_KEYSTORE = $androidPreviousKeystore
    $env:CATAN_ANDROID_KEYSTORE_PASS = $androidPreviousPassword
    $env:GRADLE_USER_HOME = $androidPreviousGradleHome
    $androidPassword = $null
    $androidSecurePassword.Dispose()
}
