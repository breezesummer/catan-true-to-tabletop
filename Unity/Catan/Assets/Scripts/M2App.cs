using System;
using System.Linq;
using Catan.Core;
using UnityEngine;
using Command = Catan.Core.M2.Command;
using CommandKind = Catan.Core.M2.CommandKind;
using CommandResult = Catan.Core.M2.CommandResult;
using GamePhase = Catan.Core.M2.GamePhase;
using PlayerView = Catan.Core.M2.PlayerView;
using DevelopmentCardKind = Catan.Core.M2.DevelopmentCardKind;

// Local seats share the core command entrance; this component owns presentation only.
public sealed class M2App : MonoBehaviour
{
    private M2LocalGameHost host;
    private PlayerView view;
    private BoardRenderer board;
    private string boardLayout;
    private string seat="P1", nextSeat="P1", hover="", status="请确认席位，开始正常基础版对局。";
    private bool curtain=true, holding, help=true, terrain=true, newGameDialog;
    private CommandKind placement;
    private int tab, give, receive=1, plentyA, plentyB=1, monopoly, tradeTarget=1;
    private bool playerTrading;
    private ResourceBag discard=new ResourceBag(), offerGive=new ResourceBag(), offerReceive=new ResourceBag();
    private Font font;
    private GUIStyle text, small, title, button, badge;
    private static readonly string[] ResourceNames={"木材","砖块","羊毛","谷物","矿石"};
    private readonly Color ink=new Color(.9f,.91f,.84f), panelColor=new Color(.06f,.115f,.125f);

