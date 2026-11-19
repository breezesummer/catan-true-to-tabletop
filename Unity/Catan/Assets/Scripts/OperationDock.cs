using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 桌面上的个人组件与手牌。拿取、费用与交换草稿均不直接修改规则状态。
public sealed partial class OperationDock
{
    public readonly OperationSelection Selection = new OperationSelection();
    public OperationHandSelection DirectSelection;
    public Action<int,Rect> InspectGood;
    public Action<string,Rect> InspectCard;
    bool DirectSelecting => DirectSelection?.Active==true;
    readonly OperationHandGesture handGesture=new OperationHandGesture();
    HandFace pressedHand,inspectedHand;
    float resourceScroll,specialScroll,scrollAtPress;
    public static readonly Rect HandViewport=new Rect(TabletopHandLayout.RestLeft,680,TabletopHandLayout.RestRight-TabletopHandLayout.RestLeft,209);
    internal static readonly Rect HandRack=new Rect(HandViewport.x-6,852,HandViewport.width+12,42);
    float HandScroll {get=>specialHand?specialScroll:resourceScroll;set {if(specialHand)specialScroll=value;else resourceScroll=value;}}
    public float ScrollOffset(bool special)=>special?specialScroll:resourceScroll;
    static readonly string[] Names = { "木材", "砖块", "羊毛", "谷物", "矿石", "布匹", "钱币", "纸张" };
    static Color Surface => ModernOperationUI.Surface;
    static Color Line => ModernOperationUI.Line;
    static Color Gold => ModernOperationUI.Accent;
    static Color Ink => ModernOperationUI.TextColor;
    static Color Muted => ModernOperationUI.Muted;
    static Color Missing => ModernOperationUI.Error;
    static readonly Rect TradeRect = new Rect(262,139,916,506);
    static readonly Rect GiveDropRect = new Rect(286,276,386,266);
    GUIStyle text, small, heading, center, tableLabel, quantity;
    Font quantityFont;
    OperationSnapshot current;
    public OperationWindowState Windows;
    public Func<string,string> PlayerName;
    string DisplayPlayer(string seat)=>PlayerName?.Invoke(seat)??M6App.PlayerLabel(seat);
    string raisedCard;
    string revision, scope, hoveredBuild, discardCard, message = "";
    bool tradeOpen, playerTrade;
    bool specialHand;
    public FishingDock Fishing;
    public M6App Generals;
    bool GeneralHandVisible=>Generals?.GeneralHandVisible==true;
    bool FishHandVisible => Fishing?.Selected==true;
    int facilityTrack=-1;
    bool sealNotePinned;
    float sealNoteReveal;
    static readonly Rect FacilitySealRect = new Rect(550, 351, 132, 124);
    static readonly Rect SlipSealRect = new Rect(499, 365, 79, 72);
    // 卡匣底图中，蓝布与右侧木框的交界位于 x = 758。
    static readonly Rect SealNoteRect = new Rect(514, 380, 244, 82);
    int? portGood;
    bool portTrade;
    string tradeTitle;
    TabletopHandOrder fallbackOrder = new TabletopHandOrder();
    TabletopSpecialHandOrder specialOrder = new TabletopSpecialHandOrder();
    int target;
    readonly int[] give = new int[8], receive = new int[8];
    int dragGood=-1, dragOrdinal;
    Vector2 dragStart;
    bool dragging;
    OperationBuild heldPiece;
    Vector2 pieceStart, pieceReleasePoint;
    bool pieceDragging, pieceReleased;
    Rect tooltipRect;
    Vector2 costAnchor;
    readonly List<HitShape> hits = new List<HitShape>();
    readonly List<HandFace> faces = new List<HandFace>();
    readonly List<PieceFace> pieceFaces = new List<PieceFace>();
    readonly Dictionary<string,float> hoverAmounts = new Dictionary<string,float>();
    readonly Dictionary<TabletopCollectionFlight,List<HandFace>> landingFaces = new Dictionary<TabletopCollectionFlight,List<HandFace>>();
    readonly Stack<List<HandFace>> landingFacePool = new Stack<List<HandFace>>();
    TabletopResourceSlot[] resourceSlots = Array.Empty<TabletopResourceSlot>();
    TabletopResourceSlot[] specialSlots = Array.Empty<TabletopResourceSlot>();
    string hoveredHand;
    Vector2 handHoverMouse;
    Rect handHoverAnchor;
    double handUpdated;
    TabletopCollectionTimeline collections;
    readonly TabletopHandCountPresentation displayedHandCounts=new TabletopHandCountPresentation();
    bool collectionFlightsVisible;
    TabletopScene table;
    public void BindTable(TabletopScene scene) { table=scene; }
    public void BindCollections(TabletopCollectionTimeline timeline,bool flightsVisible=true) { collections=timeline;collectionFlightsVisible=flightsVisible; }
    sealed class HitShape
    {
        public Rect Rect;
        public float Angle;
        public Texture2D Texture;
        public TabletopWorldObjects.Hit World;
        public bool Contains(Vector2 point)
        {
            if(World!=null)return World.Contains(point);
            float radians=-Angle*Mathf.Deg2Rad;Vector2 delta=point-Rect.center;
            var local=Rect.center+new Vector2(delta.x*Mathf.Cos(radians)-delta.y*Mathf.Sin(radians),delta.x*Mathf.Sin(radians)+delta.y*Mathf.Cos(radians));
            return Rect.Contains(local)&&(Texture==null||OperationPieceAssets.Contains(Texture,Rect,local));
        }
    }
    sealed class PieceFace
    {
        public HitShape Shape;
        public OperationBuild Build;
        public int Ordinal;
    }
    sealed class HandFace
    {
        public HitShape Shape;
        public Rect RestRect;
        public int Good=-1, Ordinal;
        public OperationHandCard Card;
        public Texture2D Texture;
        public bool Selected;
        public int SlotIndex;
        public float Hover;
        public string Key => Card==null?"good:"+Good+":"+Ordinal:"card:"+Card.Id;
    }

