param(
    [Parameter(Mandatory=$true)][string]$ApkPath,
    [string]$UnityAndroidRoot = ''
)
$ErrorActionPreference = 'Stop'
$apkCheckFailure = $null
$apkPublicReportPath = Join-Path (Get-Location).Path 'android-apk-verification.json'
$apkPublicReport = [ordered]@{
    status = 'checking'
    checkedAtUtc = [DateTime]::UtcNow.ToString('o')
    apk = [IO.Path]::GetFileName($ApkPath)
    bytes = $null
    sha256 = $null
    packageId = $null
    versionName = $null
    versionCode = $null
    minSdk = $null
    targetSdk = $null
    activity = $null
    orientation = $null
    landscape = $false
    glEsVersion = $null
    nativeArchitectures = @()
    signatureV2Verified = $false
    signatureV3Verified = $false
    signaturePolicy = 'minSdk 26 要求有效 v2；v3 仅记录，不强制。'
    signaturePolicySource = 'https://source.android.com/docs/security/features/apksigning/v2'
    signerCertificateSha256 = @()
    debuggable = $false
    zipFiles = 0
    forbiddenEntries = 0
    developmentDataAbsent = $false
    failure = $null
}

function Assert-ApkCheck([bool]$Condition, [string]$Message) {
    if (!$Condition) {
        $script:apkCheckFailure = $Message
        throw $Message
    }
}

function Invoke-ApkInspectionTool([string]$Executable, [string[]]$ToolArguments) {
    # 原始工具输出可能包含本地路径或证书身份；仅解析结果，不转发原文。
    $apkSavedErrorPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $apkToolLines = @(& $Executable @ToolArguments 2>&1)
        $apkToolExitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $apkSavedErrorPreference }
    Assert-ApkCheck ($apkToolExitCode -eq 0) 'Android SDK 检查工具返回失败。'
    return ($apkToolLines | ForEach-Object { $_.ToString() }) -join "`n"
}

function Read-ApkAttribute([string]$Line, [string]$Name) {
    $apkAttributeMatch = [regex]::Match($Line, ('\b' + [regex]::Escape($Name) + "='([^']*)'"))
    Assert-ApkCheck $apkAttributeMatch.Success 'APK 元数据缺少必要属性。'
    return $apkAttributeMatch.Groups[1].Value
}

function Read-ApkActivityBlock([string]$Manifest, [string]$Activity) {
    $apkManifestLines = $Manifest -split '\r?\n'
    for ($apkLineIndex = 0; $apkLineIndex -lt $apkManifestLines.Count; $apkLineIndex++) {
        $apkActivityStart = [regex]::Match($apkManifestLines[$apkLineIndex], '^(\s*)E: activity(?:\s|$)')
        if (!$apkActivityStart.Success) { continue }
        $apkActivityIndent = $apkActivityStart.Groups[1].Value.Length
        $apkActivityLines = [Collections.Generic.List[string]]::new()
        for ($apkNextLine = $apkLineIndex + 1; $apkNextLine -lt $apkManifestLines.Count; $apkNextLine++) {
            if ([string]::IsNullOrWhiteSpace($apkManifestLines[$apkNextLine])) { continue }
            $apkLineIndent = [regex]::Match($apkManifestLines[$apkNextLine], '^\s*').Length
            if ($apkLineIndent -le $apkActivityIndent) { break }
            $apkActivityLines.Add($apkManifestLines[$apkNextLine])
        }
        $apkActivityBlock = $apkActivityLines -join "`n"
        $apkActivityNamePattern = 'A: android:name\([^)]*\)="' + [regex]::Escape($Activity) + '"'
        if ($apkActivityBlock -match $apkActivityNamePattern) { return $apkActivityBlock }
    }
    Assert-ApkCheck $false '未能定位 APK 启动 Activity 的清单配置。'
}

