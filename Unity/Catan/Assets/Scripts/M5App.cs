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
public sealed class M5App : MonoBehaviour
{
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
    GUIStyle label,small,heading,button;
    static readonly string[] Goods={"木材","砖","羊毛","谷物","矿石","布匹","钱币","纸张"};
    static readonly string[] Tracks={"贸易 · 布匹","政治 · 钱币","科学 · 纸张"};
    static readonly string[] Cards={"炼金术","起重机","工程学","发明","灌溉","医学","采矿","印刷术","道路建设","锻造","商业港口","行会征费","商人","商船队","资源垄断","商品垄断","外交","间谍","鼓舞","谋略","征税","宪法","叛变","婚礼","破坏"};
    static readonly string[] CardHelp={
        "掷骰前指定红、白产出骰；事件骰照常随机掷出。","选择轨道，立即以少一张商品的费用改良一次。","选择自己的城市，免费建一面城墙。","选择两个地块交换数字；2、6、8、12 不能交换。","每个邻接自己建筑的麦田取 2 谷物；每个地块只算一次。","选择定居点，以 1 谷物和 2 矿石升级城市。","每个邻接自己建筑的山地取 2 矿石。","抽到后自动公开，得 1 分。","免费建设两条道路或船，逐条选择。","选择最多两名骑士免费晋升，每名每回合最多一次。","本回合可向每个对手提供一次资源，换取其自选商品。","选择分数高于自己的玩家，查看其手牌并取两张。","选择邻接自己建筑的资源地块（金矿除外），取得商人及 1 分，并以 2:1 交易该资源。","本回合所选资源或商品可按 2:1 交易。","每名对手交出所选资源最多两张。","每名对手交出所选商品最多一张。","移除一条开放道路或船；若是自己的，可立即免费重建同种棋子。","选择对手，仅你可查看其进步卡，并可取一张。","免费激活所有未激活骑士；本回合新激活者不能行动。","选择连接自己道路或船的对手骑士，将其驱离。","首次蛮族攻击后，将强盗移到新地块，向每名邻接对手各随机取一张。","抽到后自动公开，得 1 分。","对手选择移除一名骑士，你可招募同级或更低且同状态的骑士。","分数高于你的每名对手自选两张资源或商品给你。","分数不少于你的对手各弃掉一半资源和商品。"};

