using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

// 仅显式本地验收参数启用。普通 APK 启动不会接管人类、生成测试局或输出存档。
// 截图与权威存档只写入调用者指定目录或应用私有目录，公开结果不包含隐藏数据。
public sealed class MobileVerification : MonoBehaviour
{
    [Serializable] sealed class Report
    {
        public string status, stage, unity, platform, graphics, note;
        public int checks, modes, screenshots, screenWidth, screenHeight, humanCommands, aiCommands;
        public int sampledDarkest, sampledBrightest, sampledChromatic, captureAttempts;
        public float safeLeft, safeBottom, safeWidth, safeHeight, viewportLeft, viewportTop, viewportWidth, viewportHeight;
        public bool sharedPcUi, touchInputEnabled, landscape, bundledChineseFont, independentProcessLoad;
        public string[] images;
    }
    [Serializable] sealed class TouchState
    {
        public string status, mode, phase, actor, humanSeat, selectedBuild, legalPlacementId, legalPlacementKind;
        public int sequence, revisionChanges, maxTouchCount, touchFrames, turn, settlements, cities, roads, ships, knights;
        public bool menuVisible, tableMenuOpen, dockOverlay, paused, saveExists, saveMatchesCurrent;
        public bool tradeAvailable, tradeOpen, resourceHandVisible, buildAvailable;
        public float buildScreenX, buildScreenY;
        public int visibleCards;
        public float viewportLeft, viewportTop, viewportWidth, viewportHeight, cameraSize, panX, panZ;
        public bool legalPlacementAvailable;
        public float legalPlacementScreenX, legalPlacementScreenY;
    }
    M6App app;
    string output, mode, stage = "启动", request, failedAssertion;
    public bool VerificationActive => !string.IsNullOrEmpty(request);
    int checks, modes, humanCommands, aiCommands;
    int sampledDarkest, sampledBrightest, sampledChromatic, captureAttempts;
    readonly List<string> images = new List<string>();
    bool running;
    bool sharedPcUiVerified;
    int touchSequence, touchRevisionChanges, maxTouchCount, touchFrames;
    float nextTouchState;
    string authorityFingerprint;
    TabletopScene Table => mode == "base" ? GetComponent<M2App>().VerificationBoard.TableScene :
        mode == "seafarers" ? GetComponent<M3App>().VerificationBoard.TableScene :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationBoard.TableScene : GetComponent<M5App>().VerificationBoard.TableScene;
    public void Initialize(M6App value) { app = value; StartCoroutine(Run()); }
    OperationSnapshot Snapshot => mode == "base" ? GetComponent<M2App>().VerificationOperationSnapshot :
        mode == "seafarers" ? GetComponent<M3App>().VerificationOperationSnapshot :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationOperationSnapshot : GetComponent<M5App>().VerificationOperationSnapshot;
    OperationDock Dock => mode == "base" ? GetComponent<M2App>().VerificationOperationDock :
        mode == "seafarers" ? GetComponent<M3App>().VerificationOperationDock :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationOperationDock : GetComponent<M5App>().VerificationOperationDock;
    string Authority => mode == "base" ? GetComponent<M2App>().VerificationHost.ExportSave() :
        mode == "seafarers" ? GetComponent<M3App>().VerificationHost.ExportSave() :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationHost.ExportSave() : GetComponent<M5App>().VerificationHost.ExportSave();
    string ViewSeat => mode == "base" ? GetComponent<M2App>().M6View.PlayerId :
        mode == "seafarers" ? GetComponent<M3App>().M6View.PlayerId :
        mode == "cities-knights" ? GetComponent<M4App>().M6View.PlayerId : GetComponent<M5App>().M6View.PlayerId;
    int Turn => mode == "base" ? GetComponent<M2App>().M6View.Turn :
        mode == "seafarers" ? GetComponent<M3App>().M6View.Turn :
        mode == "cities-knights" ? GetComponent<M4App>().M6View.Turn : GetComponent<M5App>().M6View.Turn;
    string Phase => mode == "base" ? GetComponent<M2App>().M6View.Phase.ToString() :
        mode == "seafarers" ? GetComponent<M3App>().M6View.Phase.ToString() :
        mode == "cities-knights" ? GetComponent<M4App>().M6View.Phase.ToString() : GetComponent<M5App>().M6View.Phase.ToString();
    TabletopUI Hud
    {
        get
        {
            MonoBehaviour adapter = mode == "base" ? (MonoBehaviour)GetComponent<M2App>() : mode == "seafarers" ? GetComponent<M3App>() :
                mode == "cities-knights" ? (MonoBehaviour)GetComponent<M4App>() : GetComponent<M5App>();
            return (TabletopUI)adapter.GetType().GetField("tabletop", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter);
        }
    }

