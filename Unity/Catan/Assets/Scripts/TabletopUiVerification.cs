using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Catan.Core;
using UnityEngine;
using UnityEngine.Rendering;

// 仅显式开发参数启用；不注入鼠标键盘，不读取桌面，不输出权威存档或私有牌面。
public sealed class TabletopUiVerification : MonoBehaviour
{
    const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    M6App app;
    string mode, output;
    int checks, configurations, cameraImages, guiAttempts, guiImagesWithChanges;
    bool captureCamera;
    RenderTexture guiTarget, guiPreviousTarget, guiPreviousActive;
    Camera guiCamera;
    string guiFilename;
    bool guiBegan;
    Color32[] cameraPixels;

    [Serializable] sealed class Result
    {
        public string status, unity, note;
        public int configurations, checks, cameraImages, guiAttempts, guiImagesWithChanges;
        public bool graphics, fullUiPixelsVerified;
    }

    OperationSnapshot Snapshot => mode == "base" ? GetComponent<M2App>().VerificationOperationSnapshot :
        mode == "seafarers" ? GetComponent<M3App>().VerificationOperationSnapshot :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationOperationSnapshot : GetComponent<M5App>().VerificationOperationSnapshot;
    string Authority => mode == "base" ? GetComponent<M2App>().VerificationHost.ExportSave() :
        mode == "seafarers" ? GetComponent<M3App>().VerificationHost.ExportSave() :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationHost.ExportSave() : GetComponent<M5App>().VerificationHost.ExportSave();
    OperationDock Dock => mode == "base" ? GetComponent<M2App>().VerificationOperationDock :
        mode == "seafarers" ? GetComponent<M3App>().VerificationOperationDock :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationOperationDock : GetComponent<M5App>().VerificationOperationDock;
    Camera BoardCamera => mode == "base" ? GetComponent<M2App>().VerificationBoard.BoardCamera :
        mode == "seafarers" ? GetComponent<M3App>().VerificationBoard.BoardCamera :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationBoard.BoardCamera : GetComponent<M5App>().VerificationBoard.BoardCamera;
    MonoBehaviour Presentation => mode == "base" ? (MonoBehaviour)GetComponent<M2App>() :
        mode == "seafarers" ? GetComponent<M3App>() : mode == "cities-knights" ? (MonoBehaviour)GetComponent<M4App>() : GetComponent<M5App>();
    Vertex[] Vertices => mode == "base" ? GetComponent<M2App>().M6View.Board.Vertices :
        mode == "seafarers" ? GetComponent<M3App>().M6View.Board.Vertices :
        mode == "cities-knights" ? GetComponent<M4App>().M6View.Board.Vertices : GetComponent<M5App>().M6View.Board.Vertices;
    Edge[] Edges => mode == "base" ? GetComponent<M2App>().M6View.Board.Edges :
        mode == "seafarers" ? GetComponent<M3App>().M6View.Board.Edges :
        mode == "cities-knights" ? GetComponent<M4App>().M6View.Board.Edges : GetComponent<M5App>().M6View.Board.Edges;
    Tile[] Tiles => mode == "base" ? GetComponent<M2App>().M6View.Board.Tiles :
        mode == "seafarers" ? GetComponent<M3App>().M6View.Board.Tiles :
        mode == "cities-knights" ? GetComponent<M4App>().M6View.Board.Tiles : GetComponent<M5App>().M6View.Board.Tiles;

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs(); int flag = Array.IndexOf(args, "--tabletop-ui-verify");
        if (flag < 0) yield break;
        if (flag + 1 >= args.Length || !Application.isBatchMode)
        { Debug.LogError("CATAN_TABLETOP_UI_FAILED: 只能在批处理模式指定验证输出目录。"); Application.Quit(1); yield break; }
        output = Path.GetFullPath(args[flag + 1]);
        captureCamera = args.Contains("--tabletop-camera-render");
        if (captureCamera && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        { Debug.LogError("CATAN_TABLETOP_UI_FAILED: 离屏相机需要图形设备。"); Application.Quit(1); yield break; }
        app = GetComponent<M6App>(); Directory.CreateDirectory(output);
        if(captureCamera)
        {
            gameObject.AddComponent<TabletopCaptureBegin>().Owner=this;
            gameObject.AddComponent<TabletopCaptureEnd>().Owner=this;
        }
        var run = Verify();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception exception)
            {
                // 断言只包含固定说明，异常不拼接视图、命令或存档内容。
                string reason = exception is InvalidOperationException ? exception.Message : exception.GetType().Name;
                Debug.LogError("CATAN_TABLETOP_UI_FAILED: " + mode + " / " + reason);
                Application.Quit(1); yield break;
            }
            yield return next;
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result {
            status = "passed", configurations = configurations, checks = checks, cameraImages = cameraImages,
            guiAttempts = guiAttempts, guiImagesWithChanges = guiImagesWithChanges,
            unity = Application.unityVersion, graphics = captureCamera, fullUiPixelsVerified = false,
            note = "通过固定规则入口、真实操作适配器与公开投影验证。camera-only 仅含三维场景；imgui-attempt 是独立 RT 内的界面合成尝试，需人工检查。未执行桌面操控或完整界面像素验收。"
        }, true));
        Debug.Log("CATAN_TABLETOP_UI_OK: configurations=" + configurations + " checks=" + checks + " cameraImages=" + cameraImages);
        Application.Quit(0);
    }

    void Require(bool passed, string message) { checks++; if (!passed) throw new InvalidOperationException(message); }

    IEnumerator Verify()
    {
        foreach (string rules in new[] { "base", "seafarers", "cities-knights", "combined" })
        foreach (int players in new[] { 3, 4 })
        for (int human = 1; human <= players; human++)
        {
            mode = rules; app.StartGame(mode, "heading-for-new-shores", players, "P" + human, true, FixedAuthority(players, human));
            yield return null; yield return null;
            Require(app.Ready && app.HumanSeat == "P" + human, "人类席位初始化失败");
            Require(Snapshot.Seat == app.HumanSeat && Snapshot.OtherPlayers.Length == players - 1 && !Snapshot.OtherPlayers.Contains(app.HumanSeat), "个人视图未固定到人类席位");
            CheckSeatOrder(players);
            CheckProjection(false); CheckProjection(true); SetTop(false);
            BindDockForHeadless();
            string before = Authority;
            Dock.OpenTrade(); Require(!Dock.TradeOpen, "开局阶段错误开放交换草稿");
            Require(before == Authority, "尝试打开交换改变权威状态");
            Require(!Dock.BlocksBoardInput(new Vector2(720, 390)), "地图中央被空白操作区域截获");
            Require(!Dock.BlocksBoardInput(new Vector2(1190, 760)), "个人区域留白被整个操作栏截获");
            int steps = 0;
            while (!app.IsHumanTurn && steps++ < 80) { Advance(); if (steps % 4 == 0) yield return null; }
            Require(app.IsHumanTurn, "未能推进至人类开局");
            CheckPresentation();
            VerifyPlacement();
            steps = 0;
            while (!Snapshot.CanTrade && steps++ < 500) { Advance(); if (steps % 8 == 0) yield return null; }
            Require(Snapshot.CanTrade, "未能正常推进至可交易阶段");
            BindDockForHeadless(); before = Authority;
            Dock.OpenTrade(); Require(Dock.TradeOpen, "行动阶段未能打开银行交换草稿");
            Require(Dock.BlocksBoardInput(new Vector2(600, 350)), "交换盘未截获自身区域");
            Require(Dock.BlocksBoardInput(new Vector2(1220, 500)), "交易模态窗口未阻挡外侧桌面");
            Require(Dock.BlocksBoardInput(new Vector2(18, 18))&&Dock.BlocksBoardInput(new Vector2(1418, 882)), "交易模态窗口未完整阻挡背景输入");
            Require(before == Authority, "银行交换草稿改变权威状态");
            Dock.Reset(); Snapshot.Cancel?.Invoke(); Require(!Dock.TradeOpen && Dock.Selection.Selected.Sum() == 0, "放回交换草稿未清空");
            Require(before == Authority, "取消银行交换改变权威状态");
            BindDockForHeadless(); Dock.OpenTrade(Snapshot.OtherPlayers[0]);
            Require(Dock.TradeOpen && before == Authority, "玩家交易草稿未保持权威状态");
            Dock.Reset(); Snapshot.Cancel?.Invoke();
            Require(before == Authority, "取消玩家交易改变权威状态");
            VerifyBroadcast();
            configurations++;
            if(human==players)app.WriteSave(Path.Combine(output,mode+"-"+players+"-review.json"));
            if (captureCamera && human == players)
            {
                // 只渲染游戏相机到独立 RT，不读取屏幕帧缓冲和用户桌面。
                yield return null; CaptureCamera(mode + "-" + players + "人-camera-only.png");
                BeginGuiAttempt(mode + "-" + players + "人-imgui-attempt.png");
                for(int frame=0;guiTarget!=null&&frame<8;frame++)yield return null;
                if(guiTarget!=null)
                {
                    Debug.LogWarning("CATAN_TABLETOP_IMGUI_UNAVAILABLE: 批处理未产生可合成的 Repaint；仅保留相机图。");
                    EndGuiAttempt();
                }
            }
            Debug.Log("CATAN_TABLETOP_CONFIGURATION_OK: " + mode + " / " + players + " / P" + human);
        }
    }

    void VerifyBroadcast()
    {
        var snapshot=Snapshot;int good=Array.FindIndex(snapshot.Goods,n=>n>0);
        if(good<0)return;
        var give=new int[8];give[good]=1;var receive=new int[8];receive[(good+1)%snapshot.GoodCount]=1;
        var owned=(int[])snapshot.Goods.Clone();
        string error=snapshot.Trade(new OperationTrade{OtherPlayer=OperationTrade.AllPlayers,Give=give,Receive=receive});
        Require(string.IsNullOrEmpty(error),"正式操作适配器拒绝合法全体报价");
        var offer=Snapshot.Offer;
        Require(offer!=null&&offer.IsBroadcast&&offer.PendingRecipients.Length==app.PlayerCount-1,"全体报价没有覆盖其他席位");
        Require(offer.CanCancel&&!offer.CanAccept&&!offer.CanReject,"报价者回复权限不正确");
        Require(Snapshot.CanInteract,"报价者等待 AI 回复时不能撤回");
        Require(owned.SequenceEqual(Snapshot.Goods),"报价阶段已提前扣费");
        Snapshot.Respond("cancel");
        Require(Snapshot.Offer==null&&owned.SequenceEqual(Snapshot.Goods),"全体报价撤回未保持原手牌");
    }

    void CheckSeatOrder(int players)
    {
        // 使用生产布局函数；它只接受公开席位编号。
        var method = typeof(TabletopUI).GetMethod("OpponentOrder", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Require(method != null, "缺少可复用的公开相对座位布局函数");
        var ids = Enumerable.Range(1, players).Select(i => "P" + i).ToArray();
        var ordered = (string[])method.Invoke(null, new object[] { app.HumanSeat, ids });
        int own = Array.IndexOf(ids, app.HumanSeat);
        Require(ordered.Length == players - 1 && !ordered.Contains(app.HumanSeat), "自己的席位没有留在前方");
        for (int i = 0; i < ordered.Length; i++) Require(ordered[i] == ids[(own + i + 1) % players], "对手未按相对顺时针排列");
    }

    void CheckPresentation()
    {
        string before=Authority;var snapshot=Snapshot;
        foreach(var build in snapshot.Builds.Where(b=>b.Track<0&&b.Count>0))
        {
            var texture=OperationPieceAssets.Texture(build.Icon,snapshot.Seat,Math.Max(1,build.Level));
            if(texture==null)continue;
            Require(texture.isReadable,"棋子缩略图未保留透明命中数据");
            var rect=new Rect(0,0,texture.width,texture.height);
            Require(!OperationPieceAssets.Contains(texture,rect,new Vector2(.1f,.1f)),"棋子透明边角错误接收点击");
            var pixels=texture.GetPixels32();int opaque=Array.FindIndex(pixels,p=>p.a>220);
            Require(opaque>=0,"棋子缩略图没有可点击轮廓");
            var point=new Vector2(opaque%texture.width+.5f,texture.height-(opaque/texture.width+.5f));
            Require(OperationPieceAssets.Contains(texture,rect,point),"棋子实体轮廓无法命中");
            Dock.Selection.Choose(build,snapshot.Goods,build.Count-1);
            Require(Enumerable.Range(0,build.Count).Count(i=>Dock.Selection.IsPieceSelected(build.Id,i))==1,"拿取错误选中一组棋子");
            Dock.Selection.Clear();
        }
        var prompt=new TabletopPromptSequence();prompt.Show(0,"你的回合","请掷骰");
        Require(prompt.At(0)=="你的回合"&&prompt.At(1.5)=="请掷骰"&&prompt.At(3)=="","Unity 提示时长或顺序错误");
        var dice=new TabletopDicePresentation();int? eventDie=snapshot.GoodCount==8?(int?)2:null;
        dice.Observe(0,0,0,eventDie,0);dice.Observe(1,2,5,eventDie,1);
        Require(dice.IsRolling(1.2)&&!dice.IsRolling(2),"Unity 骰子时间轴无法结束");
        Require(dice.Face(0,2)==2&&dice.Face(1,2)==5&&dice.Event.HasValue==(snapshot.GoodCount==8),"骰子数量或最终骰面错误");
        CheckWorldObjects();
        Require(before==Authority,"拿取、提示或动画改变了权威状态");
    }

    void CheckWorldObjects()
    {
        var table=mode=="base"?GetComponent<M2App>().VerificationBoard.TableScene:
            mode=="seafarers"?GetComponent<M3App>().VerificationBoard.TableScene:
            mode=="cities-knights"?GetComponent<M4App>().VerificationBoard.TableScene:GetComponent<M5App>().VerificationBoard.TableScene;
        table.Objects.Begin("verification");
        var card=table.Objects.Card("card",new Rect(680,768,74,103),EntityCardAssets.ResourceFront(0));
        var location=card.Root.position;var initial=card.Rect;
        var v1=Position(Vertices[0].Id);var v2=Position(Vertices[1].Id);
        float boardDistance=Vector2.Distance(table.Project(v1),table.Project(v2));
        Require(card.Contains(initial.center),"正常缩放下桌面公共卡不能拾取");
        table.Frame(BoardCamera,false,.8f);var zoomed=card.Rect;
        Require((card.Root.position-location).sqrMagnitude<.00000001f,"相机缩放移动了桌面卡片");
        Require(Mathf.Abs(zoomed.width/initial.width-1.25f)<.001f,"卡片没有随地图按相同比例缩放");
        Require(Mathf.Abs(Vector2.Distance(table.Project(v1),table.Project(v2))/boardDistance-zoomed.width/initial.width)<.001f,"地图与卡牌缩放比例不一致");
        Require(card.Contains(zoomed.center),"缩放后桌面公共卡的命中区域没有更新");
        table.Frame(BoardCamera,true,1);Require(card.Contains(card.Rect.center),"俯视实体卡牌不能拾取");
        table.Frame(BoardCamera,false,1);
        if(Snapshot.GoodCount==8)
        {
            for(int track=0;track<3;track++)
            {
                var board=table.Objects.Facility("facility-"+track,new Rect(1044+track*122,639,114,152),track,0);
                for(int level=1;level<=5;level++)
                {
                    var slot=table.Objects.FacilitySlot("slot-"+track+"-"+level,board,track,level,false);
                    Require(slot.Contains(slot.Rect.center),"完整设施卡内部小翻板无法独立拾取");
                    Require(slot.Rect.width<board.Rect.width&&slot.Rect.height<board.Rect.height,"小翻板尺寸超出完整设施卡");
                }
            }
        }
        table.Objects.End();table.Objects.Begin("verification");table.Objects.End();
        var oak=Resources.Load<Texture2D>("TabletopUI/v005/smoked-oak-albedo");
        var normal=Resources.Load<Texture2D>("TabletopUI/v005/smoked-oak-normal");
        Require(oak!=null&&oak.width==2048&&oak.height==2048&&oak.anisoLevel==16&&normal!=null,"新版细纹桌面或微表面法线未正确导入");
        Require(UnityEngine.Object.FindObjectsByType<TabletopPhysicalTileV005>(FindObjectsSortMode.None)
            .Any(p=>p.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh.name=="六边实体厚板倒角 v005"&&m.sharedMesh.triangles.Length==144)),
            "真实倒角网格未在正式地图中生成");
        var handBefore=TabletopHandLayout.Card(15,32,720,1060,false);
        table.Frame(BoardCamera,false,.8f);
        var handAfter=TabletopHandLayout.Card(15,32,720,1060,false);
        Require(handBefore.X==handAfter.X&&handBefore.Y==handAfter.Y&&TabletopHandLayout.CardWidth==108,"底部手牌受桌面缩放影响");
        table.Frame(BoardCamera,false,1);
    }

    string FixedAuthority(int players, int human)
    {
        uint seed = (uint)(20261011 + players * 10 + human);
        if (mode == "base") return Catan.Core.M2.BaseGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text, players, seed).Save();
        if (mode == "seafarers") return Catan.Core.M3.SeafarersGameSession.Create("heading-for-new-shores", players, seed).Save();
        if (mode == "cities-knights") return Catan.Core.M4.CitiesKnightsGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text, players, seed).Save();
        return Catan.Core.M5.CombinedGameSession.Create("heading-for-new-shores", players, seed).Save();
    }

    void BindDockForHeadless()
    {
        // -nographics 不调用 OnGUI；仅补上 Draw 的快照绑定，不绕过任何命令校验。
        typeof(OperationDock).GetField("current", PrivateInstance).SetValue(Dock, Snapshot);
        Dock.Selection.Sync(Snapshot);
    }

    Vector3 Position(string id)
    {
        if (mode == "base") return GetComponent<M2App>().VerificationBoard.Position(id);
        if (mode == "seafarers") return GetComponent<M3App>().VerificationBoard.Position(id);
        if (mode == "cities-knights") return GetComponent<M4App>().VerificationBoard.Position(id);
        return GetComponent<M5App>().VerificationBoard.Position(id);
    }

    void SetTop(bool top)
    {
        if (mode == "base") GetComponent<M2App>().VerificationBoard.SetCamera(top);
        else if (mode == "seafarers") GetComponent<M3App>().VerificationBoard.SetCamera(top);
        else if (mode == "cities-knights") GetComponent<M4App>().VerificationBoard.SetCamera(top);
        else GetComponent<M5App>().VerificationBoard.SetCamera(top);
    }

    void CheckProjection(bool top)
    {
        SetTop(top); var camera = BoardCamera;
        Require(camera.rect == new Rect(0, 0, 1, 1), "相机仍受侧栏视口裁切");
        var expected = TabletopScene.BoardArea; expected.xMin -= 4; expected.xMax += 4; expected.yMin -= 4; expected.yMax += 4;
        var ids = Vertices.Select(v => v.Id).Concat(Edges.Select(e => e.Id)).Concat(Tiles.Select(t => t.Id));
        foreach (string id in ids)
        {
            var point = camera.WorldToViewportPoint(Position(id) + Vector3.up * .005f);
            Require(point.z > 0 && expected.Contains(new Vector2(point.x * 1440, (1 - point.y) * 900)), "公开地图节点超出中央操作范围");
        }
        string rightmost = Vertices.OrderByDescending(v => Position(v.Id).x).First().Id;
        var edgePoint = camera.WorldToScreenPoint(Position(rightmost) + Vector3.up * .005f);
        var mouse = new Vector2(edgePoint.x, Screen.height - edgePoint.y);
        string picked = mode == "base" ? GetComponent<M2App>().VerificationPick(Catan.Core.M2.CommandKind.SetupSettlement, mouse) :
            mode == "seafarers" ? GetComponent<M3App>().VerificationPick(Catan.Core.M3.CommandKind.SetupSettlement, mouse) :
            mode == "cities-knights" ? GetComponent<M4App>().VerificationPick("V", mouse) : GetComponent<M5App>().VerificationPick("V", mouse);
        Require(picked == rightmost, "地图最右交点不能被真实拾取函数命中");
    }

    void VerifyPlacement()
    {
        var snapshot = Snapshot;
        var build = snapshot.Builds.FirstOrDefault(b => b.Available && b.Id == "SetupSettlement");
        Require(build != null, "人类开局没有可拿取的建筑");
        string before = Authority;
        snapshot.PrepareBuild(build);
        Require(before == Authority, "拿取开局建筑提前改变权威状态");
        snapshot.Cancel?.Invoke(); Dock.Reset();
        Require(before == Authority, "放回建筑改变权威状态");
        snapshot.PrepareBuild(build);
        string target = mode == "base" ? GetComponent<M2App>().M6View.LegalVertexIds[0] :
            mode == "seafarers" ? GetComponent<M3App>().M6View.LegalVertexIds[0] :
            mode == "cities-knights" ? GetComponent<M4App>().M6View.LegalVertexIds[0] : GetComponent<M5App>().M6View.LegalVertexIds[0];
        var screen = BoardCamera.WorldToScreenPoint(Position(target) + Vector3.up * .005f);
        var pointer = new Vector2(screen.x, Screen.height - screen.y);
        string picked = mode == "base" ? GetComponent<M2App>().VerificationPick(Catan.Core.M2.CommandKind.SetupSettlement, pointer) :
            mode == "seafarers" ? GetComponent<M3App>().VerificationPick(Catan.Core.M3.CommandKind.SetupSettlement, pointer) :
            mode == "cities-knights" ? GetComponent<M4App>().VerificationPick("V", pointer) : GetComponent<M5App>().VerificationPick("V", pointer);
        Require(picked == target, "真实目标吸附未命中投影交点");
        bool illegal = SubmitPlacement("V-invalid-tabletop");
        Require(!illegal && before == Authority, "非法桌面落点改变权威状态");
        Snapshot.PrepareBuild(Snapshot.Builds.First(b => b.Available && b.Id == "SetupSettlement"));
        Require(SubmitPlacement(picked), "真实适配器的合法开局落点被拒绝");
        Require(before != Authority, "确认放下建筑未进入权威状态");
    }

    bool SubmitPlacement(string target)
    {
        if (mode == "base") return GetComponent<M2App>().Drop(target)?.Success == true;
        if (mode == "seafarers") return GetComponent<M3App>().Drop(target)?.Success == true;
        // 使用 PrepareBuild 创建的真实草稿；不重新构造一条绕过适配器的命令。
        var draft = Presentation.GetType().GetField("draft", PrivateInstance).GetValue(Presentation);
        Require(draft != null, "拿取建筑没有生成显示层草稿");
        if (mode == "cities-knights")
        {
            var command = (Catan.Core.M4.Command)draft; command.TargetId = target;
            return GetComponent<M4App>().Submit(command)?.Success == true;
        }
        var combined = (Catan.Core.M5.Command)draft; combined.TargetId = target;
        return GetComponent<M5App>().Submit(combined)?.Success == true;
    }

    void Advance()
    {
        if (!app.IsHumanTurn)
        {
            app.SetPaused(false); bool advanced = app.StepAi(); app.SetPaused(true);
            Require(advanced, "AI 无法继续正常对局"); return;
        }
        bool success;
        switch (mode)
        {
            case "base": var b = GetComponent<M2App>(); var bc = new Catan.AI.BaseGameAi().Decide(b.M6View); bc.Id = Guid.NewGuid().ToString("N"); success = b.Submit(bc)?.Success == true; break;
            case "seafarers": var s = GetComponent<M3App>(); var sc = new Catan.AI.SeafarersAi().Decide(s.M6View); sc.Id = Guid.NewGuid().ToString("N"); success = s.Submit(sc)?.Success == true; break;
            case "cities-knights": var c = GetComponent<M4App>(); var cc = new Catan.AI.CitiesKnightsAi().Decide(c.M6View); cc.Id = Guid.NewGuid().ToString("N"); success = c.Submit(cc)?.Success == true; break;
            default: var x = GetComponent<M5App>(); var xc = new Catan.AI.CombinedAi().Decide(x.M6View); xc.Id = Guid.NewGuid().ToString("N"); success = x.Submit(xc)?.Success == true; break;
        }
        Require(success, "同权限测试策略命令被拒绝");
    }

    void CaptureCamera(string filename)
    {
        var camera = BoardCamera; var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        var target = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32);
        Texture2D image = null;
        try
        {
            target.Create(); camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); image.Apply();
            var pixels = image.GetPixels32();
            cameraPixels = pixels;
            int light = pixels.Count(p => p.r > 12 || p.g > 12 || p.b > 12);
            Require(light > pixels.Length / 2, "离屏相机输出为空或大面积黑屏");
            File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG()); cameraImages++;
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            target.Release(); Destroy(target); if (image != null) Destroy(image);
        }
    }

    void BeginGuiAttempt(string filename)
    {
        guiAttempts++;guiFilename=filename;guiCamera=BoardCamera;
        guiTarget=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);guiTarget.Create();
        guiPreviousTarget=guiCamera.targetTexture;guiCamera.targetTexture=guiTarget;
        guiCamera.Render();guiBegan=false;
    }

    internal void BeginGuiRepaint()
    {
        if(guiTarget==null||Event.current.type!=EventType.Repaint)return;
        guiPreviousActive=RenderTexture.active;Graphics.SetRenderTarget(guiTarget);guiBegan=true;
    }

    internal void EndGuiRepaint()
    {
        if(guiTarget==null||!guiBegan||Event.current.type!=EventType.Repaint)return;
        Texture2D image=null;
        try
        {
            RenderTexture.active=guiTarget;
            image=new Texture2D(1440,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
            var pixels=image.GetPixels32();int changed=0;
            if(cameraPixels!=null)
                for(int i=0;i<pixels.Length;i++)
                    if(Math.Abs(pixels[i].r-cameraPixels[i].r)+Math.Abs(pixels[i].g-cameraPixels[i].g)+Math.Abs(pixels[i].b-cameraPixels[i].b)>45)changed++;
            if(changed>5000)guiImagesWithChanges++;
            File.WriteAllBytes(Path.Combine(output,guiFilename),image.EncodeToPNG());
            Debug.Log("CATAN_TABLETOP_IMGUI_ATTEMPT: changedPixels="+changed+"；此计数不能替代界面人工验收。");
        }
        catch(Exception exception)
        {Debug.LogWarning("CATAN_TABLETOP_IMGUI_UNAVAILABLE: "+exception.GetType().Name+"；仅保留相机图。");}
        finally{if(image!=null)Destroy(image);EndGuiAttempt();}
    }

    void EndGuiAttempt()
    {
        if(guiCamera!=null)guiCamera.targetTexture=guiPreviousTarget;
        if(guiBegan)RenderTexture.active=guiPreviousActive;
        if(guiTarget!=null){guiTarget.Release();Destroy(guiTarget);}
        guiTarget=null;guiCamera=null;guiBegan=false;
    }
}

// 只在显式图形批处理验证时注册，用独立 RT 尝试包含 IMGUI，不抓取屏幕。
public sealed class TabletopCaptureBegin : MonoBehaviour
{
    internal TabletopUiVerification Owner;
    void OnGUI(){GUI.depth=30000;if(Owner!=null)Owner.BeginGuiRepaint();}
}
public sealed class TabletopCaptureEnd : MonoBehaviour
{
    internal TabletopUiVerification Owner;
    void OnGUI(){GUI.depth=-30000;if(Owner!=null)Owner.EndGuiRepaint();}
}
