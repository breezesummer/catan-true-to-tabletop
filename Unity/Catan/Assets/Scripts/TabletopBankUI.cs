using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 银行仅消费玩家视图的公开库存与获准历史，飞牌只呈现已经完成的结算。
public sealed class TabletopBankUI
{
    public static readonly Rect Area = new Rect(18,55,220,210);
    readonly TabletopCollectionTimeline timeline = new TabletopCollectionTimeline();
    TabletopDicePresentation dice;
    readonly TabletopHandCountPresentation displayedBankCounts=new TabletopHandCountPresentation();
    public void BindDice(TabletopDicePresentation value) { dice=value; }
    int DisplayedBankCount(OperationSnapshot snapshot,int good)=>displayedBankCounts.Observe(snapshot.Scope,"bank:"+good,false,snapshot.Bank[good],dice!=null&&dice.IsRolling(Time.unscaledTimeAsDouble));
    public TabletopCollectionTimeline Collections => timeline;
    internal int VerificationCollectingFlightCount => timeline.Active(Time.unscaledTimeAsDouble).Length;
    internal string[] VerificationCollectionOrigins => timeline.Active(Time.unscaledTimeAsDouble).Select(f=>f.SourceTileId??(f.IsSpecial?f.Category+":"+f.Track:"bank")).ToArray();
    public Action<TabletopCollectionFlight> OccludeHand;
    readonly Dictionary<int,Rect> sources = new Dictionary<int,Rect>();
    readonly Dictionary<int,Rect> specialSources = new Dictionary<int,Rect>();
    Rect developmentSource;
    readonly Dictionary<string,Rect> destinations = new Dictionary<string,Rect>();
    readonly Dictionary<TabletopCollectionFlight,Rect> ownDestinations = new Dictionary<TabletopCollectionFlight,Rect>();
    readonly Dictionary<TabletopCollectionFlight,int> ownSlotIndices = new Dictionary<TabletopCollectionFlight,int>();
    OperationSnapshot current;
    static Texture2D developmentBack;
    static readonly string[] Names = {"木材","砖块","羊毛","谷物","矿石","布匹","钱币","纸张"};
    public static Rect StackLayout(int index) => new Rect(27+(index%4)*52,86+(index/4)*55,34,48);
    public void ObserveHud(OperationSnapshot snapshot,GameHistoryEntry[] entries,TabletopSeat[] seats)
    {
        if(snapshot==null)return;
        current=snapshot;sources.Clear();specialSources.Clear();destinations.Clear();ownDestinations.Clear();ownSlotIndices.Clear();
        timeline.Observe(snapshot.Scope,dice?.KnownHistory(entries)??entries,Time.unscaledTimeAsDouble,snapshot.CollectionTiles,dice?.SettlesAt);
        for(int good=0;good<snapshot.GoodCount;good++){DisplayedBankCount(snapshot,good);sources[good]=new Rect(42,32,34,48);}
        developmentSource=new Rect(70,32,34,48);
        for(int track=0;track<3;track++)specialSources[track]=new Rect(54+track*8,32,34,48);
        var slots=timeline.HandSlots(snapshot.Seat,snapshot.Goods,snapshot.GoodCount,Time.unscaledTimeAsDouble);
        for(int index=0;index<slots.Length;index++)
        {
            if(slots[index].Flight==null)continue;
            var pose=TabletopHandLayout.Resource(slots,index,TabletopHandLayout.ResourceCenter(false),TabletopHandLayout.ResourceWidth(false),false);
            ownDestinations[slots[index].Flight]=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);ownSlotIndices[slots[index].Flight]=index;
        }
        BindSpecialDestinations(snapshot);
        // 对手收牌只飞向公开头像，不恢复对手实体手牌或卡面。
        var ordered=seats.OrderBy(s=>s.Id).ToArray();
        for(int i=0;i<ordered.Length;i++)destinations[ordered[i].Id]=TabletopUI.PlayerAvatarRect(i,ordered.Length);
    }
    public void Draw(TabletopScene table,OperationSnapshot snapshot,GameHistoryEntry[] entries,TabletopSeat[] seats,
        Action<string> trade,Action purchase,List<TabletopWorldObjects.Hit> worldHits,List<Rect> hits)
    {
        if(snapshot==null)return;
        current=snapshot;sources.Clear();specialSources.Clear();destinations.Clear();ownDestinations.Clear();ownSlotIndices.Clear();
        timeline.Observe(snapshot.Scope,dice?.KnownHistory(entries)??entries,Time.unscaledTimeAsDouble,snapshot.CollectionTiles,dice?.SettlesAt);
        var title=table.ProjectLabel(new Rect(24,55,208,25));hits.Add(title);
        developmentSource=table.ProjectLabel(StackLayout(snapshot.GoodCount==5?5:9));
        if(ModernOperationUI.Button(title,"银行",snapshot.CanTrade))trade?.Invoke(null);
        for(int good=0;good<snapshot.GoodCount;good++)
        {
            int count=DisplayedBankCount(snapshot,good);var layout=StackLayout(good);
            var hit=Stack(table,"bank-good-"+good,layout,EntityCardAssets.Texture(EntityCardAssets.Good(good).Front),count);
            sources[good]=hit==null?table.ProjectLabel(layout):hit.Rect;
            if(hit!=null)
            {
                worldHits.Add(hit);
                if(GUI.enabled&&hit.Contains(Event.current.mousePosition)&&Event.current.type==EventType.MouseDown&&Event.current.button==0)
                {Event.current.Use();trade?.Invoke(null);}
            }
            DrawCount(table,layout,count);
        }
        if(snapshot.GoodCount==5)
        {
            var build=snapshot.Builds.FirstOrDefault(b=>b.Icon=="development");
            if(build!=null)
            {
                var layout=StackLayout(5);var hit=Stack(table,"bank-development",layout,DevelopmentBack(),build.Count);
                developmentSource=hit==null?table.ProjectLabel(layout):hit.Rect;
                if(hit!=null)
                {
                    worldHits.Add(hit);
                    if(GUI.enabled&&hit.Contains(Event.current.mousePosition)&&Event.current.type==EventType.MouseDown&&Event.current.button==0)
                    {Event.current.Use();purchase?.Invoke();}
                }
                DrawCount(table,layout,build.Count);
            }
        }
        else for(int track=0;track<3;track++)
        {
            var layout=StackLayout(8+track);
            // 进步牌库顺序不在公开视图中；仅展示三类牌背，不估算剩余张数。
            var hit=Stack(table,"bank-progress-"+track,layout,EntityCardAssets.ProgressBack(track),3);
            specialSources[track]=hit==null?table.ProjectLabel(layout):hit.Rect;
            worldHits.Add(hit);
        }
        BindSpecialDestinations(snapshot);
        float center=TabletopHandLayout.ResourceCenter(snapshot.Cards.Count>0),width=TabletopHandLayout.ResourceWidth(snapshot.Cards.Count>0);
        var slots=timeline.HandSlots(snapshot.Seat,snapshot.Goods,snapshot.GoodCount,Time.unscaledTimeAsDouble);
        for(int index=0;index<slots.Length;index++)
        {
            if(slots[index].Flight==null)continue;
            var pose=TabletopHandLayout.Resource(slots,index,center,width,false);
            ownDestinations[slots[index].Flight]=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
            ownSlotIndices[slots[index].Flight]=index;
        }
        var order=TabletopUI.OpponentOrder(snapshot.Seat,seats.Select(s=>s.Id));
        for(int i=0;i<order.Length;i++)
        {
            int place=order.Length==2?(i==0?0:2):i;
            var end=table.ProjectLabel(TabletopUI.OpponentHandCardLayout(place,0,1));
            destinations[order[i]]=end;
        }
    }
    void BindSpecialDestinations(OperationSnapshot snapshot)
    {
        var slots=timeline.SpecialSlots(snapshot.Seat,snapshot.Cards,Time.unscaledTimeAsDouble);
        for(int index=0;index<slots.Length;index++)
        {
            var flight=slots[index].Flight;if(flight==null)continue;
            var pose=TabletopHandLayout.Resource(slots,index,TabletopHandLayout.SpecialCenter(false),TabletopHandLayout.SpecialWidth(false),false);
            ownDestinations[flight]=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);ownSlotIndices[flight]=index;
        }
    }
    static void DrawCount(TabletopScene table,Rect layout,int count)
    {
        if(count!=0)return;
        var area=table.ProjectLabel(new Rect(layout.x-3,layout.yMax+1,layout.width+15,23));
        GUI.Label(area,"库存为0",new GUIStyle(ModernOperationUI.Center){wordWrap=false,fontSize=Mathf.Max(9,Mathf.RoundToInt(12*table.DisplayScale))});
    }
    static TabletopWorldObjects.Hit Stack(TabletopScene table,string key,Rect layout,Texture2D texture,int count)
    {
        TabletopWorldObjects.Hit hit=null;
        // 非空牌堆统一厚度，不用层数透露库存。
        for(int i=0;i<(count>0?3:0);i++)
            hit=table.Objects.Card(key+"-"+i,layout,texture,false,null,i*.00050f);
        return hit;
    }
    public void DrawFlights(bool ownHandVisible=true)
    {
        if(current==null||Event.current.type!=EventType.Repaint)return;
        double now=Time.unscaledTimeAsDouble;
        var active=timeline.Active(now);
        var landing=active.Where(f=>f.Player==current.Seat&&f.IsSpecial!=ownHandVisible&&f.Progress(now)>=.60&&ownSlotIndices.ContainsKey(f))
            .OrderBy(f=>ownSlotIndices[f]).ToArray();
        // 落位牌按真实手牌插槽从左向右合成，右邻牌逐渐遮挡下沉部分；仍在空中的牌最后绘制。
        foreach(var flight in landing)
        {
            DrawFlight(flight,now,ownHandVisible);
            OccludeHand?.Invoke(flight);
        }
        foreach(var flight in active.Where(f=>!landing.Contains(f)))DrawFlight(flight,now,ownHandVisible);
    }
    void DrawFlight(TabletopCollectionFlight flight,double now,bool resourceHandVisible)
    {
        Rect start;
        if(flight.SourcePlayerId!=null&&destinations.TryGetValue(flight.SourcePlayerId,out var playerStart))start=playerStart;
        else if(flight.SourceTileId!=null&&current.CollectionPosition!=null)
        {var point=current.CollectionPosition(flight.SourceTileId);start=new Rect(point.X-17,point.Y-24,34,48);}
        else if(flight.IsSpecial)start=flight.Category=="development"?developmentSource:specialSources.TryGetValue(flight.Track,out var specialStart)?specialStart:developmentSource;
        else if(!sources.TryGetValue(flight.Good,out start))return;
        bool own=flight.Player==current.Seat;
        Rect end;
        if(own)
        {
            if(flight.IsSpecial==resourceHandVisible||!ownDestinations.TryGetValue(flight,out end))end=new Rect(168,flight.IsSpecial?773:729,25,35);
        }
        else if(!destinations.TryGetValue(flight.Player,out end))return;
        float t=(float)flight.Progress(now),ease=t*t*(3-2*t),remaining=1-ease;
        // 银行上方起飞，接近手牌时减速落入各自插槽；起止速度为零，不再先生成一张手牌。
        var control1=start.center+new Vector2(105,-78);
        var control2=end.center+new Vector2(-95,-92);
        var center=remaining*remaining*remaining*start.center+3*remaining*remaining*ease*control1+
            3*remaining*ease*ease*control2+ease*ease*ease*end.center;
        float width=Mathf.Lerp(start.width,end.width,ease),height=Mathf.Lerp(start.height,end.height,ease);
        var r=new Rect(center.x-width/2,center.y-height/2,width,height);
        var face=flight.IsSpecial?(flight.Category=="progress"?(own&&flight.ProgressIndex>=0?EntityCardAssets.ProgressFront(flight.ProgressIndex):flight.Track>=0?EntityCardAssets.ProgressBack(flight.Track):DevelopmentBack()):DevelopmentBack()):
            EntityCardAssets.Texture(own?EntityCardAssets.Good(flight.Good).Front:EntityCardAssets.Good(0).Back);
        var matrix=GUI.matrix;
        GUIUtility.RotateAroundPivot(-5*Mathf.Sin(t*Mathf.PI),r.center);
        EntityCardAssets.DrawCardFace(r,face,Mathf.Sin(t*Mathf.PI));
        if(own&&flight.Category=="development"&&t>.5f)
            GUI.Label(new Rect(r.x+4,r.center.y-12,r.width-8,24),flight.CardName,new GUIStyle(ModernOperationUI.Center){fontSize=Mathf.Clamp(Mathf.RoundToInt(r.width*.16f),8,17),wordWrap=false});
        GUI.matrix=matrix;
    }
    public static Texture2D DevelopmentBack()
    {
        if(developmentBack!=null)return developmentBack;
        // 尚无发展牌背原照；保留简洁的程序绘制占位，不冒用资源或进步牌背。
        const int w=160,h=240;var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float diamond=Mathf.Abs(x-w*.5f)/w+Mathf.Abs(y-h*.5f)/h;
            bool emblem=diamond>.19f&&diamond<.205f||diamond>.11f&&diamond<.125f;
            pixels[y*w+x]=emblem?new Color(.83f,.66f,.35f):new Color(.075f,.17f,.23f);
        }
        developmentBack=new Texture2D(w,h,TextureFormat.RGB24,true){name="发展牌背程序占位 v006",filterMode=FilterMode.Trilinear};
        developmentBack.SetPixels(pixels);developmentBack.Apply(true,true);return developmentBack;
    }
}
