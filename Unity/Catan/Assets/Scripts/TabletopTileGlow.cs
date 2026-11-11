using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 只消费当前观看者的公开棋盘投影；流光表示常态产量，与结算历史、动画无关。
internal sealed class TabletopTileGlow
{
    readonly Dictionary<string,int> levels = new Dictionary<string,int>();
    PaintedMap map;
    internal IEnumerable<string> Owned => levels.Keys;
    internal int Level(string id) => levels.TryGetValue(id,out var level) ? level : 0;

    internal static int ProductionLevel(Tile tile, PiecePlacement[] settlements, PiecePlacement[] cities, string viewer)
    {
        bool produces = tile.Resource == "lake" || (tile.Number.HasValue && tile.Number > 0 &&
            (tile.Resource == "wood" || tile.Resource == "brick" || tile.Resource == "wool" ||
             tile.Resource == "wheat" || tile.Resource == "ore" || tile.Resource == "gold"));
        if (!produces) return 0;
        int count = settlements.Count(b => b.PlayerId == viewer && tile.Vertices.Contains(b.LocationId)) +
            2 * cities.Count(b => b.PlayerId == viewer && tile.Vertices.Contains(b.LocationId));
        return Math.Min(2,count);
    }

    internal void Bind(PaintedMap next, Tile[] tiles, PiecePlacement[] settlements, PiecePlacement[] cities, string viewer, long sequence)
    {
        bool changedMap = map != next;
        if (changedMap && map != null)
            foreach (var id in levels.Keys) map.SetTileGlow(id,false,-100);
        map = next;
        var removed = new HashSet<string>(levels.Keys);
        foreach (var tile in tiles)
        {
            int level = ProductionLevel(tile,settlements,cities,viewer);
            removed.Remove(tile.Id);
            if (changedMap || Level(tile.Id) != level)
                map?.SetTileGlow(tile.Id,level > 0,-100,level > 1);
            if (level > 0) levels[tile.Id] = level;
            else levels.Remove(tile.Id);
        }
        foreach (var id in removed)
        {
            map?.SetTileGlow(id,false,-100);
            levels.Remove(id);
        }
    }

    internal void Observe(GameHistoryEntry[] history, double settlesAt)
    {
        // 每帧仅推进共享材质时钟，地块属性仅在棋盘投影改变时更新。
        if (map != null) map.UpdateGlowClock();
    }
}
