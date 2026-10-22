using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Public presentation data only. Asset colour is selected by seat identity, never by hidden state.
public static class PlayerComponentAssets
{
    const string Root = "EntityAssets/v002/";
    static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Material> Plastics = new Dictionary<string, Material>();

    // 每种颜色以现有村庄材质为准；只替换塑料，保留头盔、窗洞等独立细节。
    public static Material BuildingPlastic(string playerId)
    {
        if(ColourKey(playerId)==null)return null;
        if(!Plastics.TryGetValue(playerId,out var material))
        {
            var model=Model(BuildingResource("village",playerId));
            material=model?.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).FirstOrDefault(m=>m!=null&&m.name.Contains("Plastic"));
            if(material!=null)Plastics[playerId]=material;
        }
        return material;
    }
    public static void MatchBuildingPlastic(GameObject instance,string playerId)
    {
        var plastic=BuildingPlastic(playerId);if(instance==null||plastic==null)return;
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            renderer.SetPropertyBlock(null);
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m!=null&&m.name.Contains("Plastic")?plastic:m).ToArray();
        }
    }

    public static string ColourKey(string playerId)
    {
        switch (playerId)
        {
            case "P1": return "red";
            case "P2": return "blue";
            case "P3": return "orange";
            case "P4": return "beige";
            default: return null;
        }
    }

    // Preview colours are deliberately not accepted as a player identity.
    public static string ColourKey(Color colour)
    {
        for (var i = 0; i < BoardRenderer.SeatColors.Length; i++)
            if (colour == BoardRenderer.SeatColors[i]) return ColourKey("P" + (i + 1));
        return null;
    }

    public static GameObject Model(string resource)
    {
        if (string.IsNullOrEmpty(resource)) return null;
        if (!Models.TryGetValue(resource, out var model) || model == null)
        {
            model = Resources.Load<GameObject>(resource);
            if (model != null) Models[resource] = model;
        }
        return model;
    }

    public static string BuildingResource(string type, string playerId)
    {
        var colour = ColourKey(playerId);
        if (colour == null) return null;
        if (colour != "red") return Root + "player-buildings/" + type + "-" + colour;
        switch (type)
        {
            case "village": return "PhotoSamples/v001/village";
            case "city": return "EntityAssets/v001/city/city-red";
            case "city-wall": return "EntityAssets/v001/city-wall/city-wall-red";
            case "metropolis": return "EntityAssets/v001/metropolis/metropolis-red";
            default: return null;
        }
    }

    public static string KnightResource(string playerId, int level, bool active)
    {
        var colour = ColourKey(playerId);
        if (colour == null || level < 1 || level > 3) return null;
        if (colour == "red")
            return "EntityAssets/v001/knights/knight-level-" + level + (active ? "-active" : "-resting");
        // Helmet = active is a provisional art convention, not a change to any rule.
        return Root + "player-knights/knight-" + colour + "-level-" + level + (active ? "-helmet" : "-bare");
    }

    public static GameObject Building(string type, string playerId, string label, Vector3 bottom, Transform parent)
    {
        var resource = BuildingResource(type, playerId);
        var model = Model(resource);
        if (model != null) {var instance=Instantiate(model,label,bottom,parent);MatchBuildingPlastic(instance,playerId);return instance;}
        if (ColourKey(playerId) != "red" || (type != "city-with-wall" && type != "metropolis-with-wall")) return null;

        // v001 red uses the existing separate source models under a single neutral root.
        var insert = Model(BuildingResource(type == "city-with-wall" ? "city" : "metropolis", playerId));
        var wall = Model(BuildingResource("city-wall", playerId));
        if (insert == null || wall == null) return null;
        var root = new GameObject(label + " [" + type + "-red]");
        root.transform.SetParent(parent, false); root.transform.position = bottom;
        root.transform.rotation = Quaternion.Euler(0, 180, 0);
        Object.Instantiate(wall, root.transform, false);
        Object.Instantiate(insert, root.transform, false);
        MatchBuildingPlastic(root,playerId);
        return root;
    }

    public static GameObject Knight(string playerId, int level, bool active, string label, Vector3 bottom, Transform parent)
    {
        var model = Model(KnightResource(playerId, level, active));
        if(model==null)return null;
        var instance=Instantiate(model,label,bottom,parent);MatchBuildingPlastic(instance,playerId);return instance;
    }

    static GameObject Instantiate(GameObject model, string label, Vector3 bottom, Transform parent)
    {
        // Match the inspected gallery orientation: the sculpt's photographed face looks toward the default board camera.
        var instance = Object.Instantiate(model, bottom, Quaternion.Euler(0, 180, 0), parent);
        instance.name = label + " [" + model.name + "]";
        // Prefabs are metre-scale and have bottom-centre pivots. Preserve their materials and scale.
        return instance;
    }
}
