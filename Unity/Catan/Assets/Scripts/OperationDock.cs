using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 桌面上的个人组件与手牌。拿取、费用与交换草稿均不直接修改规则状态。
public sealed class OperationDock
{
    public readonly OperationSelection Selection = new OperationSelection();
    static readonly string[] Names = { "木材", "砖块", "羊毛", "谷物", "矿石", "布匹", "钱币", "纸张" };
    static readonly Color Back = ModernOperationUI.Background, Surface = ModernOperationUI.Surface,
        Line = ModernOperationUI.Line, Gold = ModernOperationUI.Accent, Ink = ModernOperationUI.TextColor,
        Muted = ModernOperationUI.Muted, Missing = ModernOperationUI.Error;
    static readonly Rect TradeRect = new Rect(262,139,916,506);
    static readonly Rect CardRect = new Rect(376,167,688,459), GiveDropRect = new Rect(286,276,406,266);
    GUIStyle text, small, heading, center, tableLabel;
    OperationSnapshot current;
    string revision, scope, hoveredBuild, previewCard, message = "",lastTradeError;
    bool tradeOpen, playerTrade;
    int giveGood, receiveGood = 1, lots = 1, target;
    readonly int[] give = new int[8], receive = new int[8];
    int dragGood=-1, dragOrdinal;
    string hoveredHand;
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
        public string Key => Card!=null?"card:"+Card.Id:"good:"+Good+":"+Ordinal;
    }

    public bool TradeOpen => tradeOpen;
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
        current.Cancel?.Invoke();tradeOpen=true;previewCard=null;Selection.BuildId=null;Selection.PieceOrdinal=-1;message="";lastTradeError=null;
        current.Notify?.Invoke("请选择交换的资源");
        playerTrade=target!=null;
        this.target=target==null?0:Math.Max(0,Array.IndexOf(current.OtherPlayers,target));
        Array.Copy(Selection.Selected,give,8);Array.Clear(receive,0,8);
    }
    public void Reset()
    {
        Selection.Clear(); tradeOpen=false; previewCard=null; hoveredBuild=null; message="";lastTradeError=null;
        Array.Clear(give,0,8);Array.Clear(receive,0,8); tooltipRect=Rect.zero;
        hoveredHand=null;dragGood=-1;dragging=false;hits.Clear();faces.Clear();pieceFaces.Clear();
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
    static void Border(Rect r,Color c,int width=1)
    {Fill(new Rect(r.x,r.y,r.width,width),c);Fill(new Rect(r.x,r.yMax-width,r.width,width),c);Fill(new Rect(r.x,r.y,width,r.height),c);Fill(new Rect(r.xMax-width,r.y,width,r.height),c);}
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
        // 悬停抬升也在此完成，不能只排除上一帧或未抬升的卡牌矩形。
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
            if(texture!=null)GUI.DrawTexture(r,texture,ScaleMode.StretchToFill);
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
        var pieces=s.Builds.Where(b=>b.Track<0).ToArray();
        var facilities=s.Builds.Where(b=>b.Track>=0).OrderBy(b=>b.Track).ToArray();
        float width=Math.Min(180,(facilities.Length>0?930:1090)/Math.Max(1f,pieces.Length));
        float start=facilities.Length>0?125:Math.Max(210,(1440-pieces.Length*width)/2);
        for(int i=0;i<pieces.Length;i++)DrawBuild(new Rect(start+i*width,651,width-9,97),pieces[i],s);
        for(int i=0;i<facilities.Length;i++)DrawBuild(new Rect(1044+i*122,639,114,152),facilities[i],s);
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
            TableLabel(new Rect(r.x,r.y+136,r.width,20),build.Name.Replace("设施","")+" · "+build.Level+" 级",tableLabel);
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
            else
            {
                // 尚无实物照片的发展牌堆使用标明名称的纸牌，不伪造三维棋子。
                var world=table.Objects.Card("development-stack",new Rect(r.center.x-25,r.y+1,50,68),EntityCardAssets.Texture(EntityCardAssets.Good(0).Back));
                hits.Add(new HitShape{World=world});hovered=world.Contains(Event.current.mousePosition);
                TableLabel(new Rect(r.x,r.y+30,r.width,25),"发展卡",tableLabel);
                if(GUI.enabled&&hovered&&Event.current.type==EventType.MouseDown&&Event.current.button==0&&!HasOverlay){clicked=true;Event.current.Use();}
            }
            var caption=new Rect(r.x,r.y+75,r.width,21);
            var label=new GUIStyle(tableLabel);
            if(Selection.BuildId==null&&Selection.Matches(build)&&build.Available)label.normal.textColor=Gold;
            TableLabel(caption,build.Name+"  "+build.Count,label);
        }
        if(hovered){hoveredBuild=build.Id;if(build.Track<0)costAnchor=new Vector2(Event.current.mousePosition.x,table.ProjectLabel(r).y);}
        if(clicked)
        {
            if(!build.Available||!s.CanInteract){s.Notify?.Invoke(build.UnavailableReason??"当前不能使用");return;}
            s.Cancel?.Invoke();Selection.Choose(build,s.Goods);tradeOpen=false;previewCard=null;message="";
            s.PrepareBuild?.Invoke(build);
        }
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
        // 先以正常堆叠找到最上层，再单独抬起并绘制悬停牌；命中顺序与绘制顺序相同。
        HandFace hover=null;
        bool overOverlay=(tradeOpen||s.Offer!=null)&&TradeRect.Contains(Event.current.mousePosition)||previewCard!=null&&CardRect.Contains(Event.current.mousePosition);
        if(!overOverlay&&!dragging)
        {
            if(hoveredHand!=null)
            {
                var previous=faces.FirstOrDefault(f=>f.Key==hoveredHand);
                if(previous!=null&&Event.current.mousePosition.y<previous.Shape.Rect.y&&LiftedCard(previous.Shape.Rect).Contains(Event.current.mousePosition))hover=previous;
            }
            if(hover==null)for(int i=faces.Count-1;i>=0;i--)if(faces[i].Shape.Contains(Event.current.mousePosition)){hover=faces[i];break;}
        }
        hoveredHand=hover==null?null:hover.Key;
        if(hover!=null)
        {
            faces.Remove(hover);faces.Add(hover);hover.Shape.Rect=LiftedCard(hover.Shape.Rect);
        }
    }
    void DrawHand(OperationSnapshot s)
    {
        foreach(var face in faces){DrawHandFace(face,face.Key==hoveredHand);hits.Add(face.Shape);}
        if((Selection.Selected.Sum()>0||Selection.BuildId!=null)&&Button(new Rect(1236,711,176,30),"取消当前选择"))
        {Selection.Clear();s.Cancel?.Invoke();message="";}
    }
    static Rect LiftedCard(Rect r)=>new Rect(r.x-4,r.y-42,r.width+8,r.height+11);
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
    void DrawHandFace(HandFace face,bool hovered)
    {
        var r=face.Shape.Rect;
        ModernOperationUI.Round(new Rect(r.x+3,r.y+5,r.width+2,r.height+3),new Color(0,0,0,.42f));
        ModernOperationUI.Round(r,face.Selected||hovered?Gold:new Color(.83f,.78f,.66f));
        var inner=new Rect(r.x+2,r.y+2,r.width-4,r.height-5);
        if(face.Texture!=null)GUI.DrawTexture(inner,face.Texture,ScaleMode.StretchToFill);
        else
        {
            ModernOperationUI.Round(inner,new Color(.13f,.22f,.23f));
            Label(new Rect(r.x+10,r.y+9,r.width-20,20),face.Card.Progress?"进步卡":"发展卡",small);
            Label(new Rect(r.x+11,r.y+36,r.width-22,64),face.Card.Name,new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,fontSize=21});
            Label(new Rect(r.x+9,r.yMax-33,r.width-18,24),face.Card.Playable?"可使用":"点击查看",center);
        }
        Fill(new Rect(r.x+4,r.y+2,r.width-8,1),new Color(1,1,1,.45f));
        Fill(new Rect(r.x+3,r.yMax-3,r.width-6,2),new Color(.32f,.27f,.18f,.72f));
    }
    void HandleHandInput(OperationSnapshot s)
    {
        if(!GUI.enabled)return;
        var e=Event.current;HandFace hit=null;
        for(int i=faces.Count-1;i>=0;i--)if(faces[i].Shape.Contains(e.mousePosition)){hit=faces[i];break;}
        bool overPaper=(tradeOpen||s.Offer!=null)&&TradeRect.Contains(e.mousePosition)||previewCard!=null&&CardRect.Contains(e.mousePosition);
        if(hit!=null&&!overPaper&&!dragging&&e.type==EventType.Repaint)
        {
            string caption=hit.Card!=null?hit.Card.Name:Names[hit.Good]+" · 持有 "+s.Goods[hit.Good]+" 张 · 已抽出 "+Selection.Selected[hit.Good]+" 张";
            var note=new Rect(Mathf.Clamp(hit.Shape.Rect.center.x-132,15,1161),hit.Shape.Rect.y-32,264,28);
            Paper(note);Label(note,caption,center);
        }
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
        Paper(CardRect);
        Label(new Rect(401,184,546,22),c.Progress?"进步卡  /  使用效果":"发展卡  /  使用效果",small);
        Label(new Rect(401,216,546,39),c.Name,heading);
        if(Button(new Rect(1004,185,36,32),"×")){previewCard=null;return;}
        var texture=c.Progress&&c.ArtIndex>=0?EntityCardAssets.ProgressFront(c.ArtIndex):null;
        var face=new Rect(401,271,182,260);
        if(texture!=null){Fill(new Rect(face.x+4,face.y+5,face.width,face.height),new Color(0,0,0,.35f));GUI.DrawTexture(face,texture,ScaleMode.ScaleToFit);}
        else{ModernOperationUI.Round(face,Surface);Label(new Rect(419,306,145,147),c.Name,new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,fontSize=28});}
        Label(new Rect(611,278,418,135),c.Detail??"确认后选择所需目标或参数。",text);
        string reason=c.UnavailableReason??(!s.CanInteract?"等待你的行动或待决选择":c.Name=="胜利点"?"胜利点保留在手中，无需主动打出":c.Progress?"当前阶段或目标条件不满足此卡的使用要求":"本回合买入的卡不能使用；每回合最多打出一张行动发展卡");
        var statusRect=new Rect(611,433,417,93);ModernOperationUI.Round(statusRect,Surface);
        var state=new GUIStyle(text){fontSize=14};state.normal.textColor=c.Playable&&s.CanInteract?Gold:Muted;
        Label(new Rect(627,446,385,66),c.Playable&&s.CanInteract?"可以使用\n选择目标后确认，卡牌效果才会生效。":"暂不可用\n"+reason,state);
        if(Primary(new Rect(611,561,258,43),c.Playable?"使用卡牌  ·  选择效果":"当前不可使用",c.Playable&&s.CanInteract))
        {s.PrepareCard?.Invoke(c);previewCard=null;Selection.Clear();}
        if(Button(new Rect(883,561,145,43),"放回手牌"))previewCard=null;
    }
    void DrawTrade(OperationSnapshot s)
    {
        target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
        giveGood=Mathf.Clamp(giveGood,0,s.GoodCount-1);receiveGood=Mathf.Clamp(receiveGood,0,s.GoodCount-1);
        Paper(TradeRect);
        Label(new Rect(287,156,600,34),"资源交易",heading);
        Label(new Rect(287,193,660,22),"选择给予和获得的资源，确认后完成交换",small);
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
            Label(new Rect(867,287,280,23),"显示银行剩余库存",small);
            for(int i=0;i<s.GoodCount;i++)
            {
                float x=286+(i%4)*103,y=324+(i/4)*77;
                int g=i;
                GoodButton(new Rect(x,y,97,70),i,giveGood==i,"持有 "+s.Goods[i]+" · "+s.Rates[i]+":1",()=>{giveGood=g;lots=1;message="";});
                GoodButton(new Rect(x+458,y,97,70),i,receiveGood==i,"库存 "+s.Bank[i],()=>{receiveGood=g;lots=1;message="";});
            }
            lots=Math.Max(1,lots);
            int maximum=Math.Max(0,Math.Min(s.Goods[giveGood]/Math.Max(1,s.Rates[giveGood]),s.Bank[receiveGood]));
            if(Button(new Rect(286,489,38,34),"−",lots>1))lots--;
            Label(new Rect(331,489,296,34),"交换 "+lots+" 份  ·  最多 "+maximum+" 份",center);
            if(Button(new Rect(634,489,38,34),"+",lots<maximum))lots++;
            OperationIconAssets.DrawGood(new Rect(767,488,34,34),giveGood);
            Label(new Rect(805,489,112,32),"× "+s.Rates[giveGood]*lots+"   →",center);
            OperationIconAssets.DrawGood(new Rect(943,488,34,34),receiveGood);
            Label(new Rect(984,489,93,32),"× "+lots,center);
            Array.Clear(give,0,8);Array.Clear(receive,0,8);give[giveGood]=s.Rates[giveGood]*lots;receive[receiveGood]=lots;
        }
        var trade=new OperationTrade{Bank=!playerTrade,OtherPlayer=s.OtherPlayers.Length>0?s.OtherPlayers[target]:null,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
        string error=OperationSelection.ValidateTrade(s,trade);
        lastTradeError=error;
        var hint=new GUIStyle(small);hint.normal.textColor=error==null?Muted:Missing;
        Label(new Rect(287,549,860,28),message!=""?message:error??(playerTrade?"对方接受后才交换资源。":"已自动采用你当前可用的最佳港口与能力比例。"),hint);
        Fill(new Rect(286,581,866,1),Line);
        if(Primary(new Rect(744,593,407,35),playerTrade?"向 "+trade.OtherPlayer+" 发送报价":"确认交换",error==null))
        {message=s.Trade?.Invoke(trade)??"";if(message==""){tradeOpen=false;Selection.Clear();}else s.Notify?.Invoke(message);}
        if(Button(new Rect(286,593,173,35),"取消")){Reset();s.Cancel?.Invoke();}
        if(Button(new Rect(469,593,173,35),"清空选择")){Array.Clear(give,0,8);Array.Clear(receive,0,8);Selection.Clear();lots=1;message="";}
    }
    void GoodButton(Rect r,int good,bool selected,string caption,Action click)
    {
        ModernOperationUI.Round(r,selected?new Color(.21f,.30f,.27f):Surface);
        if(selected)Fill(new Rect(r.x+6,r.yMax-3,r.width-12,2),Gold);
        OperationIconAssets.DrawGood(new Rect(r.x+8,r.y+8,29,29),good,false);
        Label(new Rect(r.x+41,r.y+11,r.width-43,24),Names[good],small);Label(new Rect(r.x+3,r.y+43,r.width-6,22),caption,new GUIStyle(center){fontSize=11});
        if(GUI.Button(r,GUIContent.none,GUIStyle.none))click();
    }
    void DrawTradeBag(OperationSnapshot s,Rect area,int[] bag,bool paying)
    {
        Label(new Rect(area.x,area.y,area.width,27),paying?"我给予":"我获得",heading);
        Label(new Rect(area.x+115,area.y+5,area.width-115,23),paying?"共 "+bag.Sum()+" 张  ·  可从手牌拖入":"共 "+bag.Sum()+" 张",small);
        for(int i=0;i<s.GoodCount;i++)
        {
            float x=area.x+(i%4)*103,y=area.y+40+(i/4)*105;
            ModernOperationUI.Round(new Rect(x,y,97,98),Surface);
            OperationIconAssets.DrawGood(new Rect(x+9,y+8,28,28),i,false);
            Label(new Rect(x+40,y+10,54,25),Names[i],small);
            Label(new Rect(x+3,y+39,91,20),paying?"可用 "+s.Goods[i]:"希望获得",new GUIStyle(center){fontSize=11});
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
        Label(new Rect(286,202,800,25),offer.Proposer+"  →  "+offer.Recipient,small);
        bool own=offer.Proposer==s.Seat, involved=own||offer.Recipient==s.Seat;
        Label(new Rect(286,263,406,36),!involved?offer.Proposer+" 给予":own?"我给予":"我获得",heading);
        Label(new Rect(744,263,406,36),!involved?offer.Recipient+" 给予":own?"我获得":"我给予",heading);
        DrawOfferBag(new Rect(286,313,406,195),offer.Give);
        DrawOfferBag(new Rect(744,313,406,195),offer.Receive);
        Label(new Rect(286,538,860,40),!involved?"其他玩家正在交易，等待回应。":own?"等待对方回应。你可以撤回这份报价。":"接受后立即交换两侧资源。",small);
        if(offer.CanAccept&&Primary(new Rect(744,590,406,38),"接受交易"))s.Respond?.Invoke("accept");
        if(offer.CanReject&&Button(new Rect(286,590,406,38),"拒绝交易"))s.Respond?.Invoke("reject");
        if(offer.CanCancel&&Button(new Rect(744,590,406,38),"撤回报价"))s.Respond?.Invoke("cancel");
    }
    void DrawOfferBag(Rect area,int[] bag)
    {
        int index=0;
        for(int i=0;i<8;i++)if(bag[i]>0)
        {float x=area.x+(index%4)*97,y=area.y+(index/4)*84;OperationIconAssets.DrawGood(new Rect(x+19,y,46,46),i,false);Label(new Rect(x,y+50,87,25),Names[i]+" ×"+bag[i],center);index++;}
    }
}
