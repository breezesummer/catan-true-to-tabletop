using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 席位与分数均来自当前 PlayerView；对手只展示牌背、总数及公开分数。
public sealed class TabletopSeat
{
    public string Id;
    public int Points, Resources, Cards, Roads, Settlements, Cities, Ships;
    public int ResourceHandLimit = 7, SpecialHandLimit;
    public int[] Improvements;
    public TabletopScoreBreakdown Score;
}

public sealed partial class TabletopUI
{
    public static readonly Rect ContextRect = new Rect(982, 315, 432, 390);
    public static readonly Rect EndTurnRect = new Rect(1284,760,130,130);
    internal static readonly Rect SpecialRect = new Rect(1278,744,144,43);
    readonly List<Rect> hits = new List<Rect>();
    internal Rect[] VerificationHudRects => hits.ToArray();
    readonly List<TabletopWorldObjects.Hit> worldHits = new List<TabletopWorldObjects.Hit>();
    TabletopScene table;
    public void BindTable(TabletopScene scene) { table=scene; }
    GUIStyle text, small, center, title, button;
    Vector2 scroll;
    bool contextVisible;
    public bool ContextVisible=>contextVisible;
    Vector2? contextAnchor;
    public Rect CurrentContextRect => contextAnchor.HasValue?new Rect(Mathf.Clamp(contextAnchor.Value.x+14,16,1440-ContextRect.width-16),Mathf.Clamp(contextAnchor.Value.y+10,16,900-ContextRect.height-16),ContextRect.width,ContextRect.height):ContextRect;
    public void SetContextAnchor(Vector2? anchor){contextAnchor=anchor;}
    static Texture2D disc;
    readonly TabletopDicePresentation dice = new TabletopDicePresentation();
    readonly TabletopHistoryUI history = new TabletopHistoryUI();
    readonly TabletopBankUI bank = new TabletopBankUI();
    readonly TabletopDefenseUI defense = new TabletopDefenseUI();
    public void BindDefense(int position,int strength,int threat) { defense.Bind(position,strength,threat); }
    public void BindLongestRoad(string playerId,int length) { defense.BindLongestRoad(playerId,length); }
    internal static readonly Rect TurnBannerRect = new Rect(524,9,392,82);
    public TabletopCollectionTimeline Collections => bank.Collections;
    public void BindCollectionOcclusion(Action<TabletopCollectionFlight> draw) { bank.OccludeHand=draw; }
    TabletopScoreBreakdown hoveredScore;
    Rect scoreAnchor;
    string hoveredPlayerHint;
    Rect playerHintAnchor;
    OperationSnapshot operations;
    Action purchaseDevelopment;
    Catan.Core.GameHistoryEntry[] bankHistory = Array.Empty<Catan.Core.GameHistoryEntry>();
    public void BindOperations(OperationSnapshot snapshot,Action purchase,Catan.Core.GameHistoryEntry[] entries)
    {operations=snapshot;purchaseDevelopment=purchase;bankHistory=entries??Array.Empty<Catan.Core.GameHistoryEntry>();}
    public bool HistoryOpen => history.IsOpen;
    public bool HandFlightsVisible { get; set; }=true;
    public bool TryCloseOverlay()=>history.TryClose();
    // 待决/卡牌操作占用右侧区域时收起常驻历史；完整历史模态仍独占输入。
    bool HistoryBlocksInput(Vector2 p) => (history.IsOpen||!contextVisible)&&history.BlocksInput(p);
    public bool BlocksOverlayInput(Vector2 p) => HistoryBlocksInput(p)||contextVisible&&CurrentContextRect.Contains(p);
    public void DrawHistory(Font font,Catan.Core.GameHistoryEntry[] entries,string[] players)
    {if(!history.IsOpen)bank.DrawFlights(HandFlightsVisible);if(history.IsOpen||!contextVisible)history.Draw(font,entries,players);if(!history.IsOpen){DrawScoreDetails();DrawPlayerHint();}}
    public bool DiceRolling => dice.IsRolling(Time.unscaledTimeAsDouble);
    public void ObserveDice(long sequence,int first,int second,int? eventDie)
    {dice.Observe(sequence,first,second,eventDie,Time.unscaledTimeAsDouble);}
    static readonly Color Cream = new Color(.96f,.88f,.70f), Ink = new Color(.24f,.12f,.06f);
    public static readonly Color HotNumberColor = new Color(1f,.10f,.075f);
    public bool BlocksBoardInput(Vector2 p) => !TabletopScene.BoardArea.Contains(p) || HistoryBlocksInput(p) || defense.BlocksInput(p) || hits.Any(r=>r.Contains(p)) || worldHits.Any(h=>h.Contains(p)) || contextVisible && CurrentContextRect.Contains(p);
    internal static string[] OpponentOrder(string ownSeat,System.Collections.Generic.IEnumerable<string> ids)
    {
        var ordered=M6App.OrderedPlayers(ids);int own=Array.IndexOf(ordered,ownSeat);
        return Enumerable.Range(1,Math.Max(0,ordered.Length-1)).Select(i=>ordered[(own+i)%ordered.Length]).ToArray();
    }
    public void RegisterContext(){contextVisible=true;}
    public static bool ContainsMapScreenPoint(Vector2 point) =>
        TabletopScene.BoardArea.Contains(MobileTabletop.Logical(point));
    public static void DrawMapLabels(Action draw)
    {
        // 地图与桌面物件共用完整画面，文字只裁剪在屏幕边界。
        var area=TabletopScene.BoardArea;
        GUI.BeginClip(area,new Vector2(-area.x,-area.y),Vector2.zero,false);
        try{draw();}finally{GUI.EndClip();}
    }
    public static void Fill(Rect r,Color c)
    {var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    public static void Note(Rect r)
    {Fill(new Rect(r.x+4,r.y+6,r.width,r.height),new Color(.05f,.02f,0,.32f));Fill(r,Cream);}
    public static void Token(Rect r,Color color)
    {
        if(disc==null)
        {
            disc=new Texture2D(96,96,TextureFormat.RGBA32,false);var pixels=new Color[96*96];
            for(int y=0;y<96;y++)for(int x=0;x<96;x++)
            {float d=Vector2.Distance(new Vector2(x,y),new Vector2(47.5f,47.5f));pixels[y*96+x]=new Color(1,1,1,Mathf.Clamp01(47-d));}
            disc.SetPixels(pixels);disc.Apply();
        }
        var old=GUI.color;GUI.color=new Color(0,0,0,.32f);GUI.DrawTexture(new Rect(r.x+2,r.y+3,r.width,r.height),disc);
        GUI.color=color;GUI.DrawTexture(r,disc);GUI.color=old;
    }
    void Styles(Font font)
    {
        if(text!=null){text.normal.textColor=small.normal.textColor=center.normal.textColor=title.normal.textColor=ModernOperationUI.TextColor;return;}
        text=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};text.normal.textColor=Cream;
        small=new GUIStyle(text){fontSize=12};center=new GUIStyle(text){alignment=TextAnchor.MiddleCenter};
        title=new GUIStyle(center){fontSize=20,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=14,wordWrap=true};
        button.normal.textColor=Ink;button.hover.textColor=Ink;button.active.textColor=Ink;
    }
    bool ObjectButton(Rect r,string label,bool enabled=true)
    {
        hits.Add(r);return ModernOperationUI.Button(r,label,enabled,r==EndTurnRect);
    }
    public void Draw(Font font,string seat,string actor,string phase,int turn,int ownPoints,int dice1,int dice2,
        TabletopSeat[] seats,bool canRoll,bool canEnd,Action roll,Action end,Action<string> trade,
        Action special=null,string specialLabel="",string detail="",Action<int> facility=null)
    {
        Styles(font);ModernOperationUI.Styles(font);hits.Clear();worldHits.Clear();hoveredScore=null;table.Objects.Begin("public");
        // 临时便签覆盖的桌面物件不能先于上层按钮响应同一次点击。
        bool tableEnabled=GUI.enabled;
        if(Event.current.type!=EventType.Repaint&&BlocksOverlayInput(Event.current.mousePosition))GUI.enabled=false;
        contextVisible=false;
        DrawTopButtons(seats.FirstOrDefault(p=>p.Id==seat),trade,facility);
        DrawPlayers(seats,seat,actor,trade);
        DrawTurnBanner(seat,actor,phase,turn);
        defense.Draw();
        bank.ObserveHud(operations,bankHistory,seats);
        bool expansion=dice.Event.HasValue;
        var trayRect=new Rect(15,149,178,128);hits.Add(trayRect);
        table.Objects.Tray("dice-tray",trayRect,ModernOperationUI.Surface,StrategyHudArt.Gold);
        var diceRects=expansion?new[]{new Rect(43,204,39,39),new Rect(113,205,39,39),new Rect(79,164,39,39)}
            :new[]{new Rect(43,192,43,43),new Rect(112,192,43,43)};
        bool enabled=GUI.enabled;
        for(int i=0;i<diceRects.Length;i++)
        {
            double now=Time.unscaledTimeAsDouble;
            var die=table.Objects.Die("dice-"+i,diceRects[i],i,dice.Face(i,now),dice.Progress(now),DiceRolling);worldHits.Add(die);
            GUI.enabled=enabled&&canRoll&&!DiceRolling;
            if(GUI.enabled&&die.Contains(Event.current.mousePosition)&&Event.current.type==EventType.MouseDown&&Event.current.button==0)
            {Event.current.Use();roll?.Invoke();}
        }
        GUI.enabled=enabled;
        if(canRoll&&!DiceRolling&&GUI.Button(trayRect,GUIContent.none,GUIStyle.none))roll?.Invoke();
        table.Objects.End();
        string turnCaption=canRoll?"掷骰":phase.Contains("弃牌")||phase.IndexOf("Discard",StringComparison.OrdinalIgnoreCase)>=0?"等待弃牌":canEnd?"结束回合":actor!=seat?"等待玩家":"等待操作";
        hits.Add(EndTurnRect);bool turnEnabled=(canRoll||canEnd)&&!DiceRolling;
        if(DiscardActive){turnCaption=DiscardCaption;turnEnabled=DiscardReady;}
        StrategyHudArt.Disc(EndTurnRect,turnEnabled?ModernOperationUI.Surface:ModernOperationUI.Background,turnEnabled&&EndTurnRect.Contains(Event.current.mousePosition));
        var turnStyle=new GUIStyle(title){fontSize=21,wordWrap=true};turnStyle.normal.textColor=turnEnabled?ModernOperationUI.TextColor:ModernOperationUI.Muted;
        GUI.Label(new Rect(EndTurnRect.x+20,EndTurnRect.y+35,EndTurnRect.width-40,60),turnCaption,turnStyle);
        bool previous=GUI.enabled;GUI.enabled=previous&&turnEnabled;
        if(GUI.Button(EndTurnRect,GUIContent.none,GUIStyle.none)){if(DiscardActive)ConfirmDiscard();else if(canRoll)roll?.Invoke();else end?.Invoke();}GUI.enabled=previous;
        DrawDiscardCount();
        GUI.enabled=tableEnabled;
    }
    void DrawTurnBanner(string seat,string actor,string phase,int turn)
    {
        var r=TurnBannerRect;hits.Add(r);
        StrategyHudArt.Plaque(r,ModernOperationUI.Background,StrategyHudArt.Gold);
        var turnStyle=new GUIStyle(center){fontSize=27,fontStyle=FontStyle.Bold};turnStyle.normal.textColor=StrategyHudArt.Gold;
        GUI.Label(new Rect(r.x+16,r.y+10,64,35),turn.ToString("D2"),turnStyle);
        var caption=new GUIStyle(center){fontSize=10,fontStyle=FontStyle.Bold};caption.normal.textColor=ModernOperationUI.Muted;
        GUI.Label(new Rect(r.x+16,r.y+46,64,20),"回  合",caption);
        Fill(new Rect(r.x+91,r.y+17,1,r.height-34),new Color(StrategyHudArt.Gold.r,StrategyHudArt.Gold.g,StrategyHudArt.Gold.b,.40f));
        var portrait=new Rect(r.x+109,r.y+12,56,56);
        StrategyHudArt.Disc(portrait,BoardRenderer.SeatColor(actor)*.62f,true);
        StrategyHudArt.Avatar(new Rect(portrait.x+6,portrait.y+6,portrait.width-12,portrait.height-12),actor);
        var actorStyle=new GUIStyle(title){fontSize=19,alignment=TextAnchor.MiddleLeft,wordWrap=false};
        string actorName=M6App.PlayerLabel(actor)+(actor==seat?" · 你":"");
        actorStyle.fontSize=Mathf.Min(19,Mathf.Max(10,Mathf.FloorToInt(19*193/Mathf.Max(193,actorStyle.CalcSize(new GUIContent(actorName)).x))));
        GUI.Label(new Rect(r.x+181,r.y+15,193,27),actorName,actorStyle);
        var phaseStyle=new GUIStyle(small){fontSize=12,alignment=TextAnchor.MiddleLeft,wordWrap=false};phaseStyle.normal.textColor=ModernOperationUI.Muted;
        GUI.Label(new Rect(r.x+181,r.y+45,193,21),phase,phaseStyle);
        // 两端共用铭牌作为菜单入口；触屏无需键盘 Esc，也不另做一套导航栏。
        if(GUI.Button(r,GUIContent.none,GUIStyle.none)&&!MobileTabletop.TouchDragged)
            UnityEngine.Object.FindFirstObjectByType<M6App>()?.OpenTableMenu();
    }
    void DrawTopButtons(TabletopSeat own,Action<string> trade,Action<int> facility)
    {
        string[] names={"银行","玩家交易","贸易设施","政治设施","科学设施"};
        for(int i=0;i<5;i++)
        {
            var r=new Rect(24+i*82,16,70,70);hits.Add(new Rect(r.x,r.y,r.width,105));
            bool available=i<2||own?.Improvements!=null;
            bool over=r.Contains(Event.current.mousePosition);
            var face=i==2?new Color(.38f,.26f,.045f):i==3?new Color(.045f,.17f,.31f):i==4?new Color(.10f,.25f,.09f):ModernOperationUI.Surface;
            StrategyHudArt.Disc(r,available?face:ModernOperationUI.Background,available&&over);
            StrategyHudArt.Icon(new Rect(r.x+14,r.y+13,42,43),i);
            int level=i<2?0:own?.Improvements!=null&&own.Improvements.Length>i-2?own.Improvements[i-2]:0;
            var marker=new Rect(r.center.x-11,r.yMax-7,22,22);TabletopUI.Token(marker,new Color(.045f,.055f,.05f));
            var levelStyle=new GUIStyle(center){fontSize=12,fontStyle=FontStyle.Bold};levelStyle.normal.textColor=StrategyHudArt.Gold;
            GUI.Label(marker,i<2?"•":level.ToString(),levelStyle);
            GUI.Label(new Rect(r.x-5,r.yMax+17,r.width+10,21),names[i],new GUIStyle(center){fontSize=11});
            bool prior=GUI.enabled;GUI.enabled=prior&&available;
            if(GUI.Button(r,GUIContent.none,GUIStyle.none)){if(i<2)trade?.Invoke(i==0?null:"");else facility?.Invoke(i-2);}GUI.enabled=prior;
            if(over)
            {
                string tip=names[i];
                if(i>=2)tip+=" · "+level+" 级"+(level>=3?"\n"+(i==2?"商会：商品可按 2:1 兑换":i==3?"要塞：可以晋升精锐骑士":"水渠：掷骰非 7 且无产出时，选取一张资源"):"\n达到 3 级解锁能力");
                var tooltip=new Rect(r.x,125,246,i>=2?61:30);ModernOperationUI.Panel(tooltip);GUI.Label(new Rect(tooltip.x+10,tooltip.y+4,tooltip.width-20,tooltip.height-8),tip,ModernOperationUI.Small);
            }
        }
    }
    void DrawPlayers(TabletopSeat[] seats,string own,string actor,Action<string> trade)
    {
        int count=Math.Max(1,seats.Length);float width=114,x=1430-count*(width+5);
        hoveredPlayerHint=null;playerHintAnchor=Rect.zero;
        foreach(var id in M6App.OrderedPlayers(seats.Select(p=>p.Id)))
        {
            var player=seats.First(p=>p.Id==id);
            var panel=new Rect(x,13,width,191);hits.Add(panel);
            ModernOperationUI.Round(panel,new Color(ModernOperationUI.Background.r,ModernOperationUI.Background.g,ModernOperationUI.Background.b,.68f));
            var avatar=new Rect(x+(width-50)/2,20,50,50);bool active=player.Id==actor;
            StrategyHudArt.Disc(avatar,BoardRenderer.SeatColor(player.Id)*.65f,active);
            StrategyHudArt.Avatar(new Rect(avatar.x+4,avatar.y+4,avatar.width-8,avatar.height-8),player.Id);
            GUI.Label(new Rect(x+4,70,width-8,24),M6App.PlayerLabel(player.Id),new GUIStyle(center){fontSize=10,fontStyle=FontStyle.Bold,wordWrap=true});
            int points=player.Score==null?player.Points:player.Id==own?player.Score.Total:player.Score.PublicPoints;
            int[] values={points,player.Improvements==null?0:player.Improvements[0],player.Improvements==null?0:player.Improvements[1],player.Improvements==null?0:player.Improvements[2],player.Resources,player.Cards};
            string[] hints={"胜利点 · 悬停查看分数构成","贸易设施等级","政治设施等级","科学设施等级",
                "资源与商品 · 当前 / 七点保护线\n超出保护线后掷出 7，须弃去一半手牌",
                player.SpecialHandLimit>0?"进步卡 · 当前 / 回合结束上限\n公开胜利点卡不占手牌上限":"发展卡 · 当前张数 / 无持有上限"};
            for(int row=0;row<6;row++)
            {
                var r=new Rect(x+7,94+row*17,width-14,17);
                var icon=new Rect(r.x+1,r.y,23,18);
                if(row==0)StrategyHudArt.VictoryIcon(icon);
                else if(row<4)StrategyHudArt.Icon(icon,row+1);
                else StrategyHudArt.HandCardIcon(icon,row==5);
                var valueStyle=new GUIStyle(center){fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleRight};
                valueStyle.normal.textColor=row==0?StrategyHudArt.Gold:row==1?new Color(1,.8f,.3f):row==2?new Color(.48f,.77f,1):row==3?new Color(.55f,.9f,.52f):ModernOperationUI.TextColor;
                string value=values[row].ToString();
                if(row>=4)
                {
                    var counter=new TabletopHandCounter(values[row],row==4?player.ResourceHandLimit:player.SpecialHandLimit);
                    value=counter.Caption;if(counter.OverLimit)valueStyle.normal.textColor=ModernOperationUI.Error;
                }
                GUI.Label(new Rect(r.x+28,r.y,r.width-28,18),value,valueStyle);
                if(row==0)ScoreHover(r,player.Score);
                else if(r.Contains(Event.current.mousePosition)){hoveredPlayerHint=hints[row];playerHintAnchor=r;}
            }
            if(player.Id!=own&&GUI.Button(avatar,GUIContent.none,GUIStyle.none))trade?.Invoke(player.Id);
            x+=width+5;
        }
    }
    void DrawPlayerHint()
    {
        if(hoveredPlayerHint==null||contextVisible||!GUI.enabled)return;
        var tooltip=new Rect(Mathf.Clamp(playerHintAnchor.center.x-142,16,1140),203,284,hoveredPlayerHint.Contains("\n")?58:32);
        ModernOperationUI.Panel(tooltip);hits.Add(tooltip);
        var tip=new GUIStyle(small){fontSize=12,alignment=TextAnchor.MiddleLeft};
        GUI.Label(new Rect(tooltip.x+10,tooltip.y+5,tooltip.width-20,tooltip.height-10),hoveredPlayerHint,tip);
    }
    internal static Rect PlayerAvatarRect(int index,int count)
    {return new Rect(1430-Math.Max(1,count)*119+index*119+32,20,50,50);}
    void DrawSeat(TabletopSeat p,int place,bool active,Action<string> trade)
    {
        bool top=place==1;
        var layout=OpponentSeatLayout(place);var seatRect=table.ProjectLabel(layout);hits.Add(seatRect);
        var activeRect=top?new Rect(610,5,12,12):MirrorSideRect(new Rect(37,291,12,12),place);
        if(active)Token(table.ProjectLabel(activeRect),new Color(1,.79f,.29f));
        var label=new GUIStyle(center){fontSize=p.Improvements!=null&&!top?12:14,fontStyle=FontStyle.Bold};
        string levels=p.Improvements!=null&&p.Improvements.Length>=3&&seatRect.Contains(Event.current.mousePosition)?" · 贸"+p.Improvements[0]+" 政"+p.Improvements[1]+" 科"+p.Improvements[2]:"";
        var scoreRect=new Rect(layout.x,layout.y-4,layout.width,26);
        TableLabel(scoreRect,M6App.PlayerLabel(p.Id)+"   "+(p.Score==null?p.Points:p.Score.PublicPoints)+" 分"+levels,label);
        ScoreHover(table.ProjectLabel(scoreRect),p.Score);
        DrawStock("road",p.Roads,p.Id,place);
        DrawStock("settlement",p.Settlements,p.Id,place);
        DrawStock("city",p.Cities,p.Id,place);
        DrawStock("ship",p.Ships,p.Id,place);
        var back=EntityCardAssets.Texture(EntityCardAssets.Good(0).Back);
        int shown=Math.Min(6,p.Resources+p.Cards);
        for(int i=0;i<shown;i++)
        {
            var card=table.Objects.Card("opponent-"+p.Id+"-card-"+i,OpponentHandCardLayout(place,i,shown),back);worldHits.Add(card);
        }
        var countRect=top?new Rect(640,69,160,22):new Rect(layout.x,482,layout.width,22);
        TableLabel(countRect,p.Resources+" 资源 · "+p.Cards+" 卡",new GUIStyle(center){fontSize=12});
        // 浮层与银行在视觉上盖住席位时，不允许整块席位热区先吞掉点击。
        bool covered=TurnBannerRect.Contains(Event.current.mousePosition)||SpecialRect.Contains(Event.current.mousePosition)||HistoryBlocksInput(Event.current.mousePosition)
            ||table.ProjectLabel(TabletopBankUI.Area).Contains(Event.current.mousePosition);
        if(!covered&&GUI.Button(seatRect,GUIContent.none,GUIStyle.none))trade?.Invoke(p.Id);
    }
    internal static Rect OpponentSeatLayout(int place) => place==1?new Rect(420,6,600,156):MirrorSideRect(new Rect(28,286,210,222),place);
    static Rect MirrorSideRect(Rect rect,int place) => place==2?new Rect(1440-rect.xMax,rect.y,rect.width,rect.height):rect;
    internal static Rect OpponentHandCardLayout(int place,int index,int count)
    {
        float center=place==1?720:133;
        var card=new Rect(center-((count-1)*30+27)/2f+index*30,place==1?29:448,27,39);
        return place==1?card:MirrorSideRect(card,place);
    }
    internal static Vector2 OpponentStockPosition(int place,string kind,int index)
    {
        Vector2 point;
        if(place==1)
        {
            // 上方手牌靠外、库存靠内，与自己的桌边顺序上下对应。
            float x=kind=="settlement"?466:kind=="road"?546:kind=="city"?698:810;
            point=OperationDock.StockPieceLayout(new Rect(x,648,OperationDock.StockGroupWidth(kind),60),kind,index);
            point.y=819-point.y;
        }
        else
        {
            point=kind=="road"?new Vector2(70+(index%8)*18,324+(index/8)*14):
                kind=="settlement"?new Vector2(43+index*20,372):
                kind=="city"?new Vector2(148+index*29,372):
                new Vector2(69+(index%5)*32,397+(index/5)*18);
            if(place==2)point.x=1440-point.x;
        }
        return point;
    }
    void DrawStock(string kind,int count,string seat,int place)
    {
        for(int i=0;i<count;i++)
        {
            var piece=table.Objects.Piece("opponent-"+seat+"-"+kind+"-"+i,kind,seat,1,
                OpponentStockPosition(place,kind,i));
            if(piece!=null)worldHits.Add(piece);
        }
    }
    void TableLabel(Rect r,string value,GUIStyle style)
    {GUI.Label(table.ProjectLabel(r),value,new GUIStyle(style){fontSize=Mathf.Max(9,Mathf.RoundToInt(style.fontSize*table.DisplayScale))});}
    void ScoreHover(Rect anchor,TabletopScoreBreakdown score)
    {
        if(score==null||!GUI.enabled||history.IsOpen||!anchor.Contains(Event.current.mousePosition))return;
        hoveredScore=score;scoreAnchor=anchor;
    }
    void DrawScoreDetails()
    {
        if(hoveredScore==null||history.IsOpen)return;
        var score=hoveredScore;float height=118+Mathf.Max(1,score.Rows.Length)*60;
        float x=Mathf.Clamp(scoreAnchor.center.x-182,12,1440-376);
        float y=scoreAnchor.y>450?scoreAnchor.y-height-10:scoreAnchor.yMax+10;
        var panel=new Rect(x,Mathf.Clamp(y,12,900-height-12),364,height);
        ModernOperationUI.Panel(panel);
        var titleStyle=new GUIStyle(ModernOperationUI.Title){padding=new RectOffset(),alignment=TextAnchor.MiddleLeft,wordWrap=false};
        GUI.Label(new Rect(panel.x+18,panel.y+12,328,40),M6App.PlayerLabel(score.PlayerId)+" · 分数构成",titleStyle);
        string total=score.IsOwn?"总分 "+score.Total+"  ·  明分 "+score.PublicPoints+"  ·  暗分 "+score.HiddenPoints:"公开得分 "+score.PublicPoints+" 分";
        var bodyStyle=new GUIStyle(ModernOperationUI.Text){padding=new RectOffset(),alignment=TextAnchor.MiddleLeft,wordWrap=false};
        var detailStyle=new GUIStyle(ModernOperationUI.Small){padding=new RectOffset(),alignment=TextAnchor.MiddleLeft,wordWrap=false};
        GUI.Label(new Rect(panel.x+18,panel.y+55,328,32),total,bodyStyle);
        float rowY=panel.y+98;
        if(score.Rows.Length==0)GUI.Label(new Rect(panel.x+18,rowY,328,32),"尚未获得胜利点",detailStyle);
        foreach(var row in score.Rows)
        {
            var nameStyle=new GUIStyle(bodyStyle);
            if(row.Hidden)nameStyle.normal.textColor=ModernOperationUI.Accent;
            GUI.Label(new Rect(panel.x+18,rowY,254,32),row.Name,nameStyle);
            GUI.Label(new Rect(panel.x+282,rowY,64,32),row.Points+" 分",new GUIStyle(nameStyle){alignment=TextAnchor.MiddleRight});
            GUI.Label(new Rect(panel.x+18,rowY+31,328,25),row.Detail,detailStyle);
            rowY+=60;
        }
    }
    public void BeginContext(string caption)
    {
        var rect=CurrentContextRect;contextVisible=true;ModernOperationUI.Panel(rect);
        float header=44;
        GUI.Label(new Rect(rect.x+18,rect.y+12,rect.width-36,header-12),caption,ModernOperationUI.Title);
        MobileTabletop.ScrollTouch(new Rect(rect.x+16,rect.y+header,rect.width-32,rect.height-header-12),ref scroll);
        GUILayout.BeginArea(new Rect(rect.x+16,rect.y+header,rect.width-32,rect.height-header-12));
        scroll=GUILayout.BeginScrollView(scroll);
    }
    public void EndContext(){GUILayout.EndScrollView();GUILayout.EndArea();}
    public void DrawContext(string caption,Action content){BeginContext(caption);content();EndContext();}
    public static void NumberToken(Vector2 p,int? number,string fallback,GUIStyle style)
    {
        // 数字已放回棋盘的实体点数牌，服从三维深度；沙漠保留原始地貌。
        if(number.HasValue||fallback=="沙漠")return;
        if(!number.HasValue&&string.IsNullOrEmpty(fallback))return;
        var r=new Rect(p.x-19,p.y-16,38,32);Token(r,Cream);
        var s=new GUIStyle(style){fontSize=number.HasValue?21:12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        s.normal.textColor=number==6||number==8?HotNumberColor:Ink;
        GUI.Label(r,number.HasValue?number.ToString():fallback,s);
    }
}