try {
    Assert-ApkCheck (Test-Path -LiteralPath $ApkPath -PathType Leaf) '找不到待检查的 APK。'
    $apkResolvedPath = (Resolve-Path -LiteralPath $ApkPath).Path
    Assert-ApkCheck ([IO.Path]::GetExtension($apkResolvedPath) -ieq '.apk') '输入文件不是 APK。'
    $apkPublicReport.bytes = (Get-Item -LiteralPath $apkResolvedPath).Length
    $apkPublicReport.sha256 = (Get-FileHash -LiteralPath $apkResolvedPath -Algorithm SHA256).Hash

    if ([string]::IsNullOrWhiteSpace($UnityAndroidRoot)) {
        $apkToolchainPath = Join-Path $PSScriptRoot '..\development\toolchain.json'
        $apkToolchain = Get-Content -LiteralPath $apkToolchainPath -Raw | ConvertFrom-Json
        $UnityAndroidRoot = Join-Path (Split-Path $apkToolchain.unity) 'Data\PlaybackEngines\AndroidPlayer'
    }
    $apkBuildToolsRoot = Join-Path $UnityAndroidRoot 'SDK\build-tools'
    Assert-ApkCheck (Test-Path -LiteralPath $apkBuildToolsRoot -PathType Container) '未找到 Unity Android SDK build-tools。'
    $apkBuildToolsCandidates = @(Get-ChildItem -LiteralPath $apkBuildToolsRoot -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending)
    $apkSelectedBuildTools = $apkBuildToolsCandidates | Where-Object {
        (Test-Path -LiteralPath (Join-Path $_.FullName 'aapt.exe')) -and
        (Test-Path -LiteralPath (Join-Path $_.FullName 'apksigner.bat'))
    } | Select-Object -First 1
    Assert-ApkCheck ($null -ne $apkSelectedBuildTools) 'Android SDK 缺少 aapt 或 apksigner。'
    $apkAapt = Join-Path $apkSelectedBuildTools.FullName 'aapt.exe'
    $apkSigner = Join-Path $apkSelectedBuildTools.FullName 'apksigner.bat'
    $apkJavaRoot = Join-Path $UnityAndroidRoot 'OpenJDK'
    Assert-ApkCheck (Test-Path -LiteralPath (Join-Path $apkJavaRoot 'bin\java.exe')) 'Unity Android OpenJDK 不完整。'

    $apkBadging = Invoke-ApkInspectionTool $apkAapt @('dump','badging',$apkResolvedPath)
    $apkPackageLine = [regex]::Match($apkBadging, '(?m)^package:.*$').Value
    $apkPublicReport.packageId = Read-ApkAttribute $apkPackageLine 'name'
    $apkPublicReport.versionName = Read-ApkAttribute $apkPackageLine 'versionName'
    $apkPublicReport.versionCode = [int](Read-ApkAttribute $apkPackageLine 'versionCode')
    Assert-ApkCheck ($apkPublicReport.packageId -ceq 'com.catandevelopment.threekingdoms') 'APK 包名不符合当前内测发布配置。'
    Assert-ApkCheck ($apkPublicReport.versionName -ceq '1.4') 'APK versionName 应为 1.4。'
    Assert-ApkCheck ($apkPublicReport.versionCode -eq 10400) 'APK versionCode 应为 10400。'
    $apkMinMatch = [regex]::Match($apkBadging, "(?m)^sdkVersion:'(\d+)'\s*$")
    $apkTargetMatch = [regex]::Match($apkBadging, "(?m)^targetSdkVersion:'(\d+)'\s*$")
    Assert-ApkCheck ($apkMinMatch.Success -and $apkTargetMatch.Success) 'APK 缺少最低或目标 Android API。'
    $apkPublicReport.minSdk = [int]$apkMinMatch.Groups[1].Value
    $apkPublicReport.targetSdk = [int]$apkTargetMatch.Groups[1].Value
    Assert-ApkCheck ($apkPublicReport.minSdk -eq 26 -and $apkPublicReport.targetSdk -eq 36) 'APK Android API 应为 min 26 / target 36。'
    $apkGlesMatch = [regex]::Match($apkBadging, "(?m)^\s*uses-gl-es:\s*'(0x[0-9a-fA-F]+)'\s*$")
    Assert-ApkCheck $apkGlesMatch.Success 'APK 未声明 OpenGL ES 版本。'
    $apkGles = [Convert]::ToUInt32($apkGlesMatch.Groups[1].Value.Substring(2), 16)
    $apkPublicReport.glEsVersion = $apkGlesMatch.Groups[1].Value
    Assert-ApkCheck ($apkGles -ge 0x30000 -and $apkGles -lt 0x40000) 'APK 应使用 OpenGL ES 3。'
    $apkNativeLine = [regex]::Match($apkBadging, '(?m)^native-code:.*$').Value
    $apkArchitectures = @([regex]::Matches($apkNativeLine, "'([^']+)'") | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $apkPublicReport.nativeArchitectures = $apkArchitectures
    Assert-ApkCheck (($apkArchitectures -join ',') -ceq 'arm64-v8a,x86_64') 'APK 应同时且仅包含 ARM64 与 x86_64。'
    $apkPublicReport.debuggable = $apkBadging -match '(?m)^application-debuggable(?:\s|$)'
    $apkLaunchLine = [regex]::Match($apkBadging, '(?m)^launchable-activity:.*$').Value
    $apkPublicReport.activity = Read-ApkAttribute $apkLaunchLine 'name'
    $apkManifest = Invoke-ApkInspectionTool $apkAapt @('dump','xmltree',$apkResolvedPath,'AndroidManifest.xml')
    $apkActivity = Read-ApkActivityBlock $apkManifest $apkPublicReport.activity
    $apkOrientationMatch = [regex]::Match($apkActivity, 'android:screenOrientation\([^)]*\)=\(type 0x10\)(0x[0-9a-fA-F]+)')
    Assert-ApkCheck $apkOrientationMatch.Success 'APK 启动 Activity 没有明确的横屏方向配置。'
    $apkOrientation = [Convert]::ToInt32($apkOrientationMatch.Groups[1].Value.Substring(2), 16)
    $apkOrientationNames = @{0='landscape';6='sensorLandscape';8='reverseLandscape';11='userLandscape'}
    $apkPublicReport.orientation = if ($apkOrientationNames.ContainsKey($apkOrientation)) { $apkOrientationNames[$apkOrientation] } else { 'android-enum-' + $apkOrientation }
    $apkPublicReport.landscape = $apkOrientationNames.ContainsKey($apkOrientation)
    Assert-ApkCheck $apkPublicReport.landscape 'APK 清单允许竖屏，不能独立证明横屏发布配置。'

    $apkPreviousJavaHome = $env:JAVA_HOME
    try {
        $env:JAVA_HOME = $apkJavaRoot
        $apkSignature = Invoke-ApkInspectionTool $apkSigner @('verify','--verbose','--print-certs',$apkResolvedPath)
    } finally { $env:JAVA_HOME = $apkPreviousJavaHome }
    $apkPublicReport.signatureV2Verified = $apkSignature -match '(?m)^Verified using v2 scheme[^\r\n]*:\s*true\s*$'
    $apkPublicReport.signatureV3Verified = $apkSignature -match '(?m)^Verified using v3 scheme[^\r\n]*:\s*true\s*$'
    # v2 从 Android 7.0 / API 24 起支持，覆盖本包 minSdk 26。
    # v3 在 Android 9 增加签名密钥轮换资料，当前内测包不轮换密钥，不额外要求 v3。
    # 官方来源：https://source.android.com/docs/security/features/apksigning
    Assert-ApkCheck $apkPublicReport.signatureV2Verified 'APK 必须通过适用于 minSdk 26 的 v2 签名验证。'
    $apkCertificateMatches = [regex]::Matches($apkSignature, '(?m)^Signer #\d+ certificate SHA-256 digest:\s*([a-fA-F0-9]{64})\s*$')
    Assert-ApkCheck ($apkCertificateMatches.Count -gt 0) '签名验证没有返回有效的证书 SHA256 摘要。'
    $apkPublicReport.signerCertificateSha256 = @($apkCertificateMatches | ForEach-Object { $_.Groups[1].Value.ToUpperInvariant() } | Sort-Object -Unique)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $apkArchive = [IO.Compression.ZipFile]::OpenRead($apkResolvedPath)
    try {
        $apkFiles = @($apkArchive.Entries | Where-Object { $_.Name })
        $apkPublicReport.zipFiles = $apkFiles.Count
        Assert-ApkCheck ($apkFiles.Count -gt 0) 'APK ZIP 文件内容为空。'
        $apkSeenEntries = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $apkForbidden = 0
        foreach ($apkEntry in $apkFiles) {
            $apkEntryName = $apkEntry.FullName.Replace([char]92,[char]47)
            Assert-ApkCheck ($apkSeenEntries.Add($apkEntryName)) 'APK 包含重复 ZIP 条目。'
            if ($apkEntryName -match '(?i)(^/|^[a-z]:|(^|/)\.\.(/|$)|(^|/)(verification|mobile-verification|saves?|logs?|credentials|DoNotShip)(/|$)|\.(keystore|jks|p12|pfx|dpapi|log|trx|pdb)$|m6-local-ai|authority-private|review-start)') {
                $apkForbidden++
            }
        }
        $apkPublicReport.forbiddenEntries = $apkForbidden
        Assert-ApkCheck ($apkForbidden -eq 0) 'APK 含签名密钥、私有存档、日志或开发验收数据。'
        # 同时核对 ZIP 中真正携带了两个 IL2CPP 原生程序，而非只相信 badging 标签。
        foreach ($apkArchitecture in @('arm64-v8a','x86_64')) {
            Assert-ApkCheck ($null -ne $apkArchive.GetEntry('lib/' + $apkArchitecture + '/libil2cpp.so')) 'APK 缺少声明架构的 IL2CPP 原生程序。'
            Assert-ApkCheck ($null -ne $apkArchive.GetEntry('lib/' + $apkArchitecture + '/libunity.so')) 'APK 缺少声明架构的 Unity 原生程序。'
        }
        $apkPublicReport.developmentDataAbsent = $true
    } finally { $apkArchive.Dispose() }
    $apkPublicReport.status = 'passed'
} catch {
    $apkPublicReport.status = 'failed'
    $apkPublicReport.failure = if ($apkCheckFailure) { $apkCheckFailure } else { 'APK 检查未完成（' + $_.Exception.GetType().Name + '）。' }
}

$apkPublicJson = $apkPublicReport | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText($apkPublicReportPath, $apkPublicJson, [Text.UTF8Encoding]::new($false))
Write-Output $apkPublicJson
if ($apkPublicReport.status -ne 'passed') { throw 'Android APK 独立检查失败；请查看调用目录的 android-apk-verification.json。' }
