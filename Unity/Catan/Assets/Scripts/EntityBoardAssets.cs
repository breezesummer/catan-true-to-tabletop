using UnityEngine;

// Only public presentation identifiers enter this adapter. It does not inspect a game state.
public static class EntityBoardAssets
{
    const string Root = "EntityAssets/v001/";
    public const float BoardSurfaceHeight = .008f;
    public const float BarbarianTrackWidth = .190f;
    public const float BarbarianTrackDepth = .200f;
    public const float BarbarianTrackHeight = .0015f;
    public const string VictoryMarkerResource = "EntityAssets/v005/tabletop-markers/catan-victory-marker";

    public static string PortResource(Catan.Core.Resource? resource)
    {
        string name;
        switch (resource)
        {
            case Catan.Core.Resource.Wood: name = "wood"; break;
            case Catan.Core.Resource.Brick: name = "brick"; break;
            case Catan.Core.Resource.Wool: name = "wool"; break;
            case Catan.Core.Resource.Wheat: name = "wheat"; break;
            case Catan.Core.Resource.Ore: name = "ore"; break;
            case null: name = "generic"; break;
            default: return null;
        }
        return "EntityAssets/v005/tabletop-markers/port-" + name;
    }
    public static GameObject ReferenceMarker(string resource, string label, Vector3 bottom, Quaternion rotation, Transform parent)
    {
        if (resource != null && resource.Contains("/port-"))
            return CoastalDecals.Dock(resource,label,bottom,rotation,parent);
        var marker = string.IsNullOrEmpty(resource) ? null : Instantiate(resource, label, bottom, rotation, parent);
        if (marker != null && (resource.Contains("/port-") || resource.Contains("/ground-")))
            PaintedMap.ApplyCoastalInk(marker, resource.Contains("/port-"));
        return marker;
    }

    public static string TerrainResource(string resource)
    {
        return PaintedMap.Region(resource) >= 0 ? PaintedMap.Root + "board-hex" : null;
    }

    public static GameObject Terrain(string resource, Vector3 bottom, Transform parent)
    {
        return PaintedMap.Tile(resource, bottom, parent);
    }

    public static string ShipResource(string playerId)
    {
        var colour = PlayerComponentAssets.ColourKey(playerId);
        return colour==null?null:PlayerComponentAssets.ExpansionRoot+"ship-"+colour;
    }

    public static Vector3 ShipWaterline(Vector3 edge)
    {
        return edge+Vector3.up*(TabletopOcean.WaterHeight-BoardSurfaceHeight-.0004f);
    }

    public static GameObject Ship(string label,Color seatColour,Vector3 bottom,Quaternion edgeRotation,Transform parent)
    {
        var colour=PlayerComponentAssets.ColourKey(seatColour);
        if(colour==null)return null;
        return Instantiate(PlayerComponentAssets.ExpansionRoot+"ship-"+colour,label,bottom,edgeRotation,parent);
    }

    public static GameObject Robber(string label, Vector3 bottom, Transform parent)
    {
        return Standing("robber/robber-black-group", label, bottom, parent);
    }

    public static GameObject Pirate(string label, Vector3 bottom, Transform parent)
    {
        return Standing("pirate/pirate-black", label, bottom, parent);
    }

    public static GameObject Merchant(string label, Vector3 bottom, Transform parent)
    {
        return Standing("merchant/merchant-neutral-cart", label, bottom, parent);
    }

    public static GameObject BarbarianTrack(Vector3 bottom, Transform parent)
    {
        var track = Standing("barbarian-track/barbarian-track", "Barbarian track", bottom, parent);
        if (track != null) track.name = "Barbarian track";
        return track;
    }

    // Printed circle centres in the corrected photo, in route order from sea to landing.
    // The source image is presentation data; the public rule position supplies the step.
    static readonly Vector2[] BarbarianCircles =
    {
        new Vector2(.870f,.130f), new Vector2(.610f,.130f),
        new Vector2(.340f,.180f), new Vector2(.460f,.385f),
        new Vector2(.610f,.570f), new Vector2(.670f,.817f),
        new Vector2(.395f,.856f), new Vector2(.142f,.868f)
    };

    public static Vector3 BarbarianLocalPosition(int publicPosition)
    {
        var circle = BarbarianCircles[Mathf.Clamp(publicPosition, 0, BarbarianCircles.Length - 1)];
        // FBX's X-axis conversion mirrors source UV-to-position mapping in Unity.
        return new Vector3((.5f - circle.x) * BarbarianTrackWidth, BarbarianTrackHeight,
            (circle.y - .5f) * BarbarianTrackDepth);
    }

    public static GameObject BarbarianShip(int publicPosition, Transform track, Transform parent)
    {
        if (track == null) return null;
        var bottom = track.TransformPoint(BarbarianLocalPosition(publicPosition));
        return Instantiate(Root + "barbarian-ship/barbarian-ship", "Barbarian ship step " + publicPosition,
            bottom, track.rotation, parent);
    }

    static GameObject Standing(string resource, string label, Vector3 bottom, Transform parent)
    {
        return Instantiate(Root + resource, label, bottom, Quaternion.Euler(0, 180, 0), parent);
    }

    static GameObject Instantiate(string resource, string label, Vector3 bottom, Quaternion rotation, Transform parent)
    {
        var model = PlayerComponentAssets.Model(resource);
        if (model == null) return null;
        // F6 单网格 FBX 的根变换保留 Blender → Unity 轴转换。
        if(resource.StartsWith("HarborFishing/",System.StringComparison.Ordinal))rotation*=model.transform.rotation;
        var instance = Object.Instantiate(model, bottom, rotation, parent);
        instance.name = label + " [" + model.name + "]";
        // Metre-scale, bottom-centre pivots and source materials remain authoritative for art.
        // Presentation meshes never introduce an interaction or physics collider.
        foreach (var collider in instance.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
        return instance;
    }
}
