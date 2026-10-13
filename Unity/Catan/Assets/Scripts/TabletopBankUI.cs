using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 银行仅消费玩家视图的公开库存与获准历史，飞牌只呈现已经完成的结算。
public sealed class TabletopBankUI
{
    public static readonly Rect Area = new Rect(18,316,199,282);
    readonly TabletopCollectionTimeline timeline = new TabletopCollectionTimeline();
    public TabletopCollectionTimeline Collections => timeline;
    public Action<TabletopCollectionFlight> OccludeHand;
    readonly Dictionary<int,Rect> sources = new Dictionary<int,Rect>();
    readonly Dictionary<string,Rect> destinations = new Dictionary<string,Rect>();
    readonly Dictionary<TabletopCollectionFlight,Rect> ownDestinations = new Dictionary<TabletopCollectionFlight,Rect>();
    readonly Dictionary<TabletopCollectionFlight,int> ownSlotIndices = new Dictionary<TabletopCollectionFlight,int>();
    OperationSnapshot current;
    static Texture2D developmentBack;
    static readonly string[] Names = {"木材","砖块","羊毛","谷物","矿石","布匹","钱币","纸张"};
    public static Rect StackLayout(int index) => new Rect(27+(index%3)*62,340+(index/3)*63,42,59);
    public void Draw(TabletopScene table,OperationSnapshot snapshot,GameHistoryEntry[] entries,TabletopSeat[] seats,
        Action<string> trade,Action purchase,List<TabletopWorldObjects.Hit> worldHits,List<Rect> hits)
    {
        if(snapshot==null)return;
        current=snapshot;sources.Clear();destinations.Clear();ownDestinations.Clear();ownSlotIndices.Clear();
        timeline.Observe(snapshot.Scope,entries,Time.unscaledTimeAsDouble);
        var title=table.ProjectLabel(new Rect(24,316,181,25));hits.Add(title);
        if(ModernOperationUI.Button(title,"银行",snapshot.CanTrade))trade?.Invoke(null);
        for(int good=0;good<snapshot.GoodCount;good++)
        {
            int count=snapshot.Bank[good];var layout=StackLayout(good);
            var hit=Stack(table,"bank-good-"+good,layout,EntityCardAssets.Texture(EntityCardAssets.Good(good).Front),count);
            sources[good]=hit==null?table.ProjectLabel(layout):hit.Rect;
            if(hit!=null)
            {
                worldHits.Add(hit);
                if(GUI.enabled&&hit.Contains(Event.current.mousePosition)&&Event.current.type==EventType.MouseDown&&Event.current.button==0)
                {Event.current.Use();trade?.Invoke(null);}
            }
            DrawCount(table,layout,count);
            if(hit!=null&&hit.Contains(Event.current.mousePosition))
                GUI.Label(table.ProjectLabel(new Rect(layout.x-8,layout.y-9,layout.width+16,21)),Names[good],ModernOperationUI.Small);
        }
        if(snapshot.GoodCount==5)
        {
            var build=snapshot.Builds.FirstOrDefault(b=>b.Icon=="development");
            if(build!=null)
            {
                var layout=StackLayout(5);var hit=Stack(table,"bank-development",layout,DevelopmentBack(),build.Count);
                if(hit!=null)
                {
                    worldHits.Add(hit);
                    if(GUI.enabled&&hit.Contains(Event.current.mousePosition)&&Event.current.type==EventType.MouseDown&&Event.current.button==0)
                    {Event.current.Use();purchase?.Invoke();}
                }
                DrawCount(table,layout,build.Count);
                if(hit!=null&&hit.Contains(Event.current.mousePosition))
                    GUI.Label(table.ProjectLabel(new Rect(layout.x-8,layout.y-9,layout.width+16,21)),"发展",ModernOperationUI.Small);
            }
        }
        else for(int track=0;track<3;track++)
        {
            var layout=StackLayout(8+track);
            // 进步牌库顺序不在公开视图中；仅展示三类牌背，不估算剩余张数。
            var hit=Stack(table,"bank-progress-"+track,layout,EntityCardAssets.ProgressBack(track),3);
            worldHits.Add(hit);
        }
        float center=TabletopHandLayout.ResourceCenter(snapshot.Cards.Count>0),width=snapshot.Cards.Count>0?704:1060;
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
            var end=table.ProjectLabel(place==1?new Rect(889,52,27,39):new Rect(place==0?54:1271,256,27,39));
            destinations[order[i]]=end;
        }
    }
    static void DrawCount(TabletopScene table,Rect layout,int count)
    {
        var area=table.ProjectLabel(new Rect(layout.xMax+1,layout.y+20,18,20));
        GUI.Label(area,count.ToString(),new GUIStyle(ModernOperationUI.Center){wordWrap=false,fontSize=Mathf.Max(9,Mathf.RoundToInt(12*table.DisplayScale))});
    }
    static TabletopWorldObjects.Hit Stack(TabletopScene table,string key,Rect layout,Texture2D texture,int count)
    {
        TabletopWorldObjects.Hit hit=null;
        // 卡片同轴叠放，以真实纸张厚度表现数量，不增加透明牌套或装饰外框。
        for(int i=0;i<Math.Min(5,count);i++)
            hit=table.Objects.Card(key+"-"+i,layout,texture,false,null,i*.00050f);
        return hit;
    }
    public void DrawFlights()
    {
        if(current==null||Event.current.type!=EventType.Repaint)return;
        double now=Time.unscaledTimeAsDouble;
        var active=timeline.Active(now);
        var landing=active.Where(f=>f.Player==current.Seat&&f.Progress(now)>=.60&&ownSlotIndices.ContainsKey(f))
            .OrderBy(f=>ownSlotIndices[f]).ToArray();
        // 落位牌按真实手牌插槽从左向右合成，右邻牌逐渐遮挡下沉部分；仍在空中的牌最后绘制。
        foreach(var flight in landing)
        {
            DrawFlight(flight,now);
            OccludeHand?.Invoke(flight);
        }
        foreach(var flight in active.Where(f=>!landing.Contains(f)))DrawFlight(flight,now);
    }
    void DrawFlight(TabletopCollectionFlight flight,double now)
    {
        if(!sources.TryGetValue(flight.Good,out var start))return;
        bool own=flight.Player==current.Seat;
        Rect end;
        if(own?!ownDestinations.TryGetValue(flight,out end):!destinations.TryGetValue(flight.Player,out end))return;
        float t=(float)flight.Progress(now),ease=t*t*(3-2*t),remaining=1-ease;
        // 银行上方起飞，接近手牌时减速落入各自插槽；起止速度为零，不再先生成一张手牌。
        var control1=start.center+new Vector2(105,-78);
        var control2=end.center+new Vector2(-95,-92);
        var center=remaining*remaining*remaining*start.center+3*remaining*remaining*ease*control1+
            3*remaining*ease*ease*control2+ease*ease*ease*end.center;
        float width=Mathf.Lerp(start.width,end.width,ease),height=Mathf.Lerp(start.height,end.height,ease);
        var r=new Rect(center.x-width/2,center.y-height/2,width,height);
        var face=EntityCardAssets.Texture(EntityCardAssets.Good(flight.Good).Front);
        if(!own&&t>.66f)face=EntityCardAssets.Texture(EntityCardAssets.Good(0).Back);
        var matrix=GUI.matrix;
        GUIUtility.RotateAroundPivot(-5*Mathf.Sin(t*Mathf.PI),r.center);
        EntityCardAssets.DrawCardFace(r,face,Mathf.Sin(t*Mathf.PI));
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
