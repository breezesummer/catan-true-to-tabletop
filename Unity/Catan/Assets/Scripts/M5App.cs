using System;
using System.Linq;
using Catan.Core;
using Catan.Core.M5;
using UnityEngine;
using Command = Catan.Core.M5.Command;
using CommandKind = Catan.Core.M5.CommandKind;
using CommandResult = Catan.Core.M5.CommandResult;
using GamePhase = Catan.Core.M5.GamePhase;
using PlayerView = Catan.Core.M5.PlayerView;
using PublicPlayer = Catan.Core.M5.PublicPlayer;

// Presentation receives detached seat views. Every button uses the same authoritative command entrance.
public sealed partial class M5App : MonoBehaviour
{
    M6App ai;
    M5LocalGameHost host;
    PlayerView view;
    M5BoardRenderer board;
    // Retain public labels only while the seat view is destroyed by the privacy curtain.
    sealed class PublicSummary
    {
        public string ScenarioName,ActivePlayerId;
        public int TargetVictoryPoints,Turn,BarbarianPosition,LastDice1,LastDice2;
        public EventDie LastEventDie;
        public PublicPlayer[] Players;
    }
    PublicSummary summary;
    string seat="P1",nextSeat="P1",status="请确认席位，开始航海家与城市骑士组合。",layout="",pickField="",pickType="V",selectedScenario="heading-for-new-shores";
    bool curtain=true,newGame,terrain=true;
    int tab;
    Vector2 scroll;
    Command draft;
    Font font;
    GUIStyle label,small,heading,button,paperLabel,paperSmall;
    readonly EntityCardAssets.Display cardArt=new EntityCardAssets.Display();
    bool artOpen;
    static readonly string[] Goods={"木材","砖","羊毛","谷物","矿石","布匹","钱币","纸张"};
    static readonly string[] Tracks={"贸易 · 布匹","政治 · 钱币","科学 · 纸张"};
    internal static readonly string[] Cards={"炼金术","起重机","工程学","发明","灌溉","医学","采矿","印刷术","道路建设","锻铁匠","商业港口","行会征费","商人","商船队","资源垄断","商品垄断","外交","间谍","鼓舞","谋略","征税","宪法","叛变","婚礼","破坏"};
    internal static readonly string[] CardHelp={
        "掷骰前指定红、白产出骰；事件骰照常随机掷出。","选择轨道，立即以少一张商品的费用建造一级城市设施。","选择自己的城市，免费建一面城墙。","选择两个地块交换数字；2、6、8、12 不能交换。","每个邻接自己建筑的麦田取 2 谷物；每个地块只算一次。","选择村庄，以 1 谷物和 2 矿石升级城市。","每个邻接自己建筑的山地取 2 矿石。","抽到后自动公开，得 1 分。","免费建设两条道路或船，逐条选择。","选择两名可晋升骑士免费晋升；只有一名符合条件时选择一名，每名每回合最多晋升一次。","本回合可向每个对手提供一次资源，换取其自选商品。","选择分数高于自己的玩家，查看其手牌并取两张。","选择邻接自己建筑的资源地块（金矿除外），取得商人及 1 分，并以 2:1 交易该资源。","本回合所选资源或商品可按 2:1 交易。","每名对手交出所选资源最多两张。","每名对手交出所选商品最多一张。","移除一条开放道路或船；若是自己的，可立即免费重建同种棋子。","选择对手，仅你可查看其发展卡，并可取一张。","免费激活所有未激活骑士；本回合新激活者不能行动。","选择连接自己道路或船的对手骑士，将其驱离。","首次蛮族攻击后，将强盗移到新地块，向每名邻接对手各随机取一张。","抽到后自动公开，得 1 分。","对手选择移除一名骑士，你可招募同级或更低且同状态的骑士。","分数高于你的每名对手自选两张资源或商品给你。","分数不少于你的对手各弃掉一半资源和商品。"};

