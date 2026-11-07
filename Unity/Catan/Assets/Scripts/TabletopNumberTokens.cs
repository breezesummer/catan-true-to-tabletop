using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

public sealed class TabletopNumberTokens : MonoBehaviour
{
    JadeNumberAssets assets;
    Dictionary<string, Vector3> positions;
    string signature;

    public static TabletopNumberTokens Create(Transform parent, Tile[] tiles, Dictionary<string,Vector3> positions)
    {
        var root = new GameObject("悬浮玉薄签");
        root.transform.SetParent(parent, false);
        var tokens = root.AddComponent<TabletopNumberTokens>();
        tokens.assets = JadeNumberAssets.For(parent);
        tokens.positions = positions;
        tokens.Refresh(tiles);
        root.SetActive(false);
        return tokens;
    }

    public void Refresh(Tile[] tiles)
    {
        if (tiles == null) return;
        var visible = tiles.Where(t => t.Number.HasValue && t.Number >= 2 && t.Number <= 12 && t.Number != 7 &&
            t.Resource != "desert" && t.Resource != "fog" && t.Resource != "hidden" && t.Resource != "sea" && t.Resource != "water")
            .OrderBy(t => t.Id, System.StringComparer.Ordinal).ToArray();
        string next = string.Join("|", visible.Select(t => t.Id + ":" + t.Number.Value));
        if (next == signature) return;
        signature = next;
        foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        foreach (var tile in visible)
        {
            var slip = assets.CreateSlip(transform, "点数 " + tile.Number + " · " + tile.Id, tile.Number.Value);
            var anchor = positions[tile.Id] + Vector3.up * (EntityBoardAssets.BoardSurfaceHeight + .012f);
            slip.Place(anchor, new Vector2(0, .009f), JadeNumberSlip.LandWidth, .0083f);
        }
    }


}