    public bool TradeOpen => tradeOpen;
    public bool IsPortTrade => tradeOpen&&portTrade;
    internal int VerificationFacilityTrack => facilityTrack;
    internal int[] VerificationTradeGiveGoods => tradeOpen?OperationPanelOptions.PaymentGoods(current,portTrade&&current.ValidateTradeDraft==null?portGood:null):Array.Empty<int>();
    public bool ResourceHandVisible => !specialHand&&!FishHandVisible&&!GeneralHandVisible;
    internal void FocusGeneralHand(bool development)
    {
        Fishing?.Hide();
        specialHand=development;
        hoveredHand=null;
        handHoverAnchor=Rect.zero;
    }
    internal bool DiscardMatches(OperationSnapshot snapshot) => ReferenceEquals(current,snapshot);
    // 报价权限来自当前所属席位投影；已拒绝者和旁观者不会被其他人的待回应报价锁住。
    internal static bool HasActionableOffer(OperationSnapshot snapshot) => snapshot?.Offer!=null&&(snapshot.Offer.CanAccept||snapshot.Offer.CanReject||snapshot.Offer.CanCancel);
    public OperationHandCard SelectedDiscardCard => current?.Discard?.Special==true ? current.Cards.FirstOrDefault(c=>c.Id==discardCard) : null;
    internal bool InspectingDevelopment => inspectedHand?.Card!=null&&raisedCard!=null&&current?.Discard==null;
    internal bool InspectedDevelopmentReady => InspectingDevelopment&&!DirectSelecting&&current.Cards.Any(c=>c.Id==raisedCard&&CanPlayHandCard(current,c));
    public bool HasCancelableInteraction => raisedCard!=null || tradeOpen || facilityTrack>=0 || discardCard!=null || Selection.BuildId!=null || Selection.Selected.Any(n=>n>0) || heldPiece!=null || dragging;
    // 银行牌堆与旧购买流程共用这一入口，仍由适配层校验并提交指令。
    public void PrepareDevelopmentPurchase()
    {
        if(current==null||HasOverlay||DirectSelecting)return;
        var build=current.Builds.FirstOrDefault(b=>b.Icon=="development");
        if(build==null)return;
        PrepareBuild(build,current);
    }
    // 同种牌按展示序号定位；超出当前视口时返回空，让用途条退回手牌区上方。
    public Rect? HandCardRect(int good,int ordinal)
    {
        var face=faces.FirstOrDefault(f=>f.Good==good&&f.Ordinal==ordinal);
        return face!=null&&face.Shape.Rect.Overlaps(HandViewport)?face.Shape.Rect:(Rect?)null;
    }
    internal bool VerificationSpecialVisible(string id)=>faces.Any(f=>f.Card?.Id==id);
    internal string VerificationRaisedCard=>raisedCard;
    internal Rect VerificationSpecialRect(string id)=>faces.First(f=>f.Card?.Id==id).Shape.Rect;
    internal Rect[] VerificationHandRects => faces.Select(face=>face.Shape.Rect).ToArray();
    internal int VerificationPendingSpecialCount => current==null||collections==null?0:collections.SpecialSlots(current.Seat,current.Cards,Time.unscaledTimeAsDouble).Count(slot=>slot.Flight!=null);
    internal Rect[] VerificationBuildRects => pieceFaces.Select(face=>face.Shape.World?.Rect??face.Shape.Rect).ToArray();
    internal bool VerificationTryBuildPoint(string buildId,out Vector2 logicalPoint)
    {
        foreach(var face in pieceFaces.Where(face=>face.Build.Id==buildId&&face.Build.Available))
        {
            var rect=face.Shape.World?.Rect??face.Shape.Rect;
            for(int y=1;y<10;y++)for(int x=1;x<10;x++)
            {
                var point=new Vector2(rect.x+rect.width*x/10,rect.y+rect.height*y/10);
                if(!face.Shape.Contains(point))continue;
                logicalPoint=point;return true;
            }
        }
        logicalPoint=Vector2.zero;return false;
    }
    internal bool VerificationHandsAreScreenSpace => faces.All(face=>face.Shape.World==null);
    internal int VerificationVisibleResourceCount => faces.Count(face=>face.Good>=0);
    internal int VerificationPendingResourceCount => current==null?0:Math.Max(0,current.Goods.Take(current.GoodCount).Sum()-VerificationVisibleResourceCount);
    internal bool FacilityPageVisible => facilityTrack >= 0 && Windows?.Suppressed != true;
    public bool HasOverlay => Windows?.Suppressed!=true&&(tradeOpen || facilityTrack>=0 || HasActionableOffer(current));
    public bool IsDraggingPiece => pieceDragging;
    public bool ConsumeBuildRelease(out Vector2 releasedLogicalMouse)
    {releasedLogicalMouse=pieceReleasePoint;bool released=pieceReleased;pieceReleased=false;return released;}
    public bool ConsumeBuildRelease(Vector2 logicalMouse)
    {Vector2 release;return ConsumeBuildRelease(out release)&&(logicalMouse-release).sqrMagnitude<=24*24;}
    public void OpenTrade(string target=null)
    {
        if(current==null||!current.CanTrade||!current.CanInteract||current.Offer!=null||DirectSelecting)return;
        Windows?.Open();current.Cancel?.Invoke();raisedCard=null;tradeOpen=true;facilityTrack=-1;discardCard=null;Selection.BuildId=null;Selection.PieceOrdinal=-1;message="";
        portTrade=false;portGood=null;tradeTitle=target==null?"银行":"玩家交易";
        playerTrade=target!=null;
        TabletopAudio.Instance?.TradeClick(playerTrade);
        this.target=target==null?0:Math.Max(0,Array.IndexOf(current.OtherPlayers,target));
        if(playerTrade)Array.Copy(Selection.Selected,give,8);else Array.Clear(give,0,8);Array.Clear(receive,0,8);
    }
    public void OpenPortTrade(int? resource,string title)
    {
        if(resource.HasValue&&(resource.Value<0||resource.Value>=5))return;
        OpenTrade();if(!tradeOpen)return;
        portTrade=true;portGood=resource;tradeTitle=resource.HasValue?Names[resource.Value]+"港口":"通用港口";
    }
    public void OpenFacility(int track)
    {
        if(OperationPanelOptions.Facility(current,track)==null||HasActionableOffer(current)||DirectSelecting)return;
        sealNotePinned=false;
        sealNoteReveal=0;
        Windows?.Open();current.Cancel?.Invoke();raisedCard=null;Selection.Clear();tradeOpen=false;discardCard=null;facilityTrack=track;message="";
    }
    public int FacilityLevel(int track) => current?.Builds.FirstOrDefault(b=>b.Track==track)?.Level??0;
    public void Reset()
    {
        raisedCard=null;
        sealNotePinned=false;
        sealNoteReveal=0;
        Windows?.ResetInteraction();
        Selection.Clear(); tradeOpen=false;facilityTrack=-1;portTrade=false;portGood=null; discardCard=null; hoveredBuild=null; message="";
        Array.Clear(give,0,8);Array.Clear(receive,0,8); tooltipRect=Rect.zero;
        dragGood=-1;dragging=false;handGesture.Cancel();pressedHand=inspectedHand=null;hits.Clear();faces.Clear();pieceFaces.Clear();
        hoveredHand=null;handHoverAnchor=Rect.zero;
        heldPiece=null;pieceDragging=false;pieceReleased=false;
        target=0;playerTrade=false;
    }
    internal void CloseWindow(OperationSnapshot snapshot)
    {
        var offer=snapshot?.Offer;
        bool alreadyClosed=Windows?.Closed==true;
        Reset();snapshot?.Cancel?.Invoke();
        if(offer!=null)
        {
            Windows?.Close();
            if(!alreadyClosed&&offer.CanCancel)snapshot.Respond?.Invoke("cancel");
            else if(!alreadyClosed&&offer.CanReject)snapshot.Respond?.Invoke("reject");
            Windows?.Close();
        }
        else Windows?.Clear();
    }
    public bool BlocksBoardInput(Vector2 logicalMouse)
    {
        if(HasOverlay)return true;
        // 手牌视口是排版/滚动范围，透明留白不能遮挡后方棋盘；实际牌面由 hits 注册。
        return pressedHand!=null || hoveredHand!=null&&handHoverAnchor.Contains(logicalMouse) || Windows?.Suppressed!=true&&((tradeOpen||HasActionableOffer(current))&&TradeRect.Contains(logicalMouse)) || hits.Any(h=>h.Contains(logicalMouse));
    }
    internal bool BlocksHudInput(Vector2 logicalMouse) => BlocksBoardInput(logicalMouse);
    void Styles(Font font)
    {
        ModernOperationUI.Styles(font);
        if(text==null)
        {
            text=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};
            small=new GUIStyle(text){fontSize=12};heading=new GUIStyle(text){fontSize=21,fontStyle=FontStyle.Bold};
            center=new GUIStyle(text){alignment=TextAnchor.MiddleCenter,fontSize=13};
            tableLabel=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};
            quantityFont=font;
            quantity=new GUIStyle(center){font=quantityFont??font,fontSize=34,fontStyle=FontStyle.BoldAndItalic,wordWrap=false,alignment=TextAnchor.LowerRight};
        }
        text.normal.textColor=Ink;heading.normal.textColor=Ink;center.normal.textColor=Ink;
        small.normal.textColor=Muted;tableLabel.normal.textColor=Gold;quantity.normal.textColor=Ink;
    }
    static void Fill(Rect rect,Color color)
    {var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
    void Label(Rect r,string value,GUIStyle style=null)=>GUI.Label(r,value,style??text);
    bool Button(Rect r,string value,bool enabled=true)
    {
        Register(r);return ModernOperationUI.Button(r,value,enabled);
    }
    bool Primary(Rect r,string value,bool enabled=true){Register(r);return ModernOperationUI.Button(r,value,enabled,true);}
    HitShape Register(Rect r,float angle=0,Texture2D texture=null){var hit=new HitShape{Rect=r,Angle=angle,Texture=texture};hits.Add(hit);return hit;}
    static void Paper(Rect r)
    {ModernOperationUI.Panel(r);}
    public void Draw(OperationSnapshot snapshot,Font font)
    {
        Styles(font);
        if(snapshot==null||string.IsNullOrEmpty(snapshot.Scope)){Reset();current=null;return;}
        if(scope!=snapshot.Scope){hoverAmounts.Clear();hoveredHand=null;handHoverAnchor=Rect.zero;handUpdated=Time.unscaledTimeAsDouble;fallbackOrder=new TabletopHandOrder();specialOrder=new TabletopSpecialHandOrder();specialHand=false;resourceScroll=specialScroll=0;}
        if(scope!=snapshot.Scope||revision!=snapshot.Revision){Reset();scope=snapshot.Scope;revision=snapshot.Revision;}
        current=snapshot;Selection.Sync(snapshot);
        if(HasOverlay)Windows?.Describe(HasActionableOffer(snapshot)?"回应交易":tradeOpen?"继续交易":"城市设施");
        if(snapshot.Discard!=null){specialHand=snapshot.Discard.Special;Fishing?.Hide();}
        if(DirectSelecting)Fishing?.Hide();
        hoveredBuild=null;tooltipRect=Rect.zero;hits.Clear();faces.Clear();pieceFaces.Clear();
        // 在任何世界组件接收 MouseDown 前，先按本帧屏幕布局确定手牌命中。
        // 悬停与选中只向上抬牌，不挪动相邻卡牌。
        PrepareHand(snapshot);
        bool dockEnabled=GUI.enabled;
        bool overHand=hoveredHand!=null&&handHoverAnchor.Contains(Event.current.mousePosition)||HandViewport.Contains(Event.current.mousePosition)||HandTabsArea.Contains(Event.current.mousePosition);
        GUI.enabled=dockEnabled&&!DirectSelecting&&(Event.current.type==EventType.Repaint||!HasOverlay&&!overHand);
        table.Objects.Begin("own");
        DrawBuilds(snapshot);
        table.Objects.End();
        GUI.enabled=dockEnabled;
        if(overHand)hoveredBuild=null;
        DrawHand();
        DrawHandTabs(snapshot);
        HandleHandInput(snapshot);
        DrawHandInspection();
        HandleBuildInput(snapshot);
        if(Windows?.Suppressed!=true)
        {
            if(tradeOpen)DrawTrade(snapshot);
            else if(HasActionableOffer(snapshot))DrawOffer(snapshot);
            else if(facilityTrack>=0)DrawFacilities(snapshot);
            else DrawCost(snapshot);
            if(HasOverlay)
            {
                Rect panel=tradeOpen||HasActionableOffer(snapshot)?TradeRect:FacilityRect;
                if(Button(new Rect(panel.xMax-98,panel.y+16,32,30),"—"))Windows?.Hide(HasActionableOffer(snapshot)?"回应交易":tradeOpen?"继续交易":"城市设施");
                if(Button(new Rect(panel.xMax-54,panel.y+16,32,30),"×"))CloseWindow(snapshot);
            }
        }
        HandlePieceInput(snapshot);
        if(dragging&&pressedHand?.Card!=null&&pressedHand.Texture!=null)
            EntityCardAssets.DrawCardFace(new Rect(Event.current.mousePosition.x-45,Event.current.mousePosition.y-65,90,130),pressedHand.Texture);
        if(dragging&&dragGood>=0)
        {
            var texture=EntityCardAssets.Texture(EntityCardAssets.Good(dragGood).Front);
            var r=new Rect(Event.current.mousePosition.x-49,Event.current.mousePosition.y-70,98,139);
            if(texture!=null)EntityCardAssets.DrawCardFace(r,texture);
        }
        if(pieceDragging&&heldPiece!=null)
        {
            var piece=OperationPieceAssets.Texture(heldPiece.Icon??heldPiece.Id,snapshot.Seat??"P1",Math.Max(1,heldPiece.Level));
            float size=76;
            if(piece!=null)OperationPieceAssets.DrawStock(new Rect(Event.current.mousePosition.x-size/2,Event.current.mousePosition.y-size*.58f,size,size),piece);
        }
    }
    void DrawBuilds(OperationSnapshot s)
    {
        var pieces=s.Builds.Where(b=>b.Track<0&&b.Icon!="development").ToArray();
        if(pieces.Length>0)
        {
            var first=TabletopPieceDock.GroupLayout(pieces.Length,0);
            var last=TabletopPieceDock.GroupLayout(pieces.Length,pieces.Length-1);
            var rack=new Rect(first.x-4,first.y-8,first.width+8,last.yMax-first.y+16);
            if(StrategyHudTheme.HandPainted)HandPaintedTabletopSkin.Tray(rack);
            else CopperTabletopSkin.Panel(rack,ModernOperationUI.Background);
            hits.Add(new HitShape{Rect=rack});
        }
        for(int i=0;i<pieces.Length;i++)
        {
            var group=StockGroupLayout(pieces,i,false);
            TabletopPieceDock.DrawFrame(group,Selection.BuildId==pieces[i].Id,pieces[i].CanPrepare(s));
            DrawBuild(group,pieces[i],s);
        }
    }
    internal static Rect FacilityLayout(int track) => new Rect(1090+track*108,596,104,138);
    internal static float StockGroupWidth(string kind) => kind=="road"?152:kind=="ship"?164:kind=="wall"?120:kind=="city"?112:kind=="settlement"?80:54;
    internal static Rect StockGroupLayout(OperationBuild[] pieces,int index,bool hasFacilities)
    {
        return TabletopPieceDock.GroupLayout(pieces.Length,index);
    }
    internal static Vector2 StockPieceLayout(Rect group,string kind,int index)
    {
        return TabletopPieceDock.PiecePosition(group);
    }
    void DrawBuild(Rect r,OperationBuild build,OperationSnapshot s)
    {
        bool clicked=false,hovered=false;
        if(build.Track>=0)
        {
            var card=table.Objects.Facility("facility-"+build.Track,r,build.Track,build.Level);
            hits.Add(new HitShape{World=card});
            for(int level=1;level<=5;level++)
            {
                var slot=table.Objects.FacilitySlot("facility-"+build.Track+"-slot-"+level,card,build.Track,level,level<=build.Level);
                bool over=slot.Contains(Event.current.mousePosition);
                if(level==build.Level+1&&over){hovered=true;costAnchor=new Vector2(Event.current.mousePosition.x,slot.Rect.y);}
                if(GUI.enabled&&over&&Event.current.type==EventType.MouseDown&&Event.current.button==0&&!HasOverlay)
                {
                    Event.current.Use();
                    if(level<=build.Level)s.Notify?.Invoke("这一级设施已经建成");
                    else if(level!=build.Level+1)s.Notify?.Invoke("请先升级第 "+(build.Level+1)+" 级设施");
                    else clicked=true;
                }
            }
        }
        else
        {
            var piece=OperationPieceAssets.Texture(build.Icon??build.Id,s.Seat??"P1",Math.Max(1,build.Level));
            if(piece!=null)
            {
                for(int n=0;n<1;n++)
                {
                    bool picked=Selection.IsPieceSelected(build.Id,n);
                    if(picked&&pieceDragging)continue;
                    // 库存使用现有模型的透明缩略图；棋盘模型完全沿用，整格接收点击。
                    var icon=TabletopPieceDock.ModelArea(r);
                    bool enabled=GUI.enabled;GUI.enabled=true;
                    OperationPieceAssets.DrawStock(icon,piece);GUI.enabled=enabled;
                    var shape=new HitShape{Rect=r};
                    hovered|=shape.Contains(Event.current.mousePosition);
                    TabletopPointerCursor.Hover(r,build.CanPrepare(s));
                    if(build.Count>0){hits.Add(shape);pieceFaces.Add(new PieceFace{Shape=shape,Build=build,Ordinal=n});}
                }
                TabletopPieceDock.DrawCount(r,build.Count,quantity);
            }
        }
        if(hovered){hoveredBuild=build.Id;if(build.Track<0)costAnchor=new Vector2(r.xMax+35,r.center.y);}
        if(clicked)PrepareBuild(build,s);
    }
    void PrepareBuild(OperationBuild build,OperationSnapshot s)
    {
        if(DirectSelecting||!build.CanPrepare(s)){s.Notify?.Invoke(DirectSelecting?"请先完成或取消技能选择":!s.CanInteract?"等待当前行动或待决席位":build.UnavailableReason??"当前不能使用");return;}
        Windows?.Open();s.Cancel?.Invoke();raisedCard=null;Selection.Choose(build,s.Goods);tradeOpen=false;discardCard=null;message="";
        s.PrepareBuild?.Invoke(build);
    }
    void TableLabel(Rect r,string value,GUIStyle style)
    {
        var scaled=new GUIStyle(style){fontSize=Mathf.Max(9,Mathf.RoundToInt(style.fontSize*table.DisplayScale))};
        Label(table.ProjectLabel(r),value,scaled);
    }
    void HandleBuildInput(OperationSnapshot s)
    {
        var e=Event.current;
        if(!GUI.enabled||HasOverlay||DirectSelecting||e.type!=EventType.MouseDown||e.button!=0)return;
        if(faces.Any(face=>face.Shape.Contains(e.mousePosition)))return;
        // 后绘制的棋子在上层，透明边缘不接收点击；名称与整组区域没有拿取入口。
        PieceFace hit=null;
        for(int i=pieceFaces.Count-1;i>=0;i--)if(pieceFaces[i].Shape.Contains(e.mousePosition)){hit=pieceFaces[i];break;}
        if(hit==null)return;
        var build=hit.Build;
        if(DirectSelecting||!build.CanPrepare(s)){s.Notify?.Invoke(DirectSelecting?"请先完成或取消技能选择":!s.CanInteract?"等待当前行动或待决席位":build.UnavailableReason??"当前不能拿起棋子");e.Use();return;}
        s.Cancel?.Invoke();raisedCard=null;Selection.Choose(build,s.Goods,hit.Ordinal);tradeOpen=false;discardCard=null;message="";
        pieceReleased=false;pieceDragging=false;heldPiece=null;
        s.PrepareBuild?.Invoke(build);heldPiece=build;pieceStart=e.mousePosition;
        e.Use();
    }
    void HandlePieceInput(OperationSnapshot s)
    {
        if(heldPiece==null)return;
        if(MobileTabletop.Enabled&&Input.touchCount>1){heldPiece=null;pieceDragging=false;pieceReleased=false;return;}
        var e=Event.current;
        if(e.type==EventType.MouseDrag&&e.button==0)
        {if((e.mousePosition-pieceStart).sqrMagnitude>(MobileTabletop.Enabled?18*18:49))pieceDragging=true;e.Use();}
        if(e.type==EventType.MouseUp&&e.button==0)
        {
            if(pieceDragging)
            {
                pieceReleasePoint=e.mousePosition;
                pieceReleased=!BlocksBoardInput(pieceReleasePoint)&&!HasOverlay;
                if(!pieceReleased){Selection.Clear();s.Cancel?.Invoke();}
            }
            heldPiece=null;pieceDragging=false;e.Use();
        }
    }
    void DrawCost(OperationSnapshot s)
    {
        var b=s.Builds.FirstOrDefault(x=>x.Id==hoveredBuild);if(b==null)return;
        var goods=Enumerable.Range(0,s.GoodCount).Where(good=>b.Cost[good]>0).ToArray();
        var style=new GUIStyle(center){fontSize=14,wordWrap=false,alignment=TextAnchor.MiddleLeft};
        string title=b.Name+"：";
        float titleWidth=style.CalcSize(new GUIContent(title)).x+6;
        float width=titleWidth+Math.Max(40,goods.Sum(good=>b.Cost[good])*32)+20;
        tooltipRect=new Rect(Mathf.Clamp(costAnchor.x-width/2,12,1428-width),Mathf.Clamp(costAnchor.y-55,12,817),width,45);
        Paper(tooltipRect);float x=tooltipRect.x+10;
        Label(new Rect(x,tooltipRect.y,titleWidth,45),title,style);x+=titleWidth;
        if(goods.Length==0)Label(new Rect(x,tooltipRect.y,40,45),"免费",style);
        foreach(int good in goods)
        {
            for(int ordinal=0;ordinal<b.Cost[good];ordinal++)
            {
                OperationIconAssets.DrawGood(new Rect(x,tooltipRect.y+4,26,37),good,
                    tint:ordinal>=s.Goods[good]?new Color(1,.48f,.48f):Color.white);x+=32;
            }
        }
    }
}
