using System;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 只使用 PlayerView 已公开的合法边。海岸两侧都可建时，陆地一侧预览道路，海面一侧预览船。
public static class TabletopRoadPlacement
{
    public static string Instruction(int remaining,GameHistoryEntry[] history=null)
    {
        var entries=history??Array.Empty<GameHistoryEntry>();
        int start=Array.FindLastIndex(entries,e=>e.Kind=="PlayRoadBuilding"||e.Kind=="PlayProgressCard"&&(e.Action=="打出「道路建设」"||e.Action=="打出「外交」"));
        int total=start>=0&&entries[start].Action=="打出「外交」"?1:2;
        int placed=start<0?total-remaining:entries.Skip(start+1).Sum(e=>e.Kind=="PlaceFreeRoad"||e.Kind=="PlaceFreeShip"?1:e.Kind=="ResolveProgressChoice"?(e.Changes??Array.Empty<GameHistoryChange>()).Where(c=>c.Category=="piece"&&c.Amount>0&&(c.Name=="道路"||c.Name=="船")).Sum(c=>c.Amount):0);
        return "请在地图上点击合法位置新建道路 "+Mathf.Clamp(placed,0,total)+"/"+total;
    }

    public static void Update(Vector2 mouse,bool click,Edge[] edges,Tile[] tiles,string[] roads,string[] ships,
        Func<string,Vector2> screen,Action<string,bool> roadPreview,Action<string,bool> shipPreview,Action clear,Action<string,bool> place,TabletopChoiceBubble choice)
    {
        string target="";float distance=MobileTabletop.PickRadius(25);
        if(TabletopUI.ContainsMapScreenPoint(mouse))foreach(var id in roads.Concat(ships).Distinct())
        {
            var point=screen(id);if(!TabletopUI.ContainsMapScreenPoint(point))continue;
            float current=Vector2.Distance(mouse,point);
            if(current<distance){target=id;distance=current;}
        }
        if(target.Length==0){clear();return;}
        bool ship=!roads.Contains(target);
        if(!ship&&ships.Contains(target))
        {
            var edge=edges.First(e=>e.Id==target);
            var adjacent=tiles.Where(t=>edge.Vertices.All(t.Vertices.Contains)).ToArray();
            var nearest=adjacent.OrderBy(t=>Vector2.SqrMagnitude(mouse-screen(t.Id))).FirstOrDefault();
            ship=nearest!=null&&(nearest.Resource=="sea"||nearest.Resource=="water");
            if(adjacent.Length==1&&!ship)
                ship=Vector2.Dot(mouse-screen(target),screen(adjacent[0].Id)-screen(target))<0;
        }
        if(ship)shipPreview(target,true);else roadPreview(target,true);
        if(click)
        {
            if(roads.Contains(target)&&ships.Contains(target))choice.Open(screen(target),"选择免费建设棋子",new[]{"道路","船"},i=>place(target,i==1));
            else place(target,ship);
        }
    }
}
