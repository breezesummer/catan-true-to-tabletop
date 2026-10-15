using System;
using System.Linq;
using UnityEngine;

public sealed partial class OperationDock
{
    internal static readonly Rect MobileHandArea=new Rect(0,735,1440,165);
    bool mobileDrawer,mobileCards,mobileReceiving;
    Vector2 mobileScroll;
    static readonly Rect MobileModal=new Rect(12,12,1416,876);
    // 只操作本地展示状态，供显式验收截图使用，不选择棋子或提交规则命令。
    internal void VerificationOpenMobileDrawer(bool cards)
    {
        if(!MobileTabletop.Enabled)return;
        mobileCards=cards;mobileDrawer=true;mobileScroll=Vector2.zero;
    }
    void MobileText(Rect rect,string value,bool muted=false)=>ModernOperationUI.Label(rect,value,muted?ModernOperationUI.Small:ModernOperationUI.Text);
    bool MobileButton(Rect rect,string caption,bool enabled=true,bool primary=false,bool selected=false)
    {Register(rect);return ModernOperationUI.Button(rect,caption,enabled,primary,selected);}
    void DrawMobile(OperationSnapshot s)
    {
        hits.Clear();faces.Clear();pieceFaces.Clear();hoveredHand=null;hoveredBuild=null;
        bool overlay=HasOverlay;
        bool original=GUI.enabled;
        GUI.enabled=original&&!overlay;
        ModernOperationUI.Round(MobileHandArea,ModernOperationUI.Background);
        if(MobileButton(new Rect(12,744,210,66),"建造棋子")){mobileCards=false;mobileDrawer=true;mobileScroll=Vector2.zero;}
        if(MobileButton(new Rect(12,816,210,70),"手牌 · "+s.Cards.Count)){mobileCards=true;mobileDrawer=true;mobileScroll=Vector2.zero;}
        float width=1188f/Math.Max(1,s.GoodCount);
        for(int i=0;i<s.GoodCount;i++)
        {
            float x=234+i*width;var tile=new Rect(x,744,width-8,142);Register(tile);
            ModernOperationUI.Round(tile,Selection.Selected[i]>0?Color.Lerp(ModernOperationUI.Surface,Gold,.25f):ModernOperationUI.Surface);
            OperationIconAssets.DrawGood(new Rect(x+8,750,39,50),i);
            MobileText(new Rect(x+49,751,width-57,48),"×"+s.Goods[i]);
            if(MobileButton(new Rect(x+4,807,(width-16)/2,74),"−",Selection.Selected[i]>0))Selection.SetCount(i,Selection.Selected[i]-1,s.Goods[i]);
            if(MobileButton(new Rect(x+width/2,807,(width-16)/2,74),"+"+Selection.Selected[i],Selection.Selected[i]<s.Goods[i]))Selection.SetCount(i,Selection.Selected[i]+1,s.Goods[i]);
        }
        if(Selection.BuildId!=null||Selection.Selected.Sum()>0)
        {if(MobileButton(new Rect(12,620,210,104),"放回 / 清选")){Selection.Clear();s.Cancel?.Invoke();}}
        GUI.enabled=original;
        if(tradeOpen)DrawMobileTrade(s);
        else if(s.Offer!=null)DrawMobileOffer(s);
        else if(previewCard!=null)DrawMobileCard(s);
        else if(facilityTrack>=0)DrawMobileFacility(s);
        else if(mobileDrawer)DrawMobileDrawer(s);
        HandlePieceInput(s);
        if(pieceDragging&&heldPiece!=null)
        {
            var image=OperationPieceAssets.Texture(heldPiece.Icon??heldPiece.Id,s.Seat??"P1",Math.Max(1,heldPiece.Level));
            if(image!=null)GUI.DrawTexture(new Rect(Event.current.mousePosition.x-66,Event.current.mousePosition.y-120,132,132),image,ScaleMode.ScaleToFit);
        }
    }
    void MobilePanel(string caption,Action close)
    {
        ModernOperationUI.Fill(new Rect(0,0,1440,900),new Color(0,0,0,.72f));
        ModernOperationUI.Panel(MobileModal);
        MobileText(new Rect(38,31,1100,68),caption);
        if(close!=null&&MobileButton(new Rect(1220,26,180,80),"关闭"))close();
    }
    void DrawMobileDrawer(OperationSnapshot s)
    {
        MobilePanel(mobileCards?"发展卡 / 进步卡":"建造 · 点按钮选取，或拖动棋子图标",()=>mobileDrawer=false);
        var viewport=new Rect(30,126,1380,736);
        int count=mobileCards?s.Cards.Count:s.Builds.Count;
        float total=Mathf.Max(viewport.height,((count+2)/3)*220);
        MobileTabletop.ScrollTouch(viewport,ref mobileScroll);
        mobileScroll=GUI.BeginScrollView(viewport,mobileScroll,new Rect(0,0,1350,total));
        for(int i=0;i<count;i++)
        {
            var r=new Rect((i%3)*450,(i/3)*220,432,204);
            ModernOperationUI.Panel(r);
            if(mobileCards)
            {
                var c=s.Cards[i];MobileText(new Rect(r.x+14,r.y+12,r.width-28,58),c.Name);
                MobileText(new Rect(r.x+14,r.y+74,r.width-28,52),c.Playable?"可以使用":c.UnavailableReason??"当前不可用",true);
                if(MobileButton(new Rect(r.x+12,r.y+128,r.width-24,64),"查看卡牌")){previewCard=c.Id;mobileDrawer=false;}
            }
            else
            {
                var b=s.Builds[i];var texture=OperationPieceAssets.Texture(b.Icon??b.Id,s.Seat??"P1",Math.Max(1,b.Level));
                if(texture!=null)GUI.DrawTexture(new Rect(r.x+14,r.y+12,94,96),texture,ScaleMode.ScaleToFit);
                MobileText(new Rect(r.x+114,r.y+12,302,58),b.Name+" ×"+b.Count);
                string cost=string.Join(" ",Enumerable.Range(0,Math.Min(8,b.Cost.Length)).Where(g=>b.Cost[g]>0).Select(g=>Names[g]+b.Cost[g]));
                MobileText(new Rect(r.x+114,r.y+72,300,60),cost==""?"当前阶段免费":cost,true);
                var button=new Rect(r.x+12,r.y+132,r.width-24,60);
                bool choose=ModernOperationUI.Button(button,b.Available?"拿起 / 建设":b.UnavailableReason??"当前不可用",b.Available&&s.CanInteract);
                var icon=new Rect(r.x+14,r.y+12,94,96);
                bool drag=GUI.enabled&&Event.current.type==EventType.MouseDown&&Event.current.button==0&&icon.Contains(Event.current.mousePosition)&&b.Available&&s.CanInteract;
                if(choose||drag)
                {
                    var point=MobileTabletop.Logical(MobileTabletop.Pointer);
                    PrepareBuild(b,s);mobileDrawer=false;heldPiece=drag?b:null;pieceStart=point;pieceDragging=false;pieceReleased=false;
                    Event.current.Use();GUI.EndScrollView();return;
                }
            }
        }
        if(count==0)MobileText(new Rect(20,30,1100,100),mobileCards?"当前没有发展卡或进步卡。":"当前没有可建设的棋子。",true);
        GUI.EndScrollView();
    }
    void DrawMobileCard(OperationSnapshot s)
    {
        var c=s.Cards.FirstOrDefault(card=>card.Id==previewCard);if(c==null){previewCard=null;return;}
        MobilePanel(c.Name,()=>previewCard=null);
        var image=c.Progress&&c.ArtIndex>=0?EntityCardAssets.ProgressFront(c.ArtIndex):null;
        if(image!=null)EntityCardAssets.DrawCardFace(new Rect(50,140,410,600),image);
        MobileText(new Rect(510,145,840,310),c.Detail??"使用后按提示选择目标。");
        MobileText(new Rect(510,480,840,130),c.Playable?"请选择使用，再按当前选择面板完成效果。":c.UnavailableReason??"当前阶段不能使用。",true);
        if(MobileButton(new Rect(510,678,840,110),"使用卡牌",c.Playable&&s.CanInteract,true))
        {previewCard=null;mobileDrawer=false;s.Cancel?.Invoke();s.PrepareCard?.Invoke(c);}
    }
    void DrawMobileFacility(OperationSnapshot s)
    {
        int track=facilityTrack;var b=OperationPanelOptions.Facility(s,track);if(b==null){facilityTrack=-1;return;}
        MobilePanel(new[]{"贸易","政治","科学"}[track]+"设施 · "+b.Level+" 级",()=>facilityTrack=-1);
        MobileText(new Rect(70,150,1280,190),b.Detail??FacilityAbility(track,b.Level));
        MobileText(new Rect(70,380,1280,130),"费用："+string.Join("  ",Enumerable.Range(0,8).Where(g=>b.Cost[g]>0).Select(g=>Names[g]+" × "+b.Cost[g])));
        if(!b.Available)MobileText(new Rect(70,520,1280,120),b.UnavailableReason??"当前不能升级。",true);
        if(MobileButton(new Rect(70,700,1280,120),b.Level>=5?"已达到最高等级":"建设第 "+(b.Level+1)+" 级",b.Available&&b.Level<5&&s.CanInteract,true))PrepareBuild(b,s);
    }
    void DrawMobileTrade(OperationSnapshot s)
    {
        MobilePanel(tradeTitle??"交易",()=>{Reset();s.Cancel?.Invoke();});
        if(!tradeOpen)return;
        var validation=TradeValidationSnapshot(s);
        target=Mathf.Clamp(target,0,Math.Max(0,s.OtherPlayers.Length-1));
        if(playerTrade)
        {
            for(int i=0;i<s.OtherPlayers.Length;i++)if(MobileButton(new Rect(40+i*270,122,250,76),s.OtherPlayers[i],true,false,target==i))target=i;
        }
        else MobileText(new Rect(40,126,1280,62),portTrade?"此港口的资源种类和兑换比例按规则校验。":"银行兑换；专属港口请点地图上的己方港口。",true);
        if(MobileButton(new Rect(40,212,650,80),"我给予 · "+give.Sum()+" 张",true,false,!mobileReceiving))mobileReceiving=false;
        if(MobileButton(new Rect(710,212,650,80),"我获得 · "+receive.Sum()+" 张",true,false,mobileReceiving))mobileReceiving=true;
        var goods=mobileReceiving?Enumerable.Range(0,s.GoodCount).ToArray():OperationPanelOptions.PaymentGoods(s,portTrade?portGood:null);
        int[] bag=mobileReceiving?receive:give;
        for(int index=0;index<goods.Length;index++)
        {
            int g=goods[index];float x=40+(index%4)*334,y=312+(index/4)*180;
            ModernOperationUI.Round(new Rect(x,y,318,164),ModernOperationUI.Surface);
            OperationIconAssets.DrawGood(new Rect(x+10,y+8,44,58),g);
            MobileText(new Rect(x+62,y+10,244,68),Names[g]+" × "+(mobileReceiving?s.Bank[g]:s.Goods[g]),true);
            int step=!playerTrade&&!mobileReceiving?Math.Max(1,validation.Rates[g]):1;
            if(MobileButton(new Rect(x+6,y+84,90,74),"−",bag[g]>0))bag[g]=Math.Max(0,bag[g]-step);
            ModernOperationUI.Label(new Rect(x+100,y+84,110,74),bag[g].ToString(),ModernOperationUI.Center);
            int max=mobileReceiving?(playerTrade?(g<5?19:12):s.Bank[g]):s.Goods[g];
            if(MobileButton(new Rect(x+216,y+84,96,74),"+",bag[g]+step<=max))
            {
                int next=bag[g]+step;if(!playerTrade)Array.Clear(bag,0,8);
                bag[g]=Math.Min(max,next);
            }
        }
        var trade=new OperationTrade{Bank=!playerTrade,OtherPlayer=s.OtherPlayers.Length>0?s.OtherPlayers[target]:null,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
        string error=OperationSelection.ValidateTrade(validation,trade);
        MobileText(new Rect(40,681,1320,62),message!=""?message:error??"可以确认交易。",true);
        if(MobileButton(new Rect(40,766,300,96),"清空选择")){Array.Clear(give,0,8);Array.Clear(receive,0,8);}
        if(playerTrade)
        {
            var all=new OperationTrade{Bank=false,OtherPlayer=OperationTrade.AllPlayers,Give=(int[])give.Clone(),Receive=(int[])receive.Clone()};
            if(MobileButton(new Rect(360,766,480,96),"向所有玩家报价",OperationSelection.ValidateTrade(s,all)==null))SendTrade(s,all);
            if(MobileButton(new Rect(860,766,500,96),"向 "+trade.OtherPlayer+" 报价",error==null,true))SendTrade(s,trade);
        }
        else if(MobileButton(new Rect(710,766,650,96),"确认交换",error==null,true))SendTrade(s,trade);
    }
    void DrawMobileOffer(OperationSnapshot s)
    {
        var offer=s.Offer;MobilePanel("交易报价 · "+offer.Proposer+" → "+(offer.IsBroadcast?"所有玩家":offer.Recipient),null);
        MobileText(new Rect(50,160,1280,160),offer.Proposer+" 给予："+MobileBag(offer.Give));
        MobileText(new Rect(50,360,1280,160),"希望获得："+MobileBag(offer.Receive));
        if(offer.CanReject&&MobileButton(new Rect(50,680,620,130),"拒绝交易"))s.Respond?.Invoke("reject");
        if(offer.CanAccept&&MobileButton(new Rect(710,680,650,130),"接受交易",true,true))s.Respond?.Invoke("accept");
        if(offer.CanCancel&&MobileButton(new Rect(710,680,650,130),"撤回报价"))s.Respond?.Invoke("cancel");
        if(!offer.CanAccept&&!offer.CanReject&&!offer.CanCancel)MobileText(new Rect(50,620,1280,160),"等待其他玩家回应。",true);
    }
    static string MobileBag(int[] bag)=>string.Join("  ",Enumerable.Range(0,Math.Min(8,bag.Length)).Where(g=>bag[g]>0).Select(g=>Names[g]+" × "+bag[g]));
}
