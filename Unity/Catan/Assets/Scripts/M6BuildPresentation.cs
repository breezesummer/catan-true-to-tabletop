using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 四种规则共用公开棋子增量；这里只改变显示对象，绝不回写规则或提交命令。
public sealed partial class M6App
{
    TabletopBuildTimeline buildTimeline=new TabletopBuildTimeline();
    readonly List<BuildFlight> buildFlights=new List<BuildFlight>();
    readonly Dictionary<string,BuildGlow> buildGlows=new Dictionary<string,BuildGlow>();
    object buildView;
    Transform buildBoard,buildEffects;
    Camera buildCamera;
    Func<string,Vector3> buildPosition;
    internal int VerificationBuildFlightCount=>buildFlights.Count;
    internal int VerificationBuildVisibleGlowCount => buildGlows.Values.Count(glow=>glow.Object!=null&&glow.RingMaterial!=null&&glow.RingMaterial.shader!=null&&glow.RingMaterial.shader.isSupported&&glow.Object.GetComponent<Renderer>()?.enabled==true);
    internal int VerificationBuildHighlightCount=>buildGlows.Count;
    internal int VerificationBuildHiddenRenderers=>buildFlights.Sum(f=>BuildRoots(f.Piece.LocationId).Sum(r=>r.GetComponentsInChildren<Renderer>().Count(renderer=>!renderer.enabled)));
    internal Vector2 VerificationBuildFlightPosition
    {
        get
        {
            if(buildFlights.Count==0||buildCamera==null)return Vector2.zero;
            var p=buildCamera.WorldToViewportPoint(buildFlights[0].Object.transform.position);
            return new Vector2(p.x*1440,(1-p.y)*900);
        }
    }

    sealed class BuildFlight
    {
        public TabletopBuildPiece Piece;
        public GameObject Object;
        public Vector3 Destination;
        public double Started,Duration;
    }
    sealed class BuildGlow
    {
        public GameObject Object;
        public Mesh RingMesh;
        public Material RingMaterial;
        public readonly List<GlowMaterial> Materials=new List<GlowMaterial>();
    }
    sealed class GlowMaterial
    {
        public Renderer Renderer;
        public Material[] Original,Highlighted;
    }

    void ResetBuildPresentation()
    {
        foreach(var flight in buildFlights)if(flight.Object!=null)Destroy(flight.Object);
        buildFlights.Clear();
        foreach(var glow in buildGlows.Values)ClearBuildGlow(glow);
        buildGlows.Clear();
        if(buildEffects!=null)Destroy(buildEffects.gameObject);
        buildBoard=null;buildEffects=null;buildView=null;buildTimeline=new TabletopBuildTimeline();
    }

