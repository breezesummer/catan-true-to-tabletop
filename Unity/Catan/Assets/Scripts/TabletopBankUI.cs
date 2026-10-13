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
    readonly Dictionary<int,Rect> sources = new Dictionary<int,Rect>();
    readonly Dictionary<string,Rect> destinations = new Dictionary<string,Rect>();
    OperationSnapshot current;
    static Texture2D developmentBack;
    static readonly string[] Names = {"木材","砖块","羊毛","谷物","矿石","布匹","钱币","纸张"};
    public static Rect StackLayout(int index) => new Rect(27+(index%3)*62,340+(index/3)*63,42,59);
    public void Draw(TabletopScene table,OperationSnapshot snapshot,GameHistoryEntry[] entries,TabletopSeat[] seats,
        Action<string> trade,Action purchase,List<TabletopWorldObjects.Hit> worldHits,List<Rect> hits)
    {
        if(snapshot==null)return;
        current=snapshot;sources.Clear();destinations.Clear();
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
        int total=snapshot.Goods.Take(snapshot.GoodCount).Sum(),drawn=0;
        float center=TabletopHandLayout.ResourceCenter(snapshot.Cards.Count>0),width=snapshot.Cards.Count>0?704:1060;
        for(int good=0;good<snapshot.GoodCount;good++)
        {
            int index=drawn+Math.Max(0,snapshot.Goods[good]-1);
            var pose=TabletopHandLayout.Card(index,Math.Max(1,total),center,width,false);
            destinations[snapshot.Seat+":"+good]=new Rect(pose.X,pose.Y,TabletopHandLayout.CardWidth,TabletopHandLayout.CardHeight);
            drawn+=snapshot.Goods[good];
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
            hit=table.Objects.Card(key+"-"+i,layout,texture,false,null,i*.00038f);
        return hit;
    }
    public void DrawFlights()
    {
        if(current==null||Event.current.type!=EventType.Repaint)return;
        double now=Time.unscaledTimeAsDouble;
        foreach(var flight in timeline.Active(now))
        {
            if(!sources.TryGetValue(flight.Good,out var start))continue;
            bool own=flight.Player==current.Seat;
            if(!destinations.TryGetValue(own?flight.Player+":"+flight.Good:flight.Player,out var end))continue;
            float t=(float)flight.Progress(now),ease=1-Mathf.Pow(1-t,3);
            var center=Vector2.Lerp(start.center,end.center,ease)+Vector2.up*(Mathf.Sin(t*Mathf.PI)*62);
            float width=Mathf.Lerp(start.width,end.width,ease),height=Mathf.Lerp(start.height,end.height,ease);
            var r=new Rect(center.x-width/2,center.y-height/2,width,height);
            var face=EntityCardAssets.Texture(EntityCardAssets.Good(flight.Good).Front);
            if(!own&&t>.66f)face=EntityCardAssets.Texture(EntityCardAssets.Good(0).Back);
            EntityCardAssets.DrawCardFace(r,face);
        }
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
