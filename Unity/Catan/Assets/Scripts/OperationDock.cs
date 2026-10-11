using System;
using System.Linq;
using UnityEngine;

// Shared tabletop interaction surface. Selection is a local draft, never a rule mutation.
public sealed class OperationDock
{
    public readonly OperationSelection Selection = new OperationSelection();
    static readonly string[] Names = { "木材", "砖块", "羊毛", "谷物", "矿石", "布匹", "钱币", "纸张" };
    static readonly Color Back = new Color(.035f,.073f,.084f), Surface = new Color(.073f,.13f,.14f),
        Line = new Color(.20f,.31f,.31f), Gold = new Color(.91f,.72f,.39f), Ink = new Color(.94f,.94f,.87f),
        Muted = new Color(.58f,.69f,.68f), Missing = new Color(.94f,.47f,.40f);
    static readonly Rect TradeRect = new Rect(274,156,892,434);
    GUIStyle text, small, heading, center, button;
    OperationSnapshot current;
    string revision, scope, hoveredBuild, previewCard, message = "";
    bool tradeOpen, playerTrade;
    int giveGood, receiveGood = 1, lots = 1, target;
    readonly int[] give = new int[8], receive = new int[8];
    Vector2 handScroll, cardScroll;
    Rect tooltipRect;