    void LateUpdate()
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        if(!Ready||presentation==null||!presentation.enabled||MenuVisible)return;
        object view=null;int turn=0;string active=null;TabletopBuildPiece[] pieces=null;
        Transform board=null;Camera camera=null;TabletopScene table=null;Func<string,Vector3> position=null;
        if(presentation is M2App basic&&basic.M6View!=null)
        {
            var v=basic.M6View;view=v;turn=v.Turn;active=v.ActivePlayerId;
            var b=basic.VerificationBoard;
            if(b!=null){board=b.CommittedPieces;camera=b.BoardCamera;table=b.TableScene;position=b.Position;}
            if(!ReferenceEquals(buildView,view))pieces=PublicPieces(v.Board.Settlements,v.Board.Roads,v.Board.Cities).ToArray();
        }
        else if(presentation is M3App sailing&&sailing.M6View!=null)
        {
            var v=sailing.M6View;view=v;turn=v.Turn;active=v.ActivePlayerId;
            var b=sailing.VerificationBoard;if(b!=null){board=b.CommittedPieces;camera=b.BoardCamera;table=b.TableScene;position=b.Position;}
            if(!ReferenceEquals(buildView,view))pieces=PublicPieces(v.Board.Settlements,v.Board.Roads,v.Board.Cities)
                .Concat(v.Board.Ships.Select(p=>new TabletopBuildPiece{Kind="ship",PlayerId=p.PlayerId,LocationId=p.LocationId})).ToArray();
        }
        else if(presentation is M4App cities&&cities.M6View!=null)
        {
            var v=cities.M6View;view=v;turn=v.Turn;active=v.ActivePlayerId;
            var b=cities.VerificationBoard;if(b!=null){board=b.CommittedPieces;camera=b.BoardCamera;table=b.TableScene;position=b.Position;}
            if(!ReferenceEquals(buildView,view))pieces=ExpandedPieces(PublicPieces(v.Board.Settlements,v.Board.Roads,v.Board.Cities),
                v.Board.Walls.Select(p=>p.LocationId),v.Board.Metropolises.Select(p=>p.LocationId))
                .Concat(v.Board.Knights.Select(p=>new TabletopBuildPiece{Kind="knight",PlayerId=p.PlayerId,LocationId=p.LocationId,Level=p.Level,Active=p.Active})).ToArray();
        }
        else if(presentation is M5App combined&&combined.M6View!=null)
        {
            var v=combined.M6View;view=v;turn=v.Turn;active=v.ActivePlayerId;
            var b=combined.VerificationBoard;if(b!=null){board=b.CommittedPieces;camera=b.BoardCamera;table=b.TableScene;position=b.Position;}
            if(!ReferenceEquals(buildView,view))pieces=ExpandedPieces(PublicPieces(v.Board.Settlements,v.Board.Roads,v.Board.Cities),
                v.Board.Walls.Select(p=>p.LocationId),v.Board.Metropolises.Select(p=>p.LocationId))
                .Concat(v.Board.Ships.Select(p=>new TabletopBuildPiece{Kind="ship",PlayerId=p.PlayerId,LocationId=p.LocationId}))
                .Concat(v.Board.Knights.Select(p=>new TabletopBuildPiece{Kind="knight",PlayerId=p.PlayerId,LocationId=p.LocationId,Level=p.Level,Active=p.Active})).ToArray();
        }
        if(board==null||table==null||camera==null)return;
        if(buildBoard!=board){ResetBuildPresentation();buildBoard=board;buildEffects=new GameObject("公开建造动画与本回合高亮").transform;buildEffects.SetParent(board.parent,false);}
        buildCamera=camera;buildPosition=position;
        if(!ReferenceEquals(buildView,view)&&pieces!=null)
        {
            buildView=view;
            var additions=buildTimeline.Observe(turn,active,pieces);
            foreach(var stale in buildFlights.Where(flight=>!buildTimeline.Contains(flight.Piece)).ToArray())FinishBuildFlight(stale);
            foreach(var piece in additions)
                if(IsAiBuildSeat(piece.PlayerId))StartBuildFlight(piece);
            SynchronizeBuildGlows();
        }
        AnimateBuildFlights();
        AnimateBuildGlows();
    }

    static IEnumerable<TabletopBuildPiece> PublicPieces(PiecePlacement[] settlements,PiecePlacement[] roads,PiecePlacement[] cities)
    {
        return settlements.Select(p=>new TabletopBuildPiece{Kind="settlement",PlayerId=p.PlayerId,LocationId=p.LocationId})
            .Concat(roads.Select(p=>new TabletopBuildPiece{Kind="road",PlayerId=p.PlayerId,LocationId=p.LocationId}))
            .Concat(cities.Select(p=>new TabletopBuildPiece{Kind="city",PlayerId=p.PlayerId,LocationId=p.LocationId}));
    }
    static IEnumerable<TabletopBuildPiece> ExpandedPieces(IEnumerable<TabletopBuildPiece> pieces,IEnumerable<string> wallLocations,IEnumerable<string> metroLocations)
    {
        var walls=new HashSet<string>(wallLocations);var metros=new HashSet<string>(metroLocations);
        foreach(var piece in pieces)
        {
            if(piece.Kind=="city")piece.Kind=metros.Contains(piece.LocationId)?walls.Contains(piece.LocationId)?"metropolis-with-wall":"metropolis":walls.Contains(piece.LocationId)?"city-with-wall":"city";
            yield return piece;
        }
    }
    bool IsAiBuildSeat(string player)=>NetworkGame?Network.Room!=null&&Network.Room.Seats.Any(s=>s.SeatId==player&&s.IsAi):player!=HumanSeat;

    IEnumerable<Transform> BuildRoots(string location)
    {
        if(buildBoard==null)yield break;
        foreach(Transform root in buildBoard)
        {
            if(!root.gameObject.activeSelf)continue;
            string name=root.name;
            if(name==location||name.StartsWith(location+" [",StringComparison.Ordinal)||name.EndsWith(" "+location,StringComparison.Ordinal)||name.Contains(" "+location+" ["))yield return root;
        }
    }
    void StartBuildFlight(TabletopBuildPiece piece)
    {
        // 使用已落盘的同一网格复制展示；原模型临时隐藏，落地时恢复。
        var destination=buildPosition(piece.LocationId);
        var root=new GameObject(piece.PlayerId+" 建造 "+piece.Kind+" "+piece.LocationId);root.transform.SetParent(buildEffects,false);root.transform.position=destination;
        foreach(var model in BuildRoots(piece.LocationId))
        {
            var copy=Instantiate(model.gameObject,root.transform,true);
            foreach(var renderer in copy.GetComponentsInChildren<Renderer>())renderer.enabled=true;
            foreach(var collider in copy.GetComponentsInChildren<Collider>()){collider.enabled=false;Destroy(collider);}
        }
        if(root.transform.childCount==0){Destroy(root);return;}
        buildFlights.Add(new BuildFlight{Piece=piece,Object=root,Destination=destination,Started=Time.unscaledTimeAsDouble,Duration=TabletopAiSpeed.FlightDuration(AiSpeedLevel)});
    }
    void AnimateBuildFlights()
    {
        double now=Time.unscaledTimeAsDouble;
        foreach(var flight in buildFlights.ToArray())
        {
            float t=Mathf.Clamp01((float)((now-flight.Started)/flight.Duration));
            if(flight.Object==null||t>=1)
            {
                FinishBuildFlight(flight);continue;
            }
            foreach(var target in BuildRoots(flight.Piece.LocationId))foreach(var renderer in target.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            // 头像是固定 HUD 坐标；射线反投影使不同缩放与宽高比下仍从头像飞出。
            var avatar=TabletopUI.PlayerAvatarRect(int.Parse(flight.Piece.PlayerId.Substring(1))-1,PlayerCount);
            var ray=buildCamera.ViewportPointToRay(new Vector3(avatar.center.x/1440,1-avatar.center.y/900,0));
            var plane=new Plane(Vector3.up,new Vector3(0,.035f,0));
            var start=plane.Raycast(ray,out float distance)?ray.GetPoint(distance):flight.Destination+Vector3.up*.15f;
            float smooth=t*t*(3-2*t);
            flight.Object.transform.position=Vector3.Lerp(start,flight.Destination,smooth)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.065f);
            flight.Object.transform.localScale=Vector3.one*Mathf.Lerp(1.35f,1,t);
        }
    }
    void FinishBuildFlight(BuildFlight flight)
    {
        foreach(var target in BuildRoots(flight.Piece.LocationId))foreach(var renderer in target.GetComponentsInChildren<Renderer>())renderer.enabled=true;
        if(flight.Object!=null)Destroy(flight.Object);buildFlights.Remove(flight);
    }
    void SynchronizeBuildGlows()
    {
        var highlighted=buildTimeline.Highlighted;
        foreach(var key in buildGlows.Keys.Where(key=>!highlighted.Any(p=>p.Key==key)).ToArray())
        {ClearBuildGlow(buildGlows[key]);buildGlows.Remove(key);}
        foreach(var piece in highlighted)
        {
            if(!buildGlows.TryGetValue(piece.Key,out var glow))
            {
                glow=new BuildGlow{Object=new GameObject("本回合建造高亮 "+piece.LocationId)};glow.Object.transform.SetParent(buildEffects,false);
                glow.Object.transform.position=buildPosition(piece.LocationId)+Vector3.up*.0025f;
                // 独立无光照环使用固定 Resources 引用，避免实体 Lit 关键词、法线或背面剔除令环消失。
                // 仍是棋盘上的水平三维网格，保留实际相机透视、缩放及深度遮挡。
                const float radius=.0235f;
                glow.RingMesh=new Mesh{name="本回合建造金色光环"};
                glow.RingMesh.vertices=new[]{new Vector3(-radius,0,-radius),new Vector3(-radius,0,radius),new Vector3(radius,0,radius),new Vector3(radius,0,-radius)};
                glow.RingMesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
                glow.RingMesh.triangles=new[]{0,1,2,0,2,3};glow.RingMesh.RecalculateBounds();
                glow.Object.AddComponent<MeshFilter>().sharedMesh=glow.RingMesh;
                var ring=glow.Object.AddComponent<MeshRenderer>();
                var shader=Resources.Load<Shader>("TabletopUI/v012/BuildHighlight");
                if(shader!=null){glow.RingMaterial=new Material(shader);ring.sharedMaterial=glow.RingMaterial;}
                else{ring.enabled=false;Debug.LogWarning("CATAN_BUILD_HIGHLIGHT_SHADER_MISSING: TabletopUI/v012/BuildHighlight");}
                ring.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;ring.receiveShadows=false;
                buildGlows.Add(piece.Key,glow);
            }
            // 棋盘每次刷新会重建网格。只为当前实例复制材质，不改动共用资产或其他玩家的棋子。
            foreach(var root in BuildRoots(piece.LocationId))foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if(glow.Materials.Any(m=>m.Renderer==renderer))continue;
                var originals=renderer.sharedMaterials;var lit=originals.Select(m=>m==null?null:new Material(m)).ToArray();
                foreach(var material in lit)if(material!=null&&material.HasProperty("_EmissionColor")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.38f,.19f,.025f));}
                renderer.sharedMaterials=lit;glow.Materials.Add(new GlowMaterial{Renderer=renderer,Original=originals,Highlighted=lit});
            }
        }
    }
    void AnimateBuildGlows()
    {
        foreach(var glow in buildGlows.Values)
        {
            float pulse=.86f+.14f*Mathf.Sin(Time.unscaledTime*3.1f);glow.Object.transform.localScale=Vector3.one*pulse;
            foreach(var paint in glow.Materials.ToArray())
            {
                if(paint.Renderer==null){foreach(var material in paint.Highlighted)if(material!=null)Destroy(material);glow.Materials.Remove(paint);continue;}
                foreach(var material in paint.Highlighted)if(material!=null&&material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",new Color(.38f,.19f,.025f)*pulse);
            }
        }
    }
    void ClearBuildGlow(BuildGlow glow)
    {
        foreach(var paint in glow.Materials)
        {
            if(paint.Renderer!=null)paint.Renderer.sharedMaterials=paint.Original;
            foreach(var material in paint.Highlighted)if(material!=null)Destroy(material);
        }
        if(glow.RingMaterial!=null)Destroy(glow.RingMaterial);
        if(glow.RingMesh!=null)Destroy(glow.RingMesh);
        if(glow.Object!=null)Destroy(glow.Object);
    }
}
