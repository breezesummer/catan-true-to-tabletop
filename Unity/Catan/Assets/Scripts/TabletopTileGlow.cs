using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 每张棋桌只使用本人投影。读档与重返房间从公开建筑历史恢复玉光，金纹只追随新结算。
internal sealed class TabletopTileGlow
{
    const float NoGold = -100;
    readonly HashSet<string> villages = new HashSet<string>();
    readonly HashSet<string> owned = new HashSet<string>();
    readonly Dictionary<string,float> gold = new Dictionary<string,float>();
    PaintedMap map;
    Tile[] land;
    string seat;
    long baseline, latest;
    GameHistoryEntry[] lastHistory;
    internal IEnumerable<string> Owned => owned;
    internal string LastGoldTile { get; private set; }
    internal float LastGoldAt { get; private set; } = NoGold;

    internal void Bind(PaintedMap next, Tile[] tiles, PiecePlacement[] settlements, PiecePlacement[] cities, string viewer, long sequence)
    {
        bool reset = seat != viewer || sequence < latest;
        if (reset)
        {
            if (map != null) foreach (var id in owned) map.SetTileGlow(id,false,NoGold);
            villages.Clear(); owned.Clear(); gold.Clear(); lastHistory = null;
            seat = viewer; baseline = latest = sequence; LastGoldTile = null; LastGoldAt = NoGold;
        }
        bool newMap = map != next;
        map = next;
        land = tiles.Where(t => PaintedMap.Region(t.Resource) >= 0 && PaintedMap.Region(t.Resource) < 8).ToArray();
        foreach (var b in settlements.Concat(cities))
            if (b.PlayerId == seat) villages.Add(b.LocationId);
        RefreshOwned(newMap);
    }

    void RefreshOwned(bool repaint)
    {
        if (map == null || land == null) return;
        foreach (var tile in land)
            if (tile.Vertices.Any(villages.Contains) && (owned.Add(tile.Id) || repaint))
                map.SetTileGlow(tile.Id,true,gold.TryGetValue(tile.Id,out var start) ? start : NoGold);
    }

    internal void Observe(GameHistoryEntry[] history, double settlesAt)
    {
        if (map == null || history == null || ReferenceEquals(history,lastHistory)) return;
        long previous = latest;
        bool gainedSite = false;
        // 正常追加只访问新增尾段；补页或改写历史时再扫描旧建筑。
        int from = 0;
        if (lastHistory != null && lastHistory.Length > 0 && history.Length > lastHistory.Length &&
            history[lastHistory.Length-1].Sequence == lastHistory[lastHistory.Length-1].Sequence &&
            history[lastHistory.Length].Sequence > previous)
            from = lastHistory.Length;
        for (int i=from; i<history.Length; i++)
        {
            var entry = history[i];
            foreach (var change in entry.Changes)
                if (change.Category == "piece" && change.PlayerId == seat && (change.Name == "村庄" || change.Name == "城市"))
                    gainedSite |= villages.Add(change.LocationId);
            latest = Math.Max(latest,entry.Sequence);
        }
        if (gainedSite) RefreshOwned(false);
        for (int i=history.Length-1; i>=0; i--)
        {
            var entry = history[i];
            if (entry.Sequence <= previous || entry.Sequence <= baseline) break;
            foreach (var id in entry.OwnMultiHarvestTiles())
            {
                if (!owned.Contains(id)) continue;
                float start = Time.time + (float)Math.Max(0,settlesAt - Time.unscaledTimeAsDouble);
                gold[id] = start; map.SetTileGlow(id,true,start);
                LastGoldTile = id; LastGoldAt = start;
            }
        }
        lastHistory = history;
    }
}
