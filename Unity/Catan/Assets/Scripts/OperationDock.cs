using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 桌面上的个人组件与手牌。拿取、费用与交换草稿均不直接修改规则状态。
public sealed class OperationDock
{
    public readonly OperationSelection Selection = new OperationSelection();
    static readonly string[] Names = { "木材", "砖块", "羊毛", "谷物", "矿石", "布匹", "钱币", "纸张" };
    static readonly Color Back = new Color(.92f,.85f,.69f), Surface = new Color(.98f,.93f,.80f),
        Line = new Color(.49f,.36f,.21f,.45f), Gold = new Color(.89f,.66f,.27f), Ink = new Color(.25f,.18f,.11f),
        Muted = new Color(.46f,.36f,.24f), Missing = new Color(.69f,.22f,.15f);
    static readonly Rect TradeRect = new Rect(274,156,892,434);
    static readonly Rect CardRect = new Rect(390,208,650,415), GiveDropRect = new Rect(290,265,410,210);
    GUIStyle text, small, heading, center, tableLabel;
    OperationSnapshot current;
    string revision, scope, hoveredBuild, previewCard, message = "",lastTradeError;
    bool tradeOpen, playerTrade;
    int giveGood, receiveGood = 1, lots = 1, target;
    readonly int[] give = new int[8], receive = new int[8];
    int handOffset, cardOffset, dragGood=-1, dragOrdinal;
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
    }

    public bool TradeOpen => tradeOpen;
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
        handOffset=cardOffset=0;dragGood=-1;dragging=false;hits.Clear();faces.Clear();pieceFaces.Clear();
        heldPiece=null;pieceDragging=false;pieceReleased=false;
        target=0;giveGood=0;receiveGood=1;lots=1;playerTrade=false;
    }
    public bool BlocksBoardInput(Vector2 logicalMouse)
    {
        return dragGood>=0 || (tradeOpen||current!=null&&current.Offer!=null)&&TradeRect.Contains(logicalMouse) ||
            previewCard!=null&&CardRect.Contains(logicalMouse) || hits.Any(h=>h.Contains(logicalMouse));
    }
    void Styles(Font font)
    {
        if(text!=null)return;
        text=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};text.normal.textColor=Ink;
        small=new GUIStyle(text){fontSize=12};small.normal.textColor=Muted;
        heading=new GUIStyle(text){fontSize=19,fontStyle=FontStyle.Bold};
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
        Register(r);bool old=GUI.enabled;GUI.enabled=old&&enabled;
        Fill(new Rect(r.x+2,r.y+3,r.width,r.height),new Color(.12f,.06f,.02f,.22f));Fill(r,enabled?Surface:new Color(.80f,.76f,.66f));
        var style=new GUIStyle(center){fontSize=14};style.normal.textColor=enabled?Ink:Muted;
        Label(r,value,style);bool clicked=GUI.Button(r,GUIContent.none,GUIStyle.none);GUI.enabled=old;return clicked;
    }
    HitShape Register(Rect r,float angle=0,Texture2D texture=null){var hit=new HitShape{Rect=r,Angle=angle,Texture=texture};hits.Add(hit);return hit;}
    static void Paper(Rect r)
    {Fill(new Rect(r.x+5,r.y+7,r.width,r.height),new Color(.12f,.06f,.02f,.30f));Fill(r,Back);}
    public void Draw(OperationSnapshot snapshot,Font font)
    {
        Styles(font);
        if(snapshot==null||string.IsNullOrEmpty(snapshot.Scope)){Reset();current=null;return;}
        if(scope!=snapshot.Scope||revision!=snapshot.Revision){Reset();scope=snapshot.Scope;revision=snapshot.Revision;}
        current=snapshot;Selection.Sync(snapshot);
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape||Event.current.type==EventType.MouseDown&&Event.current.button==1)
        {Reset();snapshot.Cancel?.Invoke();Event.current.Use();}
        hoveredBuild=null;tooltipRect=Rect.zero;hits.Clear();faces.Clear();pieceFaces.Clear();
        table.Objects.Begin("own");
        DrawBuilds(snapshot);
        DrawHand(snapshot);
        table.Objects.End();
        HandleBuildInput(snapshot);
        if(tradeOpen)DrawTrade(snapshot);
        else if(snapshot.Offer!=null)DrawOffer(snapshot);
        else if(previewCard!=null)DrawCardPreview(snapshot);
        else DrawCost(snapshot);
        HandlePieceInput(snapshot);
        HandleHandInput(snapshot);
        if(dragging&&dragGood>=0)
        {
            var texture=EntityCardAssets.Texture(EntityCardAssets.Good(dragGood).Front);
            float scale=table.DisplayScale;
            var r=new Rect(Event.current.mousePosition.x-36*scale,Event.current.mousePosition.y-52*scale,72*scale,103*scale);
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
    void DrawHand(OperationSnapshot s)
    {
        bool resources=s.Goods.Take(s.GoodCount).Sum()>0;
        float specialWidth=resources?380:1050,resourceWidth=s.Cards.Count>0?684:1050;
        DrawSpecialHand(s,new Rect(TabletopHandLayout.SpecialCenter(resources)-specialWidth/2,768,specialWidth,112));
        DrawResourceHand(s,new Rect(TabletopHandLayout.ResourceCenter(s.Cards.Count>0)-resourceWidth/2,768,resourceWidth,112));
        if((Selection.Selected.Sum()>0||Selection.BuildId!=null)&&Button(new Rect(1118,848,103,29),"放回手中"))
        {Selection.Clear();s.Cancel?.Invoke();message="";}
    }
    void DrawSpecialHand(OperationSnapshot s,Rect area)
    {
        if(s.Cards.Count==0)return;
        int visible=TabletopHandLayout.SpecialCapacity(s.Goods.Take(s.GoodCount).Sum()>0);cardOffset=Mathf.Clamp(cardOffset,0,Math.Max(0,s.Cards.Count-visible));
        int count=Math.Min(visible,s.Cards.Count-cardOffset);
        for(int i=0;i<count;i++)
        {
            var card=s.Cards[i+cardOffset];var pose=TabletopHandLayout.Card(i,count,area.center.x,area.width,false);
            var r=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
            var texture=card.Progress&&card.ArtIndex>=0?EntityCardAssets.ProgressFront(card.ArtIndex):null;
            var world=table.Objects.Card("special-hand-"+i,r,texture??EntityCardAssets.Texture(EntityCardAssets.Good(0).Back),previewCard==card.Id);
            if(texture==null)TableLabel(new Rect(r.x,r.y+45,r.width,28),card.Name,tableLabel);
            var shape=new HitShape{World=world};hits.Add(shape);faces.Add(new HandFace{Shape=shape,Card=card});
        }
        if(s.Cards.Count>visible)PageCards(area,s.Cards.Count,visible,ref cardOffset);
    }
    void DrawResourceHand(OperationSnapshot s,Rect area)
    {
        int total=s.Goods.Take(s.GoodCount).Sum();if(total==0)return;
        int visible=TabletopHandLayout.ResourceCapacity(s.Cards.Count>0);handOffset=Mathf.Clamp(handOffset,0,Math.Max(0,total-visible));
        int count=Math.Min(visible,total-handOffset),index=0,drawn=0;
        for(int good=0;good<s.GoodCount;good++)
        {
            var texture=EntityCardAssets.Texture(EntityCardAssets.Good(good).Front);
            for(int n=0;n<s.Goods[good];n++)
            {
                int ordinal=index++;if(ordinal<handOffset||ordinal>=handOffset+count)continue;
                bool chosen=n<Selection.Selected[good];var pose=TabletopHandLayout.Card(drawn,count,area.center.x,area.width,chosen);
                var r=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
                var world=table.Objects.Card("resource-hand-"+drawn,r,texture,chosen);
                var shape=new HitShape{World=world};hits.Add(shape);faces.Add(new HandFace{Shape=shape,Good=good,Ordinal=n});drawn++;
            }
        }
        if(total>visible)PageCards(area,total,visible,ref handOffset);
    }
    void PageCards(Rect area,int total,int visible,ref int offset)
    {
        if(Button(table.ProjectLabel(new Rect(area.x-35,area.y+54,26,27)),"‹",offset>0))offset=Math.Max(0,offset-visible/2);
        if(Button(table.ProjectLabel(new Rect(area.xMax+8,area.y+54,26,27)),"›",offset+visible<total))offset=Math.Min(total-visible,offset+visible/2);
        TableLabel(new Rect(area.center.x-90,area.y-23,180,20),(offset+1)+"–"+Math.Min(total,offset+visible)+" / "+total+" 张 · 滚轮翻看",tableLabel);
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
            var note=new Rect(Mathf.Clamp(e.mousePosition.x-108,15,1206),Math.Min(748,e.mousePosition.y-36),216,28);
            Paper(note);Label(note,caption,center);
        }
        if(e.type==EventType.ScrollWheel&&hit!=null&&!overPaper)
        {
            int direction=e.delta.y>0?3:-3;
            if(hit.Card!=null)cardOffset=Mathf.Clamp(cardOffset+direction,0,Math.Max(0,s.Cards.Count-TabletopHandLayout.SpecialCapacity(s.Goods.Take(s.GoodCount).Sum()>0)));
            else handOffset=Mathf.Clamp(handOffset+direction,0,Math.Max(0,s.Goods.Take(s.GoodCount).Sum()-TabletopHandLayout.ResourceCapacity(s.Cards.Count>0)));
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
        Label(new Rect(413,227,548,36),c.Name,heading);
        var texture=c.Progress&&c.ArtIndex>=0?EntityCardAssets.ProgressFront(c.ArtIndex):null;
        if(texture!=null)GUI.DrawTexture(new Rect(412,273,166,271),texture,ScaleMode.ScaleToFit);
        else{var card=new Rect(433,292,123,190);Fill(card,Surface);Label(new Rect(447,322,96,120),c.Name,heading);}
        Label(new Rect(598,283,412,234),c.Detail??"选择使用后，按桌面纸签选择具体目标或参数。",text);
        if(Button(new Rect(600,551,239,43),c.Playable?"打出 · 选择目标":"当前不可使用",c.Playable&&s.CanInteract))
        {s.PrepareCard?.Invoke(c);previewCard=null;Selection.Clear();}
        if(Button(new Rect(854,551,149,43),"放回"))previewCard=null;
    }
    void DrawTrade(OperationSnapshot s)
    {
        target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
        giveGood=Mathf.Clamp(giveGood,0,s.GoodCount-1);receiveGood=Mathf.Clamp(receiveGood,0,s.GoodCount-1);
        Paper(TradeRect);
        Label(new Rect(298,171,686,34),"把资源摆到桌上  ·  确认后才交换",heading);
        if(Button(new Rect(1104,169,40,30),"×")){Reset();s.Cancel?.Invoke();return;}
        if(Button(new Rect(298,215,187,34),!playerTrade?"● 银行 / 港口":"银行 / 港口")){playerTrade=false;message="";}
        if(Button(new Rect(495,215,187,34),playerTrade?"● 与玩家交易":"与玩家交易")){playerTrade=true;message="";}
        if(playerTrade)
        {
            target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
            for(int i=0;i<s.OtherPlayers.Length;i++)if(Button(new Rect(755+i*122,215,114,34),(target==i?"● ":"")+s.OtherPlayers[i]))target=i;
            if(dragging&&GiveDropRect.Contains(Event.current.mousePosition))Fill(GiveDropRect,new Color(.77f,.64f,.39f,.22f));
            DrawTradeBag(s,new Rect(298,274,398,186),give,true);
            DrawTradeBag(s,new Rect(735,274,398,186),receive,false);
        }
        else
        {
            Label(new Rect(298,267,380,26),"我付出  ·  当前最优 "+s.Rates[giveGood]+" : 1",heading);
            Label(new Rect(755,267,350,26),"我获得  ·  银行库存",heading);
            for(int i=0;i<s.GoodCount;i++)
            {
                float x=299+(i%4)*92,y=306+(i/4)*66;
                GoodButton(new Rect(x,y,84,58),i,giveGood==i,"有 "+s.Goods[i],()=>{giveGood=i;message="";});
                int g=i;
                GoodButton(new Rect(x+455,y,84,58),i,receiveGood==i,"余 "+s.Bank[i],()=>{receiveGood=g;message="";});
            }
            lots=Math.Max(1,lots);
            if(Button(new Rect(299,447,38,30),"−",lots>1))lots--;
            Label(new Rect(345,448,272,27),"交换 "+lots+" 份：付 "+s.Rates[giveGood]*lots+" → 得 "+lots,center);
            if(Button(new Rect(623,447,38,30),"+",lots<19))lots++;
            Array.Clear(give,0,8);Array.Clear(receive,0,8);give[giveGood]=s.Rates[giveGood]*lots;receive[receiveGood]=lots;
        }
        var trade=new OperationTrade{Bank=!playerTrade,OtherPlayer=s.OtherPlayers.Length>0?s.OtherPlayers[target]:null,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
        string error=OperationSelection.ValidateTrade(s,trade);
        if(error!=lastTradeError){if(error!=null)s.Notify?.Invoke(error);lastTradeError=error;}
        Label(new Rect(298,486,820,31),playerTrade?"报价发送后等待对方接受。":"比例已按当前港口与能力自动计算。",small);
        if(Button(new Rect(299,532,554,39),playerTrade?"向 "+trade.OtherPlayer+" 发送报价":"确认交换",error==null))
        {message=s.Trade?.Invoke(trade)??"";if(message==""){tradeOpen=false;Selection.Clear();}else s.Notify?.Invoke(message);}
        if(Button(new Rect(875,532,255,39),"取消")){Reset();s.Cancel?.Invoke();}
    }
    void GoodButton(Rect r,int good,bool selected,string caption,Action click)
    {
        Fill(r,Surface);Border(r,selected?Gold:Line,selected?2:1);
        OperationIconAssets.DrawGood(new Rect(r.x+5,r.y+5,25,25),good,false);
        Label(new Rect(r.x+31,r.y+5,r.width-33,24),Names[good],small);Label(new Rect(r.x+3,r.y+32,r.width-6,23),caption,center);
        if(GUI.Button(r,GUIContent.none,GUIStyle.none))click();
    }
    void DrawTradeBag(OperationSnapshot s,Rect area,int[] bag,bool paying)
    {
        Label(new Rect(area.x,area.y,area.width,27),paying?"我付出  ·  将手牌拖到这里":"我希望获得",heading);
        for(int i=0;i<s.GoodCount;i++)
        {
            float x=area.x+(i%4)*99,y=area.y+37+(i/4)*75;OperationIconAssets.DrawGood(new Rect(x+29,y,31,31),i,false);
            Label(new Rect(x,y+29,93,20),Names[i]+" ×"+bag[i],center);
            if(Button(new Rect(x+3,y+51,34,23),"−",bag[i]>0))bag[i]--;
            if(Button(new Rect(x+57,y+51,34,23),"+",bag[i]<(paying?s.Goods[i]:i<5?19:12)))bag[i]++;
            if(paying)Selection.Selected[i]=bag[i];
        }
    }
    void DrawOffer(OperationSnapshot s)
    {
        var offer=s.Offer;Paper(TradeRect);
        Label(new Rect(299,177,800,40),offer.Proposer+" → "+offer.Recipient+"  /  交易报价",heading);
        bool own=offer.Proposer==s.Seat, involved=own||offer.Recipient==s.Seat;
        Label(new Rect(299,235,390,36),!involved?offer.Proposer+" 付出":own?"我付出":"我获得",heading);
        Label(new Rect(745,235,390,36),!involved?offer.Recipient+" 付出":own?"我获得":"我付出",heading);
        DrawOfferBag(new Rect(299,289,390,178),offer.Give);
        DrawOfferBag(new Rect(745,289,390,178),offer.Receive);
        Label(new Rect(299,478,818,33),!involved?"其他玩家正在交易，等待回应。":own?"等待对方回应。你可以撤回这份报价。":"检查两侧资源；只有接受后才会完成交换。",small);
        if(offer.CanAccept&&Button(new Rect(299,529,390,40),"接受交易"))s.Respond?.Invoke("accept");
        if(offer.CanReject&&Button(new Rect(745,529,390,40),"拒绝交易"))s.Respond?.Invoke("reject");
        if(offer.CanCancel&&Button(new Rect(299,529,836,40),"撤回报价"))s.Respond?.Invoke("cancel");
    }
    void DrawOfferBag(Rect area,int[] bag)
    {
        int index=0;
        for(int i=0;i<8;i++)if(bag[i]>0)
        {float x=area.x+(index%4)*97,y=area.y+(index/4)*84;OperationIconAssets.DrawGood(new Rect(x+19,y,46,46),i,false);Label(new Rect(x,y+50,87,25),Names[i]+" ×"+bag[i],center);index++;}
    }
}
