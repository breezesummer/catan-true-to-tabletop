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
    RenderTexture guiTarget, guiPreviousActive;
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
    TabletopUI Hud => (TabletopUI)Presentation.GetType().GetField("tabletop",PrivateInstance).GetValue(Presentation);
    Camera BoardCamera => mode == "base" ? GetComponent<M2App>().VerificationBoard.BoardCamera :
        mode == "seafarers" ? GetComponent<M3App>().VerificationBoard.BoardCamera :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationBoard.BoardCamera : GetComponent<M5App>().VerificationBoard.BoardCamera;
    TabletopScene Table => mode == "base" ? GetComponent<M2App>().VerificationBoard.TableScene :
        mode == "seafarers" ? GetComponent<M3App>().VerificationBoard.TableScene :
        mode == "cities-knights" ? GetComponent<M4App>().VerificationBoard.TableScene : GetComponent<M5App>().VerificationBoard.TableScene;
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
        app.HandleTableEscape();
        Require(app.MenuVisible&&!app.TableMenuOpen,"启动界面被 Esc 错误切换为游戏菜单");
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
            CheckEscapeMenu();
            CheckRequiredWindowClosure();
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
            CheckContextEntrances();
            BindDockForHeadless(); before = Authority;
            Dock.OpenTrade(); Require(Dock.TradeOpen, "行动阶段未能打开银行交换草稿");
            Require(Dock.BlocksBoardInput(new Vector2(600, 350)), "交换盘未截获自身区域");
            Require(Dock.BlocksBoardInput(new Vector2(1220, 500)), "交易模态窗口未阻挡外侧桌面");
            Require(Dock.BlocksBoardInput(new Vector2(18, 18))&&Dock.BlocksBoardInput(new Vector2(1418, 882)), "交易模态窗口未完整阻挡背景输入");
            Require(before == Authority, "银行交换草稿改变权威状态");
            app.HandleTableEscape();Require(!app.TableMenuOpen&&!Dock.TradeOpen&&!Dock.HasOverlay&&!Hud.OperationWindows.Hidden,"Esc 未关闭交换窗口或仍保留了最小化草稿");
            app.HandleTableEscape();Require(app.TableMenuOpen,"关闭草稿后 Esc 无法打开菜单");
            app.HandleTableEscape();Require(!app.TableMenuOpen,"Esc 无法关闭游戏菜单");
            Require(Dock.Selection.Selected.Sum() == 0, "放回交换草稿未清空");
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

    void CheckEscapeMenu()
    {
        string before=Authority;
        Require(!app.BlocksTableInput(new Vector2(1349,38)),"已删除的桌边按钮仍截获桌面输入");
        app.HandleTableEscape();
        Require(app.TableMenuOpen&&app.BlocksTableInput(Vector2.zero)&&app.BlocksTableInput(new Vector2(1439,899)),"Esc 菜单未完整阻挡桌面");
        bool paused=app.Paused;app.SetPaused(false);
        Require(!app.StepAi()&&Authority==before,"游戏菜单中 AI 仍然执行命令");app.SetPaused(paused);
        CheckPauseSettingsReturn(before);
        app.HandleTableEscape();
        Require(!app.TableMenuOpen&&Authority==before,"关闭菜单改变局面或没有返回游戏");
        Require(app.Paused==paused,"关闭菜单没有恢复原有暂停设置");
        app.ShowMenu();
        // ShowMenu 先回到风景主页；从正式导航进入配置页，再逐层验证 Esc。
        var navigate=typeof(M6App).GetMethod("NavigateMainMenu",PrivateInstance);
        var setupStep=typeof(M6App).GetField("menuSetupStep",PrivateInstance);
        var menuPage=typeof(M6App).GetField("menuPage",PrivateInstance);
        Require(navigate!=null&&setupStep!=null&&menuPage!=null,"缺少当前分层菜单导航入口");
        navigate.Invoke(app,new object[]{0});setupStep.SetValue(app,2);
        app.HandleTableEscape();
        Require(app.MenuVisible&&!app.TableMenuOpen&&(int)setupStep.GetValue(app)==1&&Authority==before,"配置第三步 Esc 未返回地图选择或改变了局面");
        app.HandleTableEscape();
        Require(app.MenuVisible&&!app.TableMenuOpen&&(int)setupStep.GetValue(app)==0&&Authority==before,"地图选择 Esc 未返回玩法选择或改变了局面");
        app.HandleTableEscape();
        Require(app.MenuVisible&&!app.TableMenuOpen&&(int)menuPage.GetValue(app)==-1&&Authority==before,"配置首页 Esc 未返回风景主页或改变了局面");
        app.HandleTableEscape();
        Require(!app.MenuVisible&&!app.TableMenuOpen&&Authority==before,"风景主页 Esc 未恢复当前对局或改变了局面");
        var tabletop=(TabletopUI)Presentation.GetType().GetField("tabletop",PrivateInstance).GetValue(Presentation);
        var history=(TabletopHistoryUI)typeof(TabletopUI).GetField("history",PrivateInstance).GetValue(tabletop);
        typeof(TabletopHistoryUI).GetProperty("IsOpen").GetSetMethod(true).Invoke(history,new object[]{true});
        app.HandleTableEscape();Require(!history.IsOpen&&!app.TableMenuOpen,"Esc 未优先关闭历史浮窗");
        app.HandleTableEscape();Require(app.TableMenuOpen,"关闭历史后 Esc 无法打开游戏菜单");
        app.HandleTableEscape();Require(!app.TableMenuOpen&&Authority==before,"历史与菜单切换改变了权威局面");
    }

    void CheckPauseSettingsReturn(string before)
    {
        var page=typeof(M6App).GetField("tablePausePage",PrivateInstance);
        Require(page!=null,"游戏菜单缺少独立设置页");
        page.SetValue(app,1);
        Require(app.TablePauseSettingsOpen&&app.TablePresentationBlocked&&!app.StepAi()&&before==Authority,"游戏设置页没有继续暂停对局");
        app.HandleTableEscape();
        Require(app.TableMenuOpen&&!app.TablePauseSettingsOpen&&app.TablePresentationBlocked&&!app.StepAi()&&before==Authority,"设置页 Esc 没有返回暂停中的游戏菜单");
    }

    void CheckRequiredWindowClosure()
    {
        // 仅在显式验证中替换固定待决夹具；窗口动作前后仍逐字比较完整权威存档。
        object host=Presentation.GetType().GetProperty("VerificationHost",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(Presentation);
        object session=host.GetType().GetField("session",PrivateInstance).GetValue(host);
        var stateField=session.GetType().GetField("state",PrivateInstance);
        object original=session.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(session,null);
        object state=session.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(session,null);
        void Set(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
        void Phase(object target,string name,string value)
        {var property=target.GetType().GetProperty(name);property.SetValue(target,Enum.Parse(property.PropertyType,value));}
        try
        {
            Set(state,"ActivePlayerId",app.HumanSeat);Set(state,"Turn",9);
            string phase=mode=="base"?"RoadBuilding":mode=="seafarers"?"GoldChoice":"PendingChoice";
            string kind=mode=="base"?"RoadBuilding":mode=="cities-knights"?"Aqueduct":"Gold";
            Phase(state,"Phase",phase);
            var decisionProperty=state.GetType().GetProperty("PendingDecision");
            object decision=Activator.CreateInstance(decisionProperty.PropertyType);
            Set(decision,"Kind",kind);Set(decision,"PlayerId",app.HumanSeat);Phase(decision,"ReturnPhase","Action");
            if(mode=="base")Set(decision,"RemainingRoads",2);
            if(mode=="combined")
            {
                Set(decision,"Amount",1);
                // 组合规则金矿待领取数量受实际建筑上界约束，夹具必须有对应村庄。
                var combined=(Catan.Core.M5.GameState)state;
                string vertex=combined.Tiles.First(t=>t.Resource=="gold").Vertices[0];
                combined.Settlements=new[]{new PiecePlacement{PlayerId=app.HumanSeat,LocationId=vertex}};
                combined.Players.First(p=>p.Id==app.HumanSeat).Pieces.Settlements--;
            }
            decisionProperty.SetValue(state,decision);
            if(mode=="seafarers")
            {
                var claimsProperty=state.GetType().GetProperty("GoldClaims");
                var claims=Array.CreateInstance(claimsProperty.PropertyType.GetElementType(),1);
                object claim=Activator.CreateInstance(claimsProperty.PropertyType.GetElementType());
                Set(claim,"PlayerId",app.HumanSeat);Set(claim,"Amount",1);claims.SetValue(claim,0);claimsProperty.SetValue(state,claims);
                Phase(state,"GoldReturnPhase","Action");
            }
            if(mode=="cities-knights"||mode=="combined")Phase(state,"DecisionReturnPhase","Action");
            stateField.SetValue(session,state);session.GetType().GetMethod("AssertInvariants",Type.EmptyTypes).Invoke(session,null);
            Presentation.GetType().GetMethod("M6Refresh",PrivateInstance).Invoke(Presentation,null);
            BindDockForHeadless();
            if(mode=="base")Hud.RegisterContext(); // 无图形模式不执行道路建设窗口的 OnGUI。
            Presentation.GetType().GetMethod("RefreshTablePrompt",PrivateInstance).Invoke(Presentation,null);
            Require(Snapshot.RequiresSelection&&app.HasRequiredTableSelection,"固定待决没有进入必须完成的选择阶段");
            string before=Authority;
            app.HandleTableEscape();
            Require(!app.TableMenuOpen&&Hud.OperationClosed&&!Hud.OperationWindows.Hidden&&!Dock.HasOverlay,"Esc 待决窗口未关闭或变成了最小化");
            BindDockForHeadless();
            Presentation.GetType().GetMethod("RefreshTablePrompt",PrivateInstance).Invoke(Presentation,null);
            Require(Hud.OperationClosed&&app.ClosedTableSelection&&app.HasRequiredTableSelection&&before==Authority,"重新绑定后待决窗口自动出现或权威待决被取消");
            app.HandleTableEscape();Require(app.TableMenuOpen&&Hud.OperationClosed&&before==Authority,"关闭待决窗口后 Esc 未进入游戏菜单");
            CheckPauseSettingsReturn(before);
            app.HandleTableEscape();Require(!app.TableMenuOpen&&Hud.OperationClosed&&before==Authority,"离开暂停菜单重新打开了已关闭待决窗口");
            Hud.OperationWindows.Open();
            Require(!Hud.OperationHidden&&!app.ClosedTableSelection&&Snapshot.RequiresSelection&&before==Authority,"关闭的待决窗口无法重新打开继续选择");
        }
        finally
        {
            stateField.SetValue(session,original);
            Presentation.GetType().GetMethod("M6Refresh",PrivateInstance).Invoke(Presentation,null);
            typeof(TabletopUI).GetField("contextVisible",PrivateInstance).SetValue(Hud,false);
            Hud.OperationWindows.Clear();BindDockForHeadless();
            Presentation.GetType().GetMethod("RefreshTablePrompt",PrivateInstance).Invoke(Presentation,null);
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
        // 缺料席位会自动拒绝；全员拒绝时报价立即结束。
        if(offer==null){Require(owned.SequenceEqual(Snapshot.Goods),"全部自动拒绝后手牌改变");return;}
        Require(offer.IsBroadcast&&offer.PendingRecipients.Concat(offer.RejectedRecipients).Distinct().Count()==app.PlayerCount-1,"全体报价没有覆盖其他席位");
        Require(offer.CanCancel&&!offer.CanAccept&&!offer.CanReject,"报价者回复权限不正确");
        Require(Snapshot.CanInteract,"报价者等待 AI 回复时不能撤回");
        Require(owned.SequenceEqual(Snapshot.Goods),"报价阶段已提前扣费");
        BindDockForHeadless();
        app.HandleTableEscape();BindDockForHeadless();
        Require(!app.TableMenuOpen&&!Dock.HasOverlay&&!Hud.OperationWindows.Suppressed&&Snapshot.Offer==null,"Esc 未按关闭按钮行为撤销自己的报价，或仍保留窗口入口");
        Require(owned.SequenceEqual(Snapshot.Goods),"Esc 撤销全体报价改变了手牌");
        app.HandleTableEscape();Require(app.TableMenuOpen&&Snapshot.Offer==null,"关闭报价窗口后 Esc 未打开游戏菜单");
        app.HandleTableEscape();Require(!app.TableMenuOpen&&Snapshot.Offer==null&&owned.SequenceEqual(Snapshot.Goods),"报价撤回后切换菜单改变了手牌或重新出现报价");
    }

    void CheckSeatOrder(int players)
    {
        // 使用生产布局函数；它只接受公开席位编号。
        var method = typeof(TabletopUI).GetMethod("OpponentOrder", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Require(method != null, "缺少可复用的公开相对座位布局函数");
        var ids = Enumerable.Range(1, players).Select(i => "P" + i).ToArray();
        string first=mode=="base"?GetComponent<M2App>().M6View.StartingPlayerId:
            mode=="seafarers"?GetComponent<M3App>().M6View.StartingPlayerId:
            mode=="cities-knights"?GetComponent<M4App>().M6View.StartingPlayerId:GetComponent<M5App>().M6View.StartingPlayerId;
        var actionOrder=PlayerIdentityModel.ActionOrder(first,ids);
        Require(app.HistoryPlayers.SequenceEqual(actionOrder),"公开头像与历史玩家列表没有按真实行动顺序排列");
        Require(app.OpeningOrderMessage=="你是第"+(Array.IndexOf(actionOrder,app.HumanSeat)+1)+"个行动的玩家","开局未提示自己的行动次序");
        Require(app.PlayerDisplayName(app.HumanSeat)==app.LocalPlayerName,"人类玩家没有使用设置里的名字");
        var aiNames=ids.Where(id=>id!=app.HumanSeat).Select(app.PlayerDisplayName).ToArray();
        Require(aiNames.Distinct().Count()==players-1&&aiNames.All(PlayerIdentityModel.AiNames.Contains),"AI 没有使用互不重复的指定中文名字");

        var ordered = (string[])method.Invoke(null, new object[] { app.HumanSeat, ids });
        int own = Array.IndexOf(ids, app.HumanSeat);
        Require(ordered.Length == players - 1 && !ordered.Contains(app.HumanSeat), "自己的席位没有留在前方");
        for (int i = 0; i < ordered.Length; i++) Require(ordered[i] == ids[(own + i + 1) % players], "对手未按相对顺时针排列");
    }
    void CheckContextEntrances()
    {
        BindDockForHeadless();string before=Authority;
        Require(Snapshot.Rates.Take(Math.Min(5,Snapshot.GoodCount)).All(r=>r==2||r==4),"银行入口错误混入通用港3:1");
        var open=Presentation.GetType().GetMethod("VerificationOpenPort",BindingFlags.NonPublic|BindingFlags.Instance);
        Require(open!=null&&!(bool)open.Invoke(Presentation,new object[]{"unknown-port"}),"不存在的港口错误开放兑换");
        Require(before==Authority&&!Dock.TradeOpen,"非法港口入口改变权威状态或打开窗口");
        foreach(string target in new[]{(string)null,Snapshot.OtherPlayers[0]})
        {
            Dock.OpenTrade(target);
            Require(Dock.TradeOpen&&Dock.VerificationTradeGiveGoods.SequenceEqual(OperationPanelOptions.PaymentGoods(Snapshot)),"银行或玩家交易没有按本人的已有手牌筛选付出资源");
            Require(Dock.VerificationTradeGiveGoods.All(g=>Snapshot.Goods[g]>0),"交易付出列表包含未持有资源");
            Dock.Reset();
        }
        if(Snapshot.GoodCount==8)
        {
            for(int track=0;track<3;track++)
            {
                Dock.OpenFacility(track);
                Require(Dock.VerificationFacilityTrack==track&&Dock.HasOverlay&&!Dock.TradeOpen,"设施入口没有只选择对应设施");
                Require(before==Authority,"打开设施改变了权威局面");
                Dock.Reset();
            }
        }
        if(mode=="base"||mode=="seafarers")
        {
            var development=Snapshot.Builds.FirstOrDefault(b=>b.Icon=="development");
            Require(development!=null,"资源模式丢失购买发展卡入口数据");
        }
        else
        {
            string vertex=mode=="cities-knights"?GetComponent<M4App>().M6View.Board.Knights.FirstOrDefault(k=>k.PlayerId==app.HumanSeat)?.LocationId:GetComponent<M5App>().M6View.Board.Knights.FirstOrDefault(k=>k.PlayerId==app.HumanSeat)?.LocationId;
            if(vertex!=null)
            {
                bool selected=mode=="cities-knights"?GetComponent<M4App>().VerificationSelectKnight(vertex):GetComponent<M5App>().VerificationSelectKnight(vertex);
                Require(selected&&before==Authority,"骑士菜单选择失败或改变权威状态");
                Snapshot.Cancel?.Invoke();Dock.Reset();
            }
        }
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
        table.Frame(BoardCamera,false,1);
        Require(table.TableCamera==BoardCamera&&BoardCamera.rect==new Rect(0,0,1,1)&&table.HudCamera!=BoardCamera,"地图和固定HUD相机未分离");
        table.Objects.Begin("verification");
        var layout=new Rect(680,740,74,103);var stockPoint=new Vector2(680,668);
        var card=table.Objects.Card("card",layout,EntityCardAssets.ResourceFront(0));
        var stock=table.Objects.Piece("stock-check","city",Snapshot.Seat,1,stockPoint);
        Require(stock!=null,"未生成库存城市实体");
        var location=card.Root.position;var cardScale=card.Root.localScale;var cardSize=card.Size;var initial=card.Rect;
        var stockLocation=stock.Root.position;var stockScale=stock.Root.localScale;var initialStock=stock.Rect;
        Require(Vector2.Distance(stock.Rect.center,stockPoint)<.02f&&stock.Rect.width<=48.02f&&stock.Rect.height<=40.02f,"正面库存模型没有居中放入固定小框");
        TabletopWorldObjects.Hit facility=null;
        Vector3 facilityLocation=Vector3.zero,facilityScale=Vector3.zero;Vector2 facilitySize=Vector2.zero;Rect initialFacility=Rect.zero;
        if(Snapshot.GoodCount==8)
        {
            facility=table.Objects.Facility("facility-check",OperationDock.FacilityLayout(0),0,0);
            facilityLocation=facility.Root.position;facilityScale=facility.Root.localScale;facilitySize=facility.Size;initialFacility=facility.Rect;
            Require(Mathf.Abs(facility.Root.position.y-TabletopScene.SurfaceHeight)<.002f,"城市设施没有置于桌面上");
        }
        var v1=Position(Vertices[0].Id);var v2=Position(Vertices[1].Id);
        float boardCameraSize=BoardCamera.orthographicSize;
        Require(card.Contains(initial.center),"正常缩放下桌面公共卡不能拾取");
        table.Frame(BoardCamera,false,.8f);
        // 模拟缩放后实际 Draw 再次提交同一布局，防止每帧重摆让物件锁回屏幕。
        card=table.Objects.Card("card",layout,EntityCardAssets.ResourceFront(0));
        stock=table.Objects.Piece("stock-check","city",Snapshot.Seat,1,stockPoint);var zoomed=card.Rect;
        Require((card.Root.position-location).sqrMagnitude<.00000001f&&card.Root.localScale==cardScale&&card.Size==cardSize,"相机缩放改变了桌面卡片的世界位置或尺寸");
        Require((stock.Root.position-stockLocation).sqrMagnitude<.00000001f&&stock.Root.localScale==stockScale,"相机缩放改变了库存模型的世界位置或尺寸");
        Require(Mathf.Abs(zoomed.width/initial.width-1)<.001f&&Mathf.Abs(stock.Rect.width/initialStock.width-1)<.001f,"地图缩放改变了固定HUD大小");
        Require(Mathf.Abs(boardCameraSize/BoardCamera.orthographicSize-1.25f)<.001f,"地图相机缩放比例不正确");
        Require(BoardCamera.transform.eulerAngles.x<TabletopScene.StrategyCameraAngle,"放大地图没有逐渐放低观察角度");
        if(facility!=null)
        {
            facility=table.Objects.Facility("facility-check",OperationDock.FacilityLayout(0),0,0);
            Require((facility.Root.position-facilityLocation).sqrMagnitude<.00000001f&&facility.Root.localScale==facilityScale&&facility.Size==facilitySize,"相机缩放改变了设施的世界位置或尺寸");
            Require(Mathf.Abs(facility.Rect.width/initialFacility.width-1)<.001f,"地图缩放改变了设施HUD大小");
        }
        Require(card.Contains(zoomed.center),"缩放后桌面公共卡的命中区域没有更新");
        table.Frame(BoardCamera,true,1);Require(card.Contains(card.Rect.center),"俯视实体卡牌不能拾取");
        Require(table.TableCamera==BoardCamera,"俯视时棋盘与周边物件不使用同一相机");
        stock=table.Objects.Piece("stock-check","city",Snapshot.Seat,1,stockPoint);
        Require((stock.Root.position-stockLocation).sqrMagnitude<.00000001f&&stock.Root.localScale==stockScale,"切换俯视重新摆放或缩放了库存模型");
        var bankLayout=TabletopBankUI.StackLayout(0);
        var bankCard=table.Objects.Card("bank-top",bankLayout,EntityCardAssets.ResourceFront(0));
        Require(bankCard.Contains(bankCard.Rect.center),"俯视银行纸牌不能按真实投影命中");
        if(Snapshot.GoodCount==8)
        {
            var facilityTop=table.Objects.Facility("facility-check",OperationDock.FacilityLayout(0),0,0);
            Require((facilityTop.Root.position-facilityLocation).sqrMagnitude<.00000001f&&facilityTop.Root.localScale==facilityScale&&facilityTop.Size==facilitySize,"切换俯视重新摆放或缩放了设施");
            Require(facilityTop.Contains(facilityTop.Rect.center),"俯视设施没有按实体位置响应拾取");
        }
        table.Frame(BoardCamera,false,1);
        var hudBefore=stock.Rect;var panBefore=table.PanOffset;
        table.PanFromScreen(new Vector2(720,450),new Vector2(780,480));table.EndPan();
        Require(table.PanOffset!=panBefore&&stock.Rect==hudBefore,"地图拖动没有移动地图或错误移动HUD");
        table.ResetPan();
        Require(table.TableCamera==BoardCamera,"斜视时棋盘与周边物件不使用同一相机");
        var boardRect=BoardCamera.rect;var mask=BoardCamera.cullingMask;
        table.gameObject.SetActive(false);table.gameObject.SetActive(true);
        Require(BoardCamera.rect==boardRect&&BoardCamera.cullingMask==mask,"返回当前局后没有恢复统一桌面相机");
        if(Snapshot.GoodCount==8)
        {
            for(int track=0;track<3;track++)
            {
                var board=table.Objects.Facility("facility-"+track,OperationDock.FacilityLayout(track),track,0);
                for(int level=1;level<=5;level++)
                {
                    var slot=table.Objects.FacilitySlot("slot-"+track+"-"+level,board,track,level,false);
                    Require(slot.Contains(slot.Rect.center),"完整设施卡内部小翻板无法独立拾取");
                    Require(slot.Rect.width<board.Rect.width&&slot.Rect.height<board.Rect.height,"小翻板尺寸超出完整设施卡");
                }
            }
        }
        CheckTableLayout(table);
        checks+=TabletopMapVerificationV010.Check(table);
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
        if(configurations==0)CheckNumberTokenUpdates();
    }

    static Bounds ObjectBounds(TabletopWorldObjects.Hit hit)
    {
        var bounds=hit.Renderers[0].bounds;
        foreach(var renderer in hit.Renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    void CheckTableLayout(TabletopScene table)
    {
        Require(TabletopDefenseUI.Area.xMin>1080&&TabletopDefenseUI.Area.yMin>=195,"公开防御区没有留在玩家统计下方");
        Require(TabletopHistoryUI.Feed.xMin>1100&&TabletopHistoryUI.Feed.height>=300,"历史记录没有移到右侧");
        Require(TabletopUI.EndTurnRect.xMin>1250&&TabletopUI.EndTurnRect.height>=120,"回合按钮没有使用右下圆形布局");
        var pieces=Snapshot.Builds.Where(b=>b.Track<0&&b.Icon!="development").ToArray();
        for(int index=0;index<pieces.Length;index++)
        {
            var group=OperationDock.StockGroupLayout(pieces,index,Snapshot.GoodCount==8);
            Require(group.xMax<150&&group.yMin>=280&&group.yMax<=890,"单建筑模型没有排在左下");
            Require(group.height<=52&&TabletopPieceDock.ModelArea(group).xMax<TabletopPieceDock.CountArea(group).xMin,"库存框过大或模型和数字的预留区域发生重叠");
            if(index>0)Require(group.yMin>=OperationDock.StockGroupLayout(pieces,index-1,Snapshot.GoodCount==8).yMax,"建筑token区域重叠");
        }
        int prior=StrategyHudTheme.Selected;
        for(int theme=0;theme<3;theme++)
        {
            StrategyHudTheme.Select(theme);Require(StrategyHudTheme.Selected==theme,"主题无法即时切换");
            Require(ModernOperationUI.Background==StrategyHudTheme.Background&&ModernOperationUI.Accent==StrategyHudTheme.Accent,"操作弹窗没有采用当前主题");
        }
        StrategyHudTheme.Select(prior);
        foreach(string icon in new[]{"cloth","coin","paper"})
        {
            var texture=Resources.Load<Texture2D>("TabletopUI/v010/"+icon+"-cutout");
            Require(texture!=null&&texture.width>=512&&texture.height>=512,"缺少独立商品透明图标");
        }
        if(Snapshot.GoodCount==8)
        {
            string before=Authority;Dock.OpenFacility(2);
            Require(Dock.HasOverlay&&Dock.BlocksBoardInput(Vector2.zero),"设施弹窗没有阻挡背景");
            Require(before==Authority,"打开设施界面改变权威状态");Dock.Reset();
        }
    }
    void CheckNumberTokenUpdates()
    {
        var root=new GameObject("点数同步验证");
        try
        {
            var positions=new System.Collections.Generic.Dictionary<string,Vector3>{{"a",Vector3.zero},{"b",Vector3.right},{"fog",Vector3.left},{"desert",Vector3.forward}};
            var tiles=new[]{new Tile{Id="a",Resource="wood",Number=3},new Tile{Id="b",Resource="wheat",Number=5},
                new Tile{Id="fog",Resource="fog",Number=6},new Tile{Id="desert",Resource="desert"}};
            var tokens=TabletopNumberTokens.Create(root.transform,tiles,positions);
            Require(tokens.transform.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==2,"沙漠或未揭示地块错误生成点数牌");
            tiles[0].Number=5;tiles[1].Number=3;tiles[2].Resource="wool";
            tokens.Refresh(tiles);
            var active=tokens.transform.Cast<Transform>().Where(t=>t.gameObject.activeSelf).ToArray();
            Require(active.Length==3&&active.Any(t=>t.name=="点数 5 · a")&&active.Any(t=>t.name=="点数 3 · b")&&active.Any(t=>t.name=="点数 6 · fog"),"换点数或探索后实体牌面未同步");
            // 资源打包与图形材质分开验证。Null 图形设备不提供完整 Shader 材质属性，
            // 但仍必须成功加载每张二维贴图；实际设备另外验证牌面材质绑定。
            foreach(int number in new[]{2,3,4,5,6,8,9,10,11,12})
            {
                var texture=Resources.Load<Texture2D>("TabletopUI/v008/NumberTokens/number-"+number);
                Require(texture!=null&&texture.width==512&&texture.height==512,"实体数字 "+number+" 缺少有效二维贴图");
            }
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                Require(active.All(t=>t.GetComponent<MeshRenderer>().sharedMaterials[0].HasProperty("_BaseMap")&&
                    t.GetComponent<MeshRenderer>().sharedMaterials[0].GetTexture("_BaseMap")!=null),"图形模式实体数字材质未绑定贴图");
            var identifiers=active.Select(t=>t.GetInstanceID()).ToArray();tokens.Refresh(tiles);
            Require(identifiers.SequenceEqual(tokens.transform.Cast<Transform>().Where(t=>t.gameObject.activeSelf).Select(t=>t.GetInstanceID())),"不变的公开点数被反复重建");
        }
        finally{root.SetActive(false);Destroy(root);}
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
        typeof(OperationDock).GetField("scope", PrivateInstance).SetValue(Dock, Snapshot.Scope);
        typeof(OperationDock).GetField("revision", PrivateInstance).SetValue(Dock, Snapshot.Revision);
        Hud.BindOperations(Snapshot,Dock.PrepareDevelopmentPurchase,app.HistoryEntries);Dock.Windows=Hud.OperationWindows;
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
        Require(camera.rect == new Rect(0,0,1,1)&&Table.TableCamera==camera, "棋盘与桌面没有共用全屏相机");
        var expected = top?TabletopScene.BoardArea:TabletopScene.BoardFitArea; expected.xMin -= 4; expected.xMax += 4; expected.yMin -= 4; expected.yMax += 4;
        var ids = Vertices.Select(v => v.Id).Concat(Edges.Select(e => e.Id)).Concat(Tiles.Select(t => t.Id));
        foreach (string id in ids)
        {
            var point = camera.WorldToScreenPoint(Position(id) + Vector3.up * .005f);
            Require(point.z > 0 && expected.Contains(new Vector2(point.x * 1440/Screen.width, (1 - point.y/Screen.height) * 900)), "公开地图节点超出中央操作范围");
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
        app.HandleTableEscape();
        Require(!app.TableMenuOpen&&before == Authority, "Esc 放回建筑打开了菜单或改变权威状态");
        app.HandleTableEscape();Require(app.TableMenuOpen,"放回建筑后再次 Esc 未打开菜单");
        app.HandleTableEscape();Require(!app.TableMenuOpen,"再次 Esc 未返回放置界面");
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
        var previousActive = RenderTexture.active;
        var target = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32);
        Texture2D image = null;
        try
        {
            target.Create();
            Table.RenderComposite(target); RenderTexture.active = target;
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
            RenderTexture.active = previousActive;
            target.Release(); Destroy(target); if (image != null) Destroy(image);
        }
    }

    void BeginGuiAttempt(string filename)
    {
        guiAttempts++;guiFilename=filename;
        guiTarget=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);guiTarget.Create();
        Table.RenderComposite(guiTarget);guiBegan=false;
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
        if(guiBegan)RenderTexture.active=guiPreviousActive;
        if(guiTarget!=null){guiTarget.Release();Destroy(guiTarget);}
        guiTarget=null;guiBegan=false;
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
