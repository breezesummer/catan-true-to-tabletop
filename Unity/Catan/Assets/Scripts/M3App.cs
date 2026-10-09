using System;
using System.Linq;
using Catan.Core;
using UnityEngine;
using Command = Catan.Core.M3.Command;
using CommandKind = Catan.Core.M3.CommandKind;
using CommandResult = Catan.Core.M3.CommandResult;
using GamePhase = Catan.Core.M3.GamePhase;
using PlayerView = Catan.Core.M3.PlayerView;
using DevelopmentCardKind = Catan.Core.M3.DevelopmentCardKind;

// Local seats share the core command entrance; this component owns presentation only.
public sealed class M3App : MonoBehaviour
{
    private M6App ai;
    private M3LocalGameHost host;
    private PlayerView view;
    private M3BoardRenderer board;
    private string boardLayout;
    private string seat="P1", nextSeat="P1", hover="", status="请确认席位，开始航海家对局。";
    private bool curtain=true, holding, help=true, terrain=true, newGameDialog;
    private CommandKind placement;
    private int tab, give, receive=1, plentyA, plentyB=1, monopoly, tradeTarget=1;
    private bool playerTrading;
    private string shipSource="",selectedScenario="heading-for-new-shores",portSource="";
    private ResourceBag gold=new ResourceBag();
    private int specialSelection;
    private Catan.Core.M3.SeafarersScenario[] catalog;
    private string[] placementLegalIds=Array.Empty<string>();
    private ResourceBag discard=new ResourceBag(), offerGive=new ResourceBag(), offerReceive=new ResourceBag();
    private Font font;
    private GUIStyle text, small, title, button, badge;
    private static readonly string[] ResourceNames={"木材","砖块","羊毛","谷物","矿石"};
    private readonly Color ink=new Color(.9f,.91f,.84f), panelColor=new Color(.06f,.115f,.125f);

