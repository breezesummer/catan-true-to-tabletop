using System;
using System.Linq;
using Catan.Core;
using UnityEngine;

// All gameplay is sent through LocalGameHost; this component owns only presentation state.
public sealed class CatanApp : MonoBehaviour
{
    private LocalGameHost host;
    private PlayerView view;
    private BoardRenderer board;
    private string seat="P1", nextSeat="P1", hover="", status="确认席位后，拿起定居点开始。";
    private bool curtain=true, holding, road, help=true, terrain=true;
    private int give, receive=1;
    private Font font;
    private GUIStyle text, small, title, button, badge;
    private bool styled;
    private readonly Color ink=new Color(.90f,.91f,.84f), gold=new Color(.94f,.75f,.4f);
    private static readonly string[] ResourceNames={"木材","砖块","羊毛","谷物","矿石"};
    private static readonly string[] SetupSeats={"P1","P2","P3","P4","P4","P3","P2","P1"};
    private static readonly string[] SetupVertices={"V42","V15","V29","V40","V32","V18","V49","V09"};
    private static readonly string[] SetupEdges={"E60","E17","E41","E56","E48","E27","E69","E08"};
    public void Start()
    {
        Application.targetFrameRate=60;
        host=new LocalGameHost(); view=host.View(seat);
        board=new GameObject("Board").AddComponent<BoardRenderer>(); board.Initialize(view);
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
        gameObject.AddComponent<M1PlayerVerification>().Initialize(this);
    }
    void Update()
    {
        if(host==null)return;
        if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1))CancelPickup();
        if(Input.GetKeyDown(KeyCode.Tab))board.SetCamera(!board.TopView);
        if(Input.mousePosition.x<Screen.width*.74f && Mathf.Abs(Input.mouseScrollDelta.y)>0)board.ChangeZoom(-Input.mouseScrollDelta.y*.012f);
        if(curtain||!holding)return;
        Vector2 mouse=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
        string candidate=ResolveTarget(road,mouse);
        if(candidate!=hover)
        {
            hover=candidate;
            board.Preview(hover,road,hover!="" && host.Preview(MakePlacement(hover)).Success);
        }
        if(Input.GetMouseButtonDown(0)&&mouse.x<Screen.width*.72f&&mouse.y>Screen.height*.15f&&mouse.y<Screen.height*.86f)
        {
            if(hover!="")Drop(hover); else {CancelPickup();status="没有吸附目标，棋子已退回；局面未改变。";}
        }
    }
    string ResolveTarget(bool isRoad,Vector2 mouse)
    {
        var ids=isRoad?view.Board.Edges.Select(e=>e.Id):view.Board.Vertices.Select(v=>v.Id);
        string candidate="";float distance=25f*Screen.width/1440f;
        foreach(var id in ids){float d=Vector2.Distance(mouse,board.ScreenPoint(id));if(d<distance){candidate=id;distance=d;}}
        return candidate;
    }
    Command NewCommand(CommandKind kind,string target=null) => new Command {Id=Guid.NewGuid().ToString("N"),PlayerId=seat,Kind=kind,TargetId=target};
    Command MakePlacement(string target) => NewCommand(road?(view.Phase==GamePhase.SetupRoad?CommandKind.SetupRoad:CommandKind.BuildRoad):CommandKind.SetupSettlement,target);
    public void ConfirmSeat() { seat=nextSeat; view=host.View(seat); curtain=false;status="已就座 "+seat+"。"; }
    public void SwitchSeat(string target) { CancelPickup(); nextSeat=target; view=null; curtain=true; status="私有手牌已遮蔽。"; }
    public void Pickup(bool isRoad) { if(curtain)return; holding=true;road=isRoad;hover=""; status="移动到圆点吸附并预览，单击落下；右键 / Esc 取消。"; }
    public void CancelPickup() {holding=false;hover="";if(board!=null)board.ClearPreview();}
    public CommandResult Drop(string target)
    {
        var cmd=MakePlacement(target);CancelPickup(); return Submit(cmd);
    }
    private CommandResult Submit(Command cmd)
    {
        CancelPickup();
        var result=host.Submit(cmd);
        status=result.Success?"操作完成。":("已退回 · "+result.Message);
        if(result.Success&&cmd.Kind==CommandKind.RollDice)
        {
            var rollEvent=result.Events.Last();status="掷骰 "+rollEvent.Dice1+" + "+rollEvent.Dice2+" = "+(rollEvent.Dice1+rollEvent.Dice2)+"，产出已结算。";
        }
        view=host.View(seat);board.Refresh(view);
        if(result.Success&&view.ActivePlayerId!=seat)SwitchSeat(view.ActivePlayerId);
        return result;
    }
    public void SaveGame() { try{host.Save();status="已保存当前局面（包括待决与随机继续状态）。";}catch(Exception){status="保存失败；请检查存档目录的写入权限。";} }
    public void LoadGame()
    {
        try {host.Load();CancelPickup();var publicView=host.View(seat);board.Refresh(publicView);SwitchSeat(publicView.ActivePlayerId);status="已恢复，请重新确认席位。";}
        catch(Exception){status="无法加载：存档不存在、损坏或版本不兼容。当前局面保留。";}
    }
    static string Reason(string code)
    {
        switch(code) {
            case "DistanceRule":return "与已有定居点过近，至少间隔两条边。";
            case "MustTouchPendingSettlement":return "道路必须连接刚放下的定居点。";
            case "OccupiedEdge":return "这条边已有道路。";
            case "OccupiedVertex":return "这个点已有定居点。";
            case "DisconnectedRoad":return "道路必须连接自己的路网或定居点。";
            case "InsufficientResources":return "资源不足。道路需要 1 木材 + 1 砖块。";
            case "NotActivePlayer":return "请切换到当前行动席位。";
            case "MustRollFirst":return "请先掷骰。";
            case "AlreadyRolled":return "本回合已掷骰。";
            case "PendingDecision":return "请先放下开局道路。";
            case "InvalidTradeRatio":return "本切片仅支持银行同种资源 4:1 交易。";
            case "ControlledQueueExhausted":case "TestConfigurationEnded":return "受控测试已结束，请新开一局。";
            default:return code;
        }
    }
    void Styles()
    {
        if(styled)return;styled=true;
        text=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};text.normal.textColor=ink;
        small=new GUIStyle(text){fontSize=14};title=new GUIStyle(text){fontSize=28,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=17,alignment=TextAnchor.MiddleCenter};
        badge=new GUIStyle(text){fontSize=14,alignment=TextAnchor.MiddleCenter};
    }
    void Panel(Rect rect,Color color) {var prev=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=prev;}
    bool Button(float x,float y,float w,string label) => GUI.Button(new Rect(x,y,w,38),label,button);
    void Label(float x,float y,float w,float h,string label,GUIStyle style=null) => GUI.Label(new Rect(x,y,w,h),label,style??text);
    void OnGUI()
    {
        if(host==null)return;Styles();
        // Resolution-independent 1440 x 900 layout, while targets use the actual camera projection.
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
        Panel(new Rect(0,0,1440,90),new Color(.045f,.09f,.10f));
        Label(32,17,520,40,"CATAN  /  岛屿工坊",title);
        Label(34,57,800,25,"M1 · 本地交互切片     /     固定地图 · 四个测试席位 · 估算尺寸",small);
        if(Button(795,26,115,board.TopView?"斜视 [Tab]":"俯视 [Tab]"))board.SetCamera(!board.TopView);
        if(Button(922,26,105,"地形显隐")){terrain=!terrain;board.ShowTerrain(terrain);}
        Panel(new Rect(1065,90,375,810),new Color(.06f,.115f,.125f));
        if(curtain)
        {
            Panel(new Rect(1090,122,326,325),new Color(.09f,.17f,.18f));
            Label(1110,145,290,46,"请交给 "+nextSeat,title);
            Label(1110,204,277,90,"手牌已遮蔽。\n确认后，仅显示该席位的私有资源。",text);
            if(Button(1110,333,280,"我是 "+nextSeat+" · 查看手牌"))ConfirmSeat();
        }
        else
        {
            DrawBoardLabels(); DrawSeatPanel();
        }
        if(Button(1090,690,151,"保存局面"))SaveGame();
        if(Button(1255,690,151,"载入存档"))LoadGame();
        if(Button(1090,740,151,"新开一局")){host.NewGame();board.Refresh(host.View("P1"));SwitchSeat("P1");}
        if(Button(1255,740,151,"操作帮助"))help=!help;
        Label(1090,800,320,78,"切片范围：尚无强盗、发展卡、港口、城市、奖牌、胜利、AI 或联机。受控骰子用尽即停止。",small);
        Panel(new Rect(0,808,1065,92),new Color(.045f,.09f,.10f));
        Label(30,820,1005,36,status);
        Label(30,859,1005,26,holding?"绿色目标可放置 · 红色预览会退回 · Esc / 右键取消":"滚轮缩放 · Tab 切换视角 · 地形装饰不会拦截逻辑点选择",small);
        if(help&&!curtain)DrawGuide();
        GUI.matrix=Matrix4x4.identity;
    }
    void DrawSeatPanel()
    {
        Label(1090,112,315,35,seat+"  /  "+PhaseName(view.Phase),title);
        var own=view.Players.First(p=>p.PlayerId==seat);
        Label(1090,157,315,40,"第 "+view.Turn+" 回合 · 当前行动 "+view.ActivePlayerId+"\n库存："+own.Pieces.Roads+" 路 / "+own.Pieces.Settlements+" 定居点",small);
        var hand=view.OwnResources;
        for(int i=0;i<5;i++)
        {
            Panel(new Rect(1090+i*65,204,58,70),new Color(.13f,.23f,.24f));
            Label(1091+i*65,212,56,24,ResourceNames[i],badge);
            Label(1091+i*65,243,56,27,hand[(Resource)i].ToString(),badge);
        }
        if(view.Phase==GamePhase.SetupSettlement||view.Phase==GamePhase.SetupRoad)
        {
            bool r=view.Phase==GamePhase.SetupRoad;
            if(Button(1090,296,316,holding?"取消拿起 [Esc]":r?"拿起道路":"拿起定居点")){if(holding)CancelPickup();else Pickup(r);}
        }
        else
        {
            if(Button(1090,296,151,"掷骰"))Submit(NewCommand(CommandKind.RollDice));
            if(Button(1255,296,151,holding?"取消拿起":"拿起道路")){if(holding)CancelPickup();else Pickup(true);}
            Label(1090,345,315,25,"银行交易 · 同种资源 4 : 1",small);
            if(Button(1090,378,151,"交 4 "+ResourceNames[give]))give=(give+1)%5;
            if(Button(1255,378,151,"取 1 "+ResourceNames[receive]))receive=(receive+1)%5;
            if(Button(1090,424,151,"确认交易")){var c=NewCommand(CommandKind.BankTrade);c.GiveResource=(Resource)give;c.ReceiveResource=(Resource)receive;c.GiveAmount=4;c.ReceiveAmount=1;Submit(c);}
            if(Button(1255,424,151,"结束回合"))Submit(NewCommand(CommandKind.EndTurn));
        }
        // The last submit may have raised the privacy curtain and discarded the old view.
        if(view==null)return;
        Label(1090,478,315,25,"公开席位  /  点击换座",small);
        for(int i=0;i<4;i++)
        {
            string player="P"+(i+1);var p=view.Players.First(x=>x.PlayerId==player);
            GUI.backgroundColor=BoardRenderer.SeatColor(player);
            if(Button(1090,510+i*39,316,player+"   "+p.ResourceCount+" 张资源   "+p.VictoryPoints+" 分")){SwitchSeat(player);break;}
            GUI.backgroundColor=Color.white;
        }
        GUI.backgroundColor=Color.white;
    }
    string PhaseName(GamePhase phase)
    {
        switch(phase){case GamePhase.SetupSettlement:return "开局定居点";case GamePhase.SetupRoad:return "开局道路";case GamePhase.ProductionAwaitRoll:return "等待掷骰";default:return "行动阶段";}
    }
    void DrawBoardLabels()
    {
        foreach(var tile in view.Board.Tiles)
        {
            var center=tile.Vertices.Select(board.Position).Aggregate(Vector3.zero,(s,v)=>s+v)/6;
            center.y=tile.Resource=="ore"?.037f:.01f;
            var projected=board.BoardCamera.WorldToScreenPoint(center);
            float x=projected.x*1440/Screen.width,y=(Screen.height-projected.y)*900/Screen.height;
            Panel(new Rect(x-25,y-20,50,40),new Color(.045f,.085f,.09f,.86f));
            Label(x-25,y-21,50,25,tile.Number.HasValue?tile.Number.Value.ToString():"强盗",badge);
            Label(x-25,y+2,50,20,tile.Id,new GUIStyle(badge){fontSize=10});
        }
        if(!holding)return;
        var ids=road?view.Board.Edges.Select(x=>x.Id):view.Board.Vertices.Select(x=>x.Id);
        var legal=road?view.LegalEdgeIds:view.LegalVertexIds;
        foreach(var id in ids)
        {
            var p=board.ScreenPoint(id);p.x*=1440f/Screen.width;p.y*=900f/Screen.height;
            bool valid=legal.Contains(id);Panel(new Rect(p.x-4,p.y-4,8,8),valid?new Color(.4f,.95f,.7f):new Color(.6f,.58f,.51f));
            if(id==hover) {Panel(new Rect(p.x-28,p.y-31,56,24),new Color(.04f,.1f,.1f));Label(p.x-28,p.y-32,56,25,id,badge);}
        }
    }
    void DrawGuide()
    {
        if(view==null)return;
        string guide;
        int count=view.Board.Settlements.Length, roads=view.Board.Roads.Length;
        if(view.Phase==GamePhase.SetupSettlement||view.Phase==GamePhase.SetupRoad)
        {
            int index=Math.Min(roads,7);guide="固定验收向导  "+(index+1)+" / 8\n"+SetupSeats[index]+"：定居点 "+SetupVertices[index]+" → 道路 "+SetupEdges[index];
        }
        else guide="固定验收向导\nP1：掷骰 → 4 木换 1 砖 → 道路 E65 → 结束回合\n然后保存、退出、重启载入，由 P2 掷骰。";
        Panel(new Rect(26,105,640,87),new Color(.045f,.09f,.1f,.92f));Label(41,115,610,73,guide,small);
        string target="";
        if(holding)
        {
            if(view.Phase==GamePhase.SetupSettlement)target=SetupVertices[Math.Min(roads,7)];
            else if(view.Phase==GamePhase.SetupRoad)target=SetupEdges[Math.Min(roads,7)];
            else target="E65";
            var p=board.ScreenPoint(target);float x=p.x*1440/Screen.width,y=p.y*900/Screen.height;
            Panel(new Rect(x-29,y-32,58,24),new Color(.38f,.28f,.09f));Label(x-29,y-33,58,26,target,badge);
        }
    }
    // Explicit test-only bridge. Normal UI never receives an authoritative state or save contents.
    internal LocalGameHost VerificationHost => host;
    internal BoardRenderer VerificationBoard => board;
    internal bool VerificationCurtain => curtain;
    internal bool VerificationHasPrivateView => view!=null;
    internal string VerificationSeat => seat;
    internal string VerificationPick(bool isRoad,Vector2 mouse) => ResolveTarget(isRoad,mouse);
}
