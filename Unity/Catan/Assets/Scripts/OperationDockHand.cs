using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class OperationDock
{
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
        if(FishHandVisible||GeneralHandVisible){hoveredHand=null;handHoverAnchor=Rect.zero;return;}
        if(specialHand)DrawSpecialHand(s,new Rect(center-width/2,TabletopHandLayout.Baseline,width,151));
        else DrawResourceHand(s,new Rect(center-width/2,TabletopHandLayout.Baseline,width,151));
        // 保留整副手牌原来的叠放顺序；抬牌不改变层级和相同资源的身份。
        UpdateHandHover();
    }
    void UpdateHandHover()
    {
        var mouse=Event.current.mousePosition;
        bool overPaper=Windows?.Suppressed!=true&&((tradeOpen||HasActionableOffer(current))&&TradeRect.Contains(mouse)||facilityTrack>=0);
        // 先还原上一帧实际显示位置，再决定命中；不能用未展开的底牌位置猜测鼠标所指。
        foreach(var face in faces)
        {
            hoverAmounts.TryGetValue(face.Key,out float lift);
            var rect=face.RestRect;rect.y-=lift*10;face.Shape.Rect=rect;
        }
        HandFace hovered=null;
        if(GUI.enabled&&!overPaper&&HandViewport.Contains(mouse)&&!handGesture.Moved)
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
        foreach(var face in faces)
        {
            float amount;hoverAmounts.TryGetValue(face.Key,out amount);
            amount=Mathf.Lerp(amount,face==hovered?1:0,blend);hoverAmounts[face.Key]=amount;face.Hover=amount;
            var r=face.RestRect;r.y-=amount*10;face.Shape.Rect=r;
            face.Shape.Angle=0;
        }
    }
    void DrawHand()
    {
        if(StrategyHudTheme.HandPainted)HandPaintedTabletopSkin.Tray(HandRack);
        else CopperTabletopSkin.Panel(HandRack,ModernOperationUI.Surface);
        Register(HandRack);
        foreach(var deferred in landingFaces.Values){deferred.Clear();landingFacePool.Push(deferred);}
        landingFaces.Clear();
        if(FishHandVisible||GeneralHandVisible)return;
        if(hoveredHand!=null&&GUI.enabled)TabletopPointerCursor.Request();
        var handSlots=specialHand?specialSlots:resourceSlots;
        bool composite=collectionFlightsVisible&&!HasOverlay;
        int nextSlot=0;
        TabletopCollectionFlight after=null;
        foreach(var face in faces)
        {
            // 每张卡只画一次。先留下应盖住落位飞牌的纸牌，在对应插槽之后补画，防止半透明软影反复叠深。
            while(composite&&nextSlot<face.SlotIndex)
            {
                var flight=handSlots[nextSlot++].Flight;
                if(flight!=null&&flight.Progress(Time.unscaledTimeAsDouble)>=.60)after=flight;
            }
            if(after==null)DrawHandFace(face);
            else
            {
                if(!landingFaces.TryGetValue(after,out var deferred))landingFaces[after]=deferred=landingFacePool.Count>0?landingFacePool.Pop():new List<HandFace>();
                deferred.Add(face);
            }
            if(face.Shape.Rect.Overlaps(HandViewport))hits.Add(new HitShape{Rect=Intersect(face.Shape.Rect,HandViewport)});
        }
    }
    static Rect Intersect(Rect a,Rect b)=>Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin),Mathf.Max(a.yMin,b.yMin),Mathf.Min(a.xMax,b.xMax),Mathf.Min(a.yMax,b.yMax));
    static readonly Rect HandTabsArea = new Rect(1174,681,104,219);
    void DrawHandTabs(OperationSnapshot s)
    {
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!HasOverlay;
        bool hasGenerals=Generals?.ThreeKingdomsProjection!=null;
        Rect resource=new Rect(1178,hasGenerals?681:727,96,43),special=new Rect(1178,hasGenerals?727:773,96,43),sort=new Rect(1178,Fishing?.Available==true?867:830,96,32);
        Register(resource);Register(special);Register(sort);
        bool handEnabled=GUI.enabled;GUI.enabled=handEnabled&&s.Discard==null;
        bool holdCounts=collections!=null&&Time.unscaledTimeAsDouble<collections.OutcomesReadyAt;
        int resourceCount=displayedHandCounts.Observe(s.Scope,s.Seat,false,s.Goods.Take(s.GoodCount).Sum(),holdCounts);
        int specialCount=displayedHandCounts.Observe(s.Scope,s.Seat,true,s.Cards.Count,holdCounts);
        if(StrategyHudArt.HandControl(resource,"资源卡"+(DirectSelecting?" · "+(DirectSelection.SelectedGoods?.Invoke()??0):""),0,!specialHand&&!FishHandVisible&&!GeneralHandVisible,new TabletopHandCounter(resourceCount,s.ResourceHandLimit))){Generals?.CloseGeneralLibrary();Fishing?.Hide();specialHand=false;hoveredHand=null;handHoverAnchor=Rect.zero;}
        if(StrategyHudArt.HandControl(special,"发展卡"+(DirectSelecting?" · "+(DirectSelection.SelectedCards?.Invoke()??0):""),1,specialHand&&!FishHandVisible&&!GeneralHandVisible,new TabletopHandCounter(specialCount,s.SpecialHandLimit))){Generals?.CloseGeneralLibrary();Fishing?.Hide();specialHand=true;hoveredHand=null;handHoverAnchor=Rect.zero;}
        if(hasGenerals)
        {
            var generalTab=new Rect(1178,773,96,43);Register(generalTab);
            if(StrategyHudArt.HandControl(generalTab,"武将库",4,GeneralHandVisible,new TabletopHandCounter(Generals.GeneralLibraryCount,0)))
            {Fishing?.Hide();Generals.ShowGeneralLibrary();hoveredHand=null;handHoverAnchor=Rect.zero;}
        }
        if(Fishing?.Available==true)
        {
            var fishTab=new Rect(1178,820,96,43);Register(fishTab);
            if(StrategyHudArt.HandControl(fishTab,"鱼资源",3,FishHandVisible,new TabletopHandCounter(Fishing.Count,7)))
            {Generals?.CloseGeneralLibrary();Fishing.Show();hoveredHand=null;handHoverAnchor=Rect.zero;}
        }
        GUI.enabled=handEnabled;
        if(StrategyHudArt.HandControl(sort,"整理手牌",2))
        {
            Fishing?.Sort();fallbackOrder.Sort();collections?.SortHand(s.Seat);specialOrder.Sort(s.Cards.OrderBy(c=>c.Progress?c.ArtIndex:DevelopmentOrder(c.Id)).ThenBy(c=>c.Name).Select(c=>c.Id));
            collections?.SortSpecialHand(s.Seat,s.Cards.OrderBy(c=>c.Progress?c.ArtIndex:DevelopmentOrder(c.Id)).ThenBy(c=>c.Name).Select(c=>c.Id));
            hoveredHand=null;handHoverAnchor=Rect.zero;
        }
        double visibleCount=(specialHand?specialSlots:resourceSlots).Sum(slot=>slot.Weight);
        float contentWidth=TabletopHandLayout.ContentWidth(visibleCount,HandViewport.width);
        if(!FishHandVisible&&!GeneralHandVisible&&contentWidth>HandViewport.width)
        {
            var scrollRect=new Rect(HandViewport.x,875,HandViewport.width,13);Register(scrollRect);
            HandScroll=GUI.HorizontalScrollbar(scrollRect,HandScroll,HandViewport.width,0,contentWidth);
        }
        if(!FishHandVisible&&!GeneralHandVisible&&specialHand&&s.GoodCount==5)
        {
            var purchase=s.Builds.FirstOrDefault(b=>b.Icon=="development");
            if(purchase!=null&&Button(new Rect(Fishing?.Available==true?1066:1178,681,96,34),"购买发展卡",purchase.CanPrepare(s)&&!DirectSelecting))PrepareDevelopmentPurchase();
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
        var row=new TabletopHandLayout.Row(specialSlots,area.center.x,area.width,specialScroll);
        specialScroll=row.Scroll;
        if(collections!=null)collections.SpecialHandScroll=specialScroll;
        for(int i=0;i<specialSlots.Length;i++)
        {
            var slot=specialSlots[i];float x=row.Next(slot.Weight);if(slot.Flight!=null)continue;
            var card=slot.Card;bool chosen=DirectSelecting?DirectSelection.IsCardSelected?.Invoke(card.Id)==true:discardCard==card.Id||raisedCard==card.Id;
            var r=new Rect(x,TabletopHandLayout.Baseline-(chosen?22:0),TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
            var texture=RedrawnCardAssets.Hand(card);
            faces.Add(new HandFace{Shape=new HitShape{Rect=r},RestRect=r,SlotIndex=i,Card=card,Texture=texture,Selected=chosen});
        }
    }
    void DrawResourceHand(OperationSnapshot s,Rect area)
    {
        var slots=resourceSlots;
        var row=new TabletopHandLayout.Row(slots,area.center.x,area.width,resourceScroll);
        resourceScroll=row.Scroll;
        if(collections!=null)collections.ResourceHandScroll=resourceScroll;
        for(int index=0;index<slots.Length;index++)
        {
            var slot=slots[index];float x=row.Next(slot.Weight);if(slot.Flight!=null)continue;
            int good=slot.Good,n=slot.Ordinal;
            var texture=EntityCardAssets.Texture(EntityCardAssets.Good(good).Front);
            bool chosen=DirectSelecting?DirectSelection.IsGoodSelected?.Invoke(good,n)==true:Selection.IsCardSelected(good,n);
            var r=new Rect(x,TabletopHandLayout.Baseline-(chosen?22:0),TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
            faces.Add(new HandFace{Shape=new HitShape{Rect=r},RestRect=r,SlotIndex=index,Good=good,Ordinal=n,Texture=texture,Selected=chosen});
        }
    }
    void DrawHandFace(HandFace face)
    {
        if(!face.Shape.Rect.Overlaps(HandViewport))return;
        GUI.BeginGroup(HandViewport);
        var r=face.Shape.Rect;r.position-=HandViewport.position;
        DrawHandArtwork(face,r);
        GUI.EndGroup();
    }
    void DrawHandArtwork(HandFace face,Rect r)
    {
        if(face.Texture!=null)
        {
            // 纸芯与阴影沿实际卡面轮廓绘制，避免固定槽位纸底露成卡套边框。
            EntityCardAssets.DrawCardFace(r,face.Texture,face.Hover);
        }
        else
        {
            EntityCardAssets.DrawCardPaper(r,Surface,face.Hover);
            Label(new Rect(r.x+10,r.y+9,r.width-20,20),"发展卡",small);
            Label(new Rect(r.x+11,r.y+36,r.width-22,64),face.Card.Name,new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,fontSize=21});
        }
        var cardFrame=face.Texture!=null?EntityCardAssets.FitCardRect(r,EntityCardAssets.FaceAspect(face.Texture),GUI.matrix):r;
        if(face.Selected)TabletopCardFrame.Draw(cardFrame,true);
        else if(DirectSelection?.HighlightTargets==true&&Event.current.type==EventType.Repaint&&
            (face.Good>=0?DirectSelection.GoodAllowed(face.Good,face.Ordinal):face.Card!=null&&DirectSelection.CardAllowed(face.Card.Id)))
            GUI.DrawTexture(cardFrame,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.48f,.89f,.62f),2,cardFrame.width*.095f);
        if(face.Selected)
        {
            Label(new Rect(r.x+5,r.y+5,24,22),"✓",new GUIStyle(center){normal={textColor=Ink},fontSize=19});
        }
    }
    void InspectHand(HandFace face)
    {
        if(face==null)return;
        if(face.Card!=null){inspectedHand=face;raisedCard=face.Card.Id;return;}
        if(face.Good>=0&&InspectGood!=null){InspectGood(face.Good,face.Shape.Rect);return;}
        inspectedHand=face;
    }
    void DrawHandInspection()
    {
        if(inspectedHand==null)return;
        if(inspectedHand.Card!=null)
        {
            if(raisedCard==null){inspectedHand=null;return;}
            var cardArea=new Rect(622,207,298,440);Register(cardArea);
            var texture=inspectedHand.Texture??RedrawnCardAssets.Hand(inspectedHand.Card);
            EntityCardAssets.DrawCardFace(cardArea,texture);
            if(Button(new Rect(cardArea.xMax+8,cardArea.y,30,30),"×")){inspectedHand=null;raisedCard=null;return;}
            return;
        }
        var area=new Rect(Mathf.Clamp(inspectedHand.Shape.Rect.x,342,990),486,250,185);Register(area);Paper(area);
        string title=inspectedHand.Good>=0?Names[inspectedHand.Good]:inspectedHand.Card.Name;
        string detail=inspectedHand.Good>=0?"本人的真实手牌。选择只加入草稿，确认后才按规则支付。":inspectedHand.Card.Detail;
        Label(new Rect(area.x+12,area.y+10,185,26),title,heading);
        Label(new Rect(area.x+12,area.y+48,226,121),detail??"",small);
        if(Button(new Rect(area.xMax-35,area.y+8,27,26),"×"))inspectedHand=null;
    }
    void HandleHandInput(OperationSnapshot s)=>ProcessHandInput(s,Event.current);
    internal void ProcessHandInput(OperationSnapshot s,Event e)
    {
        if(!GUI.enabled)return;
        if(MobileTabletop.Enabled&&Input.touchCount>1){dragGood=-1;dragging=false;handGesture.Cancel();pressedHand=null;return;}
        HandFace hit=null;
        if(HandViewport.Contains(e.mousePosition))
        {
            if(hoveredHand!=null&&handHoverAnchor.Contains(e.mousePosition))hit=faces.FirstOrDefault(face=>face.Key==hoveredHand);
            if(hit==null)for(int i=faces.Count-1;i>=0;i--)if(faces[i].Shape.Contains(e.mousePosition)){hit=faces[i];break;}
        }
        bool overPaper=Windows?.Suppressed!=true&&((tradeOpen||HasActionableOffer(s))&&TradeRect.Contains(e.mousePosition)||facilityTrack>=0);
        if(e.type==EventType.ScrollWheel&&hit!=null&&!overPaper)
        {
            HandScroll=TabletopHandLayout.ClampScroll(HandScroll+(Mathf.Abs(e.delta.x)>Mathf.Abs(e.delta.y)?e.delta.x:e.delta.y)*35,(specialHand?specialSlots:resourceSlots).Sum(slot=>slot.Weight),HandViewport.width);
            hoveredHand=null;handHoverAnchor=Rect.zero;e.Use();return;
        }
        if(e.type==EventType.MouseDown&&e.button==1&&hit!=null&&!overPaper)
        {
            if(raisedCard!=null&&!DirectSelecting){raisedCard=null;inspectedHand=null;s.Cancel?.Invoke();}
            e.Use();return;
        }
        if(e.type==EventType.MouseDown&&e.button==0&&hit!=null&&!overPaper)
        {
            pressedHand=hit;handGesture.Begin(e.mousePosition.x,e.mousePosition.y,Time.unscaledTimeAsDouble);
            dragGood=hit.Good;dragOrdinal=hit.Ordinal;dragStart=e.mousePosition;scrollAtPress=HandScroll;dragging=false;
            e.Use();return;
        }
        if(pressedHand==null)return;
        if(e.type==EventType.MouseDrag&&e.button==0)
        {
            handGesture.Move(e.mousePosition.x,e.mousePosition.y,MobileTabletop.Enabled?18:7);
            dragging=handGesture.Moved&&!handGesture.Inspected&&!DirectSelecting&&tradeOpen&&playerTrade&&dragGood>=0;
            if(handGesture.Moved&&!dragging)HandScroll=TabletopHandLayout.ClampScroll(scrollAtPress+dragStart.x-e.mousePosition.x,(specialHand?specialSlots:resourceSlots).Sum(slot=>slot.Weight),HandViewport.width);
            e.Use();return;
        }
        if(e.type==EventType.MouseUp)handGesture.Move(e.mousePosition.x,e.mousePosition.y,MobileTabletop.Enabled?18:7);
        if(MobileTabletop.Enabled&&handGesture.Inspect(Time.unscaledTimeAsDouble))InspectHand(pressedHand);
        if(e.type==EventType.MouseUp&&e.button==0)
        {
            var pressed=pressedHand;bool clicked=handGesture.Release(hit?.Key==pressed.Key);
            pressedHand=null;
            if(dragging&&pressed.Card!=null&&CanPlayHandCard(s,pressed.Card)&&e.mousePosition.y<HandViewport.y)
            {
                raisedCard=pressed.Card.Id;
                if(s.DropCard?.Invoke(pressed.Card,e.mousePosition.x,e.mousePosition.y)!=true)raisedCard=null;
            }
            else if(dragging&&tradeOpen&&playerTrade&&GiveDropRect.Contains(e.mousePosition))
            {Selection.SelectCard(dragGood,dragOrdinal,s.Goods[dragGood]);Array.Copy(Selection.Selected,give,8);message="";}
            else if(clicked)
            {
                if(DirectSelecting)
                {
                    if(pressed.Good>=0){if(DirectSelection.GoodAllowed(pressed.Good,pressed.Ordinal))DirectSelection.SelectGood?.Invoke(pressed.Good,pressed.Ordinal);else if(DirectSelection.Reason!=null)s.Notify?.Invoke(DirectSelection.Reason);}
                    else if(DirectSelection.CardAllowed(pressed.Card.Id))DirectSelection.SelectCard?.Invoke(pressed.Card.Id);
                    else if(DirectSelection.Reason!=null)s.Notify?.Invoke(DirectSelection.Reason);
                }
                else if(pressed.Card!=null)
                {
                    if(s.Discard!=null)PickHandCard(s,pressed.Card);
                    else {PickHandCard(s,pressed.Card);InspectHand(pressed);}
                }
                else ChangeHandCard(s,pressed.Good,pressed.Ordinal,false);
            }
            dragGood=-1;dragging=false;pressedHand=null;e.Use();
        }
    }
    bool CanPlayHandCard(OperationSnapshot snapshot,OperationHandCard card) =>
        card.Playable&&snapshot.CanInteract&&!snapshot.RequiresSelection&&snapshot.Offer==null&&snapshot.Discard==null&&!tradeOpen&&facilityTrack<0&&snapshot.PrepareCard!=null;

    internal void PickHandCard(OperationSnapshot snapshot,OperationHandCard card)
    {
        if(snapshot.Discard!=null)
        {
            if(snapshot.Discard.Special)discardCard=discardCard==card.Id?null:card.Id;
            return;
        }
        if(card==null)return;
        raisedCard=card.Id;
        inspectedHand=new HandFace{Card=card,Texture=RedrawnCardAssets.Hand(card)};
    }
    internal void UseSelectedCard(OperationSnapshot snapshot)
    {
        var card=snapshot?.Cards.FirstOrDefault(c=>c.Id==raisedCard);
        if(card==null||DirectSelecting||!CanPlayHandCard(snapshot,card))return;
        snapshot.Cancel?.Invoke();Selection.Clear();Windows?.Clear();discardCard=null;
        inspectedHand=null;raisedCard=null;
        snapshot.PrepareCard(card);
    }
    void ChangeHandCard(OperationSnapshot s,int good,int ordinal,bool putBack)
    {
        bool selected=Selection.IsCardSelected(good,ordinal);
        if(putBack&&!selected)return;
        if(!selected&&s.Discard!=null&&!s.Discard.Special&&Selection.Selected.Sum()>=s.Discard.Amount)
        {s.Notify?.Invoke("已达到可选数量，不允许继续选择。");return;}
        s.Cancel?.Invoke();raisedCard=null;discardCard=null;Selection.BuildId=null;Selection.PieceOrdinal=-1;
        Selection.Toggle(good,ordinal,s.Goods[good]);
        if(tradeOpen&&playerTrade){Array.Copy(Selection.Selected,give,8);message="";}
    }
}
