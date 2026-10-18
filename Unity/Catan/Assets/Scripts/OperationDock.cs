using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 桌面上的个人组件与手牌。拿取、费用与交换草稿均不直接修改规则状态。
public sealed partial class OperationDock
{
    public readonly OperationSelection Selection = new OperationSelection();
    static readonly string[] Names = { "木材", "砖块", "羊毛", "谷物", "矿石", "布匹", "钱币", "纸张" };
    static Color Surface => ModernOperationUI.Surface;
    static Color Line => ModernOperationUI.Line;
    static Color Gold => ModernOperationUI.Accent;
    static Color Ink => ModernOperationUI.TextColor;
    static Color Muted => ModernOperationUI.Muted;
    static Color Missing => ModernOperationUI.Error;
    static readonly Rect TradeRect = new Rect(262,139,916,506);
    static readonly Rect CardRect = new Rect(376,167,688,459), GiveDropRect = new Rect(286,276,406,266);
    GUIStyle text, small, heading, center, tableLabel, quantity;
    Font quantityFont;
    OperationSnapshot current;
    public Func<string,string> PlayerName;
    string DisplayPlayer(string seat)=>PlayerName?.Invoke(seat)??M6App.PlayerLabel(seat);
    string revision, scope, hoveredBuild, previewCard, message = "";
    bool tradeOpen, playerTrade;
    bool specialHand;
    int facilityTrack=-1;
    int? portGood;
    bool portTrade;
    string tradeTitle;
    TabletopHandOrder fallbackOrder = new TabletopHandOrder();
    TabletopSpecialHandOrder specialOrder = new TabletopSpecialHandOrder();
    int target;
    readonly int[] give = new int[8], receive = new int[8];
    int dragGood=-1, dragOrdinal;
    Vector2 dragStart;
    double handPressedAt;
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
    readonly Dictionary<string,float> spreadAmounts = new Dictionary<string,float>();
    readonly Dictionary<TabletopCollectionFlight,List<HandFace>> landingFaces = new Dictionary<TabletopCollectionFlight,List<HandFace>>();
    TabletopResourceSlot[] resourceSlots = Array.Empty<TabletopResourceSlot>();
    TabletopResourceSlot[] specialSlots = Array.Empty<TabletopResourceSlot>();
    string hoveredHand;
    Vector2 handHoverMouse;
    Rect handHoverAnchor;
    double handUpdated;
    TabletopCollectionTimeline collections;
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
        public bool Obscured;
        public float Hover;
        public string Key => Card==null?"good:"+Good+":"+Ordinal:"card:"+Card.Id;
    }

    public bool TradeOpen => tradeOpen;
    public bool IsPortTrade => tradeOpen&&portTrade;
    internal int VerificationFacilityTrack => facilityTrack;
    internal int[] VerificationTradeGiveGoods => tradeOpen?OperationPanelOptions.PaymentGoods(current,portTrade?portGood:null):Array.Empty<int>();
    public bool ResourceHandVisible => !specialHand;
    internal bool DiscardMatches(OperationSnapshot snapshot) => ReferenceEquals(current,snapshot);
    public OperationHandCard SelectedDiscardCard => current?.Discard?.Special==true ? current.Cards.FirstOrDefault(c=>c.Id==previewCard) : null;
    public bool HasCancelableInteraction => tradeOpen || facilityTrack>=0 || previewCard!=null || Selection.BuildId!=null || Selection.Selected.Any(n=>n>0) || heldPiece!=null || dragging;
    // 银行牌堆与旧购买流程共用这一入口，仍由适配层校验并提交指令。
    public void PrepareDevelopmentPurchase()
    {
        if(current==null||HasOverlay)return;
        var build=current.Builds.FirstOrDefault(b=>b.Icon=="development");
        if(build==null)return;
        PrepareBuild(build,current);
    }
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
    public bool HasOverlay => tradeOpen || facilityTrack>=0 || previewCard!=null&&current?.Discard==null || current!=null&&current.Offer!=null;
    public bool IsDraggingPiece => pieceDragging;
    public bool ConsumeBuildRelease(out Vector2 releasedLogicalMouse)
    {releasedLogicalMouse=pieceReleasePoint;bool released=pieceReleased;pieceReleased=false;return released;}
    public bool ConsumeBuildRelease(Vector2 logicalMouse)
    {Vector2 release;return ConsumeBuildRelease(out release)&&(logicalMouse-release).sqrMagnitude<=24*24;}
    public void OpenTrade(string target=null)
    {
        if(current==null||!current.CanTrade||!current.CanInteract||current.Offer!=null)return;
        current.Cancel?.Invoke();tradeOpen=true;facilityTrack=-1;previewCard=null;Selection.BuildId=null;Selection.PieceOrdinal=-1;message="";
        portTrade=false;portGood=null;tradeTitle=target==null?"银行兑换":"玩家交易";
        playerTrade=target!=null;
        this.target=target==null?0:Math.Max(0,Array.IndexOf(current.OtherPlayers,target));
        if(playerTrade)Array.Copy(Selection.Selected,give,8);else Array.Clear(give,0,8);Array.Clear(receive,0,8);
    }
    public void OpenPortTrade(int? resource,string title)
    {
        if(resource.HasValue&&(resource.Value<0||resource.Value>=5))return;
        OpenTrade();if(!tradeOpen)return;
        portTrade=true;portGood=resource;tradeTitle=string.IsNullOrWhiteSpace(title)?resource.HasValue?Names[resource.Value]+"港口":"通用港口":title;
    }
    public void OpenFacility(int track)
    {
        if(OperationPanelOptions.Facility(current,track)==null||current.Offer!=null)return;
        current.Cancel?.Invoke();Selection.Clear();tradeOpen=false;previewCard=null;facilityTrack=track;message="";
    }
    public int FacilityLevel(int track) => current?.Builds.FirstOrDefault(b=>b.Track==track)?.Level??0;
    public void Reset()
    {
        Selection.Clear(); tradeOpen=false;facilityTrack=-1;portTrade=false;portGood=null; previewCard=null; hoveredBuild=null; message="";
        Array.Clear(give,0,8);Array.Clear(receive,0,8); tooltipRect=Rect.zero;
        dragGood=-1;dragging=false;hits.Clear();faces.Clear();pieceFaces.Clear();
        hoveredHand=null;handHoverAnchor=Rect.zero;
        heldPiece=null;pieceDragging=false;pieceReleased=false;
        target=0;playerTrade=false;
    }
    public bool BlocksBoardInput(Vector2 logicalMouse)
    {
        if(HasOverlay)return true;
        return dragGood>=0 || hoveredHand!=null&&handHoverAnchor.Contains(logicalMouse) || (tradeOpen||current!=null&&current.Offer!=null)&&TradeRect.Contains(logicalMouse) ||
            previewCard!=null&&current?.Discard==null&&CardRect.Contains(logicalMouse) || hits.Any(h=>h.Contains(logicalMouse));
    }
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
        if(scope!=snapshot.Scope){hoverAmounts.Clear();spreadAmounts.Clear();hoveredHand=null;handHoverAnchor=Rect.zero;handUpdated=Time.unscaledTimeAsDouble;fallbackOrder=new TabletopHandOrder();specialOrder=new TabletopSpecialHandOrder();specialHand=false;}
        if(scope!=snapshot.Scope||revision!=snapshot.Revision){Reset();scope=snapshot.Scope;revision=snapshot.Revision;}
        current=snapshot;Selection.Sync(snapshot);
        if(snapshot.Discard!=null)specialHand=snapshot.Discard.Special;
        hoveredBuild=null;tooltipRect=Rect.zero;hits.Clear();faces.Clear();pieceFaces.Clear();
        // 在任何世界组件接收 MouseDown 前，先按本帧屏幕布局确定手牌命中。
        // 悬停只向上轻推，并让相邻手牌让出一小段空间；点击再向上推高。
        PrepareHand(snapshot);
        bool dockEnabled=GUI.enabled;
        bool overHand=hoveredHand!=null&&handHoverAnchor.Contains(Event.current.mousePosition)||faces.Any(face=>face.Shape.Contains(Event.current.mousePosition))||HandTabsArea.Contains(Event.current.mousePosition)||
            (Selection.Selected.Sum()>0||Selection.BuildId!=null)&&CancelSelectionRect.Contains(Event.current.mousePosition);
        GUI.enabled=dockEnabled&&(Event.current.type==EventType.Repaint||!HasOverlay&&!overHand);
        table.Objects.Begin("own");
        DrawBuilds(snapshot);
        table.Objects.End();
        GUI.enabled=dockEnabled;
        if(overHand)hoveredBuild=null;
        DrawHand(snapshot);
        DrawHandTabs(snapshot);
        HandleHandInput(snapshot);
        HandleBuildInput(snapshot);
        if(tradeOpen)DrawTrade(snapshot);
        else if(snapshot.Offer!=null)DrawOffer(snapshot);
        else if(previewCard!=null&&snapshot.Discard==null)DrawCardPreview(snapshot);
        else if(facilityTrack>=0)DrawFacilities(snapshot);
        else DrawCost(snapshot);
        HandlePieceInput(snapshot);
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
            if(piece!=null)GUI.DrawTexture(new Rect(Event.current.mousePosition.x-size/2,Event.current.mousePosition.y-size*.58f,size,size),piece,ScaleMode.ScaleToFit);
        }
    }
    void DrawBuilds(OperationSnapshot s)
    {
        var pieces=s.Builds.Where(b=>b.Track<0&&b.Icon!="development").ToArray();
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
                    var position=StockPieceLayout(r,build.Icon??build.Id,n);
                    var world=table.Objects.Piece("stock-"+build.Id+"-"+n,build.Icon??build.Id,s.Seat,Math.Max(1,build.Level),position,picked,displayMultiplier:1);
                    if(world==null)continue;
                    var shape=new HitShape{World=world};
                    hovered|=shape.Contains(Event.current.mousePosition);
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
        if(!build.CanPrepare(s)){s.Notify?.Invoke(!s.CanInteract?"等待当前行动或待决席位":build.UnavailableReason??"当前不能使用");return;}
        s.Cancel?.Invoke();Selection.Choose(build,s.Goods);tradeOpen=false;previewCard=null;message="";
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
        if(!GUI.enabled||HasOverlay||e.type!=EventType.MouseDown||e.button!=0)return;
        if(faces.Any(face=>face.Shape.Contains(e.mousePosition)))return;
        // 后绘制的棋子在上层，透明边缘不接收点击；名称与整组区域没有拿取入口。
        PieceFace hit=null;
        for(int i=pieceFaces.Count-1;i>=0;i--)if(pieceFaces[i].Shape.Contains(e.mousePosition)){hit=pieceFaces[i];break;}
        if(hit==null)return;
        var build=hit.Build;
        if(!build.CanPrepare(s)){s.Notify?.Invoke(!s.CanInteract?"等待当前行动或待决席位":build.UnavailableReason??"当前不能拿起棋子");e.Use();return;}
        s.Cancel?.Invoke();Selection.Choose(build,s.Goods,hit.Ordinal);tradeOpen=false;previewCard=null;message="";
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
    void PrepareHand(OperationSnapshot s)
    {
        specialOrder.Sync(s.Cards.Select(c=>c.Id));
        if(collections==null)
        {
            fallbackOrder.Sync(s.Goods,s.GoodCount,null);var ordinals=new int[8];
            resourceSlots=fallbackOrder.Goods.Select(good=>new TabletopResourceSlot{Good=good,Ordinal=ordinals[good]++}).ToArray();
        }
        else resourceSlots=collections.HandSlots(s.Seat,s.Goods,s.GoodCount,Time.unscaledTimeAsDouble);
        float width=TabletopHandLayout.ResourceWidth(false),center=TabletopHandLayout.ResourceCenter(false);
        if(specialHand)DrawSpecialHand(s,new Rect(center-width/2,TabletopHandLayout.Baseline,width,151));
        else DrawResourceHand(s,new Rect(center-width/2,TabletopHandLayout.Baseline,width,151));
        // 仅被后绘制卡牌实际遮挡的牌需要邻牌让位；已经完整露出的牌保持邻牌位置。
        for(int i=0;i<faces.Count;i++)faces[i].Obscured=faces.Skip(i+1).Any(next=>faces[i].RestRect.Overlaps(next.RestRect));
        // 保留整副手牌原来的叠放顺序；抬牌不改变层级和相同资源的身份。
        UpdateHandHover();
    }
    void UpdateHandHover()
    {
        var mouse=Event.current.mousePosition;
        bool overPaper=(tradeOpen||current.Offer!=null)&&TradeRect.Contains(mouse)||previewCard!=null&&CardRect.Contains(mouse)||facilityTrack>=0;
        // 先还原上一帧实际显示位置，再决定命中；不能用未展开的底牌位置猜测鼠标所指。
        foreach(var face in faces)
        {
            float lift,spread;hoverAmounts.TryGetValue(face.Key,out lift);spreadAmounts.TryGetValue(face.Key,out spread);
            var rect=face.RestRect;rect.x+=spread;rect.y-=lift*10;face.Shape.Rect=rect;
        }
        HandFace hovered=null;
        if(GUI.enabled&&!overPaper)
        {
            // 光标不动时，动画让出的底边不能把焦点交给下面那张牌，造成反复升降。
            // 一旦移动鼠标就重新按真实叠放位置命中，允许直接进入展开后露出的邻牌。
            if(hoveredHand!=null&&(mouse-handHoverMouse).sqrMagnitude<.01f&&handHoverAnchor.Contains(mouse))
                hovered=faces.FirstOrDefault(face=>face.Key==hoveredHand);
            if(hovered==null)
                for(int i=faces.Count-1;i>=0;i--)if(faces[i].Shape.Contains(mouse)){hovered=faces[i];break;}
            // 抬起以后仍保留原底边的命中，防止光标停留在边缘时反复升降。
            if(hovered==null&&hoveredHand!=null)
            {
                var previous=faces.FirstOrDefault(face=>face.Key==hoveredHand);
                if(previous!=null){var corridor=previous.Shape.Rect;corridor.yMax=previous.RestRect.yMax;if(corridor.Contains(mouse))hovered=previous;}
            }
        }
        if(hovered==null)handHoverAnchor=Rect.zero;
        else if(hovered.Key!=hoveredHand||(mouse-handHoverMouse).sqrMagnitude>=.01f)
        {
            handHoverAnchor=hovered.Shape.Rect;
            handHoverAnchor.yMax=Mathf.Max(handHoverAnchor.yMax,hovered.RestRect.yMax);
        }
        handHoverMouse=mouse;
        hoveredHand=hovered?.Key;
        double now=Time.unscaledTimeAsDouble;
        float blend=1-Mathf.Exp(-(float)Math.Max(0,now-handUpdated)*14);handUpdated=now;
        var active=new HashSet<string>(faces.Select(face=>face.Key));
        foreach(var stale in hoverAmounts.Keys.Where(key=>!active.Contains(key)).ToArray())hoverAmounts.Remove(stale);
        foreach(var stale in spreadAmounts.Keys.Where(key=>!active.Contains(key)).ToArray())spreadAmounts.Remove(stale);
        int hoverIndex=hovered==null?-1:faces.IndexOf(hovered);
        foreach(var face in faces)
        {
            float amount;hoverAmounts.TryGetValue(face.Key,out amount);
            amount=Mathf.Lerp(amount,face==hovered?1:0,blend);hoverAmounts[face.Key]=amount;face.Hover=amount;
            float spread;spreadAmounts.TryGetValue(face.Key,out spread);
            bool sameHand=hovered!=null&&(face.Good>=0)==(hovered.Good>=0);
            float targetSpread=sameHand&&hovered.Obscured?Math.Sign(faces.IndexOf(face)-hoverIndex)*TabletopHandLayout.HoverSpread:0;
            spread=Mathf.Lerp(spread,targetSpread,blend);spreadAmounts[face.Key]=spread;
            var r=face.RestRect;r.x+=spread;r.y-=amount*10;face.Shape.Rect=r;
            face.Shape.Angle=0;
        }
    }
    void DrawHand(OperationSnapshot s)
    {
        landingFaces.Clear();
        var handSlots=specialHand?specialSlots:resourceSlots;
        var landing=collectionFlightsVisible&&!HasOverlay?handSlots.Where(slot=>slot.Flight!=null&&slot.Flight.Progress(Time.unscaledTimeAsDouble)>=.60).ToArray():Array.Empty<TabletopResourceSlot>();
        foreach(var face in faces)
        {
            // 每张卡只画一次。先留下应盖住落位飞牌的纸牌，在对应插槽之后补画，防止半透明软影反复叠深。
            TabletopResourceSlot after=null;
            if(landing.Length>0)
            {
                if(face.Good>=0)
                {
                    int faceIndex=Array.FindIndex(handSlots,slot=>slot.Good==face.Good&&slot.Ordinal==face.Ordinal);
                    after=landing.LastOrDefault(slot=>Array.IndexOf(handSlots,slot)<faceIndex);
                }
                else if(face.Card!=null)
                {
                    int faceIndex=Array.FindIndex(handSlots,slot=>slot.Card?.Id==face.Card.Id);
                    after=landing.LastOrDefault(slot=>Array.IndexOf(handSlots,slot)<faceIndex);
                }
            }
            if(after==null)DrawHandFace(face);
            else
            {
                if(!landingFaces.TryGetValue(after.Flight,out var deferred))landingFaces[after.Flight]=deferred=new List<HandFace>();
                deferred.Add(face);
            }
            hits.Add(face.Shape);
        }
        if((Selection.Selected.Sum()>0||Selection.BuildId!=null)&&Button(CancelSelectionRect,"取消选择"))
        {Selection.Clear();s.Cancel?.Invoke();message="";}
    }
    static readonly Rect HandTabsArea = new Rect(151,681,104,189), CancelSelectionRect = new Rect(155,681,96,34);
    void DrawHandTabs(OperationSnapshot s)
    {
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!HasOverlay;
        Rect resource=new Rect(155,727,96,43),special=new Rect(155,777,96,43),sort=new Rect(155,830,96,32);
        Register(resource);Register(special);Register(sort);
        bool handEnabled=GUI.enabled;GUI.enabled=handEnabled&&s.Discard==null;
        if(StrategyHudArt.HandControl(resource,"资源卡",0,!specialHand,new TabletopHandCounter(s.Goods.Take(s.GoodCount).Sum(),s.ResourceHandLimit))){specialHand=false;hoveredHand=null;handHoverAnchor=Rect.zero;}
        if(StrategyHudArt.HandControl(special,"发展卡",1,specialHand,new TabletopHandCounter(s.Cards.Count,s.SpecialHandLimit))){specialHand=true;hoveredHand=null;handHoverAnchor=Rect.zero;}
        GUI.enabled=handEnabled;
        if(StrategyHudArt.HandControl(sort,"整理手牌",2))
        {
            fallbackOrder.Sort();collections?.SortHand(s.Seat);specialOrder.Sort(s.Cards.OrderBy(c=>c.Progress?c.ArtIndex:DevelopmentOrder(c.Id)).ThenBy(c=>c.Name).Select(c=>c.Id));
            collections?.SortSpecialHand(s.Seat,s.Cards.OrderBy(c=>c.Progress?c.ArtIndex:DevelopmentOrder(c.Id)).ThenBy(c=>c.Name).Select(c=>c.Id));
            hoveredHand=null;handHoverAnchor=Rect.zero;spreadAmounts.Clear();
        }
        if(specialHand&&s.GoodCount==5)
        {
            var purchase=s.Builds.FirstOrDefault(b=>b.Icon=="development");
            if(purchase!=null&&Button(new Rect(268,670,152,34),"购买发展卡",purchase.CanPrepare(s)))PrepareDevelopmentPurchase();
        }
        GUI.enabled=enabled;
    }
    static int DevelopmentOrder(string id){int kind;return int.TryParse((id??"").Split(':')[0],out kind)?kind:0;}
    public void DrawLandingOcclusion(TabletopCollectionFlight flight)
    {
        if(Event.current.type!=EventType.Repaint||!landingFaces.TryGetValue(flight,out var deferred))return;
        foreach(var face in deferred)DrawHandFace(face);
    }
    void DrawSpecialHand(OperationSnapshot s,Rect area)
    {
        specialSlots=collections==null?specialOrder.Sync(s.Cards.Select(c=>c.Id)).Select(id=>new TabletopResourceSlot{Good=-1,Card=s.Cards.First(c=>c.Id==id)}).ToArray():
            collections.SpecialSlots(s.Seat,s.Cards,Time.unscaledTimeAsDouble);
        for(int i=0;i<specialSlots.Length;i++)
        {
            var slot=specialSlots[i];if(slot.Flight!=null)continue;
            var card=slot.Card;var pose=TabletopHandLayout.Resource(specialSlots,i,area.center.x,area.width,previewCard==card.Id);
            var r=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
            var texture=card.Progress&&card.ArtIndex>=0?EntityCardAssets.ProgressFront(card.ArtIndex):null;
            r=EntityCardAssets.FitCardRect(r,texture!=null?(float)texture.width/texture.height:TabletopHandLayout.CardWidth/TabletopHandLayout.CardHeight,GUI.matrix);
            faces.Add(new HandFace{Shape=new HitShape{Rect=r},RestRect=r,Card=card,Texture=texture,Selected=previewCard==card.Id});
        }
    }
    void DrawResourceHand(OperationSnapshot s,Rect area)
    {
        var slots=resourceSlots;
        for(int index=0;index<slots.Length;index++)
        {
            var slot=slots[index];if(slot.Flight!=null)continue;
            int good=slot.Good,n=slot.Ordinal;
            var texture=EntityCardAssets.Texture(EntityCardAssets.Good(good).Front);
            bool chosen=Selection.IsCardSelected(good,n);var pose=TabletopHandLayout.Resource(slots,index,area.center.x,area.width,chosen);
            var r=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
            if(texture!=null)r=EntityCardAssets.FitCardRect(r,(float)texture.width/texture.height,GUI.matrix);
            faces.Add(new HandFace{Shape=new HitShape{Rect=r},RestRect=r,Good=good,Ordinal=n,Texture=texture,Selected=chosen});
        }
    }
    void DrawHandFace(HandFace face)
    {
        var r=face.Shape.Rect;
        var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(face.Shape.Angle,r.center);
        if(face.Texture!=null)EntityCardAssets.DrawCardFace(r,face.Texture,face.Hover);
        else
        {
            r=EntityCardAssets.FitCardRect(r,TabletopHandLayout.CardWidth/TabletopHandLayout.CardHeight,GUI.matrix);
            EntityCardAssets.DrawCardPaper(r,new Color(.13f,.22f,.23f),face.Hover);
            Label(new Rect(r.x+10,r.y+9,r.width-20,20),face.Card.Progress?"进步卡":"发展卡",small);
            Label(new Rect(r.x+11,r.y+36,r.width-22,64),face.Card.Name,new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,fontSize=21});
        }
        GUI.matrix=matrix;
    }
    void HandleHandInput(OperationSnapshot s)=>ProcessHandInput(s,Event.current);
    internal void ProcessHandInput(OperationSnapshot s,Event e)
    {
        if(!GUI.enabled)return;
        if(MobileTabletop.Enabled&&Input.touchCount>1){dragGood=-1;dragging=false;return;}
        HandFace hit=null;
        if(hoveredHand!=null&&handHoverAnchor.Contains(e.mousePosition))hit=faces.FirstOrDefault(face=>face.Key==hoveredHand);
        if(hit==null)for(int i=faces.Count-1;i>=0;i--)if(faces[i].Shape.Contains(e.mousePosition)){hit=faces[i];break;}
        bool overPaper=(tradeOpen||s.Offer!=null)&&TradeRect.Contains(e.mousePosition)||s.Discard==null&&previewCard!=null&&CardRect.Contains(e.mousePosition)||facilityTrack>=0;
        if(e.type==EventType.ScrollWheel&&hit!=null&&!overPaper)
        {
            // 全部手牌始终在场；手牌上滚动不再改变桌面相机距离。
            e.Use();return;
        }
        if(e.type==EventType.MouseDown&&e.button==1&&hit!=null&&hit.Good>=0&&!overPaper)
        {
            ChangeHandCard(s,hit.Good,hit.Ordinal,true);e.Use();return;
        }
        if(e.type==EventType.MouseDown&&e.button==0&&hit!=null&&!overPaper)
        {
            if(hit.Card!=null){previewCard=previewCard==hit.Card.Id?null:hit.Card.Id;tradeOpen=false;if(s.Discard==null)s.Cancel?.Invoke();}
            else{dragGood=hit.Good;dragOrdinal=hit.Ordinal;dragStart=e.mousePosition;handPressedAt=Time.unscaledTimeAsDouble;dragging=false;}
            e.Use();return;
        }
        if(dragGood<0)return;
        if(e.type==EventType.MouseDrag&&e.button==0){if((e.mousePosition-dragStart).sqrMagnitude>(MobileTabletop.Enabled?18*18:49))dragging=true;e.Use();}
        if(e.type==EventType.MouseUp&&e.button==0)
        {
            s.Cancel?.Invoke();previewCard=null;
            if(dragging&&tradeOpen&&playerTrade&&GiveDropRect.Contains(e.mousePosition))
            {Selection.SelectCard(dragGood,dragOrdinal,s.Goods[dragGood]);Array.Copy(Selection.Selected,give,8);message="";}
            else if(!dragging)
            {ChangeHandCard(s,dragGood,dragOrdinal,MobileTabletop.Enabled&&Time.unscaledTimeAsDouble-handPressedAt>=.5);}
            dragGood=-1;dragging=false;e.Use();
        }
    }
    void ChangeHandCard(OperationSnapshot s,int good,int ordinal,bool putBack)
    {
        bool selected=Selection.IsCardSelected(good,ordinal);
        if(putBack&&!selected)return;
        if(!selected&&s.Discard!=null&&!s.Discard.Special&&Selection.Selected.Sum()>=s.Discard.Amount)
        {s.Notify?.Invoke("已达到可选数量，不允许继续选择。");return;}
        s.Cancel?.Invoke();previewCard=null;Selection.BuildId=null;Selection.PieceOrdinal=-1;
        Selection.Toggle(good,ordinal,s.Goods[good]);
        if(tradeOpen&&playerTrade){Array.Copy(Selection.Selected,give,8);message="";}
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
                if(ordinal>=s.Goods[good])ModernOperationUI.Round(new Rect(x-2,tooltipRect.y+2,30,41),Missing);
                OperationIconAssets.DrawGood(new Rect(x,tooltipRect.y+4,26,37),good);x+=32;
            }
        }
    }
    void DrawCardPreview(OperationSnapshot s)
    {
        var c=s.Cards.FirstOrDefault(x=>x.Id==previewCard);if(c==null){previewCard=null;return;}
        // 实体卡面独立完整摆放，操作说明位于一旁，不用面板边框包住卡牌。
        Paper(new Rect(704,167,360,459));
        Label(new Rect(725,190,271,39),c.Name,heading);
        if(Button(new Rect(1011,181,32,32),"×")){previewCard=null;return;}
        var texture=c.Progress&&c.ArtIndex>=0?EntityCardAssets.ProgressFront(c.ArtIndex):null;
        var face=new Rect(387,178,294,435);
        if(texture!=null)EntityCardAssets.DrawCardFace(face,texture);
        else{face=EntityCardAssets.FitCardRect(face,TabletopHandLayout.CardWidth/TabletopHandLayout.CardHeight,GUI.matrix);ModernOperationUI.Round(face,Surface);Label(face,c.Name,new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,fontSize=28});}
        Label(new Rect(725,253,317,129),c.Detail??"选择使用后指定目标。",text);
        string reason=c.UnavailableReason??(!s.CanInteract?"等待你的行动或待决选择":c.Name=="胜利点"?"胜利点保留在手中，无需主动打出":c.Progress?"当前阶段或目标条件不满足此卡的使用要求":"本回合买入的卡不能使用；每回合最多打出一张行动发展卡");
        if(!c.Playable||!s.CanInteract)Label(new Rect(725,408,317,99),reason,small);
        if(Primary(new Rect(725,561,187,43),"使用卡牌",c.Playable&&s.CanInteract))
        {s.PrepareCard?.Invoke(c);previewCard=null;Selection.Clear();}
        if(Button(new Rect(923,561,119,43),"放回手牌"))previewCard=null;
    }
    void DrawFacilities(OperationSnapshot s)
    {
        var build=OperationPanelOptions.Facility(s,facilityTrack);
        if(build==null){facilityTrack=-1;return;}
        int track=facilityTrack;
        var tracks=new[]{"贸易","政治","科学"};
        var area=new Rect(440,132,560,530);Paper(area);
        Label(new Rect(466,151,430,35),tracks[track]+"设施",heading);
        Label(new Rect(466,188,470,26),"翻开下一等级，建设你的城市文明。",small);
        if(Button(new Rect(947,149,32,32),"×")){facilityTrack=-1;s.Cancel?.Invoke();return;}
        var texture=EntityCardAssets.Texture(EntityCardAssets.Guidance(false).Front);
        float[] cuts={0,.337f,.668f,1};
        // 在同一张完整建设板上铺满翻板；沿原照定位，但接合边不再留出可见暗缝。
        var slots=new[]{new Rect(.503f,.638f,.366f,.279f),new Rect(.151f,.638f,.352f,.279f),
            new Rect(.151f,.361f,.352f,.277f),new Rect(.151f,.085f,.352f,.276f),new Rect(.503f,.085f,.366f,.276f)};
        var card=new Rect(464,237,298,395);
        var accent=track==0?new Color(.94f,.73f,.25f):track==1?new Color(.40f,.67f,.94f):new Color(.49f,.79f,.42f);
        ModernOperationUI.Round(new Rect(card.x-4,card.y-4,card.width+8,card.height+8),accent);
        if(texture!=null)GUI.DrawTextureWithTexCoords(card,texture,new Rect(cuts[track],0,cuts[track+1]-cuts[track],1));
        Register(card);
        for(int level=1;level<=5;level++)
        {
            var loc=slots[level-1];var slot=new Rect(card.x+loc.x*card.width,card.y+loc.y*card.height,loc.width*card.width,loc.height*card.height);
            var face=level<=build.Level?EntityCardAssets.ImprovementBack(track,level):EntityCardAssets.ImprovementFront(track,level);
            if(face!=null)GUI.DrawTexture(slot,face,ScaleMode.StretchToFill);
            bool over=slot.Contains(Event.current.mousePosition);
            if(over&&level==build.Level+1){hoveredBuild=build.Id;costAnchor=new Vector2(slot.center.x,slot.y);}
            if(GUI.enabled&&over&&Event.current.type==EventType.MouseDown&&Event.current.button==0)
            {
                Event.current.Use();
                if(level<=build.Level)s.Notify?.Invoke("这一级设施已经建成");
                else if(level!=build.Level+1)s.Notify?.Invoke("请先升级第 "+(build.Level+1)+" 级设施");
                else PrepareBuild(build,s);
            }
        }
        Label(new Rect(788,247,186,35),"当前  "+build.Level+" 级",new GUIStyle(heading){fontSize=20});
        Fill(new Rect(788,298,184,2),accent);
        Label(new Rect(788,319,186,160),build.Detail??FacilityAbility(track,build.Level),text);
        Label(new Rect(788,506,186,72),build.Level>=5?"本设施已全部建成":"点击建设板上的下一等级，或使用下方按钮升级。",small);
        if(Primary(new Rect(788,591,186,41),build.Level>=5?"已达最高等级":"建设第 "+(build.Level+1)+" 级",build.CanPrepare(s)&&build.Level<5))PrepareBuild(build,s);
        if(hoveredBuild!=null)DrawCost(s);
    }
    public static string FacilityAbility(int track,int level)
    {
        if(level<3)return "达到 3 级后解锁城市能力";
        return track==0?"贸易所：商品可以 2 : 1 兑换资源或商品":track==1?"要塞：允许晋升精锐骑士":"引水渠：无产出时可选择一张资源（7 除外）";
    }
    OperationSnapshot TradeValidationSnapshot(OperationSnapshot s)
    {
        if(!portTrade)return s;
        var rates=(int[])s.Rates.Clone();
        if(portGood.HasValue)rates[portGood.Value]=2;
        else for(int good=0;good<s.GoodCount;good++)rates[good]=3;
        return new OperationSnapshot{CanTrade=s.CanTrade,CanInteract=s.CanInteract,Offer=s.Offer,Goods=s.Goods,Bank=s.Bank,Rates=rates,GoodCount=s.GoodCount,OtherPlayers=s.OtherPlayers};
    }
    void DrawTrade(OperationSnapshot s)
    {
        target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
        Paper(TradeRect);
        var validation=TradeValidationSnapshot(s);
        Label(new Rect(287,156,600,34),tradeTitle??"资源交易",heading);
        if(Button(new Rect(1115,158,38,32),"×")){Reset();s.Cancel?.Invoke();return;}
        Label(new Rect(287,202,760,36),playerTrade?"选择交易玩家 · "+TabletopCardInput.Hint:portTrade?TabletopCardInput.Hint+" · 按港口比例选择数量":TabletopCardInput.Hint+" · 按银行比例选择数量",small);
        if(playerTrade)
        {
            target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
            for(int i=0;i<s.OtherPlayers.Length;i++)if(ModernOperationUI.Button(new Rect(766+i*130,229,121,35),DisplayPlayer(s.OtherPlayers[i]),true,false,target==i,s.OtherPlayers[i]))target=i;
            if(dragging&&GiveDropRect.Contains(Event.current.mousePosition))Fill(GiveDropRect,new Color(.77f,.64f,.39f,.22f));
            DrawTradePlayer(new Rect(286,268,406,58),s.Seat);
            DrawTradePlayer(new Rect(744,268,406,58),s.OtherPlayers.Length>0?s.OtherPlayers[target]:"*");
            DrawTradeBag(s,new Rect(286,326,406,218),give,true);
            DrawTradeBag(s,new Rect(744,326,406,218),receive,false);
        }
        else
        {
            Label(new Rect(287,282,395,35),"付出",heading);
            Label(new Rect(744,282,395,35),"换取",heading);
            DrawTradeBag(s,new Rect(286,326,406,218),give,true,true);
            DrawTradeBag(s,new Rect(744,326,406,218),receive,false,true);
        }
        var trade=new OperationTrade{Bank=!playerTrade,OtherPlayer=s.OtherPlayers.Length>0?s.OtherPlayers[target]:null,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
        string error=OperationSelection.ValidateTrade(validation,trade);
        var hint=new GUIStyle(small);hint.normal.textColor=error==null?Muted:Missing;
        Label(new Rect(287,549,860,28),message!=""?message:error??"",hint);
        Fill(new Rect(286,581,866,1),Line);
        if(playerTrade)
        {
            var broadcast=new OperationTrade{Bank=false,OtherPlayer=OperationTrade.AllPlayers,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
            if(Button(new Rect(680,593,227,35),"向所有玩家报价",OperationSelection.ValidateTrade(s,broadcast)==null))SendTrade(s,broadcast);
            if(Primary(new Rect(921,593,230,35),"向 "+DisplayPlayer(trade.OtherPlayer)+" 报价",error==null))SendTrade(s,trade);
        }
        else if(Primary(new Rect(744,593,407,35),"确认交换",error==null))SendTrade(s,trade);
        if(Button(new Rect(286,593,154,35),"取消")){Reset();s.Cancel?.Invoke();}
        if(Button(new Rect(454,593,154,35),"清空选择")){Array.Clear(give,0,8);Array.Clear(receive,0,8);Selection.Clear();message="";}
    }
    void SendTrade(OperationSnapshot s,OperationTrade trade)
    {
        message=s.Trade?.Invoke(trade)??"";
        if(message==""){tradeOpen=false;Selection.Clear();}else s.Notify?.Invoke(message);
    }
    void DrawTradePlayer(Rect area,string seat)
    {
        if(seat=="*")
        {
            for(int i=0;i<Math.Min(3,current.OtherPlayers.Length);i++)StrategyHudArt.Avatar(new Rect(area.x+i*24,area.y+3,44,44),current.OtherPlayers[i]);
        }
        else
        {
            StrategyHudArt.Disc(new Rect(area.x,area.y,52,52),BoardRenderer.SeatColor(seat)*.65f);
            StrategyHudArt.Avatar(new Rect(area.x+4,area.y+4,44,44),seat);
        }
        Label(new Rect(area.x+88,area.y+8,area.width-88,36),DisplayPlayer(seat),M6App.PlayerNameStyle(heading,seat));
    }
    void TradeCard(Rect face,int good,bool missing)
    {
        EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(good),0,missing?new Color(1,.23f,.23f,1):Color.white);
        if(missing)Fill(new Rect(face.x,face.yMax-7,face.width,7),Missing);
    }
    void DrawTradeBag(OperationSnapshot s,Rect area,int[] bag,bool paying,bool bank=false)
    {
        var goods=bank&&paying?OperationPanelOptions.PaymentGoods(s,portTrade?portGood:null):Enumerable.Range(0,s.GoodCount).ToArray();
        if(goods.Length==0)Label(new Rect(area.x,area.y+32,area.width,60),"当前没有可以付出的资源。",small);
        for(int index=0;index<goods.Length;index++)
        {
            int i=goods[index];float x=area.x+(index%4)*103,y=area.y+(index/4)*105;
            bool missing=paying&&bag[i]>s.Goods[i];var face=new Rect(x+12,y+1,54,77);
            TradeCard(face,i,missing);
            var caption=new GUIStyle(center);if(missing)caption.normal.textColor=Missing;
            Label(new Rect(x,y+79,100,25),Names[i]+" × "+bag[i],caption);
            if(paying&&!portTrade)Label(new Rect(x+69,y+6,32,26),"有 "+s.Goods[i],small);
            else if(bank&&s.Bank[i]==0)Label(new Rect(x+60,y+6,43,48),"库存为0",small);
            if(bank&&paying&&!portTrade)Label(new Rect(x+69,y+37,32,26),TradeValidationSnapshot(s).Rates[i]+":1",small);
            int delta=TabletopCardInput.Delta(face);
            if(delta!=0)
            {
                int step=portTrade&&paying?TradeValidationSnapshot(s).Rates[i]:1;
                int maximum=bank&&!paying?s.Bank[i]:i<5?19:12;
                if(delta>0&&bag[i]+step>maximum)message="数量已达到上限，不允许继续选择。";
                else for(int n=0;n<step;n++)message=OperationCardQuantity.Change(bag,i,delta,maximum,int.MaxValue,bank&&paying)??"";
                if(paying)for(int good=0;good<8;good++)Selection.SetCount(good,bag[good],s.Goods[good]);
            }
        }
    }
    void DrawOffer(OperationSnapshot s)
    {
        var offer=s.Offer;Paper(TradeRect);
        Label(new Rect(286,157,800,36),"交易报价",heading);
        var perspective=OperationOfferPerspective.For(s);
        Label(new Rect(286,202,330,25),DisplayPlayer(offer.Proposer),M6App.PlayerNameStyle(small,offer.Proposer));
        Label(new Rect(616,202,45,25),"→",small);
        string recipient=offer.IsBroadcast?"*":offer.Recipient;
        Label(new Rect(661,202,425,25),DisplayPlayer(recipient),M6App.PlayerNameStyle(small,recipient));
        DrawTradePlayer(new Rect(286,253,406,58),s.Seat);
        DrawTradePlayer(new Rect(744,253,406,58),perspective.OtherSeat);
        DrawOfferBag(new Rect(286,323,406,195),perspective.OwnGive,s.Goods);
        DrawOfferBag(new Rect(744,323,406,195),perspective.OtherGive,null);
        bool missing=Enumerable.Range(0,8).Any(i=>perspective.OwnGive[i]>s.Goods[i]);
        string status=offer.IsBroadcast?(perspective.OwnOffer?"等待 "+offer.PendingRecipients.Length+" 位玩家回应":offer.RejectedRecipients.Contains(s.Seat)?"已拒绝，等待其他玩家回应":"任意一人接受即成交"):
            !perspective.Involved?"等待回应":perspective.OwnOffer?"等待对方回应":"";
        var hint=new GUIStyle(small);if(missing){status="红色资源牌数量不足，无法接受交易。";hint.normal.textColor=Missing;}
        Label(new Rect(286,538,860,40),status,hint);
        if(offer.CanAccept&&Primary(new Rect(744,590,406,38),"接受交易",!missing))s.Respond?.Invoke("accept");
        if(offer.CanReject&&Button(new Rect(286,590,406,38),"拒绝交易"))s.Respond?.Invoke("reject");
        if(offer.CanCancel&&Button(new Rect(744,590,406,38),"撤回报价"))s.Respond?.Invoke("cancel");
    }
    void DrawOfferBag(Rect area,int[] bag,int[] owned)
    {
        int index=0;
        for(int i=0;i<8;i++)if(bag[i]>0)
        {
            float x=area.x+(index%4)*97,y=area.y+(index/4)*93;
            bool missing=owned!=null&&bag[i]>owned[i];
            TradeCard(new Rect(x+22,y,44,65),i,missing);
            var caption=new GUIStyle(center);if(missing)caption.normal.textColor=Missing;
            Label(new Rect(x,y+66,87,25),"× "+bag[i],caption);index++;
        }
    }
}