    public void Start()
    {
        Application.targetFrameRate=60;ai=GetComponent<M6App>();
        if(ai!=null&&ai.NetworkGame){font=MobileTabletop.LoadFont();ai.ConnectNetwork(this);return;}
        host=new M5LocalGameHost(ai==null?4:ai.PlayerCount);
        if(ai!=null && string.IsNullOrEmpty(ai.InitialAuthority))host.NewGame(ai.ScenarioId,ai.PlayerCount,unchecked((uint)Guid.NewGuid().GetHashCode()),ai.TargetVictoryPoints);
        if(ai!=null&&!string.IsNullOrEmpty(ai.InitialAuthority))host.ImportSave(ai.InitialAuthority);
        var initial=host.View(ai==null?"P1":ai.HumanSeat);nextSeat=initial.ActivePlayerId;Refresh(initial);
        font=MobileTabletop.LoadFont();
        if(ai==null)gameObject.AddComponent<M5PlayerVerification>().Initialize(this);
        else ai.Connect(this,host);
    }
    void Styles()
    {
        if(label!=null)return;
        label=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};label.normal.textColor=new Color(.91f,.92f,.86f);
        small=new GUIStyle(label){fontSize=13};heading=new GUIStyle(label){fontSize=22,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=15,wordWrap=true};
        paperLabel=new GUIStyle(label);paperLabel.normal.textColor=new Color(.24f,.12f,.06f);paperSmall=new GUIStyle(paperLabel){fontSize=13};
    }
    public static string Actor(PlayerView v)
    {
        if(v.TradeOffer!=null)return v.TradeOffer.NextResponder(v.PlayerId);
        if(v.Discards.Length>0)return v.Discards[0].PlayerId;
        return v.PendingDecision!=null&&!string.IsNullOrEmpty(v.PendingDecision.PlayerId)?v.PendingDecision.PlayerId:v.ActivePlayerId;
    }
    public void SwitchSeat(string target)
    {
        if(ai!=null){status="你控制 "+ai.HumanSeat+"；其他席位为 AI，仅显示公开信息。";return;}
        dock.Reset();nextSeat=target;curtain=true;view=null;draft=null;pickField="";scroll=Vector2.zero;tab=0;artOpen=false;cardArt.Clear();board.ClearPreview();
        status="手牌与私有选择已遮蔽，请将设备交给 "+target+"。";
    }
    public void ConfirmSeat(){dock.Reset();seat=nextSeat;curtain=false;view=NetworkGame?ai.Network.View<PlayerView>():host.View(seat);Refresh(view);}
    public CommandResult Submit(Command command)
    {
        if(NetworkGame){dock.Reset();CancelOperationDraft();cardArt.Clear();ai.Network.Submit(command);status=ai.Network.Status;return null;}
        if(ai!=null&&(command.PlayerId!=ai.HumanSeat||!ai.IsHumanTurn&&!(command.Kind==CommandKind.CancelTrade&&view!=null&&view.TradeOffer!=null&&view.TradeOffer.ProposerId==seat))){status="请等待当前 AI 或待决席位完成操作。";return null;}
        if(ai!=null&&command==draft&&!DockKnightTargetMatches(command.TargetId)){status="请选择低一级的己方骑士晋升。";return null;}
        command.Id=string.IsNullOrEmpty(command.Id)?Guid.NewGuid().ToString("N"):command.Id;
        dock.Reset();CancelOperationDraft();var result=host.Submit(command);status=result.Success?"操作完成。":result.Message;
        if(ai!=null&&result.Success&&ai.PresentPlayedCard(host.History(seat),M6Refresh))return result;
        pickField="";board.ClearPreview();view=NetworkGame?ai.Network.View<PlayerView>():host.View(seat);Refresh(view);
        if(result.Success)
        {
            draft=null;
            if(view.Phase==GamePhase.Finished)status=view.WinnerPlayerId+" 达到 "+view.TargetVictoryPoints+" 分，本局结束。";
            else if(ai==null&&Actor(view)!=seat)SwitchSeat(Actor(view));
        }
        return result;
    }
    Command New(CommandKind kind,string target=null)=>new Command{Id=Guid.NewGuid().ToString("N"),PlayerId=seat,Kind=kind,TargetId=target,Resources=new ResourceBag(),Commodities=new CommodityBag(),Give=new ResourceBag(),Receive=new ResourceBag(),GiveCommodities=new CommodityBag(),ReceiveCommodities=new CommodityBag(),ChosenDice1=1,ChosenDice2=1};
    void Select(CommandKind kind){draft=New(kind);pickField="";scroll=Vector2.zero;}
    void Refresh(PlayerView v)
    {
        mapChoice.Clear();
        ai?.ObservePlayerOrder(v.StartingPlayerId,v.Players.Select(p=>p.Id));
        summary=new PublicSummary{ScenarioName=v.ScenarioName,ActivePlayerId=v.ActivePlayerId,TargetVictoryPoints=v.TargetVictoryPoints,Turn=v.Turn,BarbarianPosition=v.BarbarianPosition,LastDice1=v.LastDice1,LastDice2=v.LastDice2,LastEventDie=v.LastEventDie,Players=v.Players};
        string next=v.ScenarioId+"|"+string.Join("|",v.Board.Tiles.Select(t=>t.Id+":"+t.Resource));
        if(board==null||next!=layout)
        {
            bool top=board!=null&&board.TopView;
            if(board!=null){board.gameObject.SetActive(false);Destroy(board.gameObject);}
            board=new GameObject("M5 board").AddComponent<M5BoardRenderer>();board.Initialize(v.Board.Tiles,v.Board.Vertices,v.Board.Edges);
            board.SetCamera(top);layout=next;
        }
        board.SetOperationLayout(ai!=null);board.Refresh(v);board.ShowTerrain(terrain);
    }
    public void SaveGame(){if(ai!=null){ai.SaveGame();return;}try{host.Save();status="已保存完整局面和待决选择。";}catch(Exception){status="保存失败，请检查存档目录。";}}
    public void LoadGame(){if(ai!=null){ai.LoadGame();return;}try{host.Load();var v=host.View("P1");Refresh(v);SwitchSeat(Actor(v));status="已恢复，请确认席位。";}catch(Exception){status="存档无法读取或版本不兼容，当前局面保留。";}}
    readonly MapPanInput mapPan=new MapPanInput();
    void Update()
    {
        Vector2 releasedMouse;bool buildReleased=dock.ConsumeBuildRelease(out releasedMouse);
        if(board==null)return;
        RefreshTablePrompt();
        if(mapChoice.Active&&Input.GetMouseButtonDown(1)){mapChoice.Clear();return;}
        if(ai!=null&&ai.TableMenuOpen){mapPan.Cancel(board.TableScene);board.ClearPreview();return;}
        var pointer=MobileTabletop.Pointer;
        bool blocked=DockBlocksMouse()||cardArt.BlocksBoardInput(MobileTabletop.Logical(pointer));
        bool canMap=ai!=null&&!curtain&&!newGame&&!artOpen&&!ai.TableMenuOpen&&!blocked&&TabletopUI.ContainsMapScreenPoint(pointer);
        bool boardClick=mapPan.Update(board.TableScene,pointer,canMap,tabletop.OperationHidden||draft==null&&!operationActions);
        if(mapPan.ZoomDelta!=0)board.ChangeZoom(mapPan.ZoomDelta);
        if(mapPan.IsDragging||mapPan.MultiTouchActive){board.ClearPreview();return;}
        if(Input.GetKeyDown(KeyCode.Tab))board.SetCamera(!board.TopView);
        if(ai==null&&Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)&&!blocked&&(ai!=null||pointer.x<Screen.width*.70f))
        {
            bool knightMenu=ai!=null&&Input.GetMouseButtonDown(1)&&!blocked&&PickTabletopContext(pointer);
            if(!knightMenu){dock.Reset();CancelOperationDraft();pickField="";board.ClearPreview();newGame=false;cardArt.Clear();}
            mapPan.Cancel(board.TableScene);
        }
        if(!DockBlocksMouse()&&(ai!=null||Input.mousePosition.x<Screen.width*.70f)&&Input.mouseScrollDelta.y!=0)board.ChangeZoom(-Input.mouseScrollDelta.y*.012f);
        if(curtain||newGame||artOpen||ai!=null&&!ai.IsHumanTurn)return;
        var mouse=buildReleased?MobileTabletop.ScreenPoint(releasedMouse):MobileTabletop.Pointer;
        var logicalMouse=MobileTabletop.Logical(mouse);
        if(DockBlocksMouse(logicalMouse)||cardArt.BlocksBoardInput(logicalMouse)){board.ClearPreview();return;}
        if(ai==null&&(mouse.x>Screen.width*.70f||mouse.y<Screen.height*.15f||mouse.y>Screen.height*.90f))return;
        if(ai!=null&&tabletop.OperationHidden){board.ClearPreview();return;}
        if(ai!=null&&DirectProgressActive){UpdateDirectProgress(mouse,boardClick);return;}
        if(ai!=null&&FreeRoadPlacementActive){UpdateFreeRoadPlacement(mouse,boardClick);return;}
        if(ai!=null&&draft==null&&boardClick){PickTabletopObject(mouse);return;}
        if(draft==null||pickField=="")return;
        string target=Pick(pickType,mouse);
        if(target!="")
        {
            bool valid=ai==null||!DirectTabletopPlacement||pickField!="source"||(draft.Kind==CommandKind.MoveShip?view.MovableShipEdgeIds.Contains(target):view.LegalActions.Any(c=>c.Kind==draft.Kind&&c.SourceId==target));
            if(pickField=="target") {string old=draft.TargetId;draft.TargetId=target;valid=Preview(draft).Success&&(ai==null||DockKnightTargetMatches(target));draft.TargetId=old;}
            PreviewOperationTarget(target,valid);
            if((ai!=null?boardClick:Input.GetMouseButtonDown(0))||buildReleased)
            {
                if(ai!=null&&!DockKnightTargetMatches(target)){status="请选择低一级的己方骑士晋升。";return;}
                if(ai!=null&&DirectTabletopPlacement)
                {
                    if(pickField=="source")
                    {
                        if(!(draft.Kind==CommandKind.MoveShip?view.MovableShipEdgeIds.Contains(target):view.LegalActions.Any(c=>c.Kind==draft.Kind&&c.SourceId==target))){status="这枚棋子目前无法执行该动作。";return;}
                        draft.SourceId=target;pickField="target";board.ClearPreview();status="已拿起棋子，点击合法目的位置放下。";return;
                    }
                    if(!valid){status="这个位置不能放下当前棋子，请选择其他位置；右键放回。";return;}
                    draft.TargetId=target;Submit(draft);return;
                }
                if(pickField=="source")draft.SourceId=target;else draft.TargetId=target;
                pickField="";board.ClearPreview();status=ai!=null?"目标已选中，请在操作面板确认。":"已选择 "+target+"；检查面板后确认。";
            }
        }
        else board.ClearPreview();
    }
    void PreviewOperationTarget(string target,bool legal)
    {
        var selected=view.Board.Knights.FirstOrDefault(k=>k.LocationId==(string.IsNullOrEmpty(draft.SourceId)?target:draft.SourceId));
        int level=selected?.Level??1;
        if(draft.Kind==CommandKind.PromoteKnight)level=operationKnightLevel>0?operationKnightLevel:level+1;
        if(draft.Kind==CommandKind.RecruitKnight)level=1;
        bool setupCity=draft.Kind==CommandKind.SetupSettlement&&view.Board.Settlements.Any(p=>p.PlayerId==seat);
        var preview=TabletopPointerPreview.ForCommand(draft.Kind.ToString(),setupCity,level,selected?.Active??false);
        if(draft.Kind==CommandKind.PlayProgressCard||draft.Kind==CommandKind.ResolveChoice||draft.Kind==CommandKind.ResolveProgressChoice)
            preview=TabletopPointerPreview.ForEffect(draft.Kind==CommandKind.PlayProgressCard?draft.ProgressCard.ToString():null,view.PendingDecision?.Kind,level,selected?.Active??false);
        board.PreviewPiece(target,preview,seat,legal);
    }
    internal bool TryCancelTableInteraction()
    {
        if(mapPan.IsDragging){mapPan.Cancel(board.TableScene);return true;}
        if(tabletop.TryCloseOverlay())return true;
        if(!tabletop.OperationHidden&&(dock.HasOverlay||tabletop.ContextVisible||mapChoice.Active||ResourceChoiceVisible))
        {
            var snapshot=Operations();
            if(dock.HasOverlay&&snapshot.Offer!=null){dock.CloseWindow(snapshot);return true;}
            dock.Reset();CancelOperationDraft();tabletop.CloseOperation();
            // 规则要求完成的待决仅关闭本地窗口，仍从选择提示继续。
            return true;
        }
        if(mapChoice.Active){mapChoice.Clear();return true;}
        bool active=draft!=null||operationActions||operationKnightTarget!=null||newGame||artOpen||cardArt.HasSelection||dock.HasCancelableInteraction;
        if(!active)return false;
        dock.Reset();CancelOperationDraft();pickField="";board.ClearPreview();newGame=false;artOpen=false;cardArt.Clear();return true;
    }
    string Pick(string type,Vector2 mouse)
    {
        string[] ids=type=="E"?view.Board.Edges.Select(x=>x.Id).ToArray():type=="T"?view.Board.Tiles.Select(x=>x.Id).ToArray():view.Board.Vertices.Select(x=>x.Id).ToArray();
        string nearest="";float distance=MobileTabletop.PickRadius(type=="T"?42:25);
        foreach(var id in ids){var projected=board.ScreenPoint(id);if(ai!=null&&(!TabletopUI.ContainsMapScreenPoint(mouse)||!TabletopUI.ContainsMapScreenPoint(projected)))continue;float d=Vector2.Distance(mouse,projected);if(d<distance){distance=d;nearest=id;}}
        return nearest;
    }
    void Panel(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    bool Btn(string text)=>ai!=null?ModernOperationUI.LayoutButton(M6App.DisplayPlayerText(text)):GUILayout.Button(M6App.DisplayPlayerText(text),button,GUILayout.MinHeight(32));
    void Text(string value,bool minor=false)
    {value=M6App.DisplayPlayerText(value);if(ai!=null){ModernOperationUI.Styles(font);GUILayout.Label(value,minor?ModernOperationUI.Small:ModernOperationUI.Text);}else GUILayout.Label(value,minor?small:label);}
    void OnGUI()
    {
        if(host==null&&!NetworkGame)return;if(ai!=null){DrawOperationLayout();return;}Styles();cardArt.Begin(curtain||newGame||view==null?null:seat);GUI.matrix=MobileTabletop.GuiMatrix;
        Panel(new Rect(0,0,1440,125),new Color(.045f,.09f,.10f));
        GUI.Label(new Rect(25,15,700,36),"CATAN  /  航海家 × 城市与骑士",heading);
        var publicView=summary;
        GUI.Label(new Rect(25,54,760,25),"M5 · "+publicView.ScenarioName+" · 官方 2025 组合 · "+publicView.TargetVictoryPoints+" 分获胜",small);
        if(GUI.Button(new Rect(800,18,180,34),board.TopView?"斜视 [Tab]":"俯视 [Tab]",button))board.SetCamera(!board.TopView);
        if(GUI.Button(new Rect(800,60,180,32),"地形显隐",button)){terrain=!terrain;board.ShowTerrain(terrain);}
        GUI.Label(new Rect(25,90,960,28),"第 "+publicView.Turn+" 回合 · 行动 "+M6App.PlayerLabel(publicView.ActivePlayerId)+"     蛮族 "+publicView.BarbarianPosition+" / 7     红骰 "+publicView.LastDice1+" + 白骰 "+publicView.LastDice2+"     事件 "+EventName(publicView.LastEventDie),label);
        Panel(new Rect(1015,0,425,900),new Color(.065f,.12f,.135f));
        if(newGame)
        {
            GUILayout.BeginArea(new Rect(1040,80,370,660));Text("新开一局会替换当前未保存局面。");
            selectedScenario=new[]{"heading-for-new-shores","through-the-desert"}[Cycle("组合剧本",selectedScenario=="heading-for-new-shores"?0:1,new[]{"驶向新海岸","穿越沙漠"})];
            Text("两个剧本均支持 3–4 人，目标 16 分。新岛奖励、金矿、商品、骑士和发展卡共同生效。",true);
            foreach(int n in new[]{3,4})if(Btn(n+" 人新局")){host.NewGame(selectedScenario,n);var v=host.View("P1");Refresh(v);SwitchSeat(v.ActivePlayerId);newGame=false;}
            GUILayout.Space(16);Text("组合兼容范围",true);Text("本阶段按 2025 城市与骑士规则第 12 页开放驶向新海岸、穿越沙漠。四岛、迷雾岛、遗忘部落、布匹、海盗岛、奇迹及新世界暂不开放组合；其单扩展版本可在 M3 游玩。",true);
            if(Btn("返回当前局"))newGame=false;GUILayout.EndArea();GUI.matrix=Matrix4x4.identity;return;
        }
        if(curtain)
        {
            GUILayout.BeginArea(new Rect(1040,80,370,340));GUILayout.Label("请交给 "+nextSeat,heading);Text("资源、商品、发展卡和窥看信息均已遮蔽。\n\n请其他玩家暂时移开视线。");GUILayout.Space(30);
            if(Btn("我是 "+nextSeat+" · 查看手牌"))ConfirmSeat();GUILayout.EndArea();
        }
        else
        {
            DrawBoardLabels();GUILayout.BeginArea(new Rect(1032,12,390,724));
            GUILayout.Label(seat+" · "+PhaseName(view.Phase)+" · "+view.OwnVictoryPoints+" 分",heading);
            cardArt.GoodRow(Enumerable.Range(0,8).Select(Own).ToArray(),Goods,small);
            var own=view.Players.First(p=>p.Id==seat);Text("城市设施 贸易 "+own.Improvements[0]+" / 政治 "+own.Improvements[1]+" / 科学 "+own.Improvements[2]+"   城墙余 "+own.WallSupply,true);
            Text("路 "+own.Pieces.Roads+" / 船 "+own.ShipsRemaining+" / 村 "+own.Pieces.Settlements+" / 城 "+own.Pieces.Cities+"   骑士余 "+string.Join("/",own.KnightSupply),true);
            Text("防御 "+view.DefenderStrength+" / 蛮族力量 "+view.BarbarianStrength+" · "+(view.FirstBarbarianAttack?"强盗 / 海盗已启用":"强盗 / 海盗尚未启用"),true);
            if(Btn(artOpen?"返回操作面板":"查看手牌与设施实体卡面")){artOpen=!artOpen;scroll=Vector2.zero;}
            scroll=GUILayout.BeginScrollView(scroll);
            if(artOpen)DrawEntityCards();else DrawControls();GUILayout.EndScrollView();GUILayout.EndArea();
        }
        GUILayout.BeginArea(new Rect(1032,749,390,142));GUILayout.BeginHorizontal();if(Btn("保存"))SaveGame();if(Btn("恢复"))LoadGame();if(Btn("新开一局")){if(ai!=null)ai.ShowMenu();else newGame=true;}GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();foreach(var p in publicView.Players)if(Btn(p.Id+"\n"+p.VictoryPoints+"分 / "+p.ResourceCount+"牌")){SwitchSeat(p.Id);break;}GUILayout.EndHorizontal();
        Text(ai==null?"点击席位换座 · Tab 视角 · 滚轮缩放 · Esc 取消":"你："+ai.HumanSeat+" · 其他席位为 AI · Tab 视角 · Esc 取消",true);GUILayout.EndArea();
        Panel(new Rect(0,812,1015,88),new Color(.045f,.09f,.10f));GUI.Label(new Rect(24,826,970,58),pickField==""?M6App.DisplayPlayerText(status):"请在棋盘选择"+(pickType=="V"?"交点":pickType=="E"?"道路":"地块")+"，然后在右侧确认；Esc 取消。",label);
        if(!curtain&&!newGame&&view!=null)cardArt.DrawPreview(label,small,button);
        GUI.matrix=Matrix4x4.identity;
    }
    int Own(int i)=>i<5?view.OwnResources[(Resource)i]:view.OwnCommodities[(Commodity)(i-5)];
    void ProgressFace(ProgressCardKind card,string caption)
    { cardArt.LayoutBadge(EntityCardAssets.Progress((int)card),caption,ai!=null?paperSmall:small); }
    void DrawEntityCards()
    {
        Text("悬停放大 · 点击固定查看与翻面。实体照片中的文字仅作外观，规则以操作提示为准。",true);
        Text("我的发展卡");
        foreach(var card in view.OwnProgressCards.Distinct())
        {
            GUILayout.BeginHorizontal();ProgressFace(card,CardName(card)+" ×"+view.OwnProgressCards.Count(c=>c==card));Text(CardHelp[(int)card],true);GUILayout.EndHorizontal();
        }
        if(view.OwnProgressCards.Length==0)Text("当前没有发展卡。",true);
        if(view.PrivateTargetProgressCards!=null&&view.PrivateTargetProgressCards.Length>0)
        {
            Text("本次待决允许查看的发展卡");
            foreach(var card in view.PrivateTargetProgressCards.Distinct())ProgressFace(card,"允许查看 · "+CardName(card));
        }
        Text("我的城市设施");
        cardArt.Improvements(view.OwnImprovements,Tracks,small);
        var own=view.Players.First(p=>p.Id==seat);
        cardArt.LayoutBadge(EntityCardAssets.Defender,"保卫者 ×"+own.DefenderPoints,small,100,133);
        Text("公开的奖励牌");
        foreach(var p in view.Players)
        {
            if(p.DefenderPoints==0&&p.ProgressVictoryCards.Length==0)continue;
            Text(p.Id+" · 公开奖励",true);GUILayout.BeginHorizontal();
            if(p.DefenderPoints>0)cardArt.LayoutBadge(EntityCardAssets.Defender,p.Id+" 保卫者 ×"+p.DefenderPoints,small,100,133);
            foreach(var card in p.ProgressVictoryCards.Distinct())ProgressFace(card,p.Id+" "+CardName(card)+" ×"+p.ProgressVictoryCards.Count(c=>c==card));
            GUILayout.EndHorizontal();
        }
    }
    void DrawBoardLabels()
    {
        foreach(var tile in view.Board.Tiles)
        {
            var p=board.ScreenPoint(tile.Id);p=MobileTabletop.Logical(p);
            if(tile.Resource!="sea")
            {
                if(ai!=null)TabletopUI.NumberToken(p,tile.Number>0?(int?)tile.Number:null,tile.Resource=="fog"?"迷雾":"沙漠",heading);
                else
                {
                    var numberStyle=new GUIStyle(heading);if(tile.Number==6||tile.Number==8)numberStyle.normal.textColor=TabletopUI.HotNumberColor;
                    GUI.Label(new Rect(p.x-30,p.y-16,70,24),(tile.Number>0?tile.Number.ToString():"沙漠")+(tile.Resource=="gold"?" 金":""),numberStyle);
                }
            }
        }
        if(pickField!=""&&ai==null)
        {
            var ids=pickType=="V"?view.Board.Vertices.Select(v=>v.Id):pickType=="E"?view.Board.Edges.Select(e=>e.Id):view.Board.Tiles.Select(t=>t.Id);
            foreach(var id in ids){var p=board.ScreenPoint(id);p=MobileTabletop.Logical(p);GUI.Label(new Rect(p.x-25,p.y+8,65,20),id,small);}
        }
        foreach(var port in view.Board.Ports)
        {
            if(board.ReferenceMarkers.HasPort(port.Id))
            {
                // 点击与地图拖动统一在 Update 的松键入口处理。
                continue;
            }
            var a=board.ScreenPoint(port.Vertices[0]);var b=board.ScreenPoint(port.Vertices[1]);var p=(a+b)*.5f;p=MobileTabletop.Logical(p);
            GUI.Label(new Rect(p.x-40,p.y+15,100,22),port.Resource.HasValue?Goods[(int)port.Resource.Value]+" 2:1":"港口 3:1",small);
        }
        foreach(var k in view.Board.Knights)
        {
            var p=board.ScreenPoint(k.LocationId);p=MobileTabletop.Logical(p);
            if(ai==null)
                GUI.Label(new Rect(p.x-26,p.y-36,86,21),M6App.PlayerLabel(k.PlayerId)+" 骑"+k.Level+(k.Active?" ●":" ○"),M6App.PlayerNameStyle(small,k.PlayerId));
        }
    }
    void DrawControls()
    {
        if(ai!=null&&!ai.IsHumanTurn&&view.Phase!=GamePhase.Finished){Text(ai.WaitingText);return;}
        if(view.Phase==GamePhase.Finished){Text(view.WinnerPlayerId+" 获胜。可保存或新开一局。");return;}
        if(view.TradeOffer!=null)
        {
            var t=view.TradeOffer;Text(t.ProposerId+" 向 "+t.OtherPlayerId+" 提议：\n付出 "+BagText(t.Give,t.GiveCommodities)+"\n换取 "+BagText(t.Receive,t.ReceiveCommodities));
            if(Btn("接受交易")){Submit(New(CommandKind.AcceptTrade));return;}if(Btn("拒绝交易")){Submit(New(CommandKind.RejectTrade));return;}if(Btn("发起者取消")){Submit(New(CommandKind.CancelTrade));return;}return;
        }
        if(view.PendingDecision!=null)Text("待决 · "+DecisionName(view.PendingDecision.Kind)+" · "+view.PendingDecision.PlayerId+(view.PendingDecision.Amount>0?" · 数量 "+view.PendingDecision.Amount:""));
        if(view.Phase==GamePhase.SetupSettlement||view.Phase==GamePhase.SetupRoad)
        {
            Text("第一轮村庄，逆序第二轮城市；第二轮每邻接地块取一张资源。可选择道路或船，必须邻接刚放下的建筑。",true);
            if(draft==null)Select(view.Phase==GamePhase.SetupRoad?CommandKind.SetupRoad:CommandKind.SetupSettlement);
            if(view.Phase==GamePhase.SetupRoad){GUILayout.BeginHorizontal();ActionButton("开局道路",CommandKind.SetupRoad);ActionButton("开局船",CommandKind.SetupShip);GUILayout.EndHorizontal();}
        }
        else if(view.Phase==GamePhase.Discard)
        {
            Text("资源和商品合计弃掉要求数量。城墙仅提高触发门槛。");if(draft==null)Select(CommandKind.DiscardResources);
            var need=view.Discards.FirstOrDefault(d=>d.PlayerId==seat);if(need!=null)Text("需弃 "+need.Amount+" 张");
        }
        else if(view.Phase==GamePhase.RobberMove)
        {
            if(draft==null)Select(CommandKind.MoveRobber);
            Text("按本次待决选择移动强盗或海盗；征税只可移动强盗。",true);
            GUILayout.BeginHorizontal();ActionButton("移动强盗",CommandKind.MoveRobber);ActionButton("移动海盗",CommandKind.MovePirate);GUILayout.EndHorizontal();
            if(Btn("海盗移至外框海域")){Submit(New(CommandKind.MovePirate,"frame"));return;}
        }
        else if(view.Phase==GamePhase.RobberSteal){if(draft==null)Select(CommandKind.StealResource);}
        else if(view.Phase==GamePhase.RoadBuilding)
        {
            if(Btn("免费道路"))Select(CommandKind.PlaceFreeRoad);if(Btn("完成道路建设")){Submit(New(CommandKind.FinishRoadBuilding));return;}
        }
        else if(view.Phase==GamePhase.PendingChoice)
        {
            if(draft==null)Select(view.PendingDecision.Kind=="ProgressDiscard"?CommandKind.DiscardProgressCard:view.PendingDecision.Kind.StartsWith("Progress")?CommandKind.ResolveProgressChoice:CommandKind.ResolveChoice);
        }
        else
        {
            int chosen=GUILayout.Toolbar(tab,new[]{"行动","骑士","设施","交易","发展卡"},button);if(chosen!=tab){tab=chosen;draft=null;pickField="";}
            if(tab==0)
            {
                GUILayout.BeginHorizontal();if(Btn("掷骰")){Submit(New(CommandKind.RollDice));GUIUtility.ExitGUI();}if(Btn("结束回合")){Submit(New(CommandKind.EndTurn));GUIUtility.ExitGUI();}GUILayout.EndHorizontal();
                ActionButton("道路 · 木1 砖1",CommandKind.BuildRoad);ActionButton("村庄 · 木砖羊麦各1",CommandKind.BuildSettlement);ActionButton("城市 · 麦2 矿3",CommandKind.BuildCity);ActionButton("城墙 · 砖2",CommandKind.BuildWall);
                ActionButton("船 · 木1 羊1",CommandKind.BuildShip);ActionButton("移动开口航线末端的船",CommandKind.MoveShip);
                Text("每回合至多移 1 船；刚造的船不能移。骑士可沿连续道路和船移动；最长航线的路船接续须经过己方村或城。",true);
            }
            if(tab==1)
            {
                Text("激活花费 1 谷物；本回合刚激活的骑士不能行动。行动后可再激活。",true);
                ActionButton("招募 · 羊1 矿1",CommandKind.RecruitKnight);ActionButton("晋升 · 羊1 矿1",CommandKind.PromoteKnight);ActionButton("激活 · 麦1",CommandKind.ActivateKnight);ActionButton("移动骑士",CommandKind.MoveKnight);ActionButton("驱离弱骑士",CommandKind.DisplaceKnight);ActionButton("驱逐强盗",CommandKind.ChaseRobber);
                ActionButton("驱逐海盗",CommandKind.ChasePirate);
            }
            if(tab==2){Text("每级费用等于新等级。三级获得能力；四级/五级争夺大都会。",true);ActionButton("城市设施",CommandKind.ImproveCity);}
            if(tab==3){ActionButton("银行与港口交易",CommandKind.BankTrade);ActionButton("玩家交易",CommandKind.ProposeTrade);}
            if(tab==4)
            {
                Text("发展卡可当回合使用。仅炼金术在掷骰前使用；回合结束最多留 4 张。",true);
                foreach(var card in view.OwnProgressCards.Distinct())
                {
                    GUILayout.BeginHorizontal();ProgressFace(card,CardName(card)+" ×"+view.OwnProgressCards.Count(c=>c==card));
                    if(Btn(CardName(card)+" ×"+view.OwnProgressCards.Count(c=>c==card))){draft=New(CommandKind.PlayProgressCard);draft.ProgressCard=card;}
                    GUILayout.EndHorizontal();
                }
                if(view.OwnProgressCards.Length==0)Text("当前没有发展卡。",true);
                if(view.CommercialHarborPlayers.Length>0)ActionButton("商业港口 · 提供资源",CommandKind.CommercialHarborOffer);
            }
        }
        if(view==null)return;
        if(draft!=null)DrawDraft();
        if(view.Phase==GamePhase.PendingChoice&&view.LegalActions!=null)
        {
            foreach(var c in view.LegalActions.Take(70))if(Btn("选择 · "+ChoiceLabel(c))){Submit(c);return;}
        }
    }
    void ActionButton(string text,CommandKind kind){if(Btn(text))Select(kind);}
    void DrawDraft()
    {
        GUILayout.Space(6);if(ai==null)Text("当前选择 · "+CommandName(draft.Kind));
        var k=draft.Kind;var pending=view.PendingDecision==null?"":view.PendingDecision.Kind;
        bool card=k==CommandKind.PlayProgressCard;bool resourceReady=true;
        if(card&&ai==null){Text(CardName(draft.ProgressCard));Text(CardHelp[(int)draft.ProgressCard],true);if(view.OwnProgressCards.Contains(draft.ProgressCard))ProgressFace(draft.ProgressCard,CardName(draft.ProgressCard));}
        if(k==CommandKind.ImproveCity&&ai!=null&&operationFacilityLocked)Text("设施轨道："+Tracks[(int)draft.Track]);
        else if(k==CommandKind.ImproveCity||card&&draft.ProgressCard==ProgressCardKind.Crane||pending=="DefenderProgress")draft.Track=(ImprovementTrack)Cycle("轨道",(int)draft.Track,Tracks);
        bool source=k==CommandKind.MoveKnight||k==CommandKind.DisplaceKnight||k==CommandKind.MoveShip;
        bool vertex=k==CommandKind.SetupSettlement||k==CommandKind.BuildSettlement||k==CommandKind.BuildCity||k==CommandKind.BuildWall||k==CommandKind.RecruitKnight||k==CommandKind.PromoteKnight||k==CommandKind.ActivateKnight||k==CommandKind.ChaseRobber||k==CommandKind.ChasePirate||source&&k!=CommandKind.MoveShip;
        bool edge=k==CommandKind.SetupRoad||k==CommandKind.BuildRoad||k==CommandKind.PlaceFreeRoad||IsShip(draft);
        bool tile=k==CommandKind.MoveRobber||k==CommandKind.MovePirate;
        if(card)
        {
            var p=draft.ProgressCard;vertex=p==ProgressCardKind.Engineering||p==ProgressCardKind.Medicine||p==ProgressCardKind.Smithing||p==ProgressCardKind.Intrigue;
            tile=p==ProgressCardKind.Invention||p==ProgressCardKind.Merchant||p==ProgressCardKind.Taxation;edge=p==ProgressCardKind.Diplomacy;
            source=p==ProgressCardKind.Invention||p==ProgressCardKind.Smithing;
            if(p==ProgressCardKind.Alchemy){draft.ChosenDice1=Cycle("红骰",draft.ChosenDice1-1,new[]{"1","2","3","4","5","6"})+1;draft.ChosenDice2=Cycle("白骰",draft.ChosenDice2-1,new[]{"1","2","3","4","5","6"})+1;}
        }
        if(k==CommandKind.ResolveChoice||k==CommandKind.ResolveProgressChoice)
        {
            if(view.PendingDecision!=null&&view.PendingDecision.Options.Length>0)
            {
                var options=view.PendingDecision.Options;int index=Array.IndexOf(options,draft.TargetId);index=Cycle("选项",Math.Max(0,index),options.Select(OptionName).ToArray());draft.TargetId=options[index];
            }
            else if(pending=="ProgressRoads")edge=true;else if(pending=="ProgressTreasonPlace")vertex=true;
            if(pending=="ProgressRoads")
            {
                if(view.PendingDecision.ShipsOnly){draft.BuildShip=true;Text("外交：免费重建一艘船。",true);}
                else if(view.PendingDecision.RoadsOnly){draft.BuildShip=false;Text("外交：免费重建一条道路。",true);}
                else draft.BuildShip=Cycle("建设类型",draft.BuildShip?1:0,new[]{"道路","船"})==1;
            }
            if(pending=="ProgressTreasonPlace")draft.KnightLevel=Cycle("骑士等级",draft.KnightLevel-1,new[]{"基础 1","强力 2","精锐 3"})+1;
            if(pending=="ProgressEspionage"||pending=="ProgressRoads"||pending=="ProgressTreasonPlace")if(Btn((draft.Decline?"✓ ":"□ ")+"放弃可选效果"))draft.Decline=!draft.Decline;
        }
        if(source)Target("起点 / 第一目标","source",tile?"T":k==CommandKind.MoveShip?"E":"V");
        if(vertex||edge||tile)Target("目标","target",tile?"T":edge?"E":"V");
        if(card&&(draft.ProgressCard==ProgressCardKind.Invention||draft.ProgressCard==ProgressCardKind.Smithing))draft.TargetIds=new[]{draft.SourceId,draft.TargetId}.Where(x=>!string.IsNullOrEmpty(x)).Distinct().ToArray();
        if(k==CommandKind.CommercialHarborOffer||k==CommandKind.StealResource||k==CommandKind.ProposeTrade||card&&(draft.ProgressCard==ProgressCardKind.GuildDues||draft.ProgressCard==ProgressCardKind.Espionage||draft.ProgressCard==ProgressCardKind.Treason))
        {
            var others=view.Players.Where(p=>p.Id!=seat).Select(p=>p.Id).ToArray();draft.OtherPlayerId=others[Cycle("对手",Math.Max(0,Array.IndexOf(others,draft.OtherPlayerId)),others)];
        }
        if(k==CommandKind.BankTrade)
        {
            Bag("付出",draft.Give,draft.GiveCommodities,true);Bag("换取",draft.Receive,draft.ReceiveCommodities,true);
            int give=Enumerable.Range(0,8).FirstOrDefault(i=>(i<5?draft.Give[(Resource)i]:draft.GiveCommodities[(Commodity)(i-5)])>0);
            int receive=Enumerable.Range(0,8).FirstOrDefault(i=>(i<5?draft.Receive[(Resource)i]:draft.ReceiveCommodities[(Commodity)(i-5)])>0);
            draft.GiveIsCommodity=give>=5;if(give<5)draft.GiveResource=(Resource)give;else draft.GiveCommodity=(Commodity)(give-5);
            draft.ReceiveIsCommodity=receive>=5;if(receive<5)draft.ReceiveResource=(Resource)receive;else draft.ReceiveCommodity=(Commodity)(receive-5);
            draft.GiveAmount=draft.Give.Total+draft.GiveCommodities.Total;draft.ReceiveAmount=draft.Receive.Total+draft.ReceiveCommodities.Total;
            Text("当前最优比例 "+TradeRate(give)+" : 1",true);
        }
        if(k==CommandKind.ProposeTrade){Bag("付出",draft.Give,draft.GiveCommodities);Bag("换取",draft.Receive,draft.ReceiveCommodities);}
        if(k==CommandKind.DiscardResources||pending=="ProgressGuildDues"||pending=="ProgressWedding"||pending=="ProgressSabotage")
        {
            if(view.PrivateTargetResources!=null)Text("允许查看的对手手牌："+BagText(view.PrivateTargetResources,view.PrivateTargetCommodities));
            Bag("选择资源与商品",draft.Resources,draft.Commodities);
        }
        if(pending=="Gold")
        {
            Text("金矿仅选资源，不取商品。需取 "+Math.Min(view.PendingDecision.Amount,view.Bank.Total)+" 张，已选 "+draft.Resources.Total+" 张。",true);
            var selected=Enumerable.Range(0,8).Select(i=>i<5?draft.Resources[(Resource)i]:0).ToArray();
            ModernOperationUI.ResourceCards("选择金矿资源",selected,Enumerable.Range(0,8).Select(i=>i<5?view.Bank[(Resource)i]:0).ToArray(),Math.Min(view.PendingDecision.Amount,view.Bank.Total),5);
            for(int i=0;i<5;i++)draft.Resources[(Resource)i]=selected[i];
        }
        if(card&&draft.ProgressCard==ProgressCardKind.MerchantFleet)
        {
            int good=draft.ChooseCommodity?5+(int)draft.Commodity:(int)draft.Resource;good=Cycle("资源 / 商品",good,Goods);draft.ChooseCommodity=good>=5;if(good<5)draft.Resource=(Resource)good;else draft.Commodity=(Commodity)(good-5);resourceReady&=good>=0;
        }
        if(k==CommandKind.CommercialHarborOffer||pending=="Aqueduct"||card&&draft.ProgressCard==ProgressCardKind.ResourceMonopoly)
        {draft.Resource=(Resource)Cycle("资源",(int)draft.Resource,Goods.Take(5).ToArray());resourceReady&=(int)draft.Resource>=0;}
        if(pending=="ProgressHarbor"||card&&draft.ProgressCard==ProgressCardKind.TradeMonopoly)
        {draft.Commodity=(Commodity)Cycle("商品",(int)draft.Commodity,Goods.Skip(5).ToArray());resourceReady&=(int)draft.Commodity>=0;}
        if(view.PrivateTargetProgressCards!=null&&view.PrivateTargetProgressCards.Length>0)
        {
            var cards=view.PrivateTargetProgressCards.Distinct().ToArray();draft.SelectedProgressCard=cards[Cycle("允许查看的发展卡",Math.Max(0,Array.IndexOf(cards,draft.SelectedProgressCard)),cards.Select(CardName).ToArray())];
            ProgressFace(draft.SelectedProgressCard,"允许查看 · "+CardName(draft.SelectedProgressCard));
        }
        if(k==CommandKind.DiscardProgressCard)
        {
            var cards=view.OwnProgressCards.Distinct().ToArray();if(cards.Length>0){var selected=cards[Cycle("发展卡",Math.Max(0,Array.IndexOf(cards,draft.ProgressCard)),cards.Select(CardName).ToArray())];draft.ProgressCard=selected;draft.SelectedProgressCard=selected;}
        }
        if(TabletopCardInput.Changed)GUIUtility.ExitGUI();
        if(!resourceReady){ModernOperationUI.Info("请点选一种资源或商品。",true);return;}
        GUILayout.Space(8);
        if(ai!=null)
        {
            var preview=Preview(draft);bool legal=preview.Success;
            if(!legal)ModernOperationUI.Info(preview.Message??"请完成所需选择，当前尚不能确认。",true);
            if(ModernOperationUI.LayoutButton(card?"确认使用 · "+CardName(draft.ProgressCard):"确认执行",legal,true)){Submit(draft);GUIUtility.ExitGUI();}
        }
        else if(Btn("确认执行")){Submit(draft);GUIUtility.ExitGUI();}
    }
    void Target(string title,string field,string type)
    {
        string value=field=="source"?draft.SourceId:draft.TargetId;
        if(Btn(title+"："+(ai!=null?(string.IsNullOrEmpty(value)?"未选":"已选中"):(value??"未选"))+" · 点地图")){pickField=field;pickType=type;}
    }
    int Cycle(string title,int value,string[] choices)
    {
        if(choices.Length==0)return 0;
        int resourceStart=title=="商品"?5:title=="资源"||title=="资源 / 商品"||title=="交出类型"||title=="取得类型"?0:-1;
        if(resourceStart>=0)return ModernOperationUI.ResourceKind(title,value,resourceStart,choices.Length);
        value=Mathf.Clamp(value,0,choices.Length-1);
        if(ai!=null)
        {
            int firstGood=title=="商品"?5:title=="资源"||title=="资源 / 商品"||title=="交出类型"||title=="取得类型"?0:-1;
            return ModernOperationUI.Choices(title,value,choices,firstGood);
        }
        GUILayout.BeginHorizontal();Text(title+"："+choices[value]);if(GUILayout.Button("‹",button,GUILayout.Width(36),GUILayout.Height(30)))value=(value+choices.Length-1)%choices.Length;if(GUILayout.Button("›",button,GUILayout.Width(36),GUILayout.Height(30)))value=(value+1)%choices.Length;GUILayout.EndHorizontal();return value;
    }
    void Bag(string title,ResourceBag resources,CommodityBag commodities,bool singleKind=false)
    {
        var selected=Enumerable.Range(0,8).Select(i=>i<5?resources[(Resource)i]:commodities[(Commodity)(i-5)]).ToArray();
        var error=ModernOperationUI.ResourceCards(title,selected,Enumerable.Range(0,8).Select(i=>i<5?19:12).ToArray(),singleKind:singleKind);if(error!=null)status=error;
        for(int i=0;i<8;i++){if(i<5)resources[(Resource)i]=selected[i];else commodities[(Commodity)(i-5)]=selected[i];}
    }
    int TradeRate(int good)
    {
        int rate=4;var locations=view.Board.Settlements.Concat(view.Board.Cities).Where(x=>x.PlayerId==seat).Select(x=>x.LocationId).ToArray();
        foreach(var port in view.Board.Ports.Where(p=>p.Vertices.Any(locations.Contains)))if(!port.Resource.HasValue)rate=Math.Min(rate,3);else if(good<5&&(int)port.Resource.Value==good)rate=2;
        var own=view.Players.First(p=>p.Id==seat);if(good>=5&&(own.Improvements[0]>=3||view.MerchantFleetCommodities.Contains((Commodity)(good-5))))rate=2;
        if(good<5&&(view.MerchantFleetResources.Contains((Resource)good)||view.MerchantPlayerId==seat&&view.Board.Tiles.Any(t=>t.Id==view.MerchantTileId&&t.Resource==new[]{"wood","brick","wool","wheat","ore"}[good])))rate=2;
        return rate;
    }
    static string BagText(ResourceBag r,CommodityBag c)=>string.Join(" / ",Enumerable.Range(0,8).Where(i=>(i<5?(r==null?0:r[(Resource)i]):(c==null?0:c[(Commodity)(i-5)]))>0).Select(i=>Goods[i]+" "+(i<5?r[(Resource)i]:c[(Commodity)(i-5)])));
    static bool IsShip(Command c)=>c.Kind==CommandKind.SetupShip||c.Kind==CommandKind.BuildShip||c.Kind==CommandKind.PlaceFreeShip||c.Kind==CommandKind.MoveShip||c.Kind==CommandKind.ResolveProgressChoice&&c.BuildShip;
    static string CardName(ProgressCardKind c)=>Cards[(int)c];
    static string EventName(EventDie d)=>d==EventDie.Barbarian?"蛮族船":d==EventDie.Trade?"贸易":d==EventDie.Politics?"政治":"科学";
    static string PhaseName(GamePhase phase){switch(phase){case GamePhase.SetupSettlement:return "开局建筑";case GamePhase.SetupRoad:return "开局道路";case GamePhase.ProductionAwaitRoll:return "等待掷骰";case GamePhase.Action:return "行动";case GamePhase.Discard:return "弃牌";case GamePhase.RobberMove:return "移动强盗或海盗";case GamePhase.RobberSteal:return "偷取";case GamePhase.RoadBuilding:return "免费道路";case GamePhase.Finished:return "已结束";default:return "待决选择";}}
    static string DecisionName(string key){switch(key){case "Gold":return "金矿选择资源";case "PillageCity":return "选择被劫掠的城市";case "Metropolis":return "放置大都会";case "DefenderProgress":return "防御奖励";case "Aqueduct":return "水渠选资源";case "DisplacedKnight":return "安置被驱离骑士";case "ProgressDiscard":return "发展卡超限";case "ProgressEspionage":return "间谍选牌";case "ProgressGuildDues":return "行会征费选牌";case "ProgressRoads":return "免费道路或船";case "ProgressHarbor":return "商业港口交商品";case "ProgressTreasonRemove":return "移除自己的骑士";case "ProgressTreasonPlace":return "放置替补骑士";case "ProgressWedding":return "婚礼赠礼";case "ProgressSabotage":return "破坏弃牌";default:return "按提示完成选择";}}
    static string CommandName(CommandKind kind){switch(kind){case CommandKind.SetupShip:return "开局船";case CommandKind.BuildShip:return "建船";case CommandKind.MoveShip:return "移动船";case CommandKind.MovePirate:return "移动海盗";case CommandKind.ChasePirate:return "驱逐海盗";case CommandKind.SetupSettlement:return "开局建筑";case CommandKind.SetupRoad:return "开局道路";case CommandKind.BuildRoad:return "建道路";case CommandKind.BuildSettlement:return "建村庄";case CommandKind.BuildCity:return "升级城市";case CommandKind.BuildWall:return "城墙";case CommandKind.ImproveCity:return "城市设施";case CommandKind.RecruitKnight:return "招募骑士";case CommandKind.PromoteKnight:return "晋升骑士";case CommandKind.ActivateKnight:return "激活骑士";case CommandKind.MoveKnight:return "移动骑士";case CommandKind.DisplaceKnight:return "驱离骑士";case CommandKind.ChaseRobber:return "驱逐强盗";case CommandKind.PlayProgressCard:return "发展卡";case CommandKind.BankTrade:return "银行交易";case CommandKind.ProposeTrade:return "玩家交易";case CommandKind.DiscardResources:return "弃牌";case CommandKind.MoveRobber:return "移动强盗";case CommandKind.StealResource:return "选择偷取对象";default:return "完成待决操作";}}
    static string OptionName(string value){ImprovementTrack track;if(Enum.TryParse(value,out track)&&Enum.IsDefined(typeof(ImprovementTrack),track))return Tracks[(int)track];return value;}
    string ChoiceLabel(Command c)
    {
        string pending=view.PendingDecision.Kind;
        if(pending=="Gold")return BagText(c.Resources,null);
        if(pending=="ProgressRoads")return c.Decline?"结束免费建设":(c.BuildShip?"船 ":"道路 ")+c.TargetId;
        if(pending=="Aqueduct")return Goods[(int)c.Resource];
        if(pending=="DefenderProgress")return Tracks[(int)c.Track];
        if(pending=="ProgressEspionage")return c.Decline?"不取牌":CardName(c.SelectedProgressCard);
        if(pending=="ProgressHarbor")return Goods[5+(int)c.Commodity];
        if(pending=="ProgressTreasonPlace")return c.Decline?"不放置":c.TargetId+" · 等级 "+c.KnightLevel;
        if(pending=="ProgressGuildDues"||pending=="ProgressWedding"||pending=="ProgressSabotage")return BagText(c.Resources,c.Commodities);
        return c.Decline?"放弃可选效果":OptionName(c.TargetId??"")+(c.Kind==CommandKind.DiscardProgressCard?" "+CardName(c.ProgressCard):"");
    }
    public M5LocalGameHost VerificationHost=>host;
    internal PlayerView M6View=>view;
    internal void M6Refresh(){
        if(NetworkGame&&ai.WaitForPresentationHistory(ai.Network.View<PlayerView>()?.Events.LastOrDefault()?.Sequence??0,M6Refresh))return;
        if(ai.PresentPlayedCard(NetworkGame?ai.Network.History:host.History(ai.HumanSeat),M6Refresh))return;
        dock.Reset();seat=ai.HumanSeat;nextSeat=seat;curtain=false;if(NetworkGame)cardArt.Clear();view=NetworkGame?ai.Network.View<PlayerView>():host.View(seat);CancelOperationDraft();Refresh(view);status=view.Phase==GamePhase.Finished?view.WinnerPlayerId+" 获胜。":ai.IsHumanTurn?"轮到 "+seat+"，从面前拿起棋子与卡牌，或点击地图上的己方棋子。":ai.WaitingText;}
    internal void M6Visible(bool value){if(board!=null)board.gameObject.SetActive(value);}
    internal void M6Close(){enabled=false;if(board!=null){board.gameObject.SetActive(false);Destroy(board.gameObject);}Destroy(this);}
    public M5BoardRenderer VerificationBoard=>board;
    public bool VerificationCurtain=>curtain;
    public bool VerificationHasPrivateView=>view!=null;
    public bool VerificationSelectionsCleared=>draft==null&&pickField=="";
    public string VerificationSeat=>seat;
    public void VerificationPreparePrivateSelections(){draft=New(CommandKind.ProposeTrade);draft.Give.Wood=1;pickField="target";}
    public string VerificationPick(string type,Vector2 position)=>Pick(type,position);
    internal void M7PreviewRefresh(){operationView=null;}
    bool NetworkGame=>ai!=null&&ai.NetworkGame;
    M7Preview Preview(Command command)
    {
        if(NetworkGame)return ai.Network.Preview(command);
        var result=host.Preview(command);return new M7Preview(result.Success,result.Message);
    }
}
