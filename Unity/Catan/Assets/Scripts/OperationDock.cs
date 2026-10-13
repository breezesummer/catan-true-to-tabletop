using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 桌面上的个人组件与手牌。拿取、费用与交换草稿均不直接修改规则状态。
public sealed class OperationDock
{
    public readonly OperationSelection Selection = new OperationSelection();
    static readonly string[] Names = { "木材", "砖块", "羊毛", "谷物", "矿石", "布匹", "钱币", "纸张" };
    static readonly Color Surface = ModernOperationUI.Surface,
        Line = ModernOperationUI.Line, Gold = ModernOperationUI.Accent, Ink = ModernOperationUI.TextColor,
        Muted = ModernOperationUI.Muted, Missing = ModernOperationUI.Error;
    static readonly Rect TradeRect = new Rect(262,139,916,506);
    static readonly Rect CardRect = new Rect(376,167,688,459), GiveDropRect = new Rect(286,276,406,266);
    GUIStyle text, small, heading, center, tableLabel;
    OperationSnapshot current;
    string revision, scope, hoveredBuild, previewCard, message = "";
    bool tradeOpen, playerTrade;
    int giveGood, receiveGood = 1, lots = 1, target;
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
    TabletopScene table;
    public void BindTable(TabletopScene scene) { table=scene; }
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
        public int Good=-1, Ordinal;
        public OperationHandCard Card;
        public Texture2D Texture;
        public bool Selected;
    }

    public bool TradeOpen => tradeOpen;
    // 银行牌堆与旧购买流程共用这一入口，仍由适配层校验并提交指令。
    public void PrepareDevelopmentPurchase()
    {
        if(current==null||HasOverlay)return;
        var build=current.Builds.FirstOrDefault(b=>b.Icon=="development");
        if(build==null)return;
        PrepareBuild(build,current);
    }
    internal Rect[] VerificationHandRects => faces.Select(face=>face.Shape.Rect).ToArray();
    internal bool VerificationHandsAreScreenSpace => faces.All(face=>face.Shape.World==null);
    public bool HasOverlay => tradeOpen || previewCard!=null || current!=null&&current.Offer!=null;
    public bool IsDraggingPiece => pieceDragging;
    public bool ConsumeBuildRelease(out Vector2 releasedLogicalMouse)
    {releasedLogicalMouse=pieceReleasePoint;bool released=pieceReleased;pieceReleased=false;return released;}
    public bool ConsumeBuildRelease(Vector2 logicalMouse)
    {Vector2 release;return ConsumeBuildRelease(out release)&&(logicalMouse-release).sqrMagnitude<=24*24;}
    public void OpenTrade(string target=null)
    {
        if(current==null||!current.CanTrade||!current.CanInteract||current.Offer!=null)return;
        current.Cancel?.Invoke();tradeOpen=true;previewCard=null;Selection.BuildId=null;Selection.PieceOrdinal=-1;message="";
        playerTrade=target!=null;
        this.target=target==null?0:Math.Max(0,Array.IndexOf(current.OtherPlayers,target));
        Array.Copy(Selection.Selected,give,8);Array.Clear(receive,0,8);
    }
    public void Reset()
    {
        Selection.Clear(); tradeOpen=false; previewCard=null; hoveredBuild=null; message="";
        Array.Clear(give,0,8);Array.Clear(receive,0,8); tooltipRect=Rect.zero;
        dragGood=-1;dragging=false;hits.Clear();faces.Clear();pieceFaces.Clear();
        heldPiece=null;pieceDragging=false;pieceReleased=false;
        target=0;giveGood=0;receiveGood=1;lots=1;playerTrade=false;
    }
    public bool BlocksBoardInput(Vector2 logicalMouse)
    {
        if(HasOverlay)return true;
        return dragGood>=0 || (tradeOpen||current!=null&&current.Offer!=null)&&TradeRect.Contains(logicalMouse) ||
            previewCard!=null&&CardRect.Contains(logicalMouse) || hits.Any(h=>h.Contains(logicalMouse));
    }
    void Styles(Font font)
    {
        if(text!=null)return;
        ModernOperationUI.Styles(font);
        text=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};text.normal.textColor=Ink;
        small=new GUIStyle(text){fontSize=12};small.normal.textColor=Muted;
        heading=new GUIStyle(text){fontSize=21,fontStyle=FontStyle.Bold};
        center=new GUIStyle(text){alignment=TextAnchor.MiddleCenter,fontSize=13};
        tableLabel=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};tableLabel.normal.textColor=new Color(.98f,.91f,.76f);
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
        if(scope!=snapshot.Scope||revision!=snapshot.Revision){Reset();scope=snapshot.Scope;revision=snapshot.Revision;}
        current=snapshot;Selection.Sync(snapshot);
        if(GUI.enabled&&(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape||Event.current.type==EventType.MouseDown&&Event.current.button==1))
        {Reset();snapshot.Cancel?.Invoke();Event.current.Use();}
        hoveredBuild=null;tooltipRect=Rect.zero;hits.Clear();faces.Clear();pieceFaces.Clear();
        // 在任何世界组件接收 MouseDown 前，先按本帧屏幕布局确定手牌命中。
        // 只有明确选择会抬起卡牌；鼠标经过不会改变卡牌位置或叠放顺序。
        PrepareHand(snapshot);
        bool dockEnabled=GUI.enabled;
        bool overHand=faces.Any(face=>face.Shape.Contains(Event.current.mousePosition))||
            (Selection.Selected.Sum()>0||Selection.BuildId!=null)&&new Rect(1236,711,176,30).Contains(Event.current.mousePosition);
        GUI.enabled=dockEnabled&&!HasOverlay&&!overHand;
        table.Objects.Begin("own");
        DrawBuilds(snapshot);
        table.Objects.End();
        GUI.enabled=dockEnabled;
        if(overHand)hoveredBuild=null;
        DrawHand(snapshot);
        HandleHandInput(snapshot);
        HandleBuildInput(snapshot);
        if(tradeOpen)DrawTrade(snapshot);
        else if(snapshot.Offer!=null)DrawOffer(snapshot);
        else if(previewCard!=null)DrawCardPreview(snapshot);
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
            float size=66*table.DisplayScale;
            if(piece!=null)GUI.DrawTexture(new Rect(Event.current.mousePosition.x-size/2,Event.current.mousePosition.y-size*.58f,size,size),piece,ScaleMode.ScaleToFit);
        }
    }
    void DrawBuilds(OperationSnapshot s)
    {
        var pieces=s.Builds.Where(b=>b.Track<0&&b.Icon!="development").ToArray();
        var facilities=s.Builds.Where(b=>b.Track>=0).OrderBy(b=>b.Track).ToArray();
        float width=Math.Min(180,(facilities.Length>0?780:1090)/Math.Max(1f,pieces.Length));
        float start=facilities.Length>0?248:Math.Max(248,(1440-pieces.Length*width)/2);
        for(int i=0;i<pieces.Length;i++)DrawBuild(new Rect(start+i*width,651,width-9,97),pieces[i],s);
        for(int i=0;i<facilities.Length;i++)DrawBuild(new Rect(1044+i*122,590,114,152),facilities[i],s);
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
            if(hovered)TableLabel(new Rect(r.x,r.y-20,r.width,20),build.Name.Replace("设施","")+" · "+build.Level+" 级",tableLabel);
        }
        else
        {
            var piece=OperationPieceAssets.Texture(build.Icon??build.Id,s.Seat??"P1",Math.Max(1,build.Level));
            if(piece!=null)
            {
                int amount=Math.Max(1,build.Count),columns=Math.Min(8,Math.Max(3,(amount+1)/2));
                float step=Math.Min(25,(r.width-35)/Math.Max(1,columns-1));
                float left=r.center.x-(Math.Min(columns,amount)-1)*step/2-20;
                for(int n=0;n<amount;n++)
                {
                    bool picked=Selection.IsPieceSelected(build.Id,n);
                    if(build.Count==0||picked&&pieceDragging)continue;
                    var position=new Vector2(left+(n%columns)*step+20,r.y+(n/columns)*30+23);
                    var world=table.Objects.Piece("stock-"+build.Id+"-"+n,build.Icon??build.Id,s.Seat,Math.Max(1,build.Level),position,picked);
                    if(world==null)continue;
                    var shape=new HitShape{World=world};
                    hovered|=shape.Contains(Event.current.mousePosition);
                    if(build.Count>0){hits.Add(shape);pieceFaces.Add(new PieceFace{Shape=shape,Build=build,Ordinal=n});}
                }
            }
            var caption=new Rect(r.x,r.y+75,r.width,21);
            var label=new GUIStyle(tableLabel);
            if(Selection.BuildId==null&&Selection.Matches(build)&&build.Available)label.normal.textColor=Gold;
            if(hovered)TableLabel(caption,build.Name+"  "+build.Count,label);
        }
        if(hovered){hoveredBuild=build.Id;if(build.Track<0)costAnchor=new Vector2(Event.current.mousePosition.x,table.ProjectLabel(r).y);}
        if(clicked)PrepareBuild(build,s);
    }
    void PrepareBuild(OperationBuild build,OperationSnapshot s)
    {
        if(!build.Available||!s.CanInteract){s.Notify?.Invoke(build.UnavailableReason??"当前不能使用");return;}
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
        if(!build.Available||!s.CanInteract){s.Notify?.Invoke(build.UnavailableReason??"当前不能拿起棋子");e.Use();return;}
        s.Cancel?.Invoke();Selection.Choose(build,s.Goods,hit.Ordinal);tradeOpen=false;previewCard=null;message="";
        pieceReleased=false;pieceDragging=false;heldPiece=null;
        s.PrepareBuild?.Invoke(build);heldPiece=build;pieceStart=e.mousePosition;
        e.Use();
    }
    void HandlePieceInput(OperationSnapshot s)
    {
        if(heldPiece==null)return;
        var e=Event.current;
        if(e.type==EventType.MouseDrag&&e.button==0)
        {if((e.mousePosition-pieceStart).sqrMagnitude>49)pieceDragging=true;e.Use();}
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
        bool resources=s.Goods.Take(s.GoodCount).Sum()>0;
        float specialWidth=resources?350:1060,resourceWidth=s.Cards.Count>0?704:1060;
        DrawSpecialHand(s,new Rect(TabletopHandLayout.SpecialCenter(resources)-specialWidth/2,734,specialWidth,151));
        DrawResourceHand(s,new Rect(TabletopHandLayout.ResourceCenter(s.Cards.Count>0)-resourceWidth/2,734,resourceWidth,151));
        // 选中的卡牌绘制在最上层，命中顺序与实际叠放顺序一致。
        var selected=faces.Where(face=>face.Selected).ToArray();
        faces.RemoveAll(face=>face.Selected);faces.AddRange(selected);
    }
    void DrawHand(OperationSnapshot s)
    {
        foreach(var face in faces){DrawHandFace(face);hits.Add(face.Shape);}
        if((Selection.Selected.Sum()>0||Selection.BuildId!=null)&&Button(new Rect(1236,711,176,30),"取消当前选择"))
        {Selection.Clear();s.Cancel?.Invoke();message="";}
    }
    void DrawSpecialHand(OperationSnapshot s,Rect area)
    {
        if(s.Cards.Count==0)return;
        int count=s.Cards.Count;
        for(int i=0;i<count;i++)
        {
            var card=s.Cards[i];var pose=TabletopHandLayout.Card(i,count,area.center.x,area.width,previewCard==card.Id);
            var r=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
            var texture=card.Progress&&card.ArtIndex>=0?EntityCardAssets.ProgressFront(card.ArtIndex):null;
            faces.Add(new HandFace{Shape=new HitShape{Rect=r},Card=card,Texture=texture,Selected=previewCard==card.Id});
        }
    }
    void DrawResourceHand(OperationSnapshot s,Rect area)
    {
        int total=s.Goods.Take(s.GoodCount).Sum();if(total==0)return;
        int drawn=0;
        for(int good=0;good<s.GoodCount;good++)
        {
            var texture=EntityCardAssets.Texture(EntityCardAssets.Good(good).Front);
            for(int n=0;n<s.Goods[good];n++)
            {
                bool chosen=n<Selection.Selected[good];var pose=TabletopHandLayout.Card(drawn,total,area.center.x,area.width,chosen);
                var r=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
                faces.Add(new HandFace{Shape=new HitShape{Rect=r},Good=good,Ordinal=n,Texture=texture,Selected=chosen});drawn++;
            }
        }
    }
    void DrawHandFace(HandFace face)
    {
        var r=face.Shape.Rect;
        if(face.Texture!=null)EntityCardAssets.DrawCardFace(r,face.Texture);
        else
        {
            r=EntityCardAssets.FitCardRect(r,TabletopHandLayout.CardWidth/TabletopHandLayout.CardHeight,GUI.matrix);
            ModernOperationUI.Round(r,new Color(.13f,.22f,.23f));
            Label(new Rect(r.x+10,r.y+9,r.width-20,20),face.Card.Progress?"进步卡":"发展卡",small);
            Label(new Rect(r.x+11,r.y+36,r.width-22,64),face.Card.Name,new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,fontSize=21});
        }
    }
    void HandleHandInput(OperationSnapshot s)
    {
        if(!GUI.enabled)return;
        var e=Event.current;HandFace hit=null;
        for(int i=faces.Count-1;i>=0;i--)if(faces[i].Shape.Contains(e.mousePosition)){hit=faces[i];break;}
        bool overPaper=(tradeOpen||s.Offer!=null)&&TradeRect.Contains(e.mousePosition)||previewCard!=null&&CardRect.Contains(e.mousePosition);
        if(e.type==EventType.ScrollWheel&&hit!=null&&!overPaper)
        {
            // 全部手牌始终在场；手牌上滚动不再改变桌面相机距离。
            e.Use();return;
        }
        if(e.type==EventType.MouseDown&&e.button==0&&hit!=null&&!overPaper)
        {
            if(hit.Card!=null){previewCard=previewCard==hit.Card.Id?null:hit.Card.Id;tradeOpen=false;s.Cancel?.Invoke();}
            else{dragGood=hit.Good;dragOrdinal=hit.Ordinal;dragStart=e.mousePosition;dragging=false;}
            e.Use();return;
        }
        if(dragGood<0)return;
        if(e.type==EventType.MouseDrag&&e.button==0){if((e.mousePosition-dragStart).sqrMagnitude>49)dragging=true;e.Use();}
        if(e.type==EventType.MouseUp&&e.button==0)
        {
            s.Cancel?.Invoke();previewCard=null;
            if(dragging&&tradeOpen&&playerTrade&&GiveDropRect.Contains(e.mousePosition))
            {give[dragGood]=Math.Min(s.Goods[dragGood],give[dragGood]+1);Selection.Selected[dragGood]=give[dragGood];Selection.BuildId=null;Selection.PieceOrdinal=-1;message="";}
            else if(!dragging)
            {Selection.Toggle(dragGood,dragOrdinal,s.Goods[dragGood]);if(tradeOpen&&playerTrade){Array.Copy(Selection.Selected,give,8);message="";}}
            dragGood=-1;dragging=false;e.Use();
        }
    }
    void DrawCost(OperationSnapshot s)
    {
        var b=s.Builds.FirstOrDefault(x=>x.Id==hoveredBuild);if(b==null)return;
        var tokens=TabletopCostToken.For(b.Cost,s.Goods);
        var style=new GUIStyle(center){fontSize=14,wordWrap=false};
        float[] widths=tokens.Select(token=>style.CalcSize(new GUIContent(token.Name)).x+10).ToArray();
        float width=tokens.Length==0?52:widths.Sum()+12;
        tooltipRect=new Rect(Mathf.Clamp(costAnchor.x-width/2,12,1428-width),costAnchor.y-39,width,29);
        Paper(tooltipRect);float x=tooltipRect.x+6;
        if(tokens.Length==0)Label(tooltipRect,"免费",style);
        for(int i=0;i<tokens.Length;i++)
        {
            style.normal.textColor=tokens[i].Missing?Missing:Ink;
            Label(new Rect(x,tooltipRect.y,widths[i],29),tokens[i].Name,style);x+=widths[i];
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
    void DrawTrade(OperationSnapshot s)
    {
        target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
        giveGood=Mathf.Clamp(giveGood,0,s.GoodCount-1);receiveGood=Mathf.Clamp(receiveGood,0,s.GoodCount-1);
        Paper(TradeRect);
        Label(new Rect(287,156,600,34),"资源交易",heading);
        if(Button(new Rect(1115,158,38,32),"×")){Reset();s.Cancel?.Invoke();return;}
        if(ModernOperationUI.Button(new Rect(286,229,190,35),"银行 / 港口",true,false,!playerTrade)){playerTrade=false;message="";}
        if(ModernOperationUI.Button(new Rect(484,229,190,35),"玩家交易",true,false,playerTrade)){playerTrade=true;message="";}
        if(playerTrade)
        {
            target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
            for(int i=0;i<s.OtherPlayers.Length;i++)if(ModernOperationUI.Button(new Rect(766+i*130,229,121,35),s.OtherPlayers[i],true,false,target==i))target=i;
            if(dragging&&GiveDropRect.Contains(Event.current.mousePosition))Fill(GiveDropRect,new Color(.77f,.64f,.39f,.22f));
            DrawTradeBag(s,new Rect(286,282,406,265),give,true);
            DrawTradeBag(s,new Rect(744,282,406,265),receive,false);
        }
        else
        {
            Label(new Rect(287,282,395,27),"我给予",heading);
            Label(new Rect(744,282,395,27),"我获得",heading);
            Label(new Rect(409,287,273,23),"当前比例  "+s.Rates[giveGood]+" : 1",small);
            for(int i=0;i<s.GoodCount;i++)
            {
                float x=286+(i%4)*103,y=324+(i/4)*77;
                int g=i;
                GoodButton(new Rect(x,y,97,70),i,giveGood==i,"× "+s.Goods[i]+"\n"+s.Rates[i]+" : 1",()=>{giveGood=g;lots=1;message="";});
                GoodButton(new Rect(x+458,y,97,70),i,receiveGood==i,"× "+s.Bank[i],()=>{receiveGood=g;lots=1;message="";});
            }
            lots=Math.Max(1,lots);
            int maximum=Math.Max(0,Math.Min(s.Goods[giveGood]/Math.Max(1,s.Rates[giveGood]),s.Bank[receiveGood]));
            if(Button(new Rect(286,489,38,34),"−",lots>1))lots--;
            Label(new Rect(331,489,296,34),"交换 "+lots+" 份  ·  最多 "+maximum+" 份",center);
            if(Button(new Rect(634,489,38,34),"+",lots<maximum))lots++;
            EntityCardAssets.DrawCardFace(new Rect(767,483,28,41),EntityCardAssets.ResourceOrCommodityFront(giveGood));
            Label(new Rect(805,489,112,32),"× "+s.Rates[giveGood]*lots+"   →",center);
            EntityCardAssets.DrawCardFace(new Rect(943,483,28,41),EntityCardAssets.ResourceOrCommodityFront(receiveGood));
            Label(new Rect(984,489,93,32),"× "+lots,center);
            Array.Clear(give,0,8);Array.Clear(receive,0,8);give[giveGood]=s.Rates[giveGood]*lots;receive[receiveGood]=lots;
        }
        var trade=new OperationTrade{Bank=!playerTrade,OtherPlayer=s.OtherPlayers.Length>0?s.OtherPlayers[target]:null,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
        string error=OperationSelection.ValidateTrade(s,trade);
        var hint=new GUIStyle(small);hint.normal.textColor=error==null?Muted:Missing;
        Label(new Rect(287,549,860,28),message!=""?message:error??"",hint);
        Fill(new Rect(286,581,866,1),Line);
        if(playerTrade)
        {
            var broadcast=new OperationTrade{Bank=false,OtherPlayer=OperationTrade.AllPlayers,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
            if(Button(new Rect(680,593,227,35),"向所有玩家报价",OperationSelection.ValidateTrade(s,broadcast)==null))SendTrade(s,broadcast);
            if(Primary(new Rect(921,593,230,35),"向 "+trade.OtherPlayer+" 报价",error==null))SendTrade(s,trade);
        }
        else if(Primary(new Rect(744,593,407,35),"确认交换",error==null))SendTrade(s,trade);
        if(Button(new Rect(286,593,154,35),"取消")){Reset();s.Cancel?.Invoke();}
        if(Button(new Rect(454,593,154,35),"清空选择")){Array.Clear(give,0,8);Array.Clear(receive,0,8);Selection.Clear();lots=1;message="";}
    }
    void SendTrade(OperationSnapshot s,OperationTrade trade)
    {
        message=s.Trade?.Invoke(trade)??"";
        if(message==""){tradeOpen=false;Selection.Clear();}else s.Notify?.Invoke(message);
    }
    void GoodButton(Rect r,int good,bool selected,string caption,Action click)
    {
        var face=new Rect(r.x+7,r.y+(selected?0:5),36,54);
        EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(good));
        Label(new Rect(r.x+47,r.y+8,r.width-48,48),caption,new GUIStyle(center){fontSize=12});
        if(GUI.Button(r,GUIContent.none,GUIStyle.none))click();
    }
    void DrawTradeBag(OperationSnapshot s,Rect area,int[] bag,bool paying)
    {
        Label(new Rect(area.x,area.y,area.width,27),paying?"我给予":"我获得",heading);
        Label(new Rect(area.x+115,area.y+5,area.width-115,23),bag.Sum()+" 张",small);
        for(int i=0;i<s.GoodCount;i++)
        {
            float x=area.x+(i%4)*103,y=area.y+40+(i/4)*105;
            EntityCardAssets.DrawCardFace(new Rect(x+5,y+3,39,58),EntityCardAssets.ResourceOrCommodityFront(i));
            Label(new Rect(x+49,y+6,48,25),Names[i],small);
            if(paying)Label(new Rect(x+49,y+32,48,23),"× "+s.Goods[i],small);
            if(Button(new Rect(x+4,y+65,26,27),"−",bag[i]>0))bag[i]--;
            Label(new Rect(x+32,y+65,32,27),bag[i].ToString(),center);
            if(Button(new Rect(x+66,y+65,26,27),"+",bag[i]<(paying?s.Goods[i]:i<5?19:12)))bag[i]++;
            if(paying)Selection.Selected[i]=bag[i];
        }
    }
    void DrawOffer(OperationSnapshot s)
    {
        var offer=s.Offer;Paper(TradeRect);
        Label(new Rect(286,157,800,36),"交易报价",heading);
        string recipient=offer.IsBroadcast?"所有玩家":offer.Recipient;
        Label(new Rect(286,202,800,25),offer.Proposer+"  →  "+recipient,small);
        bool own=offer.Proposer==s.Seat, involved=own||offer.Recipient==s.Seat||offer.IsBroadcast&&s.OtherPlayers.Contains(offer.Proposer);
        Label(new Rect(286,263,406,36),!involved?offer.Proposer+" 给予":own?"我给予":"我获得",heading);
        Label(new Rect(744,263,406,36),!involved?recipient+" 给予":own?"我获得":"我给予",heading);
        DrawOfferBag(new Rect(286,313,406,195),offer.Give);
        DrawOfferBag(new Rect(744,313,406,195),offer.Receive);
        string status=offer.IsBroadcast?(own?"等待 "+offer.PendingRecipients.Length+" 位玩家回应":offer.RejectedRecipients.Contains(s.Seat)?"已拒绝，等待其他玩家回应":"任意一人接受即成交"):
            !involved?"等待回应":own?"等待对方回应":"";
        Label(new Rect(286,538,860,40),status,small);
        if(offer.CanAccept&&Primary(new Rect(744,590,406,38),"接受交易"))s.Respond?.Invoke("accept");
        if(offer.CanReject&&Button(new Rect(286,590,406,38),"拒绝交易"))s.Respond?.Invoke("reject");
        if(offer.CanCancel&&Button(new Rect(744,590,406,38),"撤回报价"))s.Respond?.Invoke("cancel");
    }
    void DrawOfferBag(Rect area,int[] bag)
    {
        int index=0;
        for(int i=0;i<8;i++)if(bag[i]>0)
        {float x=area.x+(index%4)*97,y=area.y+(index/4)*93;EntityCardAssets.DrawCardFace(new Rect(x+22,y,44,65),EntityCardAssets.ResourceOrCommodityFront(i));Label(new Rect(x,y+66,87,25),"× "+bag[i],center);index++;}
    }
}
