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
        return string.IsNullOrEmpty(resource) ? null : Instantiate(resource, label, bottom, rotation, parent);
    }

    public static string TerrainResource(string resource)
    {
        switch (resource)
        {
            case "wood": return "PhotoSamples/v001/forest";
            case "ore": return "EntityAssets/v006/ore-tile/tile-ore";
            case "brick": return Root + "tiles/tile-hills";
            case "wool": return Root + "tiles/tile-pasture";
            case "wheat": return Root + "tiles/tile-fields";
            case "desert": return Root + "tiles/tile-desert";
            case "sea": case "water": return Root + "tiles/tile-sea";
            case "gold": return Root + "tiles/tile-gold";
            // Fog is an unrevealed public placeholder, never a request for a terrain asset.
            default: return null;
        }
    }

    public static GameObject Terrain(string resource, Vector3 bottom, Transform parent)
    {
        // v001 has already converted the photographed flat-top hexagon to the board's
        // 30-degree pointy hexagon. Its 8 mm top matches the existing placement surface.
        var tile = Instantiate(TerrainResource(resource), resource, bottom, Quaternion.identity, parent);
        TabletopPhysicalTileV005.Apply(tile);
        return tile;
    }

    public static string ShipResource(string playerId)
    {
        var colour = PlayerComponentAssets.ColourKey(playerId);
        if (colour == "beige") colour = "ivory";
        return colour == null ? null : Root + "ships/ship-" + colour;
    }

    public static GameObject Ship(string label, Color seatColour, Vector3 bottom, Quaternion edgeRotation, Transform parent)
    {
        var colour = PlayerComponentAssets.ColourKey(seatColour);
        if (colour == null) return null; // Legal/illegal previews retain their readable tint.
        if (colour == "beige") colour = "ivory";
        return Instantiate(Root + "ships/ship-" + colour, label, bottom, edgeRotation, parent);
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
