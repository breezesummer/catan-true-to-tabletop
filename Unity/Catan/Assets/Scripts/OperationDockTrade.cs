using System;
using System.Linq;
using UnityEngine;

public sealed partial class OperationDock
{
    OperationSnapshot TradeValidationSnapshot(OperationSnapshot s)
    {
        if(!portTrade)return s;
        var rates=(int[])s.Rates.Clone();
        if(portGood.HasValue)rates[portGood.Value]=2;
        else for(int good=0;good<s.GoodCount;good++)rates[good]=3;
        return new OperationSnapshot{CanTrade=s.CanTrade,CanInteract=s.CanInteract,Offer=s.Offer,Goods=s.Goods,Bank=s.Bank,Rates=rates,GoodCount=s.GoodCount,OtherPlayers=s.OtherPlayers,ValidateTradeDraft=s.ValidateTradeDraft};
    }
    void DrawTrade(OperationSnapshot s)
    {
        target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
        DrawTradeFrame(tradeTitle??"资源交易",playerTrade?1:0,portTrade?portGood:null);
        var validation=TradeValidationSnapshot(s);
        Label(new Rect(287,210,866,28),playerTrade?"选择交易玩家 · "+TabletopCardInput.Hint:TabletopCardInput.Hint,new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
        if(playerTrade)
        {
            target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
            float choicesWidth=s.OtherPlayers.Length*130;
            for(int i=0;i<s.OtherPlayers.Length;i++)
            {
                var hit=new Rect(958-choicesWidth/2+i*130,237,121,39);
                StrategyHudArt.Avatar(new Rect(hit.x,hit.y,32,32),s.OtherPlayers[i]);
                Label(new Rect(hit.x+34,hit.y,87,34),DisplayPlayer(s.OtherPlayers[i]),M6App.PlayerNameStyle(new GUIStyle(small){alignment=TextAnchor.MiddleCenter},s.OtherPlayers[i]));
                if(target==i)ModernOperationUI.Fill(new Rect(hit.x,hit.yMax-2,hit.width,2),ModernOperationUI.Accent);
                if(ThreeKingdomsDirectInput.Read(hit,GUI.enabled)==1)target=i;
            }
            if(dragging&&GiveDropRect.Contains(Event.current.mousePosition))Fill(GiveDropRect,new Color(.77f,.64f,.39f,.22f));
            DrawTradePlayer(new Rect(286,280,386,58),s.Seat);
            DrawTradePlayer(new Rect(768,280,386,58),s.OtherPlayers.Length>0?s.OtherPlayers[target]:"*");
            DrawTradeBag(s,new Rect(286,344,386,200),give,true);
            DrawTradeBag(s,new Rect(768,344,386,200),receive,false);
        }
        else
        {
            var sideTitle=new GUIStyle(heading){alignment=TextAnchor.MiddleCenter};
            Label(new Rect(286,282,386,35),"付出",sideTitle);
            Label(new Rect(768,282,386,35),"换取",sideTitle);
            DrawTradeBag(s,new Rect(286,326,386,218),give,true,true);
            DrawTradeBag(s,new Rect(768,326,386,218),receive,false,true);
        }
        DrawTradeArrows(new Rect(686,(playerTrade?444:435)-OperationTradeCardSlot.CaptionHeight/2-23,68,46));
        var trade=new OperationTrade{Bank=!playerTrade,OtherPlayer=s.OtherPlayers.Length>0?s.OtherPlayers[target]:null,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
        string error=OperationSelection.ValidateTrade(validation,trade);
        var hint=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};hint.normal.textColor=error==null?Muted:Missing;
        Label(new Rect(287,549,860,28),message!=""?message:error??"",hint);
        Fill(new Rect(286,581,866,1),Line);
        if(playerTrade)
        {
            var broadcast=new OperationTrade{Bank=false,OtherPlayer=OperationTrade.AllPlayers,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
            if(Button(new Rect(680,593,227,35),"向所有玩家报价",OperationSelection.ValidateTrade(s,broadcast)==null))SendTrade(s,broadcast);
            if(Primary(new Rect(921,593,230,35),"向 "+DisplayPlayer(trade.OtherPlayer)+" 报价",error==null))SendTrade(s,trade);
        }
        else if(Primary(new Rect(744,593,407,35),"确认交换",error==null))SendTrade(s,trade);
        if(Button(new Rect(286,593,154,35),"清空选择")){Array.Clear(give,0,8);Array.Clear(receive,0,8);Selection.Clear();message="";}
    }
    void SendTrade(OperationSnapshot s,OperationTrade trade)
    {
        // 点击时再次检查当前快照，避免窗口草稿或库存变化提交空白、过期的请求。
        message=OperationSelection.ValidateTrade(TradeValidationSnapshot(s),trade)??"";
        if(message!=""){s.Notify?.Invoke(message);return;}
        message=s.Trade?.Invoke(trade)??"";
        if(message==""){tradeOpen=false;Selection.Clear();}else s.Notify?.Invoke(message);
    }
    void DrawTradePlayer(Rect area,string seat)
    {
        var name=DisplayPlayer(seat);
        var nameStyle=M6App.PlayerNameStyle(new GUIStyle(heading){alignment=TextAnchor.MiddleCenter},seat);
        float nameWidth=Mathf.Min(area.width-80,nameStyle.CalcSize(new GUIContent(name)).x+8);
        float avatarWidth=seat=="*"?92:52;
        float start=area.center.x-(avatarWidth+14+nameWidth)/2;
        if(seat=="*")
        {
            for(int i=0;i<Math.Min(3,current.OtherPlayers.Length);i++)StrategyHudArt.Avatar(new Rect(start+i*24,area.y+7,44,44),current.OtherPlayers[i]);
        }
        else
        {
            StrategyHudArt.Disc(new Rect(start,area.y+3,52,52),BoardRenderer.SeatColor(seat)*.65f);
            StrategyHudArt.Avatar(new Rect(start+4,area.y+7,44,44),seat);
        }
        Label(new Rect(start+avatarWidth+14,area.y,nameWidth,area.height),name,nameStyle);
    }
    Rect TradeCard(Rect face,int good,bool missing)
    {
        var texture=EntityCardAssets.ResourceOrCommodityFront(good);
        face=EntityCardAssets.FitCardRect(face,texture!=null?EntityCardAssets.FaceAspect(texture):OperationTradeCardLayout.Aspect,GUI.matrix);
        EntityCardAssets.DrawCardFace(face,texture,0,missing?new Color(1,.48f,.48f):Color.white);
        return face;
    }
    void DrawTradeBag(OperationSnapshot s,Rect area,int[] bag,bool paying,bool bank=false)
    {
        var goods=paying?OperationPanelOptions.PaymentGoods(s,bank&&portTrade&&s.ValidateTradeDraft==null?portGood:null):Enumerable.Range(0,s.GoodCount).ToArray();
        if(goods.Length==0)Label(new Rect(area.x,area.y+32,area.width,60),"当前没有可以付出的资源。",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
        var slots=OperationTradeCardLayout.Arrange(goods.Length,area.width,area.height);
        for(int index=0;index<goods.Length;index++)
        {
            int i=goods[index];var slot=slots[index];
            bool missing=paying&&bag[i]>s.Goods[i];
            var face=TradeCard(new Rect(area.x+slot.X,area.y+slot.Y,slot.Width,slot.Height),i,missing);
            var caption=new GUIStyle(center){fontSize=Mathf.Clamp(Mathf.RoundToInt(slot.Width*.19f),13,18),padding=new RectOffset(),wordWrap=false};if(missing)caption.normal.textColor=Missing;
            Label(new Rect(area.x+slot.X,area.y+slot.CaptionY,slot.Width,OperationTradeCardSlot.CaptionHeight),bag[i].ToString(),caption);
            if(bank&&!paying&&s.Bank[i]==0)Label(face,"库存为0",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
            int delta=TabletopCardInput.Delta(face);
            if(delta!=0)
            {
                int step=portTrade&&paying&&s.ValidateTradeDraft==null?TradeValidationSnapshot(s).Rates[i]:1;
                int maximum=bank&&!paying?s.Bank[i]:i<5?19:12;
                if(delta>0&&bag[i]+step>maximum)message="数量已达到上限，不允许继续选择。";
                else for(int n=0;n<step;n++)message=OperationCardQuantity.Change(bag,i,delta,maximum,int.MaxValue,bank&&paying&&s.ValidateTradeDraft==null)??"";
                if(paying)for(int good=0;good<8;good++)Selection.SetCount(good,bag[good],s.Goods[good]);
            }
        }
    }
    void DrawOffer(OperationSnapshot s)
    {
        if(!HasActionableOffer(s))return;
        var offer=s.Offer;DrawTradeFrame("交易请求",1);
        var perspective=OperationOfferPerspective.For(s);
        DrawTradePlayer(new Rect(286,246,386,58),s.Seat);
        DrawTradePlayer(new Rect(768,246,386,58),perspective.OtherSeat);
        DrawOfferBag(new Rect(286,323,386,195),perspective.OwnGive,s.Goods);
        DrawOfferBag(new Rect(768,323,386,195),perspective.OtherGive,null);
        DrawTradeArrows(new Rect(686,420.5f-OperationTradeCardSlot.CaptionHeight/2-23,68,46));
        bool missing=Enumerable.Range(0,8).Any(i=>perspective.OwnGive[i]>s.Goods[i]);
        string status=offer.IsBroadcast?(perspective.OwnOffer?"等待 "+offer.PendingRecipients.Length+" 位玩家回应":offer.RejectedRecipients.Contains(s.Seat)?"已拒绝，等待其他玩家回应":"任意一人接受即成交"):
            !perspective.Involved?"等待回应":perspective.OwnOffer?"等待对方回应":"";
        var hint=new GUIStyle(small);if(missing){status="红色资源牌数量不足，无法接受交易。";hint.normal.textColor=Missing;}
        hint.alignment=TextAnchor.MiddleCenter;Label(new Rect(286,538,868,40),status,hint);
        if(offer.CanAccept&&Primary(new Rect(744,590,406,38),"接受交易",!missing))s.Respond?.Invoke("accept");
        if(offer.CanReject&&Button(new Rect(286,590,406,38),"拒绝交易"))s.Respond?.Invoke("reject");
    }
    void DrawOfferBag(Rect area,int[] bag,int[] owned)
    {
        var goods=Enumerable.Range(0,8).Where(good=>bag[good]>0).ToArray();
        var slots=OperationTradeCardLayout.Arrange(goods.Length,area.width,area.height);
        for(int index=0;index<goods.Length;index++)
        {
            int i=goods[index];var slot=slots[index];
            bool missing=owned!=null&&bag[i]>owned[i];
            TradeCard(new Rect(area.x+slot.X,area.y+slot.Y,slot.Width,slot.Height),i,missing);
            var caption=new GUIStyle(center){fontSize=Mathf.Clamp(Mathf.RoundToInt(slot.Width*.19f),13,18),padding=new RectOffset(),wordWrap=false};if(missing)caption.normal.textColor=Missing;
            Label(new Rect(area.x+slot.X,area.y+slot.CaptionY,slot.Width,OperationTradeCardSlot.CaptionHeight),bag[i].ToString(),caption);
        }
    }

    void DrawTradeFrame(string title,int icon,int? good=null)
    {
        var plaque=new Rect(TradeRect.center.x-180,152,360,51);
        ModernOperationUI.WindowFrame(TradeRect,plaque,title,icon,good,heading);
    }
    static Texture2D tradeArrows;
    static void DrawTradeArrows(Rect area)
    {
        if(tradeArrows==null)
        {
            const int width=100,height=64,samples=3;
            var pixels=new Color[width*height];
            // 两支半箭头以中心旋转对称：上支向左，下支向右。
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float coverage=0;
                for(int sy=0;sy<samples;sy++)for(int sx=0;sx<samples;sx++)
                {
                    float u=x+(sx+.5f)/samples,v=y+(sy+.5f)/samples;
                    if(TradeArrowContains(u,v)||TradeArrowContains(width-u,height-v))coverage+=1f/(samples*samples);
                }
                var ink=Color.Lerp(new Color(.72f,.43f,.10f),new Color(1f,.85f,.35f),y/(float)height);
                ink.a=coverage;pixels[y*width+x]=ink;
            }
            tradeArrows=new Texture2D(width,height,TextureFormat.RGBA32,true){name="金色对称交易半箭头",filterMode=FilterMode.Trilinear};
            tradeArrows.SetPixels(pixels);tradeArrows.Apply(true,true);
        }
        GUI.DrawTexture(area,tradeArrows,ScaleMode.ScaleToFit);
    }
    static bool TradeArrowContains(float x,float y)
        =>x>=32&&x<=88&&y>=38&&y<=46||x>=12&&x<=32&&y>=38&&y<=x+26;
}