    public void Start()
    {
        Application.targetFrameRate=60;
        ai=GetComponent<M6App>();
        host=new M3LocalGameHost(ai==null?4:ai.PlayerCount);
        if(ai!=null)host.NewGame(ai.ScenarioId,ai.PlayerCount);
        if(ai!=null&&!string.IsNullOrEmpty(ai.InitialAuthority))host.ImportSave(ai.InitialAuthority);
        catalog=Catan.Core.M3.SeafarersScenarios.Ids.Select(id=>Catan.Core.M3.SeafarersScenarios.Create(id,4)).ToArray();
        var initial=host.View(ai==null?"P1":ai.HumanSeat);
        seat=initial.ActivePlayerId;nextSeat=seat;
        RefreshBoard(initial);
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
        if(ai==null)gameObject.AddComponent<M3PlayerVerification>().Initialize(this);
        else ai.Connect(this,host);
    }
    void Update()
    {
        if(host==null)return;
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)){CancelPickup();newGameDialog=false;}
        if(Input.GetKeyDown(KeyCode.Tab))board.SetCamera(!board.TopView);
        if(Input.mousePosition.x<Screen.width*.74f&&Mathf.Abs(Input.mouseScrollDelta.y)>0)board.ChangeZoom(-Input.mouseScrollDelta.y*.012f);
        if(curtain||newGameDialog||!holding||view==null||ai!=null&&!ai.IsHumanTurn)return;
        var mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
        var candidate=ResolveTarget(mouse);
        if(candidate!=hover)
        {
            hover=candidate;
            bool valid=hover!=""&&placementLegalIds.Contains(hover);
            if(IsToken(placement))board.PreviewRobber(hover,valid);
            else if(IsShip(placement))board.PreviewShip(hover,valid);
            else board.Preview(hover,IsRoad(placement),valid);
        }
        bool guideHit=help&&mouse.x>24f*Screen.width/1440f&&mouse.x<732f*Screen.width/1440f&&mouse.y>104f*Screen.height/900f&&mouse.y<227f*Screen.height/900f;
        if(Input.GetMouseButtonDown(0)&&!guideHit&&mouse.x<Screen.width*.73f&&mouse.y>Screen.height*.10f&&mouse.y<Screen.height*.895f)
        {
            if(hover!="")Drop(hover);
            else {CancelPickup();status="没有吸附目标，已取消；局面保持不变。";}
        }
    }
    static bool IsShip(CommandKind kind) => kind==CommandKind.SetupShip||kind==CommandKind.BuildShip||kind==CommandKind.PlaceFreeShip||kind==CommandKind.MoveShip;
    static bool IsToken(CommandKind kind) => kind==CommandKind.MoveRobber||kind==CommandKind.MovePirate;
    static bool IsRoad(CommandKind kind) => kind==CommandKind.SetupRoad||kind==CommandKind.BuildRoad||kind==CommandKind.PlaceFreeRoad||IsShip(kind)||kind==CommandKind.PlacePort||kind==CommandKind.SetupPort;
    string[] TargetIds()
    {
        if(IsToken(placement))return view.Board.Tiles.Select(t=>t.Id).ToArray();
        return IsRoad(placement)?view.Board.Edges.Select(e=>e.Id).ToArray():view.Board.Vertices.Select(v=>v.Id).ToArray();
    }
    string[] LegalTargets() => placementLegalIds;
    string[] ComputeLegalTargets()
    {
        if(placement==CommandKind.MoveShip&&string.IsNullOrEmpty(shipSource))return view.MovableShipEdgeIds;
        if(placement==CommandKind.MoveShip)
        {
            var ownBuildings=view.Board.Settlements.Concat(view.Board.Cities).Where(b=>b.PlayerId==seat).Select(b=>b.LocationId);
            var remainingShips=view.Board.Ships.Where(s=>s.PlayerId==seat&&s.LocationId!=shipSource).Select(s=>s.LocationId).ToArray();
            var joins=ownBuildings.Concat(view.Board.Edges.Where(e=>remainingShips.Contains(e.Id)).SelectMany(e=>e.Vertices)).ToArray();
            return view.Board.Edges.Where(e=>e.Vertices.Any(joins.Contains)).Select(e=>e.Id).Where(id=>host.Preview(NewCommand(placement,id)).Success).ToArray();
        }
        if(IsToken(placement))return view.Board.Tiles.Where(t=>placement==CommandKind.MovePirate?t.Resource=="sea":t.Resource!="sea"&&t.Resource!="fog").Select(t=>t.Id).Where(id=>host.Preview(NewCommand(placement,id)).Success).ToArray();
        if(placement==CommandKind.PlacePort||placement==CommandKind.SetupPort)return view.LegalPortEdgeIds;
        if(placement==CommandKind.ChooseInvasionRoute)return view.LegalInvasionRootVertexIds;
        if(IsShip(placement))return view.LegalShipEdgeIds;
        return IsRoad(placement)?view.LegalEdgeIds:placement==CommandKind.BuildCity?view.LegalCityVertexIds:view.LegalVertexIds;
    }
    string ResolveTarget(Vector2 mouse)
    {
        string candidate="";float distance=(IsToken(placement)?46f:25f)*Screen.width/1440f;
        foreach(var id in TargetIds())
        {
            float current=Vector2.Distance(mouse,board.ScreenPoint(id));
            if(current<distance){distance=current;candidate=id;}
        }
        return candidate;
    }
    Command NewCommand(CommandKind kind,string target=null) => new Command{Id=Guid.NewGuid().ToString("N"),PlayerId=seat,Kind=kind,TargetId=target,SourceId=kind==CommandKind.MoveShip?shipSource:kind==CommandKind.PlacePort?portSource:null};
    public void ConfirmSeat()
    {
        seat=nextSeat;view=host.View(seat);curtain=false;status="已就座 "+seat+"。";
        RefreshBoard(view);
    }
    public void SwitchSeat(string target)
    {
        if(ai!=null){status="你控制 "+ai.HumanSeat+"；其他席位为 AI，仅显示公开信息。";return;}
        CancelPickup();nextSeat=target;view=null;curtain=true;
        discard=new ResourceBag();offerGive=new ResourceBag();offerReceive=new ResourceBag();
        gold=new ResourceBag();shipSource="";portSource="";specialSelection=0;
        tab=0;give=0;receive=1;plentyA=0;plentyB=1;monopoly=0;tradeTarget=1;playerTrading=false;
        status="已遮蔽手牌，请将设备交给 "+target+"。";
    }
    public void Pickup(CommandKind kind)
    {
        if(curtain||view==null)return;
        holding=true;placement=kind;hover="";shipSource="";
        placementLegalIds=ComputeLegalTargets();
        status=kind==CommandKind.MoveShip?"先选择绿色标记的可移动船，再选择目标边。":IsToken(kind)?"选择新地块，单击确认；不能留在原地。":"移动到目标吸附并预览，单击放置；右键 / Esc 取消。";
    }
    public void CancelPickup(){holding=false;hover="";shipSource="";placementLegalIds=Array.Empty<string>();if(board!=null)board.ClearPreview();}
    public CommandResult Drop(string target)
    {
        if(placement==CommandKind.MoveShip&&string.IsNullOrEmpty(shipSource)&&view.MovableShipEdgeIds.Contains(target))
        {shipSource=target;hover="";placementLegalIds=ComputeLegalTargets();board.ClearPreview();status="已拿起 "+target+" 的船。选择合法目标边；Esc 取消。";return null;}
        var command=NewCommand(placement,target);return Submit(command);
    }
    public CommandResult Submit(Command command)
    {
        if(ai!=null&&(!ai.IsHumanTurn||command.PlayerId!=ai.HumanSeat)){status="请等待当前 AI 或待决席位完成操作。";return null;}
        CancelPickup();
        var result=host.Submit(command);
        status=result.Success?"操作完成。":"操作未执行："+Reason(result.ErrorCode,result.Message);
        view=host.View(seat);RefreshBoard(view);
        if(result.Success)
        {
            if(command.Kind==CommandKind.RollDice)status="掷骰 "+view.LastDice1+" + "+view.LastDice2+" = "+(view.LastDice1+view.LastDice2)+"。";
            if(view.Phase==GamePhase.Finished)status=view.WinnerPlayerId+" 达到胜利条件，本局结束。";
            string actor=NextActor(view);
            if(ai==null&&actor!=seat&&view.Phase!=GamePhase.Finished)SwitchSeat(actor);
        }
        return result;
    }
    static string NextActor(PlayerView current)
    {
        if(current.TradeOffer!=null)return current.TradeOffer.OtherPlayerId;
        if(current.Discards.Length>0)return current.Discards[0].PlayerId;
        if(current.Phase==GamePhase.GoldChoice&&current.GoldClaims.Length>0)return current.GoldClaims[0].PlayerId;
        if(current.PendingDecision!=null&&!string.IsNullOrEmpty(current.PendingDecision.PlayerId))return current.PendingDecision.PlayerId;
        return current.ActivePlayerId;
    }
    void RefreshBoard(PlayerView current)
    {
        string layout=string.Join("|",current.Board.Tiles.Select(t=>t.Id+":"+t.Resource+":"+t.Number));
        if(board==null||boardLayout!=layout)
        {
            bool existing=board!=null,top=existing&&board.TopView;float zoom=existing?board.Zoom:0;
            if(board!=null){board.gameObject.SetActive(false);Destroy(board.gameObject);}
            board=new GameObject("M3 board").AddComponent<M3BoardRenderer>();
            board.Initialize(current.Board.Tiles,current.Board.Vertices,current.Board.Edges);
            if(existing)board.ChangeZoom(zoom-board.Zoom);board.SetCamera(top);board.ShowTerrain(terrain);boardLayout=layout;
        }
        board.Refresh(current.Board.Settlements,current.Board.Roads,current.Board.Cities,current.Board.RobberTileId);
        board.RefreshSeafarers(current.Board.Ships,current.Board.PirateTileId);
    }
    public void SaveGame()
    {
        if(ai!=null){ai.SaveGame();return;}
        try{host.Save();status="已保存，包括待决选择和随机继续状态。";}
        catch(Exception){status="保存失败，请检查存档目录的写入权限。";}
    }
    public void LoadGame()
    {
        if(ai!=null){ai.LoadGame();return;}
        try
        {
            host.Load();CancelPickup();var restored=host.View("P1");RefreshBoard(restored);
            SwitchSeat(NextActor(restored));status="已恢复局面，请重新确认席位。";
        }
        catch(Exception){status="无法加载：文件不存在、损坏或版本不兼容；当前局面保留。";}
    }
    public void NewGame(int players)
    {
        host.NewGame(selectedScenario,players);var initial=host.View("P1");RefreshBoard(initial);SwitchSeat(initial.ActivePlayerId);newGameDialog=false;
    }
    static string Reason(string code,string fallback)
    {
        switch(code)
        {
            case "NotActivePlayer":return "请换到当前行动席位。";
            case "DistanceRule":return "定居点之间必须至少间隔两条边。";
            case "OccupiedVertex":case "OccupiedEdge":return "此处已有棋子。";
            case "DisconnectedRoad":case "DisconnectedSettlement":return "必须连接自己的道路，且不能穿过他人的建筑。";
            case "MustTouchPendingSettlement":return "开局道路必须连接刚放下的定居点。";
            case "InsufficientResources":return "资源不足，请查看手牌和建造费用。";
            case "MustRollFirst":return "请先掷骰。";
            case "AlreadyRolled":return "本回合已经掷骰。";
            case "PendingDecision":return "请先完成当前待决选择。";
            case "DevelopmentCardBoughtThisTurn":case "NoPlayableCard":return "本回合买入的行动发展卡要等以后回合才能打出。";
            case "DevelopmentCardAlreadyPlayed":return "每回合只能打出一张行动发展卡。";
            case "BankShortage":return "银行中该资源不足。";
            case "InvalidTradeRatio":return "交易比例不符合当前港口权限。";
            case "GameFinished":return "本局已结束。";
            default:return string.IsNullOrEmpty(fallback)?code:fallback;
        }
    }
    void Styles()
    {
        if(text!=null)return;
        text=new GUIStyle(GUI.skin.label){font=font,fontSize=17,wordWrap=true};text.normal.textColor=ink;
        small=new GUIStyle(text){fontSize=13};title=new GUIStyle(text){fontSize=26,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=15,alignment=TextAnchor.MiddleCenter};
        badge=new GUIStyle(text){fontSize=14,alignment=TextAnchor.MiddleCenter};
    }
    static void Panel(Rect rect,Color color){var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;}
    bool Button(float x,float y,float width,string label,float height=34) => GUI.Button(new Rect(x,y,width,height),label,button);
    void Label(float x,float y,float width,float height,string label,GUIStyle style=null) => GUI.Label(new Rect(x,y,width,height),label,style??text);
    void OnGUI()
    {
        if(host==null)return;Styles();
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
        Panel(new Rect(0,0,1440,90),new Color(.045f,.09f,.10f));
        Label(30,15,570,40,"CATAN  /  岛屿工坊",title);
        Label(32,57,740,23,"M3 · 航海家  /  "+(view==null?"本地 3–4 人 · 隐私换座":view.ScenarioName+" · 目标 "+view.TargetVictoryPoints+" 分"),small);
        if(Button(785,27,121,board.TopView?"斜视 [Tab]":"俯视 [Tab]"))board.SetCamera(!board.TopView);
        if(Button(918,27,112,"地形显隐")){terrain=!terrain;board.ShowTerrain(terrain);}
        if(newGameDialog){DrawNewGame();GUI.matrix=Matrix4x4.identity;return;}
        Panel(new Rect(1065,90,375,810),panelColor);
        if(curtain)
        {
            Panel(new Rect(1084,121,337,350),new Color(.09f,.17f,.18f));
            Label(1103,146,296,43,"请交给 "+nextSeat,title);
            Label(1103,209,294,112,"资源和发展卡已遮蔽。\n确认席位后，仅显示该玩家的手牌。\n\n请其他玩家暂时移开视线。");
            if(Button(1103,367,298,"我是 "+nextSeat+" · 查看手牌",44))ConfirmSeat();
        }
        else if(view!=null){DrawBoardLabels();DrawSeatPanel();}
        if(Button(1085,749,160,"保存局面"))SaveGame();
        if(Button(1256,749,160,"载入存档"))LoadGame();
        if(Button(1085,794,160,"新开一局")){if(ai!=null)ai.ShowMenu();else newGameDialog=true;}
        if(Button(1256,794,160,help?"隐藏提示":"规则提示"))help=!help;
        Label(1085,846,335,42,"规则按官方 2025 航海家。\nTab 切换视角 · 滚轮缩放 · Esc 取消",small);
        Panel(new Rect(0,808,1065,92),new Color(.045f,.09f,.10f));
        Label(28,818,1008,40,status);
        Label(28,859,1008,25,holding?"绿色可落点 · 单击放置 · 红色目标将拒绝 · Esc / 右键取消":"建筑需连到自己路网；城市需升级自己的定居点。所有操作均由规则核心验证。",small);
        if(help&&!curtain&&view!=null)DrawGuide();
        if(newGameDialog)DrawNewGame();
        GUI.matrix=Matrix4x4.identity;
    }
    void DrawSeatPanel()
    {
        Label(1085,104,335,36,seat+"  /  "+PhaseName(view.Phase),title);
        var own=view.Players.First(p=>p.Id==seat);
        Label(1085,145,335,28,"第 "+view.Turn+" 回合 · 行动 "+view.ActivePlayerId+" · 自己 "+view.OwnVictoryPoints+" 分",small);
        for(int i=0;i<5;i++)
        {
            Panel(new Rect(1085+i*67,180,60,62),new Color(.13f,.23f,.24f));
            Label(1085+i*67,182,60,22,ResourceNames[i],badge);
            Label(1085+i*67,208,60,27,view.OwnResources[(Resource)i].ToString(),badge);
        }
        Label(1085,248,335,24,"余件 "+own.Pieces.Roads+"路 "+own.ShipsRemaining+"船 "+own.Pieces.Settlements+"村 "+own.Pieces.Cities+"城 · 牌库 "+view.DevelopmentDeckCount,small);
        DrawActions();
        if(view==null)return;
        Label(1085,570,335,23,ai==null?"公开席位  /  点击换座查看各自手牌":"公开席位  /  你："+ai.HumanSeat+" · 其他为 AI",small);
        for(int i=0;i<view.Players.Length;i++)
        {
            var p=view.Players[i];string award=(p.Id==view.LongestRoadPlayerId?" 路王":"")+(p.Id==view.LargestArmyPlayerId?" 军队":"");
            GUI.backgroundColor=M3BoardRenderer.SeatColor(p.Id);
            bool clicked=Button(1085,599+i*34,331,p.Id+"  "+p.VictoryPoints+"分  "+p.ResourceCount+"资源  "+p.DevelopmentCardCount+"卡"+award,30);
            GUI.backgroundColor=Color.white;
            if(clicked){SwitchSeat(p.Id);return;}
        }
    }
    void DrawActions()
    {
        if(ai!=null&&!ai.IsHumanTurn&&view.Phase!=GamePhase.Finished){Label(1085,300,331,115,ai.WaitingText);return;}
        if(view.Phase==GamePhase.Finished)
        {
            Label(1093,304,320,48,view.WinnerPlayerId+" 获胜",title);
            Label(1093,365,315,126,ai==null?"本局已结束。\n可以换座查看各自的最终手牌，或保存这局游戏。\n\n选择“新开一局”再来一局。":"本局已结束。\n可保存这局游戏，或选择“新开一局”更换规则、剧本与席位。");return;
        }
        if(view.TradeOffer!=null){DrawTradeResponse();return;}
        if(view.Phase==GamePhase.GoldChoice){DrawGold();return;}
        if(view.Phase==GamePhase.PortPlacement){DrawPortPlacement();return;}
        if(view.Phase==GamePhase.SetupPort)
        {
            var port=view.NextSetupPort;
            Label(1085,288,331,102,"新世界：轮流安置翻开的港口。\n当前港口："+(port.Resource.HasValue?ResourceNames[(int)port.Resource.Value]+" 2:1":"通用 3:1")+"\n选择绿色沿岸空边；港口之间须保持间隔。");
            if(Button(1085,421,331,"安置当前港口",41))Pickup(CommandKind.SetupPort);
            Label(1085,477,331,69,"港口全部安置后，开始正常定居点和道路 / 船的开局。",small);return;
        }
        if(view.Phase==GamePhase.SetupSettlement||view.Phase==GamePhase.SetupRoad)
        {
            bool road=view.Phase==GamePhase.SetupRoad;
            Label(1085,288,330,65,road?"放置连接刚才定居点的道路或船。\n道路须沿陆地，船须沿海域。":"按剧本顺序放置村与路 / 船。\n最后一轮起始村获得相邻资源。");
            if(Button(1085,367,331,holding?"取消拿起 [Esc]":road?"拿起开局道路":"拿起开局定居点",42))
            {if(holding)CancelPickup();else Pickup(road?CommandKind.SetupRoad:CommandKind.SetupSettlement);}
            if(road&&Button(1085,417,331,"拿起开局船",38))Pickup(CommandKind.SetupShip);
            Label(1085,472,330,76,ai==null?"选择绿色标记并单击放下。\n相邻顶点之间不能同时建村。\n完成后自动遮蔽并提示下一席位。":"选择绿色标记并单击放下。\n相邻顶点之间不能同时建村。\n其他席位由 AI 自动行动；始终只显示你的手牌。",small);return;
        }
        if(view.Phase==GamePhase.Discard){DrawDiscard();return;}
        if(view.Phase==GamePhase.RobberMove)
        {
            Label(1085,291,331,91,"请 "+view.ActivePlayerId+" 移动强盗或海盗。\n强盗阻止陆地产出；海盗封锁船的建造与移动。剧本的限制由核心检查。");
            if(Button(1085,395,160,"移动强盗",39))Pickup(CommandKind.MoveRobber);
            if(Button(1256,395,160,"移动海盗",39))Pickup(CommandKind.MovePirate);
            if(Button(1085,446,331,"将海盗移到外框海域")){Submit(NewCommand(CommandKind.MovePirate,"frame"));return;}
            if(holding&&Button(1085,490,331,"取消选择"))CancelPickup();return;
        }
        if(view.Phase==GamePhase.RobberSteal)
        {
            Label(1085,291,331,66,"选择相邻对手。\n随机抽取一张资源；空手则不偷取。");
            if(view.PendingDecision!=null)
            {
                for(int i=0;i<view.PendingDecision.EligibleVictimIds.Length;i++)
                {
                    string target=view.PendingDecision.EligibleVictimIds[i];
                    if(Button(1085,365+i*42,160,"资源："+target)){var c=NewCommand(CommandKind.StealResource);c.OtherPlayerId=target;Submit(c);return;}
                    if(view.PendingDecision.Token=="pirate"&&Button(1256,365+i*42,160,"布匹："+target)){var c=NewCommand(CommandKind.StealCloth);c.OtherPlayerId=target;Submit(c);return;}
                }
            }
            return;
        }
        if(view.Phase==GamePhase.RoadBuilding)
        {
            Label(1085,291,331,69,"道路建设 · 尚可放 "+view.PendingDecision.RemainingRoads+" 段\n路或船仍须按各自规则合法连接。");
            if(Button(1085,387,160,"免费道路"))Pickup(CommandKind.PlaceFreeRoad);
            if(Button(1256,387,160,"免费船"))Pickup(CommandKind.PlaceFreeShip);
            if(Button(1085,437,331,"结束道路建设")){Submit(NewCommand(CommandKind.FinishRoadBuilding));return;}
            Label(1085,484,331,61,"只有无合法位置或道路库存耗尽时，才可提前结束。",small);return;
        }
        for(int i=0;i<4;i++)if(Button(1085+i*84,283,79,new[]{"行动","交易","发展卡","航海"}[i],30)){tab=i;CancelPickup();}
        switch(tab){case 0:DrawBuildActions();break;case 1:DrawTrade();break;case 2:DrawDevelopment();break;default:DrawSeafaring();break;}
    }
    void DrawBuildActions()
    {
        if(Button(1085,325,160,"掷骰")){Submit(NewCommand(CommandKind.RollDice));return;}
        if(Button(1256,325,160,"结束回合")){Submit(NewCommand(CommandKind.EndTurn));return;}
        if(Button(1085,367,331,"建道路  ·  木 1 + 砖 1"))Pickup(CommandKind.BuildRoad);
        if(Button(1085,410,331,"建定居点  ·  木砖羊麦各 1"))Pickup(CommandKind.BuildSettlement);
        if(Button(1085,453,331,"升级城市  ·  麦 2 + 矿 3"))Pickup(CommandKind.BuildCity);
        if(Button(1085,498,160,"购买发展卡")){Submit(NewCommand(CommandKind.BuyDevelopmentCard));return;}
        if(Button(1256,498,160,"宣告胜利")){Submit(NewCommand(CommandKind.DeclareVictory));return;}
        Label(1085,537,331,28,"发展卡：羊麦矿各 1；本剧本目标 "+view.TargetVictoryPoints+" 分。",small);
    }
    void DrawGold()
    {
        var claim=view.GoldClaims.FirstOrDefault(c=>c.PlayerId==seat);
        if(claim==null){Label(1085,291,331,100,"等待金矿资源选择，请交给 "+view.GoldClaims[0].PlayerId);return;}
        Label(1085,286,331,47,"金矿：可取 "+Math.Min(claim.Amount,view.Bank.Total)+" 张，已选 "+gold.Total+" 张");
        for(int i=0;i<5;i++){float y=338+i*34;Label(1085,y,100,29,ResourceNames[i]);Counter(gold,(Resource)i,1220,y,view.Bank[(Resource)i]);}
        if(Button(1085,516,331,"确认金矿资源")){var c=NewCommand(CommandKind.ChooseGoldResources);c.Resources=gold.Copy();Submit(c);gold=new ResourceBag();}
    }
    void DrawPortPlacement()
    {
        Label(1085,289,331,73,"安置获得的港口。\n选择港口，再选择沿海且连接自己建筑的合法边。");
        if(view.OwnHeldPorts.Length==0)return;
        specialSelection%=view.OwnHeldPorts.Length;var port=view.OwnHeldPorts[specialSelection];
        if(Button(1085,385,331,"选择："+(port.Resource.HasValue?ResourceNames[(int)port.Resource.Value]+" 2:1":"通用 3:1")))specialSelection=(specialSelection+1)%view.OwnHeldPorts.Length;
        if(Button(1085,437,331,"放置此港口")){portSource=port.Id;Pickup(CommandKind.PlacePort);}
    }
    void DrawSeafaring()
    {
        var own=view.Players.First(p=>p.Id==seat);
        if(Button(1085,325,160,"建船 · 木 1 羊 1"))Pickup(CommandKind.BuildShip);
        if(Button(1256,325,160,"移动开口航线的船"))Pickup(CommandKind.MoveShip);
        Label(1085,366,331,48,"每回合至多移动 1 船；本回合新造船不能移。\n路与船须经过自己的村 / 城连接。",small);
        float y=418;
        if(view.OwnHeldPorts.Length>0)
        {
            if(Button(1085,y,331,"安置持有港口")){portSource=view.OwnHeldPorts[0].Id;Pickup(CommandKind.PlacePort);}y+=40;
        }
        var fortress=view.Board.Fortresses.FirstOrDefault(f=>f.PlayerId==seat&&f.Strength>0);
        if(fortress!=null)
        {
            if(Button(1085,y,331,"攻击己方海盗堡垒 · 强度 "+fortress.Strength)){Submit(NewCommand(CommandKind.AttackFortress,fortress.VertexId));return;}y+=40;
            if(view.LegalInvasionRootVertexIds.Length>0)
            {
                if(Button(1085,y,331,"选择西行进攻航线的起点"))Pickup(CommandKind.ChooseInvasionRoute);y+=40;
            }
        }
        string detail="额外分 "+own.BonusVictoryPoints+" · 布匹 "+own.Cloth+" · 奇观层数 "+own.WonderLevel+"\n"+(view.ShipMovedThisTurn?"本回合已移动船":"船移动额度可用");
        if(view.Wonders.Length>0)
        {
            specialSelection%=view.Wonders.Length;var wonder=view.Wonders[specialSelection];
            if(Button(1085,y,331,"奇观："+WonderName(wonder.Id),30))specialSelection=(specialSelection+1)%view.Wonders.Length;y+=35;
            if(Button(1085,y,160,"认领此奇观",30)){Submit(NewCommand(CommandKind.ClaimWonder,wonder.Id));return;}
            if(Button(1256,y,160,"建造一层",30)){Submit(NewCommand(CommandKind.BuildWonder,wonder.Id));return;}y+=37;
            detail="每层："+BagText(wonder.Cost)+"\n条件："+WonderRequirement(wonder.Requirement)+" · 已建 "+own.WonderLevel+" 层";
        }
        Label(1085,y,331,Mathf.Max(25,563-y),detail,small);
    }
    static string WonderName(string id)
    {
        switch(id){case "great-wall":return "长城";case "great-bridge":return "大桥";case "grand-monument":return "纪念碑";case "grand-theater":return "大剧院";case "grand-castle":return "大城堡";default:return id;}
    }
    static string WonderRequirement(string requirement)
    {
        switch(requirement){case "marker-building":return "在相应标记点有建筑";case "port-city-route-five":return "港口城市，且航线至少 5 段";case "two-cities":return "至少 2 座城市";case "city-six-vp":return "至少 1 城且至少 6 分";default:return requirement;}
    }
    int TradeRatio(Resource resource)
    {
        int ratio=4;
        var locations=view.Board.Settlements.Concat(view.Board.Cities).Where(p=>p.PlayerId==seat).Select(p=>p.LocationId).ToArray();
        foreach(var port in view.Board.Ports.Where(p=>p.Vertices.Any(locations.Contains)))
        {if(!port.Resource.HasValue)ratio=Math.Min(ratio,3);else if(port.Resource.Value==resource)ratio=2;}
        return ratio;
    }
    void DrawTrade()
    {
        if(Button(1085,322,331,playerTrading?"切换：银行 / 港口交易":"切换：玩家之间交易",29))playerTrading=!playerTrading;
        if(!playerTrading)
        {
            int ratio=TradeRatio((Resource)give);
            Label(1085,364,331,45,"按你的建筑港口计算比例："+ratio+" : 1");
            if(Button(1085,412,160,"交 "+ratio+" "+ResourceNames[give]))give=(give+1)%5;
            if(Button(1256,412,160,"取 1 "+ResourceNames[receive]))receive=(receive+1)%5;
            if(Button(1085,459,331,"确认银行交易"))
            {
                var c=NewCommand(CommandKind.BankTrade);c.GiveResource=(Resource)give;c.ReceiveResource=(Resource)receive;c.GiveAmount=TradeRatio((Resource)give);c.ReceiveAmount=1;Submit(c);return;
            }
            Label(1085,507,331,49,"银行库存 木 / 砖 / 羊 / 麦 / 矿\n"+string.Join("  /  ",Enumerable.Range(0,5).Select(i=>view.Bank[(Resource)i].ToString())),small);
            return;
        }
        var opponents=view.Players.Where(p=>p.Id!=seat).ToArray();tradeTarget%=opponents.Length;
        if(Button(1085,360,170,"对象："+opponents[tradeTarget].Id,28))tradeTarget=(tradeTarget+1)%opponents.Length;
        if(Button(1267,360,149,"发送提案",28))
        {var c=NewCommand(CommandKind.ProposeTrade);c.OtherPlayerId=opponents[tradeTarget].Id;c.Give=offerGive.Copy();c.Receive=offerReceive.Copy();Submit(c);return;}
        Label(1085,391,330,20,"资源                我给出                 我收到",small);
        for(int i=0;i<5;i++)
        {
            float y=416+i*29;Label(1085,y,64,27,ResourceNames[i],small);
            Counter(offerGive,(Resource)i,1164,y,view.OwnResources[(Resource)i]);Counter(offerReceive,(Resource)i,1304,y,19);
        }
    }
    void Counter(ResourceBag bag,Resource resource,float x,float y,int maximum)
    {
        if(Button(x,y,24,"−",25))bag[resource]=Math.Max(0,bag[resource]-1);
        Label(x+26,y,32,25,bag[resource].ToString(),badge);
        if(Button(x+62,y,24,"+",25))bag[resource]=Math.Min(maximum,bag[resource]+1);
    }
    static string BagText(ResourceBag bag) => string.Join(" · ",Enumerable.Range(0,5).Where(i=>bag[(Resource)i]>0).Select(i=>ResourceNames[i]+" "+bag[(Resource)i]));
    void DrawTradeResponse()
    {
        var offer=view.TradeOffer;
        Label(1085,289,331,33,offer.ProposerId+" → "+offer.OtherPlayerId+" 交易提案");
        Label(1085,334,331,80,offer.ProposerId+" 给出："+BagText(offer.Give)+"\n"+offer.OtherPlayerId+" 给出："+BagText(offer.Receive));
        if(seat==offer.OtherPlayerId)
        {
            if(Button(1085,438,160,"接受交易")){Submit(NewCommand(CommandKind.AcceptTrade));return;}
            if(Button(1256,438,160,"拒绝交易")){Submit(NewCommand(CommandKind.RejectTrade));return;}
        }
        else if(seat==offer.ProposerId)
        {if(Button(1085,438,331,"取消提案")){Submit(NewCommand(CommandKind.CancelTrade));return;}}
        else Label(1085,438,331,59,"等待交易双方确认，请换到相应席位。");
        Label(1085,495,331,58,"提案公开；接受时双方资源重新校验。对方可以拒绝，发起方可以取消。",small);
    }
    void DrawDiscard()
    {
        var required=view.Discards.FirstOrDefault(d=>d.PlayerId==seat);
        if(required==null){Label(1085,291,331,112,"等待其他玩家弃牌。\n请换到 "+view.Discards[0].PlayerId+" 席位完成选择。");return;}
        Label(1085,286,331,47,"掷出 7：需弃 "+required.Amount+" 张，已选 "+discard.Total+" 张");
        for(int i=0;i<5;i++)
        {float y=338+i*34;Label(1085,y,100,29,ResourceNames[i]);Counter(discard,(Resource)i,1220,y,view.OwnResources[(Resource)i]);}
        if(Button(1085,516,331,"确认弃牌")){var c=NewCommand(CommandKind.DiscardResources);c.Resources=discard.Copy();Submit(c);discard=new ResourceBag();}
    }
    int CardCount(DevelopmentCardKind kind) => view.OwnDevelopmentCards.Count(c=>c.Kind==kind);
    void DrawDevelopment()
    {
        if(Button(1085,324,331,"打出骑士  ·  持有 "+CardCount(DevelopmentCardKind.Knight),31)){Submit(NewCommand(CommandKind.PlayKnight));return;}
        if(Button(1085,361,331,"道路建设  ·  持有 "+CardCount(DevelopmentCardKind.RoadBuilding),31)){Submit(NewCommand(CommandKind.PlayRoadBuilding));return;}
        if(Button(1085,398,151,"选 1："+(plentyA<5?ResourceNames[plentyA]:"不取"),29))plentyA=(plentyA+1)%6;
        if(Button(1247,398,169,"选 2："+(plentyB<5?ResourceNames[plentyB]:"不取"),29))plentyB=(plentyB+1)%6;
        if(Button(1085,433,331,"发明（丰收） · 持有 "+CardCount(DevelopmentCardKind.YearOfPlenty),29))
        {var c=NewCommand(CommandKind.PlayYearOfPlenty);c.Resources=new ResourceBag();if(plentyA<5)c.Resources[(Resource)plentyA]++;if(plentyB<5)c.Resources[(Resource)plentyB]++;Submit(c);return;}
        if(Button(1085,468,151,"垄断："+ResourceNames[monopoly],29))monopoly=(monopoly+1)%5;
        if(Button(1247,468,169,"打出垄断 ×"+CardCount(DevelopmentCardKind.Monopoly),29))
        {var c=NewCommand(CommandKind.PlayMonopoly);c.Resource=(Resource)monopoly;Submit(c);return;}
        Label(1085,504,331,63,"隐藏胜利点卡："+CardCount(DevelopmentCardKind.VictoryPoint)+" 张；每回合限 1 张行动卡。\n当回合买入不能打出。发明取 2 张；银行不足 2 张时用“不取”取剩余。",small);
    }
    void DrawBoardLabels()
    {
        foreach(var tile in view.Board.Tiles)
        {
            if(tile.Resource=="sea"||tile.Resource=="water")continue;
            var p=board.ScreenPoint(tile.Id);float x=p.x*1440f/Screen.width,y=p.y*900f/Screen.height;
            Panel(new Rect(x-25,y-20,50,40),new Color(.045f,.085f,.09f,.86f));
            Label(x-25,y-21,50,25,tile.Resource=="fog"?"迷雾":tile.Number.HasValue?tile.Number.Value.ToString()+(tile.Resource=="gold"?" 金":""):"沙漠",badge);
            Label(x-25,y+2,50,20,tile.Id+(tile.Id==view.Board.RobberTileId?" 强盗":""),new GUIStyle(badge){fontSize=10});
        }
        foreach(var village in view.Board.Villages)DrawMarker(village.VertexId,"村 "+village.Number+" · 布 "+village.Cloth,new Color(.25f,.15f,.35f));
        foreach(var fortress in view.Board.Fortresses.Where(f=>f.Strength>0))DrawMarker(fortress.VertexId,fortress.PlayerId+" 堡 "+fortress.Strength,new Color(.35f,.08f,.11f));
        foreach(var fortress in view.Board.Fortresses.Where(f=>!string.IsNullOrEmpty(f.BeachheadVertexId)))DrawMarker(fortress.BeachheadVertexId,fortress.PlayerId+" 滩头",new Color(.12f,.26f,.38f));
        foreach(var fortress in view.Board.Fortresses.Where(f=>!string.IsNullOrEmpty(f.InvasionStartingVertexId)))DrawMarker(fortress.InvasionStartingVertexId,fortress.PlayerId+" 西行起点",new Color(.12f,.26f,.38f));
        foreach(var edge in view.Board.BonusEdgeIds)DrawMarker(edge,"★",new Color(.55f,.4f,.05f));
        foreach(var edge in view.Board.GiftCardEdgeIds)DrawMarker(edge,"礼",new Color(.10f,.4f,.35f));
        foreach(var port in view.Board.GiftPorts)DrawMarker(port.EdgeId,port.Resource.HasValue?ResourceNames[(int)port.Resource.Value]+"港礼":"港礼",new Color(.10f,.4f,.35f));
        foreach(var wonder in view.Wonders)foreach(var vertex in wonder.VertexIds)DrawMarker(vertex,WonderName(wonder.Id),new Color(.4f,.3f,.08f));
        if(!string.IsNullOrEmpty(view.Board.PirateTileId))DrawMarker(view.Board.PirateTileId,"海盗",new Color(.25f,.05f,.3f));
        if(view.Board.RobberTileId=="frame")DrawMarker("frame","强盗",new Color(.08f,.075f,.09f),true);
        foreach(var port in view.Board.Ports)
        {
            var center=(board.Position(port.Vertices[0])+board.Position(port.Vertices[1]))*.5f;
            center*=1.14f;var p=board.BoardCamera.WorldToScreenPoint(center);
            float x=p.x*1440f/Screen.width,y=(Screen.height-p.y)*900f/Screen.height;
            Panel(new Rect(x-36,y-12,72,24),new Color(.11f,.21f,.27f,.94f));
            Label(x-36,y-12,72,24,port.Resource.HasValue?ResourceNames[(int)port.Resource.Value]+" 2:1":"港口 3:1",badge);
        }
        if(!holding)return;
        var legal=LegalTargets();
        foreach(var id in TargetIds())
        {
            var p=board.ScreenPoint(id);p.x*=1440f/Screen.width;p.y*=900f/Screen.height;
            Panel(new Rect(p.x-4,p.y-4,8,8),legal.Contains(id)?new Color(.4f,.95f,.7f):new Color(.6f,.58f,.51f));
            if(id==hover){Panel(new Rect(p.x-28,p.y-31,56,24),new Color(.04f,.1f,.1f));Label(p.x-28,p.y-32,56,25,id,badge);}
        }
    }
    void DrawMarker(string location,string label,Color color,bool frameRobber=false)
    {
        var p=frameRobber?board.FrameRobberScreenPoint():board.ScreenPoint(location);float x=p.x*1440f/Screen.width,y=p.y*900f/Screen.height;
        float width=Math.Max(25,label.Length*12);Panel(new Rect(x-width/2,y-10,width,21),color);Label(x-width/2,y-11,width,22,label,small);
    }
    void DrawGuide()
    {
        string guide;
        switch(view.Phase)
        {
            case GamePhase.SetupSettlement:guide="开局：按提示席位顺序放村和路 / 船，遵守本剧本的起始区域。\n最后一轮起始村获得邻近资源；村之间至少相隔两条边。";break;
            case GamePhase.SetupRoad:guide=ai==null?"开局路 / 船：连接刚放下的村。\n完成后自动交给下一席位；确认后才会显示其手牌。":"开局路 / 船：连接刚放下的村。\n始终只显示你的手牌；其他席位由 AI 自动行动。";break;
            case GamePhase.SetupPort:guide="新世界：每位玩家轮流安置当前翻开的港口。\n合法沿海边以绿色标记；放完港口后才开始放村。";break;
            case GamePhase.Discard:guide=ai==null?"掷出 7：手牌超过 7 张者各弃一半（向下取整）。\n依次换座完成弃牌后，由当前玩家移动强盗。":"掷出 7：手牌超过 7 张者各弃一半（向下取整）。\nAI 会自动弃牌；轮到你时在右侧选择要弃的资源。";break;
            case GamePhase.RobberMove:case GamePhase.RobberSteal:guide="强盗所在的地块不产出。移动后，选择一名相邻对手，随机偷取一张资源；若对方空手则不偷取。";break;
            case GamePhase.GoldChoice:guide=ai==null?"金矿：选择银行现有的任意资源，数量与提示一致。\n需多人领取时依次换座；选择完成后回到原先阶段。":"金矿：选择银行现有的任意资源，数量与提示一致。\n轮到你时在右侧选择资源；其他席位由 AI 自动处理。";break;
            case GamePhase.PortPlacement:guide="遗忘部落赠送港口：选择有己方沿岸建筑的合法空边安置。";break;
            default:guide="回合：掷骰 → 交易 / 建造 / 航海 / 发展卡 → 结束回合。\n船沿海连接，路船转换须经过己方建筑；金矿产出自选资源。\n"+(view.ScenarioId=="wonders-of-catan"?"奇观建成 4 层即胜，或至少 10 分且层数领先所有对手。":"目标 "+view.TargetVictoryPoints+" 分；海盗岛还须夺回自己的堡垒。");break;
        }
        Panel(new Rect(24,104,708,123),new Color(.045f,.09f,.1f,.92f));
        Label(39,115,678,103,guide,small);
        string dice=view.LastDice1>0?"骰子 "+view.LastDice1+" + "+view.LastDice2:"尚未掷骰";
        var own=view.Players.First(p=>p.Id==seat);
        Label(768,114,267,103,dice+"\n行动席位 "+view.ActivePlayerId+"\n自己最长道路 "+own.LongestRoadLength+" 段\n已用骑士 "+own.PlayedKnights+" 张",small);
    }
    static string PhaseName(GamePhase phase)
    {
        switch(phase)
        {
            case GamePhase.SetupSettlement:return "开局建村";case GamePhase.SetupRoad:return "开局路 / 船";
            case GamePhase.ProductionAwaitRoll:return "等待掷骰";case GamePhase.Discard:return "弃牌";
            case GamePhase.RobberMove:return "移动强盗";case GamePhase.RobberSteal:return "随机偷取";
            case GamePhase.RoadBuilding:return "道路建设";case GamePhase.GoldChoice:return "金矿选择";case GamePhase.PortPlacement:return "安置港口";case GamePhase.SetupPort:return "开局安置港口";case GamePhase.Finished:return "对局结束";default:return "行动阶段";
        }
    }
    void DrawNewGame()
    {
        Panel(new Rect(0,90,1440,810),new Color(.025f,.06f,.07f,.98f));
        Label(230,126,990,54,"选择航海家剧本",title);
        Label(230,185,990,44,"官方 2025 单扩展规则 · 当前局面不会自动保存，请先保存需要保留的对局。",small);
        for(int i=0;i<catalog.Length;i++)
        {
            var scenario=catalog[i];
            GUI.backgroundColor=selectedScenario==scenario.Id?new Color(.25f,.7f,.65f):Color.white;
            if(Button(230+(i%3)*330,250+(i/3)*67,310,scenario.Name,52))selectedScenario=scenario.Id;
        }
        GUI.backgroundColor=Color.white;
        var selected=catalog.First(s=>s.Id==selectedScenario);
        Label(230,473,970,110,selected.Description+(selected.Id=="wonders-of-catan"?"\n胜利：建成 4 层，或至少 10 分且奇观层数领先所有对手。":"\n胜利目标："+selected.TargetVictoryPoints+" 分。"),small);
        if(Button(230,606,310,"以 3 人开始",47))NewGame(3);
        if(Button(560,606,310,"以 4 人开始",47))NewGame(4);
        if(Button(890,606,310,"返回当前对局",47))newGameDialog=false;
    }
    // Opt-in runtime verification only; no authoritative state is exposed to the UI.
    internal M3LocalGameHost VerificationHost => host;
    internal PlayerView M6View=>view;
    internal void M6Refresh()
    {
        CancelPickup();seat=ai.HumanSeat;nextSeat=seat;curtain=false;view=host.View(seat);
        gold=new ResourceBag();portSource="";specialSelection=0;
        discard=new ResourceBag();offerGive=new ResourceBag();offerReceive=new ResourceBag();
        RefreshBoard(view);
        status=view.Phase==GamePhase.Finished?view.WinnerPlayerId+" 获胜。":ai.IsHumanTurn?"轮到 "+seat+"，请完成右侧的行动或待决选择。":ai.WaitingText;
    }
    internal void M6Visible(bool value){if(board!=null)board.gameObject.SetActive(value);}
    internal void M6Close(){enabled=false;if(board!=null){board.gameObject.SetActive(false);Destroy(board.gameObject);}Destroy(this);}
    internal M3BoardRenderer VerificationBoard => board;
    internal bool VerificationCurtain => curtain;
    internal bool VerificationHasPrivateView => view!=null;
    internal string VerificationSeat => seat;
    internal bool VerificationSelectionsCleared => discard.Total==0&&offerGive.Total==0&&offerReceive.Total==0&&gold.Total==0&&shipSource=="";
    internal void VerificationPreparePrivateSelections(){discard.Wood=1;offerGive.Brick=2;offerReceive.Wool=3;}
    internal string VerificationPick(CommandKind kind,Vector2 mouse){placement=kind;return ResolveTarget(mouse);}
}