    public void Start()
    {
        Application.targetFrameRate=60;
        host=new M2LocalGameHost();
        var initial=host.View("P1");
        seat=initial.ActivePlayerId;nextSeat=seat;
        RefreshBoard(initial);
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
        gameObject.AddComponent<M2PlayerVerification>().Initialize(this);
    }
    void Update()
    {
        if(host==null)return;
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)){CancelPickup();newGameDialog=false;}
        if(Input.GetKeyDown(KeyCode.Tab))board.SetCamera(!board.TopView);
        if(Input.mousePosition.x<Screen.width*.74f&&Mathf.Abs(Input.mouseScrollDelta.y)>0)board.ChangeZoom(-Input.mouseScrollDelta.y*.012f);
        if(curtain||newGameDialog||!holding||view==null)return;
        var mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
        var candidate=ResolveTarget(mouse);
        if(candidate!=hover)
        {
            hover=candidate;
            bool valid=hover!=""&&host.Preview(NewCommand(placement,hover)).Success;
            if(placement==CommandKind.MoveRobber)board.PreviewRobber(hover,valid);
            else board.Preview(hover,IsRoad(placement),valid);
        }
        bool guideHit=help&&mouse.x>24f*Screen.width/1440f&&mouse.x<732f*Screen.width/1440f&&mouse.y>104f*Screen.height/900f&&mouse.y<227f*Screen.height/900f;
        if(Input.GetMouseButtonDown(0)&&!guideHit&&mouse.x<Screen.width*.73f&&mouse.y>Screen.height*.10f&&mouse.y<Screen.height*.895f)
        {
            if(hover!="")Drop(hover);
            else {CancelPickup();status="没有吸附目标，已取消；局面保持不变。";}
        }
    }
    static bool IsRoad(CommandKind kind) => kind==CommandKind.SetupRoad||kind==CommandKind.BuildRoad||kind==CommandKind.PlaceFreeRoad;
    string[] TargetIds()
    {
        if(placement==CommandKind.MoveRobber)return view.Board.Tiles.Select(t=>t.Id).ToArray();
        return IsRoad(placement)?view.Board.Edges.Select(e=>e.Id).ToArray():view.Board.Vertices.Select(v=>v.Id).ToArray();
    }
    string[] LegalTargets()
    {
        if(placement==CommandKind.MoveRobber)return view.Board.Tiles.Where(t=>t.Id!=view.Board.RobberTileId).Select(t=>t.Id).ToArray();
        return IsRoad(placement)?view.LegalEdgeIds:placement==CommandKind.BuildCity?view.LegalCityVertexIds:view.LegalVertexIds;
    }
    string ResolveTarget(Vector2 mouse)
    {
        string candidate="";float distance=(placement==CommandKind.MoveRobber?46f:25f)*Screen.width/1440f;
        foreach(var id in TargetIds())
        {
            float current=Vector2.Distance(mouse,board.ScreenPoint(id));
            if(current<distance){distance=current;candidate=id;}
        }
        return candidate;
    }
    Command NewCommand(CommandKind kind,string target=null) => new Command{Id=Guid.NewGuid().ToString("N"),PlayerId=seat,Kind=kind,TargetId=target};
    public void ConfirmSeat()
    {
        seat=nextSeat;view=host.View(seat);curtain=false;status="已就座 "+seat+"。";
        RefreshBoard(view);
    }
    public void SwitchSeat(string target)
    {
        CancelPickup();nextSeat=target;view=null;curtain=true;
        discard=new ResourceBag();offerGive=new ResourceBag();offerReceive=new ResourceBag();
        tab=0;give=0;receive=1;plentyA=0;plentyB=1;monopoly=0;tradeTarget=1;playerTrading=false;
        status="已遮蔽手牌，请将设备交给 "+target+"。";
    }
    public void Pickup(CommandKind kind)
    {
        if(curtain||view==null)return;
        holding=true;placement=kind;hover="";
        status=kind==CommandKind.MoveRobber?"选择强盗的新地块，单击确认；不能留在原地。":"移动到目标吸附并预览，单击放置；右键 / Esc 取消。";
    }
    public void CancelPickup(){holding=false;hover="";if(board!=null)board.ClearPreview();}
    public CommandResult Drop(string target){var command=NewCommand(placement,target);return Submit(command);}
    public CommandResult Submit(Command command)
    {
        CancelPickup();
        var result=host.Submit(command);
        status=result.Success?"操作完成。":"操作未执行："+Reason(result.ErrorCode,result.Message);
        view=host.View(seat);RefreshBoard(view);
        if(result.Success)
        {
            if(command.Kind==CommandKind.RollDice)status="掷骰 "+view.LastDice1+" + "+view.LastDice2+" = "+(view.LastDice1+view.LastDice2)+"。";
            if(view.Phase==GamePhase.Finished)status=view.WinnerPlayerId+" 达到胜利条件，本局结束。";
            string actor=NextActor(view);
            if(actor!=seat&&view.Phase!=GamePhase.Finished)SwitchSeat(actor);
        }
        return result;
    }
    static string NextActor(PlayerView current)
    {
        if(current.TradeOffer!=null)return current.TradeOffer.OtherPlayerId;
        if(current.Discards.Length>0)return current.Discards[0].PlayerId;
        if(current.PendingDecision!=null&&!string.IsNullOrEmpty(current.PendingDecision.PlayerId))return current.PendingDecision.PlayerId;
        return current.ActivePlayerId;
    }
    void RefreshBoard(PlayerView current)
    {
        string layout=string.Join("|",current.Board.Tiles.Select(t=>t.Id+":"+t.Resource+":"+t.Number));
        if(board==null||boardLayout!=layout)
        {
            bool top=board!=null&&board.TopView;float zoom=board==null?.255f:board.Zoom;
            if(board!=null){board.gameObject.SetActive(false);Destroy(board.gameObject);}
            board=new GameObject("M2 board").AddComponent<BoardRenderer>();
            board.Initialize(current.Board.Tiles,current.Board.Vertices,current.Board.Edges);
            board.ChangeZoom(zoom-board.Zoom);board.SetCamera(top);board.ShowTerrain(terrain);boardLayout=layout;
        }
        board.Refresh(current.Board.Settlements,current.Board.Roads,current.Board.Cities,current.Board.RobberTileId);
    }
    public void SaveGame()
    {
        try{host.Save();status="已保存，包括待决选择和随机继续状态。";}
        catch(Exception){status="保存失败，请检查存档目录的写入权限。";}
    }
    public void LoadGame()
    {
        try
        {
            host.Load();CancelPickup();var restored=host.View("P1");RefreshBoard(restored);
            SwitchSeat(NextActor(restored));status="已恢复局面，请重新确认席位。";
        }
        catch(Exception){status="无法加载：文件不存在、损坏或版本不兼容；当前局面保留。";}
    }
    public void NewGame(int players)
    {
        host.NewGame(players);var initial=host.View("P1");RefreshBoard(initial);SwitchSeat(initial.ActivePlayerId);newGameDialog=false;
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
        Label(32,57,680,23,"M2 · 基础版完整规则     /     本地 3–4 人 · 隐私换座",small);
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
        if(Button(1085,794,160,"新开一局"))newGameDialog=true;
        if(Button(1256,794,160,help?"隐藏提示":"规则提示"))help=!help;
        Label(1085,846,335,42,"规则按官方 2025 基础版。\nTab 切换视角 · 滚轮缩放 · Esc 取消",small);
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
        Label(1085,248,335,24,"余件 "+own.Pieces.Roads+" 路 / "+own.Pieces.Settlements+" 村 / "+own.Pieces.Cities+" 城 · 牌库 "+view.DevelopmentDeckCount,small);
        DrawActions();
        if(view==null)return;
        Label(1085,570,335,23,"公开席位  /  点击换座查看各自手牌",small);
        for(int i=0;i<view.Players.Length;i++)
        {
            var p=view.Players[i];string award=(p.Id==view.LongestRoadPlayerId?" 路王":"")+(p.Id==view.LargestArmyPlayerId?" 军队":"");
            GUI.backgroundColor=BoardRenderer.SeatColor(p.Id);
            bool clicked=Button(1085,599+i*34,331,p.Id+"  "+p.VictoryPoints+"分  "+p.ResourceCount+"资源  "+p.DevelopmentCardCount+"卡"+award,30);
            GUI.backgroundColor=Color.white;
            if(clicked){SwitchSeat(p.Id);return;}
        }
    }
    void DrawActions()
    {
        if(view.Phase==GamePhase.Finished)
        {
            Label(1093,304,320,48,view.WinnerPlayerId+" 获胜",title);
            Label(1093,365,315,126,"本局已结束。\n可以换座查看各自的最终手牌，或保存这局游戏。\n\n选择“新开一局”再来一局。");return;
        }
        if(view.TradeOffer!=null){DrawTradeResponse();return;}
        if(view.Phase==GamePhase.SetupSettlement||view.Phase==GamePhase.SetupRoad)
        {
            bool road=view.Phase==GamePhase.SetupRoad;
            Label(1085,288,330,65,road?"放置连接刚才定居点的道路。":"按顺序各放两组村与路。\n第二个定居点获得相邻地块资源。");
            if(Button(1085,367,331,holding?"取消拿起 [Esc]":road?"拿起开局道路":"拿起开局定居点",42))
            {if(holding)CancelPickup();else Pickup(road?CommandKind.SetupRoad:CommandKind.SetupSettlement);}
            Label(1085,429,330,96,"选择绿色标记并单击放下。\n相邻顶点之间不能同时建村。\n完成后会自动遮蔽并提示下一席位。",small);return;
        }
        if(view.Phase==GamePhase.Discard){DrawDiscard();return;}
        if(view.Phase==GamePhase.RobberMove)
        {
            Label(1085,291,331,91,"请 "+view.ActivePlayerId+" 将强盗移到另一地块。\n被占地块停止产出，然后选择一位相邻对手；若其空手则不偷取。");
            if(Button(1085,410,331,holding?"取消选择":"移动强盗",42)){if(holding)CancelPickup();else Pickup(CommandKind.MoveRobber);}return;
        }
        if(view.Phase==GamePhase.RobberSteal)
        {
            Label(1085,291,331,66,"选择相邻对手。\n随机抽取一张资源；空手则不偷取。");
            if(view.PendingDecision!=null)
            {
                for(int i=0;i<view.PendingDecision.EligibleVictimIds.Length;i++)
                {
                    string target=view.PendingDecision.EligibleVictimIds[i];
                    if(Button(1085,375+i*44,331,"选择 "+target)){var c=NewCommand(CommandKind.StealResource);c.OtherPlayerId=target;Submit(c);return;}
                }
            }
            return;
        }
        if(view.Phase==GamePhase.RoadBuilding)
        {
            Label(1085,291,331,69,"道路建设 · 尚可放 "+view.PendingDecision.RemainingRoads+" 条\n每条道路仍须合法连接自己的路网。");
            if(Button(1085,387,331,holding?"取消拿起":"拿起免费道路")){if(holding)CancelPickup();else Pickup(CommandKind.PlaceFreeRoad);}
            if(Button(1085,437,331,"结束道路建设")){Submit(NewCommand(CommandKind.FinishRoadBuilding));return;}
            Label(1085,484,331,61,"只有无合法位置或道路库存耗尽时，才可提前结束。",small);return;
        }
        for(int i=0;i<3;i++)if(Button(1085+i*112,283,107,new[]{"行动","交易","发展卡"}[i],30)){tab=i;CancelPickup();}
        switch(tab){case 0:DrawBuildActions();break;case 1:DrawTrade();break;default:DrawDevelopment();break;}
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
        Label(1085,537,331,28,"发展卡：羊麦矿各 1；自己的回合达到 10 分获胜。",small);
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
            var p=board.ScreenPoint(tile.Id);float x=p.x*1440f/Screen.width,y=p.y*900f/Screen.height;
            Panel(new Rect(x-25,y-20,50,40),new Color(.045f,.085f,.09f,.86f));
            Label(x-25,y-21,50,25,tile.Number.HasValue?tile.Number.Value.ToString():"沙漠",badge);
            Label(x-25,y+2,50,20,tile.Id+(tile.Id==view.Board.RobberTileId?" 强盗":""),new GUIStyle(badge){fontSize=10});
        }
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
    void DrawGuide()
    {
        string guide;
        switch(view.Phase)
        {
            case GamePhase.SetupSettlement:guide="开局：依次放村和路，再按逆序各放一组。\n第二个村获得邻近资源；村之间至少相隔两条边。";break;
            case GamePhase.SetupRoad:guide="开局道路：连接刚放下的村。\n完成后自动交给下一席位；确认后才会显示其手牌。";break;
            case GamePhase.Discard:guide="掷出 7：手牌超过 7 张者各弃一半（向下取整）。\n依次换座完成弃牌后，由当前玩家移动强盗。";break;
            case GamePhase.RobberMove:case GamePhase.RobberSteal:guide="强盗所在的地块不产出。移动后，选择一名相邻对手，随机偷取一张资源；若对方空手则不偷取。";break;
            default:guide="回合：掷骰 → 交易 / 建造 / 发展卡 → 结束回合。\n路王至少 5 段、最大军队至少 3 骑士，各值 2 分。\n村 1 分，城 2 分；自己的回合达到 10 分获胜。";break;
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
            case GamePhase.SetupSettlement:return "开局建村";case GamePhase.SetupRoad:return "开局道路";
            case GamePhase.ProductionAwaitRoll:return "等待掷骰";case GamePhase.Discard:return "弃牌";
            case GamePhase.RobberMove:return "移动强盗";case GamePhase.RobberSteal:return "随机偷取";
            case GamePhase.RoadBuilding:return "道路建设";case GamePhase.Finished:return "对局结束";default:return "行动阶段";
        }
    }
    void DrawNewGame()
    {
        Panel(new Rect(0,90,1440,810),new Color(.025f,.06f,.07f,.98f));
        Label(455,238,580,54,"开始新的本地对局",title);
        Label(455,309,530,90,"采用正常开局、随机起始玩家和随机骰子。\n当前局面不会自动保存，请先保存需要保留的对局。");
        if(Button(455,426,240,"3 人对局",47))NewGame(3);
        if(Button(718,426,240,"4 人对局",47))NewGame(4);
        if(Button(455,493,503,"返回当前对局",41))newGameDialog=false;
    }
    // Opt-in runtime verification only; no authoritative state is exposed to the UI.
    internal M2LocalGameHost VerificationHost => host;
    internal BoardRenderer VerificationBoard => board;
    internal bool VerificationCurtain => curtain;
    internal bool VerificationHasPrivateView => view!=null;
    internal string VerificationSeat => seat;
    internal bool VerificationSelectionsCleared => discard.Total==0&&offerGive.Total==0&&offerReceive.Total==0;
    internal void VerificationPreparePrivateSelections(){discard.Wood=1;offerGive.Brick=2;offerReceive.Wool=3;}
    internal string VerificationPick(CommandKind kind,Vector2 mouse){placement=kind;return ResolveTarget(mouse);}
}