    void LateUpdate()
    {
        if (running || string.IsNullOrEmpty(request) || !request.StartsWith("open:", StringComparison.Ordinal) || !app.Ready) return;
        maxTouchCount = Math.Max(maxTouchCount, Input.touchCount);
        if (Input.touchCount > 0) touchFrames++;
        if (Time.unscaledTime < nextTouchState) return;
        nextTouchState = Time.unscaledTime + .2f;
        try
        {
            string fingerprint;
            using (var sha = SHA256.Create()) fingerprint = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(Authority)));
            if (authorityFingerprint != null && authorityFingerprint != fingerprint) touchRevisionChanges++;
            authorityFingerprint = fingerprint; // 仅在内存比较；不写入摘要、日志或网络。
            var viewport = MobileTabletop.Viewport;
            var state = new TouchState {
                status = "observing-real-input", mode = mode, phase = Phase, actor = app.CurrentActor, humanSeat = app.HumanSeat,
                sequence = ++touchSequence, revisionChanges = touchRevisionChanges, maxTouchCount = maxTouchCount, touchFrames = touchFrames, turn = Turn,
                menuVisible = app.MenuVisible, tableMenuOpen = app.TableMenuOpen, dockOverlay = Dock.HasOverlay, paused = app.Paused,
                selectedBuild = Dock.Selection.BuildId ?? "", viewportLeft = viewport.Left, viewportTop = viewport.Top, viewportWidth = viewport.Width, viewportHeight = viewport.Height,
                cameraSize = Table.TableCamera.orthographicSize, panX = Table.PanOffset.x, panZ = Table.PanOffset.z,
                saveExists = File.Exists(app.SavePath)
            };
            state.tradeAvailable = Snapshot.CanTrade && Snapshot.CanInteract;
            state.tradeOpen = Dock.TradeOpen; state.resourceHandVisible = Dock.ResourceHandVisible;
            state.visibleCards = Dock.VerificationHandRects.Length;
            var setupBuild = Snapshot.Builds.FirstOrDefault(build => build.Available && build.Id.StartsWith("SetupSettlement", StringComparison.Ordinal));
            Vector2 buildPoint;
            if (setupBuild != null && Dock.VerificationTryBuildPoint(setupBuild.Id, out buildPoint))
            {
                var screenPoint = MobileTabletop.ScreenPoint(buildPoint);
                state.buildAvailable = true; state.buildScreenX = screenPoint.x; state.buildScreenY = screenPoint.y;
            }
            state.saveMatchesCurrent = state.saveExists && File.ReadAllText(app.SavePath) == app.ExportEnvelope();
            string[] legal;
            Func<string, Vector2> point;
            switch (mode)
            {
                case "base":
                    var b = GetComponent<M2App>(); var bv = b.M6View;
                    state.settlements = bv.Board.Settlements.Length; state.cities = bv.Board.Cities.Length; state.roads = bv.Board.Roads.Length;
                    legal = Phase == "SetupSettlement" ? bv.LegalVertexIds : Phase == "SetupRoad" ? bv.LegalEdgeIds : Array.Empty<string>(); point = b.VerificationBoard.ScreenPoint; break;
                case "seafarers":
                    var s = GetComponent<M3App>(); var sv = s.M6View;
                    state.settlements = sv.Board.Settlements.Length; state.cities = sv.Board.Cities.Length; state.roads = sv.Board.Roads.Length; state.ships = sv.Board.Ships.Length;
                    legal = Phase == "SetupSettlement" ? sv.LegalVertexIds : Phase == "SetupRoad" ? sv.LegalEdgeIds : Array.Empty<string>(); point = s.VerificationBoard.ScreenPoint; break;
                case "cities-knights":
                    var c = GetComponent<M4App>(); var cv = c.M6View;
                    state.settlements = cv.Board.Settlements.Length; state.cities = cv.Board.Cities.Length; state.roads = cv.Board.Roads.Length; state.knights = cv.Board.Knights.Length;
                    legal = Phase == "SetupSettlement" ? cv.LegalVertexIds : Phase == "SetupRoad" ? cv.LegalEdgeIds : Array.Empty<string>(); point = c.VerificationBoard.ScreenPoint; break;
                default:
                    var x = GetComponent<M5App>(); var xv = x.M6View;
                    state.settlements = xv.Board.Settlements.Length; state.cities = xv.Board.Cities.Length; state.roads = xv.Board.Roads.Length; state.ships = xv.Board.Ships.Length; state.knights = xv.Board.Knights.Length;
                    legal = Phase == "SetupSettlement" ? xv.LegalVertexIds : Phase == "SetupRoad" ? xv.LegalEdgeIds : Array.Empty<string>(); point = x.VerificationBoard.ScreenPoint; break;
            }
            var candidates = legal.Select(point).Where(p => {
                var logical = MobileTabletop.Logical(p); return logical.x > 250 && logical.x < 1150 && logical.y > 280 && logical.y < 600;
            }).OrderBy(p => (MobileTabletop.Logical(p) - new Vector2(720, 440)).sqrMagnitude).ToArray();
            if (candidates.Length > 0)
            {
                state.legalPlacementAvailable = true; state.legalPlacementScreenX = candidates[0].x; state.legalPlacementScreenY = candidates[0].y;
                state.legalPlacementId = legal.First(id => (point(id) - candidates[0]).sqrMagnitude < .001f);
                state.legalPlacementKind = Phase == "SetupRoad" ? "edge" : "vertex";
            }
            File.WriteAllText(Path.Combine(output, "touch-state.json"), JsonUtility.ToJson(state, true));
        }
        catch (Exception error)
        {
            Debug.LogWarning("CATAN_MOBILE_TOUCH_OBSERVER_FAILED: type=" + error.GetType().Name);
            enabled = false;
        }
    }

    IEnumerator Run()
    {
        var args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, "--mobile-verify");
        int loadFlag = Array.IndexOf(args, "--mobile-verify-load");
        if (flag < 0 && loadFlag >= 0) flag = loadFlag;
        if (flag >= 0 && flag + 1 < args.Length)
        {
            request = loadFlag >= 0 ? "load" : "run"; output = Path.GetFullPath(args[flag + 1]);
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        else
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                request = intent.Call<string>("getStringExtra", "catan_mobile_verify");
            if (!string.IsNullOrEmpty(request)) output = Path.Combine(Application.persistentDataPath, "mobile-verification");
        }