    public void Start()
    {
        Application.targetFrameRate=60;host=new M5LocalGameHost();
        var initial=host.View("P1");nextSeat=initial.ActivePlayerId;Refresh(initial);
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
        gameObject.AddComponent<M5PlayerVerification>().Initialize(this);
    }
    void Styles()
    {
        if(label!=null)return;
        label=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};label.normal.textColor=new Color(.91f,.92f,.86f);
        small=new GUIStyle(label){fontSize=13};heading=new GUIStyle(label){fontSize=22,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=15,wordWrap=true};
    }
    public static string Actor(PlayerView v)
    {
        if(v.TradeOffer!=null)return v.TradeOffer.OtherPlayerId;
        if(v.Discards.Length>0)return v.Discards[0].PlayerId;
        return v.PendingDecision!=null&&!string.IsNullOrEmpty(v.PendingDecision.PlayerId)?v.PendingDecision.PlayerId:v.ActivePlayerId;
    }
    public void SwitchSeat(string target)
    {
        nextSeat=target;curtain=true;view=null;draft=null;pickField="";scroll=Vector2.zero;tab=0;board.ClearPreview();
        status="手牌与私有选择已遮蔽，请将设备交给 "+target+"。";
    }
    public void ConfirmSeat(){seat=nextSeat;curtain=false;view=host.View(seat);Refresh(view);}
    public CommandResult Submit(Command command)
    {
        command.Id=string.IsNullOrEmpty(command.Id)?Guid.NewGuid().ToString("N"):command.Id;
        var result=host.Submit(command);status=result.Success?"操作完成。":result.Message;
        pickField="";board.ClearPreview();view=host.View(seat);Refresh(view);
        if(result.Success)
        {
            draft=null;
            if(view.Phase==GamePhase.Finished)status=view.WinnerPlayerId+" 达到 "+view.TargetVictoryPoints+" 分，本局结束。";
            else if(Actor(view)!=seat)SwitchSeat(Actor(view));
        }
        return result;
    }
    Command New(CommandKind kind,string target=null)=>new Command{Id=Guid.NewGuid().ToString("N"),PlayerId=seat,Kind=kind,TargetId=target,Resources=new ResourceBag(),Commodities=new CommodityBag(),Give=new ResourceBag(),Receive=new ResourceBag(),GiveCommodities=new CommodityBag(),ReceiveCommodities=new CommodityBag(),ChosenDice1=1,ChosenDice2=1};
    void Select(CommandKind kind){draft=New(kind);pickField="";scroll=Vector2.zero;}
    void Refresh(PlayerView v)
    {
        summary=new PublicSummary{ScenarioName=v.ScenarioName,ActivePlayerId=v.ActivePlayerId,TargetVictoryPoints=v.TargetVictoryPoints,Turn=v.Turn,BarbarianPosition=v.BarbarianPosition,LastDice1=v.LastDice1,LastDice2=v.LastDice2,LastEventDie=v.LastEventDie,Players=v.Players};
        string next=v.ScenarioId+"|"+string.Join("|",v.Board.Tiles.Select(t=>t.Id+":"+t.Resource));
        if(board==null||next!=layout)
        {
            bool top=board!=null&&board.TopView;
            if(board!=null){board.gameObject.SetActive(false);Destroy(board.gameObject);}
            board=new GameObject("M5 board").AddComponent<M5BoardRenderer>();board.Initialize(v.Board.Tiles,v.Board.Vertices,v.Board.Edges);
            board.SetCamera(top);layout=next;
        }
        board.Refresh(v);board.ShowTerrain(terrain);
    }
    public void SaveGame(){try{host.Save();status="已保存完整局面和待决选择。";}catch(Exception){status="保存失败，请检查存档目录。";}}
    public void LoadGame(){try{host.Load();var v=host.View("P1");Refresh(v);SwitchSeat(Actor(v));status="已恢复，请确认席位。";}catch(Exception){status="存档无法读取或版本不兼容，当前局面保留。";}}
    void Update()
    {
        if(board==null)return;
        if(Input.GetKeyDown(KeyCode.Tab))board.SetCamera(!board.TopView);
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)){pickField="";board.ClearPreview();newGame=false;}
        if(Input.mousePosition.x<Screen.width*.70f&&Input.mouseScrollDelta.y!=0)board.ChangeZoom(-Input.mouseScrollDelta.y*.012f);
        if(curtain||newGame||draft==null||pickField=="")return;
        var mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
        if(mouse.x>Screen.width*.70f||mouse.y<Screen.height*.15f||mouse.y>Screen.height*.90f)return;
        string target=Pick(pickType,mouse);
        if(target!="")
        {
            bool valid=true;
            if(pickField=="target") {string old=draft.TargetId;draft.TargetId=target;valid=host.Preview(draft).Success;draft.TargetId=old;}
            if(pickType=="T")board.PreviewRobber(target,valid);else if(IsShip(draft))board.PreviewShip(target,valid);else board.Preview(target,pickType=="E",valid);
            if(Input.GetMouseButtonDown(0))
            {
                if(pickField=="source")draft.SourceId=target;else draft.TargetId=target;
                pickField="";board.ClearPreview();status="已选择 "+target+"；检查面板后确认。";
            }
        }
    }
    string Pick(string type,Vector2 mouse)
    {
        string[] ids=type=="E"?view.Board.Edges.Select(x=>x.Id).ToArray():type=="T"?view.Board.Tiles.Select(x=>x.Id).ToArray():view.Board.Vertices.Select(x=>x.Id).ToArray();
        string nearest="";float distance=(type=="T"?42:25)*Screen.width/1440f;
        foreach(var id in ids){float d=Vector2.Distance(mouse,board.ScreenPoint(id));if(d<distance){distance=d;nearest=id;}}
        return nearest;
    }
    void Panel(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    bool Btn(string text)=>GUILayout.Button(text,button,GUILayout.MinHeight(32));
    void Text(string value,bool minor=false)=>GUILayout.Label(value,minor?small:label);
    void OnGUI()
    {
        if(host==null)return;Styles();GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
        Panel(new Rect(0,0,1440,125),new Color(.045f,.09f,.10f));
        GUI.Label(new Rect(25,15,700,36),"CATAN  /  航海家 × 城市与骑士",heading);
        var publicView=summary;
        GUI.Label(new Rect(25,54,760,25),"M5 · "+publicView.ScenarioName+" · 官方 2025 组合 · "+publicView.TargetVictoryPoints+" 分获胜",small);
        if(GUI.Button(new Rect(800,18,180,34),board.TopView?"斜视 [Tab]":"俯视 [Tab]",button))board.SetCamera(!board.TopView);
        if(GUI.Button(new Rect(800,60,180,32),"地形显隐",button)){terrain=!terrain;board.ShowTerrain(terrain);}
        GUI.Label(new Rect(25,90,960,28),"第 "+publicView.Turn+" 回合 · 行动 "+publicView.ActivePlayerId+"     蛮族 "+publicView.BarbarianPosition+" / 7     红骰 "+publicView.LastDice1+" + 白骰 "+publicView.LastDice2+"     事件 "+EventName(publicView.LastEventDie),label);
        Panel(new Rect(1015,0,425,900),new Color(.065f,.12f,.135f));
        if(newGame)
        {
            GUILayout.BeginArea(new Rect(1040,80,370,660));Text("新开一局会替换当前未保存局面。");
            selectedScenario=new[]{"heading-for-new-shores","through-the-desert"}[Cycle("组合剧本",selectedScenario=="heading-for-new-shores"?0:1,new[]{"驶向新海岸","穿越沙漠"})];
            Text("两个剧本均支持 3–4 人，目标 16 分。新岛奖励、金矿、商品、骑士和进步卡共同生效。",true);
            foreach(int n in new[]{3,4})if(Btn(n+" 人新局")){host.NewGame(selectedScenario,n);var v=host.View("P1");Refresh(v);SwitchSeat(v.ActivePlayerId);newGame=false;}
            GUILayout.Space(16);Text("组合兼容范围",true);Text("本阶段按 2025 城市与骑士规则第 12 页开放驶向新海岸、穿越沙漠。四岛、迷雾岛、遗忘部落、布匹、海盗岛、奇迹及新世界暂不开放组合；其单扩展版本可在 M3 游玩。",true);
            if(Btn("返回当前局"))newGame=false;GUILayout.EndArea();GUI.matrix=Matrix4x4.identity;return;
        }
        if(curtain)
        {
            GUILayout.BeginArea(new Rect(1040,80,370,340));GUILayout.Label("请交给 "+nextSeat,heading);Text("资源、商品、进步卡和窥看信息均已遮蔽。\n\n请其他玩家暂时移开视线。");GUILayout.Space(30);
            if(Btn("我是 "+nextSeat+" · 查看手牌"))ConfirmSeat();GUILayout.EndArea();
        }
        else
        {
            DrawBoardLabels();GUILayout.BeginArea(new Rect(1032,12,390,724));
            GUILayout.Label(seat+" · "+PhaseName(view.Phase)+" · "+view.OwnVictoryPoints+" 分",heading);
            Text(string.Join("  ",Enumerable.Range(0,8).Select(i=>Goods[i]+" "+Own(i))),true);
            var own=view.Players.First(p=>p.Id==seat);Text("改良 贸易 "+own.Improvements[0]+" / 政治 "+own.Improvements[1]+" / 科学 "+own.Improvements[2]+"   城墙余 "+own.WallSupply,true);
            Text("路 "+own.Pieces.Roads+" / 船 "+own.ShipsRemaining+" / 村 "+own.Pieces.Settlements+" / 城 "+own.Pieces.Cities+"   骑士余 "+string.Join("/",own.KnightSupply),true);
            Text("防御 "+view.Board.Knights.Where(k=>k.Active).Sum(k=>k.Level)+" / 蛮族力量 "+view.Board.Cities.Length+" · "+(view.FirstBarbarianAttack?"强盗 / 海盗已启用":"强盗 / 海盗尚未启用"),true);
            scroll=GUILayout.BeginScrollView(scroll);
            DrawControls();GUILayout.EndScrollView();GUILayout.EndArea();
        }
        GUILayout.BeginArea(new Rect(1032,749,390,142));GUILayout.BeginHorizontal();if(Btn("保存"))SaveGame();if(Btn("恢复"))LoadGame();if(Btn("新开一局"))newGame=true;GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();foreach(var p in publicView.Players)if(Btn(p.Id+"\n"+p.VictoryPoints+"分 / "+p.ResourceCount+"牌")){SwitchSeat(p.Id);break;}GUILayout.EndHorizontal();
        Text("点击席位换座 · Tab 视角 · 滚轮缩放 · Esc 取消",true);GUILayout.EndArea();
        Panel(new Rect(0,812,1015,88),new Color(.045f,.09f,.10f));GUI.Label(new Rect(24,826,970,58),pickField==""?status:"请在棋盘选择"+(pickType=="V"?"交点":pickType=="E"?"道路":"地块")+"，然后在右侧确认；Esc 取消。",label);
        GUI.matrix=Matrix4x4.identity;
    }
    int Own(int i)=>i<5?view.OwnResources[(Resource)i]:view.OwnCommodities[(Commodity)(i-5)];
    void DrawBoardLabels()
    {
        foreach(var tile in view.Board.Tiles)
        {
            var p=board.ScreenPoint(tile.Id);p=new Vector2(p.x*1440/Screen.width,p.y*900/Screen.height);
            if(tile.Resource!="sea")GUI.Label(new Rect(p.x-30,p.y-16,70,24),(tile.Number>0?tile.Number.ToString():"沙漠")+(tile.Resource=="gold"?" 金":""),heading);
        }
        if(pickField!="")
        {
            var ids=pickType=="V"?view.Board.Vertices.Select(v=>v.Id):pickType=="E"?view.Board.Edges.Select(e=>e.Id):view.Board.Tiles.Select(t=>t.Id);
            foreach(var id in ids){var p=board.ScreenPoint(id);p=new Vector2(p.x*1440/Screen.width,p.y*900/Screen.height);GUI.Label(new Rect(p.x-25,p.y+8,65,20),id,small);}
        }
        foreach(var port in view.Board.Ports)
        {
            var a=board.ScreenPoint(port.Vertices[0]);var b=board.ScreenPoint(port.Vertices[1]);var p=(a+b)*.5f;p=new Vector2(p.x*1440/Screen.width,p.y*900/Screen.height);
            GUI.Label(new Rect(p.x-40,p.y+15,100,22),port.Resource.HasValue?Goods[(int)port.Resource.Value]+" 2:1":"港口 3:1",small);
        }
        foreach(var k in view.Board.Knights)
        {
            var p=board.ScreenPoint(k.LocationId);p=new Vector2(p.x*1440/Screen.width,p.y*900/Screen.height);
            GUI.Label(new Rect(p.x-26,p.y-36,86,21),k.PlayerId+" 骑"+k.Level+(k.Active?" ●":" ○"),small);
        }
    }
    void DrawControls()
    {
        if(view.Phase==GamePhase.Finished){Text(view.WinnerPlayerId+" 获胜。可保存或新开一局。");return;}
        if(view.TradeOffer!=null)
        {
            var t=view.TradeOffer;Text(t.ProposerId+" 向 "+t.OtherPlayerId+" 提议：\n付出 "+BagText(t.Give,t.GiveCommodities)+"\n换取 "+BagText(t.Receive,t.ReceiveCommodities));
            if(Btn("接受交易")){Submit(New(CommandKind.AcceptTrade));return;}if(Btn("拒绝交易")){Submit(New(CommandKind.RejectTrade));return;}if(Btn("发起者取消")){Submit(New(CommandKind.CancelTrade));return;}return;
        }
        if(view.PendingDecision!=null)Text("待决 · "+DecisionName(view.PendingDecision.Kind)+" · "+view.PendingDecision.PlayerId+(view.PendingDecision.Amount>0?" · 数量 "+view.PendingDecision.Amount:""));
        if(view.Phase==GamePhase.SetupSettlement||view.Phase==GamePhase.SetupRoad)
        {
            Text("第一轮定居点，逆序第二轮城市；第二轮每邻接地块取一张资源。可选择道路或船，必须邻接刚放下的建筑。",true);
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
            int chosen=GUILayout.Toolbar(tab,new[]{"行动","骑士","改良","交易","进步卡"},button);if(chosen!=tab){tab=chosen;draft=null;pickField="";}
            if(tab==0)
            {
                GUILayout.BeginHorizontal();if(Btn("掷骰")){Submit(New(CommandKind.RollDice));GUIUtility.ExitGUI();}if(Btn("结束回合")){Submit(New(CommandKind.EndTurn));GUIUtility.ExitGUI();}GUILayout.EndHorizontal();
                ActionButton("道路 · 木1 砖1",CommandKind.BuildRoad);ActionButton("定居点 · 木砖羊麦各1",CommandKind.BuildSettlement);ActionButton("城市 · 麦2 矿3",CommandKind.BuildCity);ActionButton("城墙 · 砖2",CommandKind.BuildWall);
                ActionButton("船 · 木1 羊1",CommandKind.BuildShip);ActionButton("移动开口航线末端的船",CommandKind.MoveShip);
                Text("每回合至多移 1 船；刚造的船不能移。骑士可沿连续道路和船移动；最长航线的路船接续须经过己方村或城。",true);
            }
            if(tab==1)
            {
                Text("激活花费 1 谷物；本回合刚激活的骑士不能行动。行动后可再激活。",true);
                ActionButton("招募 · 羊1 矿1",CommandKind.RecruitKnight);ActionButton("晋升 · 羊1 矿1",CommandKind.PromoteKnight);ActionButton("激活 · 麦1",CommandKind.ActivateKnight);ActionButton("移动骑士",CommandKind.MoveKnight);ActionButton("驱离弱骑士",CommandKind.DisplaceKnight);ActionButton("驱逐强盗",CommandKind.ChaseRobber);
                ActionButton("驱逐海盗",CommandKind.ChasePirate);
            }
            if(tab==2){Text("每级费用等于新等级。三级获得能力；四级/五级争夺大都会。",true);ActionButton("城市改良",CommandKind.ImproveCity);}
            if(tab==3){ActionButton("银行与港口交易",CommandKind.BankTrade);ActionButton("玩家交易",CommandKind.ProposeTrade);}
            if(tab==4)
            {
                Text("进步卡可当回合使用。仅炼金术在掷骰前使用；回合结束最多留 4 张。",true);
                foreach(var card in view.OwnProgressCards.Distinct())
                {
                    if(Btn(CardName(card)+" ×"+view.OwnProgressCards.Count(c=>c==card))){draft=New(CommandKind.PlayProgressCard);draft.ProgressCard=card;}
                }
                if(view.OwnProgressCards.Length==0)Text("当前没有进步卡。",true);
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
        GUILayout.Space(10);Text("当前选择 · "+CommandName(draft.Kind));
        var k=draft.Kind;var pending=view.PendingDecision==null?"":view.PendingDecision.Kind;
        bool card=k==CommandKind.PlayProgressCard;
        if(card){Text(CardName(draft.ProgressCard)+"："+CardHelp[(int)draft.ProgressCard]);}
        if(k==CommandKind.ImproveCity||card&&draft.ProgressCard==ProgressCardKind.Crane||pending=="DefenderProgress")draft.Track=(ImprovementTrack)Cycle("轨道",(int)draft.Track,Tracks);
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
            int give=draft.GiveIsCommodity?5+(int)draft.GiveCommodity:(int)draft.GiveResource;give=Cycle("交出类型",give,Goods);draft.GiveIsCommodity=give>=5;if(give<5)draft.GiveResource=(Resource)give;else draft.GiveCommodity=(Commodity)(give-5);
            Text("当前最优比例 "+TradeRate(give)+" : 1",true);
            int receive=draft.ReceiveIsCommodity?5+(int)draft.ReceiveCommodity:(int)draft.ReceiveResource;receive=Cycle("取得类型",receive,Goods);draft.ReceiveIsCommodity=receive>=5;if(receive<5)draft.ReceiveResource=(Resource)receive;else draft.ReceiveCommodity=(Commodity)(receive-5);
            draft.GiveAmount=Count("交出张数",draft.GiveAmount,19);draft.ReceiveAmount=Count("取得张数",draft.ReceiveAmount,19);
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
            for(int i=0;i<5;i++)draft.Resources[(Resource)i]=Count(Goods[i]+" / 银行 "+view.Bank[(Resource)i],draft.Resources[(Resource)i],view.Bank[(Resource)i]);
        }
        if(card&&draft.ProgressCard==ProgressCardKind.MerchantFleet)
        {
            int good=draft.ChooseCommodity?5+(int)draft.Commodity:(int)draft.Resource;good=Cycle("资源 / 商品",good,Goods);draft.ChooseCommodity=good>=5;if(good<5)draft.Resource=(Resource)good;else draft.Commodity=(Commodity)(good-5);
        }
        if(k==CommandKind.CommercialHarborOffer||pending=="Aqueduct"||card&&draft.ProgressCard==ProgressCardKind.ResourceMonopoly)
            draft.Resource=(Resource)Cycle("资源",(int)draft.Resource,Goods.Take(5).ToArray());
        if(pending=="ProgressHarbor"||card&&draft.ProgressCard==ProgressCardKind.TradeMonopoly)
            draft.Commodity=(Commodity)Cycle("商品",(int)draft.Commodity,Goods.Skip(5).ToArray());
        if(view.PrivateTargetProgressCards!=null&&view.PrivateTargetProgressCards.Length>0)
        {
            var cards=view.PrivateTargetProgressCards.Distinct().ToArray();draft.SelectedProgressCard=cards[Cycle("允许查看的进步卡",Math.Max(0,Array.IndexOf(cards,draft.SelectedProgressCard)),cards.Select(CardName).ToArray())];
        }
        if(k==CommandKind.DiscardProgressCard)
        {
            var cards=view.OwnProgressCards.Distinct().ToArray();if(cards.Length>0){var selected=cards[Cycle("进步卡",Math.Max(0,Array.IndexOf(cards,draft.ProgressCard)),cards.Select(CardName).ToArray())];draft.ProgressCard=selected;draft.SelectedProgressCard=selected;}
        }
        if(Btn("确认执行")){Submit(draft);GUIUtility.ExitGUI();}
        if(card&&Btn("弃置此进步卡")){var c=New(CommandKind.DiscardProgressCard);c.ProgressCard=draft.ProgressCard;c.SelectedProgressCard=draft.ProgressCard;Submit(c);GUIUtility.ExitGUI();}
        if(Btn("取消选择")){draft=null;pickField="";board.ClearPreview();}
    }
    void Target(string title,string field,string type)
    {
        string value=field=="source"?draft.SourceId:draft.TargetId;
        if(Btn(title+"："+(value??"未选")+" · 点棋盘")){pickField=field;pickType=type;}
    }
    int Cycle(string title,int value,string[] choices)
    {
        if(choices.Length==0)return 0;value=Mathf.Clamp(value,0,choices.Length-1);
        GUILayout.BeginHorizontal();Text(title+"："+choices[value]);if(GUILayout.Button("‹",button,GUILayout.Width(36),GUILayout.Height(30)))value=(value+choices.Length-1)%choices.Length;if(GUILayout.Button("›",button,GUILayout.Width(36),GUILayout.Height(30)))value=(value+1)%choices.Length;GUILayout.EndHorizontal();return value;
    }
    int Count(string name,int value,int max)
    {
        GUILayout.BeginHorizontal();Text(name+"  "+value);if(GUILayout.Button("−",button,GUILayout.Width(36),GUILayout.Height(28)))value=Math.Max(0,value-1);if(GUILayout.Button("+",button,GUILayout.Width(36),GUILayout.Height(28)))value=Math.Min(max,value+1);GUILayout.EndHorizontal();return value;
    }
    void Bag(string title,ResourceBag resources,CommodityBag commodities)
    {
        Text(title+" · 共 "+(resources.Total+commodities.Total)+" 张",true);
        for(int i=0;i<8;i++){int n=i<5?resources[(Resource)i]:commodities[(Commodity)(i-5)];n=Count(Goods[i],n,i<5?19:12);if(i<5)resources[(Resource)i]=n;else commodities[(Commodity)(i-5)]=n;}
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
    static bool IsShip(Command c)=>c.Kind==CommandKind.SetupShip||c.Kind==CommandKind.BuildShip||c.Kind==CommandKind.MoveShip||c.Kind==CommandKind.ResolveProgressChoice&&c.BuildShip;
    static string CardName(ProgressCardKind c)=>Cards[(int)c];
    static string EventName(EventDie d)=>d==EventDie.Barbarian?"蛮族船":d==EventDie.Trade?"贸易":d==EventDie.Politics?"政治":"科学";
    static string PhaseName(GamePhase phase){switch(phase){case GamePhase.SetupSettlement:return "开局建筑";case GamePhase.SetupRoad:return "开局道路";case GamePhase.ProductionAwaitRoll:return "等待掷骰";case GamePhase.Action:return "行动";case GamePhase.Discard:return "弃牌";case GamePhase.RobberMove:return "移动强盗或海盗";case GamePhase.RobberSteal:return "偷取";case GamePhase.RoadBuilding:return "免费道路";case GamePhase.Finished:return "已结束";default:return "待决选择";}}
    static string DecisionName(string key){switch(key){case "Gold":return "金矿选择资源";case "PillageCity":return "选择被劫掠的城市";case "Metropolis":return "放置大都会";case "DefenderProgress":return "防御奖励";case "Aqueduct":return "水渠选资源";case "DisplacedKnight":return "安置被驱离骑士";case "ProgressDiscard":return "进步卡超限";case "ProgressEspionage":return "间谍选牌";case "ProgressGuildDues":return "行会征费选牌";case "ProgressRoads":return "免费道路或船";case "ProgressHarbor":return "商业港口交商品";case "ProgressTreasonRemove":return "移除自己的骑士";case "ProgressTreasonPlace":return "放置替补骑士";case "ProgressWedding":return "婚礼赠礼";case "ProgressSabotage":return "破坏弃牌";default:return "按提示完成选择";}}
    static string CommandName(CommandKind kind){switch(kind){case CommandKind.SetupShip:return "开局船";case CommandKind.BuildShip:return "建船";case CommandKind.MoveShip:return "移动船";case CommandKind.MovePirate:return "移动海盗";case CommandKind.ChasePirate:return "驱逐海盗";case CommandKind.SetupSettlement:return "开局建筑";case CommandKind.SetupRoad:return "开局道路";case CommandKind.BuildRoad:return "建道路";case CommandKind.BuildSettlement:return "建定居点";case CommandKind.BuildCity:return "升级城市";case CommandKind.BuildWall:return "城墙";case CommandKind.ImproveCity:return "城市改良";case CommandKind.RecruitKnight:return "招募骑士";case CommandKind.PromoteKnight:return "晋升骑士";case CommandKind.ActivateKnight:return "激活骑士";case CommandKind.MoveKnight:return "移动骑士";case CommandKind.DisplaceKnight:return "驱离骑士";case CommandKind.ChaseRobber:return "驱逐强盗";case CommandKind.PlayProgressCard:return "进步卡";case CommandKind.BankTrade:return "银行交易";case CommandKind.ProposeTrade:return "玩家交易";case CommandKind.DiscardResources:return "弃牌";case CommandKind.MoveRobber:return "移动强盗";case CommandKind.StealResource:return "选择偷取对象";default:return "完成待决操作";}}
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
    public M5BoardRenderer VerificationBoard=>board;
    public bool VerificationCurtain=>curtain;
    public bool VerificationHasPrivateView=>view!=null;
    public bool VerificationSelectionsCleared=>draft==null&&pickField=="";
    public string VerificationSeat=>seat;
    public void VerificationPreparePrivateSelections(){draft=New(CommandKind.ProposeTrade);draft.Give.Wood=1;pickField="target";}
    public string VerificationPick(string type,Vector2 position)=>Pick(type,position);
}