    public bool TradeOpen => tradeOpen;
    public void Reset()
    {
        Selection.Clear(); tradeOpen=false; previewCard=null; hoveredBuild=null; message="";
        Array.Clear(give,0,8);Array.Clear(receive,0,8); tooltipRect=Rect.zero;
        handScroll=cardScroll=Vector2.zero;
        target=0;giveGood=0;receiveGood=1;lots=1;playerTrade=false;
    }
    public bool BlocksBoardInput(Vector2 logicalMouse)
    {
        return logicalMouse.y>=590 || tradeOpen || current!=null&&current.Offer!=null ||
            tooltipRect.Contains(logicalMouse) || previewCard!=null;
    }
    void Styles(Font font)
    {
        if(text!=null)return;
        text=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};text.normal.textColor=Ink;
        small=new GUIStyle(text){fontSize=12};small.normal.textColor=Muted;
        heading=new GUIStyle(text){fontSize=19,fontStyle=FontStyle.Bold};
        center=new GUIStyle(text){alignment=TextAnchor.MiddleCenter,fontSize=13};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=15,wordWrap=true};
        button.normal.textColor=Ink;
    }
    static void Fill(Rect rect,Color color)
    {var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
    static void Border(Rect r,Color c,int width=1)
    {Fill(new Rect(r.x,r.y,r.width,width),c);Fill(new Rect(r.x,r.yMax-width,r.width,width),c);Fill(new Rect(r.x,r.y,width,r.height),c);Fill(new Rect(r.xMax-width,r.y,width,r.height),c);}
    void Label(Rect r,string value,GUIStyle style=null)=>GUI.Label(r,value,style??text);
    bool Button(Rect r,string value,bool enabled=true)
    {bool old=GUI.enabled;GUI.enabled=old&&enabled;bool clicked=GUI.Button(r,value,button);GUI.enabled=old;return clicked;}
    public void Draw(OperationSnapshot snapshot,Font font)
    {
        Styles(font);
        if(snapshot==null||string.IsNullOrEmpty(snapshot.Scope)){Reset();current=null;return;}
        if(scope!=snapshot.Scope||revision!=snapshot.Revision){Reset();scope=snapshot.Scope;revision=snapshot.Revision;}
        current=snapshot;Selection.Sync(snapshot);
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){Reset();snapshot.Cancel?.Invoke();Event.current.Use();}
        hoveredBuild=null;tooltipRect=Rect.zero;
        Fill(new Rect(0,590,1440,310),Back);Fill(new Rect(0,590,1440,2),Gold);
        Label(new Rect(24,599,680,25),"我的备件  /  点击查看费用，再在棋盘放置",small);
        Label(new Rect(1000,599,260,25),"城市设施  /  当前等级与下次升级",small);
        if(Button(new Rect(1280,597,136,28),tradeOpen?"收起交易":"⇄  交易",snapshot.CanTrade||tradeOpen))
        {if(tradeOpen)Reset();else{tradeOpen=true;previewCard=null;Selection.BuildId=null;Array.Copy(Selection.Selected,give,8);Array.Clear(receive,0,8);}snapshot.Cancel?.Invoke();message="";}
        DrawBuilds(snapshot);
        Fill(new Rect(24,736,1392,1),Line);
        DrawHand(snapshot);
        if(tradeOpen)DrawTrade(snapshot);
        else if(snapshot.Offer!=null)DrawOffer(snapshot);
        else if(previewCard!=null)DrawCardPreview(snapshot);
        else DrawCost(snapshot);
    }
    void DrawBuilds(OperationSnapshot s)
    {
        var pieces=s.Builds.Where(b=>b.Track<0).ToArray();
        float width=Math.Min(116,936f/Math.Max(1,pieces.Length));
        for(int i=0;i<pieces.Length;i++)DrawBuild(new Rect(24+i*width,634,width-8,90),pieces[i],s);
        var facilities=s.Builds.Where(b=>b.Track>=0).OrderBy(b=>b.Track).ToArray();
        for(int i=0;i<facilities.Length;i++)DrawBuild(new Rect(992+i*141,634,132,90),facilities[i],s);
        if(facilities.Length==0)Label(new Rect(1003,654,400,48),"当前规则没有城市设施\n城市与骑士模式中开放",small);
    }
    void DrawBuild(Rect r,OperationBuild build,OperationSnapshot s)
    {
        bool selected=Selection.BuildId==build.Id, matched=Selection.Matches(build)&&build.Available;
        if(selected||matched)r.y-=8;
        Fill(new Rect(r.x+3,r.y+5,r.width,r.height),new Color(0,0,0,.24f));
        Fill(r,selected?new Color(.24f,.24f,.15f):matched?new Color(.10f,.24f,.22f):Surface);
        Border(r,selected||matched?Gold:Line,selected||matched?2:1);
        var icon=new Rect(r.x+8,r.y+5,Math.Min(48,r.width*.47f),48);
        if(build.Track>=0)
        {
            var face=EntityCardAssets.ImprovementFront(build.Track,Math.Max(1,Math.Min(5,build.Level)));
            if(face!=null)GUI.DrawTexture(icon,face,ScaleMode.ScaleToFit);
            Label(new Rect(r.x+61,r.y+12,r.width-64,32),"Lv."+build.Level,center);
        }
        else
        {
            var piece=OperationPieceAssets.Texture(build.Icon??build.Id,s.Seat??"P1",Math.Max(1,build.Level));
            if(piece!=null)GUI.DrawTexture(icon,piece,ScaleMode.ScaleToFit);
            else DrawPiece(icon,build.Icon??build.Id,BoardRenderer.SeatColor(s.Seat??"P1"));
            Label(new Rect(r.x+r.width-43,r.y+9,38,29),"×"+build.Count,center);
        }
        Label(new Rect(r.x+4,r.y+53,r.width-8,24),build.Name,center);
        var prior=GUI.color;GUI.color=build.Available?new Color(.53f,.83f,.69f):Muted;
        Label(new Rect(r.x+2,r.y+75,r.width-4,15),selected?"已选择":matched?"资源匹配":build.Available?"可用":"查看条件",new GUIStyle(center){fontSize=10});GUI.color=prior;
        if(r.Contains(Event.current.mousePosition))hoveredBuild=build.Id;
        if(GUI.Button(r,GUIContent.none,GUIStyle.none))
        {
            s.Cancel?.Invoke();Selection.Choose(build,s.Goods);tradeOpen=false;previewCard=null;message="";
            if(build.Available&&s.CanInteract)s.PrepareBuild?.Invoke(build);
        }
    }
    // Small code-drawn inventory silhouettes. These are UI symbols; board pieces use real meshes.
    static void DrawPiece(Rect r,string id,Color color)
    {
        id=id.ToLowerInvariant();color.a=1;
        if(id.Contains("road")){Fill(new Rect(r.x+4,r.y+24,r.width-8,12),color);Fill(new Rect(r.x+6,r.y+20,r.width-12,4),color*1.2f);}
        else if(id.Contains("ship")){Fill(new Rect(r.x+4,r.y+29,r.width-8,9),color);Fill(new Rect(r.x+23,r.y+5,3,26),Gold);Fill(new Rect(r.x+8,r.y+8,14,17),color);}
        else if(id.Contains("knight")){Fill(new Rect(r.x+15,r.y+5,18,16),color);Fill(new Rect(r.x+12,r.y+24,24,16),color);Fill(new Rect(r.x+5,r.y+39,38,5),color);Fill(new Rect(r.x+22,r.y+10,14,3),Back);}
        else if(id.Contains("wall")){Fill(new Rect(r.x+4,r.y+19,40,23),color);for(int i=0;i<3;i++)Fill(new Rect(r.x+4+i*15,r.y+10,10,12),color);}
        else if(id.Contains("development")){Fill(new Rect(r.x+9,r.y+4,30,42),Gold);Fill(new Rect(r.x+13,r.y+8,22,34),Surface);}
        else{Fill(new Rect(r.x+9,r.y+20,30,23),color);Fill(new Rect(r.x+15,r.y+11,18,12),color);if(id.Contains("city"))Fill(new Rect(r.x+30,r.y+4,13,39),color);Fill(new Rect(r.x+20,r.y+31,8,12),Back);}
    }
    void DrawHand(OperationSnapshot s)
    {
        Label(new Rect(140,743,340,22),s.GoodCount==8?"进步卡  ·  点击阅读与使用":"发展卡  ·  点击阅读与使用",small);
        Label(new Rect(531,743,620,22),"资源手牌  ·  点击抽出 / 再次点击收回  ·  已选 "+Selection.Selected.Sum()+" 张",small);
        if(Button(new Rect(1201,742,116,25),"清空选择",Selection.Selected.Sum()>0||Selection.BuildId!=null))
        {Selection.Clear();s.Cancel?.Invoke();message="";}
        Fill(new Rect(500,772,1,109),Line);
        DrawSpecialHand(s,new Rect(132,769,343,127));
        DrawResourceHand(s,new Rect(529,768,790,131));
    }
    void DrawSpecialHand(OperationSnapshot s,Rect area)
    {
        if(s.Cards.Count==0){Label(new Rect(area.x,area.y+39,area.width,35),"尚未持有卡牌",center);return;}
        float step=58,content=Math.Max(area.width,s.Cards.Count*step+26);
        cardScroll=GUI.BeginScrollView(area,cardScroll,new Rect(0,0,content,110),false,false);
        for(int i=0;i<s.Cards.Count;i++)
        {
            var card=s.Cards[i];var r=new Rect(i*step,4,78,100);
            Fill(r,Surface);Border(r,previewCard==card.Id?Gold:Line);
            var texture=card.Progress&&card.ArtIndex>=0?EntityCardAssets.ProgressFront(card.ArtIndex):null;
            if(texture!=null)GUI.DrawTexture(new Rect(r.x+2,r.y+2,74,80),texture,ScaleMode.ScaleToFit);
            else{Label(new Rect(r.x+7,r.y+14,64,45),card.Name,center);Fill(new Rect(r.x+15,r.y+62,48,2),Gold);}
            Fill(new Rect(r.x,r.yMax-22,r.width,22),Back);Label(new Rect(r.x,r.yMax-22,r.width,22),card.Name,center);
            if(GUI.Button(r,GUIContent.none,GUIStyle.none)){previewCard=previewCard==card.Id?null:card.Id;tradeOpen=false;s.Cancel?.Invoke();}
        }
        GUI.EndScrollView();
    }
    void DrawResourceHand(OperationSnapshot s,Rect area)
    {
        int total=s.Goods.Sum();
        if(total==0){Label(new Rect(area.x,area.y+35,area.width,45),"手牌区  ·  产出或交易后，资源会来到这里",center);return;}
        int kinds=s.Goods.Count(n=>n>0);float step=Mathf.Clamp((area.width-kinds*80+12)/Math.Max(1,total-kinds),19,62);
        float handWidth=(total-kinds)*step+kinds*80-12;
        float content=Math.Max(area.width,handWidth);
        handScroll=GUI.BeginScrollView(area,handScroll,new Rect(0,0,content,114),false,false);
        float x=Math.Max(0,(content-handWidth)/2);
        for(int good=0;good<s.GoodCount;good++)
        {
            if(s.Goods[good]==0)continue;
            var texture=EntityCardAssets.Texture(EntityCardAssets.Good(good).Front);
            for(int n=0;n<s.Goods[good];n++)
            {
                bool chosen=n<Selection.Selected[good];var r=new Rect(x,chosen?0:22,68,91);
                Fill(new Rect(r.x+2,r.y+3,68,91),new Color(0,0,0,.5f));Fill(r,Surface);
                if(texture!=null)GUI.DrawTexture(new Rect(r.x+2,r.y+2,64,87),texture,ScaleMode.StretchToFill);
                Fill(new Rect(r.x,r.yMax-20,r.width,20),Back);
                Label(new Rect(r.x+1,r.yMax-20,66,20),Names[good],new GUIStyle(center){fontSize=11});
                Border(r,chosen?Gold:Line,chosen?2:1);
                // Only the exposed strip is clickable, so overlapped cards have unambiguous hit targets.
                float hitWidth=n==s.Goods[good]-1?68:step;
                if(GUI.Button(new Rect(r.x,r.y,hitWidth,r.height),GUIContent.none,GUIStyle.none))
                {
                    s.Cancel?.Invoke();Selection.Toggle(good,n,s.Goods[good]);previewCard=null;
                    if(tradeOpen&&playerTrade){Array.Copy(Selection.Selected,give,8);message="";}
                }
                x+=step;
            }
            x+=80-step;
        }
        GUI.EndScrollView();
    }
    void DrawCost(OperationSnapshot s)
    {
        // The cost floats while inspecting inventory/hand, then yields the entire board
        // as soon as the pointer moves to a placement target. Selection remains intact.
        string id=hoveredBuild??(Event.current.mousePosition.y>=590?Selection.BuildId:null);
        var b=s.Builds.FirstOrDefault(x=>x.Id==id);if(b==null)return;
        tooltipRect=new Rect(280,448,650,132);Fill(tooltipRect,Back);Border(tooltipRect,Gold);
        Label(new Rect(298,458,570,28),b.Name+"  /  "+(b.Cost.Sum()==0?"无需资源":"建造费用"),heading);
        int index=0;
        for(int i=0;i<8;i++)if(b.Cost[i]>0)
        {
            float x=299+index++*113;bool missing=s.Goods[i]<b.Cost[i];
            OperationIconAssets.DrawGood(new Rect(x,494,31,31),i,missing);
            Label(new Rect(x+35,493,76,35),Names[i]+" ×"+b.Cost[i],small);
            if(missing){var old=GUI.color;GUI.color=Missing;Label(new Rect(x,526,106,22),"缺 "+(b.Cost[i]-s.Goods[i])+" 张",center);GUI.color=old;}
        }
        Label(new Rect(298,550,611,24),b.Available?(string.IsNullOrEmpty(b.Detail)?"资源已备齐，选择后按右侧提示完成放置。":b.Detail):(b.UnavailableReason??"请先满足回合、资源、库存与落点条件。"),small);
    }
    void DrawCardPreview(OperationSnapshot s)
    {
        var c=s.Cards.FirstOrDefault(x=>x.Id==previewCard);if(c==null){previewCard=null;return;}
        var rect=new Rect(286,160,650,415);Fill(rect,Back);Border(rect,Gold);
        Label(new Rect(309,179,548,36),c.Name,heading);
        var texture=c.Progress&&c.ArtIndex>=0?EntityCardAssets.ProgressFront(c.ArtIndex):null;
        if(texture!=null)GUI.DrawTexture(new Rect(308,225,166,271),texture,ScaleMode.ScaleToFit);
        Label(new Rect(494,235,412,234),c.Detail??"选择使用后，在操作面板完成具体目标或参数。",text);
        if(Button(new Rect(496,503,239,43),c.Playable?"使用 · 选择目标":"当前不可使用",c.Playable&&s.CanInteract))
        {s.PrepareCard?.Invoke(c);previewCard=null;Selection.Clear();}
        if(Button(new Rect(750,503,149,43),"关闭"))previewCard=null;
    }
    void DrawTrade(OperationSnapshot s)
    {
        target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
        giveGood=Mathf.Clamp(giveGood,0,s.GoodCount-1);receiveGood=Mathf.Clamp(receiveGood,0,s.GoodCount-1);
        Fill(TradeRect,Back);Border(TradeRect,Gold);
        Label(new Rect(298,171,386,34),"交换盘  /  确认后才交换资源",heading);
        if(Button(new Rect(1104,169,40,30),"×")){Reset();s.Cancel?.Invoke();return;}
        if(Button(new Rect(298,215,187,34),!playerTrade?"● 银行 / 港口":"银行 / 港口")){playerTrade=false;message="";}
        if(Button(new Rect(495,215,187,34),playerTrade?"● 与玩家交易":"与玩家交易")){playerTrade=true;message="";}
        if(playerTrade)
        {
            target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
            for(int i=0;i<s.OtherPlayers.Length;i++)if(Button(new Rect(755+i*122,215,114,34),(target==i?"● ":"")+s.OtherPlayers[i]))target=i;
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
        Label(new Rect(298,486,820,31),message!=""?message:error??(playerTrade?"报价发送后等待对方接受；对方手牌保持隐藏。":"比例已按当前港口与能力自动计算。"),small);
        if(Button(new Rect(299,532,554,39),playerTrade?"向 "+trade.OtherPlayer+" 发送报价":"确认交换",error==null))
        {message=s.Trade?.Invoke(trade)??"";if(message==""){tradeOpen=false;Selection.Clear();}}
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
        Label(new Rect(area.x,area.y,area.width,27),paying?"我付出  ·  也可点击下方手牌":"我希望获得",heading);
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
        var offer=s.Offer;Fill(TradeRect,Back);Border(TradeRect,Gold);
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
