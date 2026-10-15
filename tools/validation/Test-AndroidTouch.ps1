param(
    [ValidateSet('Probe','Exercise')][string]$Mode = 'Probe',
    [ValidateSet('emulator-5554')][string]$Serial = 'emulator-5554',
    [string]$AdbPath = 'D:/Development/Android/Sdk/platform-tools/adb.exe',
    [string]$OutputDirectory = '.local/m6-release-1.4/android-touch'
)
$ErrorActionPreference = 'Stop'
$touchPackage = 'com.catandevelopment.threekingdoms'
$touchActivity = $touchPackage + '/com.unity3d.player.UnityPlayerActivity'
$touchDeviceState = '/sdcard/Android/data/' + $touchPackage + '/files/mobile-verification/touch-state.json'
$touchOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $touchOutput -Force | Out-Null
$touchFailure = $null
$touchAssertions = [Collections.Generic.List[string]]::new()
$touchScreenshots = [Collections.Generic.List[object]]::new()
$touchReport = [ordered]@{
    status='checking'; mode=$Mode; serial=$Serial; checkedAtUtc=[DateTime]::UtcNow.ToString('o')
    androidApi=$null; cpuAbis=$null; physicalSize=$null; density=$null; inputDevice=$null
    multiTouchSlots=$null; rawXMaximum=$null; rawYMaximum=$null
    actualInputExecuted=$false; manualScreenshotReviewRequired=$true; systemFullscreenHintsDismissed=0
    assertions=@(); screenshots=@(); failure=$null
}

