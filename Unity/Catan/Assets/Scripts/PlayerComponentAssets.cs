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
            // 其他棋子继续使用原塑料色；三国村庄有独立共享材质和归属色带。
            var key=ColourKey(playerId);
            var model=Model(key=="red"?"PhotoSamples/v001/village":Root+"player-buildings/village-"+key);
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

    public const string ExpansionRoot="ExpansionPieces/v001/";
    static readonly string[] MetropolisKinds={"metro-trade","metro-politics","metro-science"};

    public static string BuildingResource(string type,string playerId,int track=0)
    {
        var colour=ColourKey(playerId);
        if(colour==null)return null;
        if(type=="city"||type=="village")return "NaturalPieces/v001/"+type+"-"+colour;
        if(type=="city-wall")return ExpansionRoot+"wall-"+colour;
        if(type=="metropolis" && track>=0 && track<MetropolisKinds.Length)
            return ExpansionRoot+MetropolisKinds[track]+"-"+colour;
        return null;
    }

    public static string KnightResource(string playerId,int level,bool active)
    {
        var colour=ColourKey(playerId);
        if(colour==null||level<1||level>3)return null;
        return ExpansionRoot+"knight-"+level+(active?"-active-":"-rest-")+colour;
    }

    public static GameObject Building(string type,string playerId,string label,Vector3 bottom,Transform parent,int track=0)
    {
        bool walled=type=="city-with-wall"||type=="metropolis-with-wall";
        string insertType=walled?type.Replace("-with-wall",""):type;
        var insert=Model(BuildingResource(insertType,playerId,track));
        if(insert==null)return null;
        if(!walled)return Instantiate(insert,label,bottom,parent);
        var wall=Model(BuildingResource("city-wall",playerId));
        if(wall==null)return null;
        var root=new GameObject(label+" ["+type+"-"+ColourKey(playerId)+"]");
        root.transform.SetParent(parent,false);
        root.transform.SetPositionAndRotation(bottom,Quaternion.Euler(0,180,0));
        var enclosure=Object.Instantiate(wall,root.transform,false);
        if(insertType=="metropolis")enclosure.transform.localScale*=1.14f;
        Object.Instantiate(insert,root.transform,false);
        return root;
    }

    public static string RoadResource(string playerId)
    {
        var colour=ColourKey(playerId);
        return colour==null?null:"NaturalPieces/v001/road-"+colour;
    }

    public static GameObject Road(string playerId,string label,Vector3 bottom,Quaternion rotation,Transform parent)
    {
        var model=Model(RoadResource(playerId));if(model==null)return null;
        var instance=Object.Instantiate(model,bottom,rotation,parent);instance.name=label;return instance;
    }

    public static GameObject Knight(string playerId, int level, bool active, string label, Vector3 bottom, Transform parent)
    {
        var model = Model(KnightResource(playerId, level, active));
        if(model==null)return null;
        return Instantiate(model,label,bottom,parent);
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