#endif
        if (string.IsNullOrEmpty(request)) yield break;
        Directory.CreateDirectory(output); running = true;
        // M6 与各适配器的 Start 完成后才检查 UI 与字体。
        yield return null; yield return null;
        var work = Verify();
        while (true)
        {
            object next;
            try { if (!work.MoveNext()) break; next = work.Current; }
            catch (Exception error)
            {
                SaveReport("failed", failedAssertion ?? error.GetType().Name);
                Debug.LogError("CATAN_MOBILE_VERIFY_FAILED: stage=" + stage + "; type=" + error.GetType().Name);
                running = false; Application.Quit(1); yield break;
            }
            yield return next;
        }
        running = false;
        if (request.StartsWith("open:", StringComparison.Ordinal))
        {
            SaveReport("ready-for-touch", "仅创建本地验收局，后续屏幕触控由外部输入执行。该记录不等同于触控通过。");
            Debug.Log("CATAN_MOBILE_TOUCH_READY: mode=" + mode); yield break;
        }
        SaveReport("passed", "四模式共用 PC HUD、常驻建造与手牌、设施、交易、菜单、同权限命令及存档检查通过。截图仍需人工检查；触控操作与长期性能单独验收。");
        Debug.Log("CATAN_MOBILE_VERIFY_OK: modes=" + modes + "; checks=" + checks + "; screenshots=" + images.Count);
        Application.Quit(0);
    }

    IEnumerator Verify()
    {
        stage = "横屏安全区与中文字体";
        Require(request == "run" || request == "load" || request.StartsWith("open:", StringComparison.Ordinal), "未知验收请求");
        Require(MobileTabletop.Enabled, "未启用触控与安全区适配");
        Require(Screen.width > Screen.height, "屏幕尚未横屏");
        Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "需要真实图形设备");
        var viewport = MobileTabletop.Viewport;
        Require(viewport.Width > 0 && viewport.Height > 0, "安全区为空");
        var zero = MobileTabletop.Logical(MobileTabletop.ScreenPoint(Vector2.zero));
        var corner = MobileTabletop.Logical(MobileTabletop.ScreenPoint(new Vector2(1440, 900)));
        Require(zero.sqrMagnitude < .001f && (corner - new Vector2(1440, 900)).sqrMagnitude < .001f, "UI 与点击坐标不一致");
        var font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
        Require(font != null, "未打包中文字体");
        foreach (char character in "卡坦岛船骑贸粮") Require(font.HasCharacter(character), "中文字体缺少验收字形");
        if (request.StartsWith("open:", StringComparison.Ordinal))
        {
            bool actionFixture = request.EndsWith(":action", StringComparison.Ordinal);
            mode = request.Substring(5); if (actionFixture) mode = mode.Substring(0, mode.Length - ":action".Length);
            Require(new[] { "base", "seafarers", "cities-knights", "combined" }.Contains(mode), "未知验收模式");
            app.StartGame(mode, "heading-for-new-shores", 3, "P1", true);
            yield return null; yield return null;
            Require(app.Ready && ViewSeat == "P1", "未建立固定人类视图");
            if (actionFixture)
            {
                int steps = 0;
                while (steps < 900 && !(app.IsHumanTurn && Snapshot.CanTrade && Snapshot.Goods.Sum() >= 2))
                { while(app.TablePresentationBlocked)yield return null; Advance(); steps++; if (steps % 4 == 0) yield return null; }
                Require(steps < 900, "触控验收未推进到正常人类交易回合");
                yield return new WaitForSecondsRealtime(3.1f);
            }
            yield return new WaitForEndOfFrame();
            CheckSharedPcHud();
            yield break;
        }
        foreach (var frame in CaptureReady("00-pc-menu.png")) yield return frame;
        foreach (string rules in new[] { "base", "seafarers", "cities-knights", "combined" })
        {
            mode = rules; stage = mode + "-开局";
            string originalPath = Path.Combine(output, mode + "-authority-private.json");
            if (request == "load") app.ReadSave(originalPath);
            else app.StartGame(mode, "heading-for-new-shores", modes % 2 == 0 ? 3 : 4, "P1", true);
            yield return null; yield return null;
            Require(app.Ready && ViewSeat == app.HumanSeat && Snapshot.Seat == app.HumanSeat, "视图未固定到人类");
            if (request == "load") Require(app.ExportEnvelope() == File.ReadAllText(originalPath), "独立进程恢复未精确还原权威局面");
            Require(Snapshot.OtherPlayers.Length == app.PlayerCount - 1, "对手席位数错误");
            string initial = Authority;
            Require(!app.StepAi() && initial == Authority, "暂停状态推进了规则");
            foreach (var frame in CaptureReady(mode + "-setup.png")) yield return frame;
            CheckSharedPcHud();
            int steps = 0, previousAi = app.AiCommandsExecuted;
            stage = mode + "-同权限行动";
            while (steps < 900 && !(app.IsHumanTurn && Snapshot.CanTrade && Snapshot.Goods.Sum() >= 2 && Turn >= 3 && app.AiCommandsExecuted > previousAi))
            {
                while(app.TablePresentationBlocked)yield return null;
                Advance(); steps++;
                if (steps % 4 == 0) yield return null;
            }
            Require(steps < 900 && app.AiCommandsExecuted > previousAi, "未推进到正常人类回合");
            Require(ViewSeat == app.HumanSeat && app.Fault == "", "AI 续跑改变了人类视图");
            yield return new WaitForSecondsRealtime(3.1f);
            foreach (var frame in CaptureReady(mode + "-action.png")) yield return frame;
            stage = mode + "-PC常驻建造与完整手牌";
            string beforePanels = Authority;
            CheckSharedPcHud();
            Require(Dock.ResourceHandVisible && Dock.VerificationHandsAreScreenSpace, "未使用 PC 屏幕底部资源手牌");
            Require(Dock.VerificationVisibleResourceCount == Snapshot.Goods.Take(Snapshot.GoodCount).Sum(), "常驻手牌没有逐张展示当前玩家全部资源");
            Require(Dock.VerificationPendingResourceCount == 0, "收牌完成后仍缺少可见手牌");
            foreach (var rect in Dock.VerificationHandRects)
                Require(rect.width > 0 && rect.height > 0 && rect.x >= 0 && rect.xMax <= 1440 && rect.y >= 0 && rect.yMax <= 900, "PC 手牌超出可见逻辑画布");
            Require(!Dock.HasOverlay && Authority == beforePanels, "常驻建造与手牌被独立移动弹层替代或改变了权威局面");
            foreach (var frame in CaptureReady(mode + "-pc-build-and-hand.png")) yield return frame;
            if (mode == "cities-knights" || mode == "combined")
            {
                stage = mode + "-三类城市设施";
                for (int track = 0; track < 3; track++)
                {
                    Dock.OpenFacility(track);
                    Require(Dock.VerificationFacilityTrack == track, "城市设施面板未打开");
                    foreach (var frame in CaptureReady(mode + "-facility-" + track + ".png")) yield return frame;
                    Dock.Reset(); Require(Authority == beforePanels, "设施查看改变了权威局面");
                }
            }
            stage = mode + "-PC交易面板";
            string beforeDraft = Authority; Dock.OpenTrade();
            Require(Dock.TradeOpen && beforeDraft == Authority, "打开交易改变规则或面板未打开");
            yield return null;
            foreach (var frame in CaptureReady(mode + "-trade.png")) yield return frame;
            Require(Dock.VerificationTradeGiveGoods.SequenceEqual(OperationPanelOptions.PaymentGoods(Snapshot)), "PC 交易付出资源筛选与当前授权手牌不一致");
            Dock.Reset();
            stage = mode + "-存档覆盖与恢复";
            string path = request == "load" ? Path.Combine(output, mode + "-reload-authority-private.json") : originalPath;
            string before = app.ExportEnvelope();
            app.WriteSave(path); app.WriteSave(path); // 同一路径第二次写入验证覆盖流程。
            Require(File.ReadAllText(path) == before, "存档内容不一致");
            app.ReadSave(path); yield return null; yield return null;
            Require(app.ExportEnvelope() == before && ViewSeat == app.HumanSeat, "恢复改变权威局面或人类席位");
            stage = mode + "-PC菜单暂停";
            app.OpenTableMenu();
            Require(app.TableMenuOpen && app.BlocksTableInput(Vector2.zero) && app.BlocksTableInput(new Vector2(1439,899)), "PC 游戏菜单未完整阻挡背景");
            bool paused = app.Paused; app.SetPaused(false);
            Require(!app.StepAi() && Authority == beforePanels, "PC 游戏菜单中 AI 仍然执行命令"); app.SetPaused(paused);
            foreach (var frame in CaptureReady(mode + "-pc-table-menu.png")) yield return frame;
            app.HandleTableEscape(); Require(!app.TableMenuOpen, "返回操作未关闭 PC 游戏菜单");
            app.ShowMenu(); Require(!app.StepAi(), "菜单推进了 AI");
            app.ResumeGame(); Require(app.ExportEnvelope() == before, "菜单改变存档配置");
            while(app.TablePresentationBlocked)yield return null;
            Advance(); Require(app.Fault == "", "恢复后无法继续同权限行动"); modes++;
        }
    }

    void CheckSharedPcHud()
    {
        var viewport = MobileTabletop.Viewport;
        var expected = new Rect(viewport.Left, Screen.height - viewport.Top - viewport.Height, viewport.Width, viewport.Height);
        foreach (var camera in new[] { Table.TableCamera, Table.HudCamera })
        {
            var actual = camera.pixelRect;
            Require(Mathf.Abs(actual.x - expected.x) < 1 && Mathf.Abs(actual.y - expected.y) < 1 &&
                Mathf.Abs(actual.width - expected.width) < 1 && Mathf.Abs(actual.height - expected.height) < 1,
                "三维桌面与 PC HUD 相机未使用同一安全区");
        }
        foreach (var logical in new[] { new Vector2(24,16), new Vector2(720,450), new Vector2(1330,825) })
        {
            var screen = Table.HudCamera.WorldToScreenPoint(Table.World(logical));
            var projected = MobileTabletop.Logical(new Vector2(screen.x, Screen.height - screen.y));
            Require((projected - logical).sqrMagnitude < .01f, "实际 HUD 相机投影与触控位置不一致");
        }
        var hudRects = Hud.VerificationHudRects;
        foreach (var logical in new[] { new Vector2(59,51), new Vector2(141,51), new Vector2(223,51),
            new Vector2(305,51), new Vector2(387,51), new Vector2(720,50), new Vector2(1349,825) })
            Require(hudRects.Any(rect => rect.Contains(logical)), "未绘制完整 PC HUD 常驻入口");
        // 组合模式含八类库存，最上方定居点从 y=430 开始；全部应在骰盘下方的左列。
        Require(Dock.VerificationBuildRects.Length > 0 && Dock.VerificationBuildRects.All(rect => rect.x >= 0 && rect.xMax < 155 && rect.y > 400 && rect.yMax <= 900),
            "未在 PC 左侧常驻建造区绘制库存棋子");
        sharedPcUiVerified = true;
    }

    void Advance()
    {
        Require(!app.Finished, "验收局过早结束");
        if (!app.IsHumanTurn)
        {
            app.SetPaused(false); bool advanced = app.StepAi(); app.SetPaused(true);
            Require(advanced, "AI 未通过普通调度入口"); aiCommands++; return;
        }
        string before = Authority;
        app.SetPaused(false); Require(!app.StepAi() && before == Authority, "AI 消耗了人类待决"); app.SetPaused(true);
        bool accepted;
        switch (mode)
        {
            case "base":
                var b = GetComponent<M2App>(); var bc = new Catan.AI.BaseGameAi().Decide(b.M6View); bc.Id = Guid.NewGuid().ToString("N"); accepted = b.Submit(bc)?.Success == true; break;
            case "seafarers":
                var s = GetComponent<M3App>(); var sc = new Catan.AI.SeafarersAi().Decide(s.M6View); sc.Id = Guid.NewGuid().ToString("N"); accepted = s.Submit(sc)?.Success == true; break;
            case "cities-knights":
                var c = GetComponent<M4App>(); var cc = new Catan.AI.CitiesKnightsAi().Decide(c.M6View); cc.Id = Guid.NewGuid().ToString("N"); accepted = c.Submit(cc)?.Success == true; break;
            default:
                var x = GetComponent<M5App>(); var xc = new Catan.AI.CombinedAi().Decide(x.M6View); xc.Id = Guid.NewGuid().ToString("N"); accepted = x.Submit(xc)?.Success == true; break;
        }
        Require(accepted, "同权限人类验收命令被拒绝"); humanCommands++;
    }

    IEnumerable CaptureReady(string filename)
    {
        if (request == "load") filename = "reload-" + filename;
        // 后台窗口的首帧可能尚未提交图形；等待实际帧，保留每次失败图，不能把黑图算作通过。
        for (int attempt = 1; attempt <= 8; attempt++)
        {
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            if (Capture(filename, attempt)) yield break;
        }
        Require(false, "图形预热后截图仍为空；已保留诊断图，请检查窗口可见性与真实屏幕输出");
    }

    bool Capture(string filename, int attempt)
    {
        captureAttempts++;
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        try
        {
            Require(texture != null && texture.width == Screen.width && texture.height == Screen.height, "截图尺寸不匹配");
            var pixels = texture.GetPixels32(); int chromatic = 0; byte darkest = 255, brightest = 0;
            for (int i = 0; i < pixels.Length; i += 97)
            {
                var p = pixels[i]; byte value = Math.Max(p.r, Math.Max(p.g, p.b));
                darkest = Math.Min(darkest, value); brightest = Math.Max(brightest, value);
                if (Math.Abs(p.r - p.g) + Math.Abs(p.g - p.b) > 20) chromatic++;
            }
            sampledDarkest = darkest; sampledBrightest = brightest; sampledChromatic = chromatic;
            byte[] bytes = texture.EncodeToPNG();
            bool complete = brightest - darkest > 40 && chromatic > 50 && bytes.Length > 8192;
            string path = complete ? filename : Path.GetFileNameWithoutExtension(filename) + "-attempt-" + attempt + "-diagnostic.png";
            File.WriteAllBytes(Path.Combine(output, path), bytes);
            if (complete) { images.Add(filename); return true; }
            return false;
        }
        finally { if (texture != null) Destroy(texture); }
    }

    void Require(bool value, string message)
    {
        checks++;
        if (!value) { failedAssertion = message; throw new InvalidOperationException(message); }
    }
    void SaveReport(string status, string note)
    {
        var safe = Screen.safeArea; var viewport = MobileTabletop.Viewport;
        File.WriteAllText(Path.Combine(output, request == "load" ? "result-load.json" : "result.json"), JsonUtility.ToJson(new Report {
            status = status, stage = stage, unity = Application.unityVersion, platform = Application.platform.ToString(), graphics = SystemInfo.graphicsDeviceType.ToString(), note = note,
            checks = checks, modes = modes, screenshots = images.Count, humanCommands = humanCommands, aiCommands = aiCommands,
            sampledDarkest = sampledDarkest, sampledBrightest = sampledBrightest, sampledChromatic = sampledChromatic, captureAttempts = captureAttempts,
            screenWidth = Screen.width, screenHeight = Screen.height, safeLeft = safe.x, safeBottom = safe.y, safeWidth = safe.width, safeHeight = safe.height,
            viewportLeft = viewport.Left, viewportTop = viewport.Top, viewportWidth = viewport.Width, viewportHeight = viewport.Height,
            sharedPcUi = sharedPcUiVerified, touchInputEnabled = MobileTabletop.Enabled, landscape = Screen.width > Screen.height, bundledChineseFont = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular") != null, independentProcessLoad = request == "load", images = images.ToArray()
        }, true));
    }
}