function Assert-Touch([bool]$Condition,[string]$Message) {
    if (!$Condition) { $script:touchFailure='断言失败：'+$Message; throw $script:touchFailure }
    $script:touchAssertions.Add($Message)
}
function Invoke-TouchAdb([string[]]$TouchArguments,[switch]$AllowFailure) {
    $touchSavedPreference=$ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        $touchLines=@(& $AdbPath -s $Serial @TouchArguments 2>&1)
        $touchExit=$LASTEXITCODE
    } finally { $ErrorActionPreference=$touchSavedPreference }
    if ($touchExit -ne 0) {
        if ($AllowFailure) { return $null }
        $script:touchFailure='指定模拟器上的 adb 命令失败。'
        throw $script:touchFailure
    }
    return ($touchLines | ForEach-Object {$_.ToString()}) -join "`n"
}
function Read-TouchState {
    $touchJson=Invoke-TouchAdb -TouchArguments @('shell','cat',$touchDeviceState) -AllowFailure
    if (!$touchJson) { return $null }
    try { return $touchJson | ConvertFrom-Json } catch { return $null }
}
function Wait-TouchState([scriptblock]$Predicate,[string]$Failure,[int]$TimeoutSeconds=20) {
    $touchWait=[Diagnostics.Stopwatch]::StartNew()
    do {
        $touchState=Read-TouchState
        if ($null -ne $touchState -and (& $Predicate $touchState)) { return $touchState }
        Start-Sleep -Milliseconds 200
    } while ($touchWait.Elapsed.TotalSeconds -lt $TimeoutSeconds)
    Assert-Touch $false $Failure
}
function Invoke-TouchTap([double]$X,[double]$Y) {
    $touchState=Read-TouchState
    $touchPixelX=[int][Math]::Round($touchState.viewportLeft+$X*$touchState.viewportWidth/1440)
    $touchPixelY=[int][Math]::Round($touchState.viewportTop+$Y*$touchState.viewportHeight/900)
    Invoke-TouchAdb -TouchArguments @('shell','input','tap',"$touchPixelX","$touchPixelY") | Out-Null
}
function Save-TouchScreenshot([string]$Name) {
    # SwiftShader 的合成显示可能滞后于 LateUpdate 观察记录，给实际绘制留出完整帧。
    Start-Sleep -Milliseconds 500
    $touchRemotePng='/data/local/tmp/catan-touch-capture.png'
    $touchLocalPng=Join-Path $touchOutput $Name
    Invoke-TouchAdb -TouchArguments @('shell','screencap','-p',$touchRemotePng) | Out-Null
    Invoke-TouchAdb -TouchArguments @('pull',$touchRemotePng,$touchLocalPng) | Out-Null
    Invoke-TouchAdb -TouchArguments @('shell','rm','-f',$touchRemotePng) | Out-Null
    Assert-Touch ((Get-Item -LiteralPath $touchLocalPng).Length -gt 8192) '实际模拟器截图已落盘且不是空文件。'
    $script:touchScreenshots.Add([ordered]@{file=$Name;bytes=(Get-Item -LiteralPath $touchLocalPng).Length;sha256=(Get-FileHash -LiteralPath $touchLocalPng -Algorithm SHA256).Hash})
}
function Open-TouchSession {
    Invoke-TouchAdb -TouchArguments @('shell','am','force-stop',$touchPackage) | Out-Null
    # 只清理本验收会话的公开观察文件，保留存档、run/load 证据和所有其他应用数据。
    Invoke-TouchAdb -TouchArguments @('shell','rm','-f',$touchDeviceState) | Out-Null
    Invoke-TouchAdb -TouchArguments @('shell','am','start','-W','-n',$touchActivity,'--es','catan_mobile_verify','open:base') | Out-Null
    $touchOpened=Wait-TouchState {param($s) $s.status -eq 'observing-real-input' -and $s.mode -eq 'base'} '未收到 APK 的独立触控观察记录。' 40
    # Android 首次进入沉浸式界面会延迟弹出系统教学，只在识别到该明确系统节点后关闭。
    Start-Sleep -Milliseconds 1500
    Invoke-TouchAdb -TouchArguments @('shell','uiautomator','dump','/data/local/tmp/catan-touch-ui.xml') -AllowFailure | Out-Null
    $touchHierarchy=Invoke-TouchAdb -TouchArguments @('shell','cat','/data/local/tmp/catan-touch-ui.xml') -AllowFailure
    if ($touchHierarchy -and $touchHierarchy.Contains('android:id/immersive_cling_title')) {
        [xml]$touchUi=$touchHierarchy
        $touchOk=$touchUi.SelectSingleNode("//node[@package='android' and @resource-id='android:id/ok']")
        if ($touchOk -and $touchOk.bounds -match '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$') {
            $touchOkX=[int](([int]$Matches[1]+[int]$Matches[3])/2)
            $touchOkY=[int](([int]$Matches[2]+[int]$Matches[4])/2)
            Invoke-TouchAdb -TouchArguments @('shell','input','tap',"$touchOkX","$touchOkY") | Out-Null
            $script:touchReport.systemFullscreenHintsDismissed++
            Start-Sleep -Milliseconds 300
        }
    }
    Invoke-TouchAdb -TouchArguments @('shell','rm','-f','/data/local/tmp/catan-touch-ui.xml') -AllowFailure | Out-Null
    return $touchOpened
}
function Invoke-TouchPinch([switch]$Inward) {
    # 只写已探测的模拟器 MT 设备；B 协议使用不同 tracking ID，真实产生两指 MotionEvent。
    $touchRawScript=[Collections.Generic.List[string]]::new()
    $touchRawScript.Add('#!/system/bin/sh')
    $touchCenterX=[int]($touchReport.rawXMaximum/2)
    $touchCenterY=[int]($touchReport.rawYMaximum/2)
    $touchRawStep={param($slot,$tracking,$spread)
        $touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 47 $slot || exit 91")
        if ($tracking -ge 0) {$touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 57 $tracking || exit 91")}
        $touchSign=if ($slot -eq 0) {-1} else {1}
        # 此 AVD 自然方向为竖屏；横屏手势沿原始 Y 轴对称移动，不依赖左右旋转符号。
        $touchRawY=$touchCenterY+$touchSign*$spread
        $touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 53 $touchCenterX || exit 91")
        $touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 54 $touchRawY || exit 91")
        $touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 48 12 || exit 91")
        $touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 58 600 || exit 91")
    }
    $touchInitialSpread=if ($Inward) {6500} else {3500}
    & $touchRawStep 0 901 $touchInitialSpread
    & $touchRawStep 1 902 $touchInitialSpread
    $touchRawScript.Add("sendevent $($touchReport.inputDevice) 0 0 0 || exit 91")
    $touchRawScript.Add('sleep 0.10')
    for ($touchFrame=1;$touchFrame -le 12;$touchFrame++) {
        $touchSpread=[int]($touchInitialSpread+$(if($Inward){-1}else{1})*250*$touchFrame)
        & $touchRawStep 0 -1 $touchSpread
        & $touchRawStep 1 -1 $touchSpread
        $touchRawScript.Add("sendevent $($touchReport.inputDevice) 0 0 0 || exit 91")
        $touchRawScript.Add('sleep 0.04')
    }
    # 先抬第二指，保留第一指片刻，专门检查双指结束不会变成一次地图放置。
    $touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 47 1")
    $touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 57 -1")
    $touchRawScript.Add("sendevent $($touchReport.inputDevice) 0 0 0")
    $touchRawScript.Add('sleep 0.15')
    $touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 47 0")
    $touchRawScript.Add("sendevent $($touchReport.inputDevice) 3 57 -1")
    $touchRawScript.Add("sendevent $($touchReport.inputDevice) 0 0 0")
    $touchLocalScript=Join-Path $touchOutput 'pinch-input-script.sh'
    [IO.File]::WriteAllText($touchLocalScript,($touchRawScript -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
    $touchRemoteScript='/data/local/tmp/catan-touch-pinch.sh'
    try {
        Invoke-TouchAdb -TouchArguments @('push',$touchLocalScript,$touchRemoteScript) | Out-Null
        Invoke-TouchAdb -TouchArguments @('shell','sh',$touchRemoteScript) | Out-Null
    } finally {
        # 即使注入中途失败，也放开本脚本占用的两个 slot，不留悬挂手指。
        Invoke-TouchAdb -TouchArguments @('shell',"sendevent $($touchReport.inputDevice) 3 47 1; sendevent $($touchReport.inputDevice) 3 57 -1; sendevent $($touchReport.inputDevice) 3 47 0; sendevent $($touchReport.inputDevice) 3 57 -1; sendevent $($touchReport.inputDevice) 0 0 0") -AllowFailure | Out-Null
        Invoke-TouchAdb -TouchArguments @('shell','rm','-f',$touchRemoteScript) -AllowFailure | Out-Null
    }
}

try {
    Assert-Touch (Test-Path -LiteralPath $AdbPath -PathType Leaf) 'adb 工具存在。'
    Assert-Touch ((Invoke-TouchAdb -TouchArguments @('get-state')).Trim() -eq 'device') '明确指定的 emulator-5554 在线。'
    $touchReport.androidApi=(Invoke-TouchAdb -TouchArguments @('shell','getprop','ro.build.version.sdk')).Trim()
    $touchReport.cpuAbis=(Invoke-TouchAdb -TouchArguments @('shell','getprop','ro.product.cpu.abilist')).Trim()
    $touchReport.physicalSize=(Invoke-TouchAdb -TouchArguments @('shell','wm','size')).Trim()
    $touchReport.density=(Invoke-TouchAdb -TouchArguments @('shell','wm','density')).Trim()
    $touchCapabilities=Invoke-TouchAdb -TouchArguments @('shell','getevent','-lp')
    [IO.File]::WriteAllText((Join-Path $touchOutput 'input-capabilities.txt'),$touchCapabilities,[Text.UTF8Encoding]::new($false))
    $touchBlocks=[regex]::Matches($touchCapabilities,'(?ms)^add device \d+: (?<path>/dev/input/event\d+)\r?\n(?<body>.*?)(?=^add device |\z)')
    $touchMain=@($touchBlocks | Where-Object {$_.Groups['body'].Value -match 'name:\s+"virtio_input_multi_touch_1"' -and $_.Groups['body'].Value -match 'ABS_MT_SLOT'})
    Assert-Touch ($touchMain.Count -eq 1) '已唯一识别模拟器主多触点设备。'
    $touchReport.inputDevice=$touchMain[0].Groups['path'].Value
    $touchBody=$touchMain[0].Groups['body'].Value
    $touchSlot=[regex]::Match($touchBody,'ABS_MT_SLOT\s*:.*?max (\d+)')
    $touchRawX=[regex]::Match($touchBody,'ABS_MT_POSITION_X\s*:.*?max (\d+)')
    $touchRawY=[regex]::Match($touchBody,'ABS_MT_POSITION_Y\s*:.*?max (\d+)')
    Assert-Touch ($touchSlot.Success -and $touchRawX.Success -and $touchRawY.Success) '多触点 slot 与原始坐标范围完整。'
    $touchReport.multiTouchSlots=[int]$touchSlot.Groups[1].Value+1
    $touchReport.rawXMaximum=[int]$touchRawX.Groups[1].Value
    $touchReport.rawYMaximum=[int]$touchRawY.Groups[1].Value
    Assert-Touch ($touchReport.multiTouchSlots -ge 2) '设备声明支持至少两指。'
    if ($Mode -eq 'Probe') {
        $touchReport.status='probe-passed'
    } else {
        Assert-Touch ((Invoke-TouchAdb -TouchArguments @('shell','pm','path',$touchPackage)) -match '^package:') '目标 APK 已安装在指定模拟器。'
        $touchState=Open-TouchSession
        $touchReport.actualInputExecuted=$true
        if ($touchState.actor -ne $touchState.humanSeat) {
            # 先手由正常规则随机决定。只让产品 AI 执行其席位，轮到人类时再经真实菜单暂停。
            Invoke-TouchTap 97 48
            $null=Wait-TouchState {param($s) $s.tableMenuOpen} '随机先手准备阶段菜单可打开。'
            Invoke-TouchTap 382 440
            $null=Wait-TouchState {param($s) !$s.paused} '随机先手准备阶段可继续产品 AI。'
            Invoke-TouchTap 380 190
            $touchState=Wait-TouchState {param($s) !$s.tableMenuOpen -and $s.actor -eq $s.humanSeat -and $s.phase -eq 'SetupSettlement'} '普通产品 AI 未推进到人类首次摆放。'
            Invoke-TouchTap 97 48
            $null=Wait-TouchState {param($s) $s.tableMenuOpen} '人类回合暂停前菜单可打开。'
            Invoke-TouchTap 382 440
            $null=Wait-TouchState {param($s) $s.paused} '普通产品 AI 可在到达人类回合后暂停。'
            Invoke-TouchTap 380 190
            $touchState=Wait-TouchState {param($s) !$s.tableMenuOpen -and $s.paused} '随机先手准备后返回正常人类回合。'
        }
        Assert-Touch ($touchState.paused -and $touchState.phase -eq 'SetupSettlement' -and $touchState.actor -eq $touchState.humanSeat) '初始验收基线为真实暂停的人类首次摆放。'
        $touchInitialRevision=$touchState.revisionChanges
        $touchInitialSettlements=$touchState.settlements
        Save-TouchScreenshot '01-initial.png'
        Invoke-TouchTap 97 48
        $touchState=Wait-TouchState {param($s) $s.tableMenuOpen} '菜单按钮没有打开游戏菜单。'
        Save-TouchScreenshot '02-menu.png'
        Invoke-TouchTap 380 190
        $touchState=Wait-TouchState {param($s) !$s.tableMenuOpen} '返回游戏按钮没有响应。'
        Invoke-TouchTap 117 777
        $touchState=Wait-TouchState {param($s) $s.dockOverlay} '建造按钮没有打开抽屉。'
        Save-TouchScreenshot '03-build-drawer.png'
        Invoke-TouchTap 1310 66
        $touchState=Wait-TouchState {param($s) !$s.dockOverlay} '关闭建造抽屉没有响应。'
        $touchBeforePan=$touchState
        $touchPanX1=[int]($touchState.viewportLeft+600*$touchState.viewportWidth/1440)
        $touchPanX2=[int]($touchState.viewportLeft+800*$touchState.viewportWidth/1440)
        $touchPanY1=[int]($touchState.viewportTop+430*$touchState.viewportHeight/900)
        $touchPanY2=[int]($touchState.viewportTop+480*$touchState.viewportHeight/900)
        Invoke-TouchAdb -TouchArguments @('shell','input','swipe',"$touchPanX1","$touchPanY1","$touchPanX2","$touchPanY2",'650') | Out-Null
        $touchState=Wait-TouchState {param($s) [Math]::Abs($s.panX-$touchBeforePan.panX)+[Math]::Abs($s.panZ-$touchBeforePan.panZ) -gt .0001} '单指拖动没有改变地图相机。'
        Assert-Touch ($touchState.revisionChanges -eq $touchInitialRevision -and $touchState.settlements -eq $touchInitialSettlements) '单指平移没有放置棋子或改变规则。'
        Save-TouchScreenshot '04-after-pan.png'
        $touchBeforePinch=$touchState
        Invoke-TouchPinch
        $touchState=Wait-TouchState {param($s) $s.maxTouchCount -ge 2 -and [Math]::Abs($s.cameraSize-$touchBeforePinch.cameraSize) -gt .0001} '没有观察到真实双指输入及地图缩放。'
        Assert-Touch ($touchState.revisionChanges -eq $touchInitialRevision -and $touchState.settlements -eq $touchInitialSettlements) '双指缩放及单指残留没有误放置。'
        Save-TouchScreenshot '05-after-pinch.png'
        Invoke-TouchTap 117 777
        $null=Wait-TouchState {param($s) $s.dockOverlay} '建造抽屉未重新打开。'
        Invoke-TouchTap 252 288
        $touchState=Wait-TouchState {param($s) $s.selectedBuild -match '^SetupSettlement' -and !$s.dockOverlay} '点击选取定居点没有进入放置状态。'
        Invoke-TouchPinch -Inward
        $touchState=Wait-TouchState {param($s) $s.selectedBuild -match '^SetupSettlement'} '双指结束丢失了已选择的棋子。'
        Assert-Touch ($touchState.revisionChanges -eq $touchInitialRevision -and $touchState.settlements -eq $touchInitialSettlements) '拿起棋子后双指缩放没有误放置。'
        Invoke-TouchTap 117 672
        $touchState=Wait-TouchState {param($s) $s.selectedBuild -eq ''} '放回棋子按钮未清除选择。'
        Assert-Touch ($touchState.revisionChanges -eq $touchInitialRevision) '取消建造未改变规则。'
        Save-TouchScreenshot '06-cancelled-build.png'
        Invoke-TouchTap 117 777
        $null=Wait-TouchState {param($s) $s.dockOverlay} '建造抽屉未再次打开。'
        Invoke-TouchTap 252 288
        $touchState=Wait-TouchState {param($s) $s.selectedBuild -match '^SetupSettlement' -and $s.legalPlacementAvailable} '未取得人类公开视图中的可触达合法落点。'
        Invoke-TouchAdb -TouchArguments @('shell','input','tap',"$([int]$touchState.legalPlacementScreenX)","$([int]$touchState.legalPlacementScreenY)") | Out-Null
        $touchState=Wait-TouchState {param($s) $s.settlements -eq $touchInitialSettlements+1 -and $s.phase -eq 'SetupRoad'} '真实点击合法点没有建村并进入修路阶段。'
        Assert-Touch ($touchState.revisionChanges -eq $touchInitialRevision+1) '一次真实点击仅造成一次合法规则变更。'
        Save-TouchScreenshot '07-settlement-placed.png'
        Invoke-TouchTap 97 48
        $null=Wait-TouchState {param($s) $s.tableMenuOpen} '建村后菜单未打开。'
        Invoke-TouchTap 382 318
        $touchState=Wait-TouchState {param($s) $s.saveExists -and $s.saveMatchesCurrent} '保存按钮没有持久保存当前权威局面。'
        Invoke-TouchTap 382 440
        $null=Wait-TouchState {param($s) !$s.paused} '继续 AI 按钮没有响应。'
        Invoke-TouchTap 382 440
        $touchState=Wait-TouchState {param($s) $s.paused} '暂停 AI 按钮没有响应。'
        Invoke-TouchAdb -TouchArguments @('shell','input','keyevent','4') | Out-Null
        $touchState=Wait-TouchState {param($s) !$s.tableMenuOpen} 'Android 返回操作没有返回游戏。'
        Save-TouchScreenshot '08-returned-and-saved.png'
        $touchState=Open-TouchSession
        Assert-Touch ($touchState.settlements -eq 0 -and $touchState.saveExists) '独立重启保留存档且不自动替换验收新局。'
        Invoke-TouchTap 97 48
        $null=Wait-TouchState {param($s) $s.tableMenuOpen} '独立重启后菜单未打开。'
        Invoke-TouchTap 1057 318
        $touchState=Wait-TouchState {param($s) $s.settlements -eq $touchInitialSettlements+1 -and $s.phase -eq 'SetupRoad' -and $s.saveMatchesCurrent} '独立重启后读取存档未恢复建村及修路待决。'
        Save-TouchScreenshot '09-restored-in-new-process.png'
        $touchReport.status='passed'
    }
} catch {
    $touchReport.status='failed'
    $touchReport.failure=if($touchFailure){$touchFailure}else{'触控检查未完成（'+$_.Exception.GetType().Name+'）。'}
}
$touchReport.assertions=@($touchAssertions)
$touchReport.screenshots=@($touchScreenshots)
$touchPublicJson=$touchReport | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $touchOutput 'touch-verification.json'),$touchPublicJson,[Text.UTF8Encoding]::new($false))
Write-Output $touchPublicJson
if($touchReport.status -eq 'failed'){throw 'Android 触控检查失败，请查看输出目录 touch-verification.json。'}
