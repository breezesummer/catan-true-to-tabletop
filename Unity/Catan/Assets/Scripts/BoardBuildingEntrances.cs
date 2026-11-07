using System.Collections.Generic;
using Catan.Core;
using UnityEngine;

// 入口只取公开道路的建造顺序。接出第二条路后，原有入口朝向仍可从读档重建。
internal sealed class BoardBuildingEntrances
{
    readonly Dictionary<string, (string a, string b, Quaternion fromA, Quaternion fromB)> topology =
        new Dictionary<string, (string, string, Quaternion, Quaternion)>();
    readonly Dictionary<(string vertex, string seat), (string edge, Quaternion facing)> entrances =
        new Dictionary<(string, string), (string, Quaternion)>();

    public void SetEdge(Edge edge, Vector3 direction)
    {
        topology[edge.Id] = (edge.Vertices[0], edge.Vertices[1],
            Quaternion.LookRotation(direction), Quaternion.LookRotation(-direction));
    }

    public void Refresh(PiecePlacement[] roads)
    {
        entrances.Clear();
        foreach (var road in roads)
        {
            if (!topology.TryGetValue(road.LocationId, out var edge)) continue;
            Register(edge.a, road, edge.fromA);
            Register(edge.b, road, edge.fromB);
        }
    }

    void Register(string vertex, PiecePlacement road, Quaternion facing)
    {
        var key = (vertex, road.PlayerId);
        if (!entrances.ContainsKey(key)) entrances.Add(key, (road.LocationId, facing));
    }

    public string Appearance(PiecePlacement piece) => Appearance(piece.LocationId,piece.PlayerId);

    public string Appearance(string vertex,string seat)
    {
        return entrances.TryGetValue((vertex, seat), out var entrance)
            ? seat + "/" + entrance.edge : seat;
    }

    public void Orient(GameObject building, string vertex, string seat)
    {
        if (building != null && entrances.TryGetValue((vertex, seat), out var entrance))
        {
            building.transform.rotation = entrance.facing;
            building.GetComponentInChildren<FantasyBoardPiece>()?.Align(entrance.facing*Vector3.forward);
        }
    }

    public GameObject Place(string kind, PiecePlacement piece, string label, Vector3 bottom, Transform parent)
    {
        var building = PlayerComponentAssets.Building(kind, piece.PlayerId, label, bottom, parent);
        Orient(building, piece.LocationId, piece.PlayerId);
        return building;
    }
}
