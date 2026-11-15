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
        if(!walled)
        {
            var piece=Instantiate(insert,label,bottom,parent);
            if(type=="metropolis")return FantasyBoardPiece.Attach(piece,MetropolisKinds[track],ColourKey(playerId));
            return type=="village" || type=="city"
                ? FantasyBoardPiece.Attach(piece,type,ColourKey(playerId)) : piece;
        }
        var wall=Model(BuildingResource("city-wall",playerId));
        if(wall==null)return null;
        var root=new GameObject(label+" ["+type+"-"+ColourKey(playerId)+"]");
        root.transform.SetParent(parent,false);
        root.transform.SetPositionAndRotation(bottom,Quaternion.Euler(0,180,0));
        var enclosure=Object.Instantiate(wall,root.transform,false);
        if(insertType=="metropolis")enclosure.transform.localScale*=1.14f;
        var innerBuilding=Object.Instantiate(insert,root.transform,false);
        // 城内建筑落在夯土地表；外墙原有的门洞、街道朝向和棋盘支点保持配准。
        innerBuilding.transform.localPosition+=Vector3.up*(.00077f*enclosure.transform.localScale.y);
        var ink=FantasyBoardPiece.Attach(innerBuilding,insertType=="metropolis"?MetropolisKinds[track]:"city",ColourKey(playerId));
        ink.GetComponent<FantasyBoardPiece>()?.Align(root.transform.forward);
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
        var instance=Object.Instantiate(model,bottom,rotation,parent);instance.name=label;
        return FantasyBoardPiece.Attach(instance,"road",ColourKey(playerId));
    }

    public static GameObject Knight(string playerId, int level, bool active, string label, Vector3 bottom, Transform parent)
    {
        var model = Model(KnightResource(playerId, level, active));
        if(model==null)return null;
        var instance = Instantiate(model,label,bottom,parent);
        ApplyKnightFinish(instance,playerId,level,active);
        return FantasyBoardPiece.Attach(instance,"knights",ColourKey(playerId),
            level-1+(active?3:0));
    }

    static readonly Dictionary<(Material,string),Material> helmetFinishes = new();
    static Material updraftMaterial;

    static void ApplyKnightFinish(GameObject instance,string playerId,int level,bool active)
    {
        var renderer=instance.GetComponentInChildren<MeshRenderer>();
        var parts=renderer.sharedMaterials;
        for(int i=0;i<parts.Length;i++)
        {
            var metal=parts[i];
            if(metal==null || !metal.name.StartsWith("Expansion_Metal_knight-"))continue;
            if(!helmetFinishes.TryGetValue((metal,playerId),out var finish))
            {
                finish=new Material(Resources.Load<Shader>(ExpansionRoot+"KnightHelmet"));
                finish.SetTexture("_BaseMap",metal.GetTexture("_BaseMap"));
                finish.SetColor("_Pigment",BoardRenderer.SeatColor(playerId));
                // 当前雕塑的头部原点在下颌，二级出击前倾，骑兵从马背起算。
                float scale = level == 3 ? .0106f : .0108f;
                float hip = level == 3 ? 1.84f : level == 2 && active ? .844f : .931f;
                float chin = hip + (level == 2 && active ? .445f : .46f);
                finish.SetVector("_Helmet", new Vector4((chin + .14f) * scale,
                    (chin + .43f) * scale, (level == 3 ? .49f : .38f) * scale, 0));
                helmetFinishes.Add((metal,playerId),finish);
            }
            parts[i]=finish;
        }
        renderer.sharedMaterials=parts;
        if(!active)return;
        AddKnightUpdraft(instance,BoardRenderer.SeatColor(playerId),level);
    }

    static void AddKnightUpdraft(GameObject knight, Color colour, int level)
    {
        float height = level == 3 ? .026f : .019f;
        var wind = new GameObject("骑士激活气流");
        wind.transform.SetParent(knight.transform, false);
        wind.transform.localPosition = Vector3.up * .0005f;
        var particles = wind.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.duration = 1.6f;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.32f, 1.42f);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(height * .027f, height * .052f);
        main.startColor = colour;
        main.maxParticles = 96;
        var emission = particles.emission;
        emission.rateOverTime = 65;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90, 0, 0);
        shape.radius = height * .245f;
        shape.radiusThickness = .23f;

        var flow = particles.velocityOverLifetime;
        flow.enabled = true;
        flow.space = ParticleSystemSimulationSpace.Local;
        var steady = AnimationCurve.Linear(0, 1, 1, 1);
        var zero = new ParticleSystem.MinMaxCurve(0, steady);
        flow.x = zero;
        flow.z = zero;
        flow.orbitalX = zero;
        flow.orbitalZ = zero;
        flow.y = new ParticleSystem.MinMaxCurve(height * 1.06f, steady);
        flow.orbitalY = new ParticleSystem.MinMaxCurve(2.4f, steady);
        flow.radial = new ParticleSystem.MinMaxCurve(height, new AnimationCurve(new Keyframe(0, 0), new Keyframe(.73f, 0), new Keyframe(1, .53f)));
        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.95f, .13f),
                new GradientAlphaKey(1, .80f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;

        var renderer = wind.GetComponent<ParticleSystemRenderer>();
        if (updraftMaterial == null)
            updraftMaterial = new Material(Resources.Load<Shader>(ExpansionRoot + "KnightUpdraft"));
        renderer.sharedMaterial = updraftMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 5.2f;
        renderer.velocityScale = .06f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Play();
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
