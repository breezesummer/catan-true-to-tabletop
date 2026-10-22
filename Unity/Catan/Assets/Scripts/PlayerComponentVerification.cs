using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catan.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Opt-in art acceptance. Synthetic public projections never create or inspect a GameState.
public sealed class PlayerComponentVerification : MonoBehaviour
{
    [Serializable] public class Record
    {
        public string renderer,playerId,locationId,kind,resource;
        public int level,track=-1,meshCount;
        public bool active,wall,uniqueRoot,metreScale,noColliders,sourceMeshesMatched,explicitStateMarker,explicitLevelMarker;
    }
    [Serializable] public class Report
    {
        public string status,unityVersion,projectionMethod="RefreshProjectedPieces shared by live PlayerView";
        public string helmetConvention="provisional: active = helmet; independent active/resting marker retained";
        public int checkedInstances;
        public bool previewColoursPreserved;
        public Record[] instances;
        public string[] capturedViews;
    }
    sealed class Fixture
    {
        public readonly Vertex[] Vertices;
        public readonly PiecePlacement[] Settlements,Cities,Walls;
        public readonly Catan.Core.M4.Knight[] Knights;
        public readonly Catan.Core.M4.Metropolis[] Metropolises;
        public Fixture()
        {
            var vertices=new List<Vertex>();var settlements=new List<PiecePlacement>();
            var cities=new List<PiecePlacement>();var walls=new List<PiecePlacement>();
            var knights=new List<Catan.Core.M4.Knight>();var metros=new List<Catan.Core.M4.Metropolis>();
            for(var row=0;row<11;row++)
                for(var seat=0;seat<4;seat++)
                {
                    var id="V-art-"+row+"-"+seat;var player="P"+(seat+1);
                    vertices.Add(new Vertex{Id=id,X=seat*2-3,Y=row*2-10});
                    var piece=new PiecePlacement{PlayerId=player,LocationId=id};
                    if(row==0)settlements.Add(piece);
                    else if(row<5)
                    {
                        cities.Add(piece);
                        if(row==2||row==4)walls.Add(piece);
                        if(row>=3)metros.Add(new Catan.Core.M4.Metropolis{PlayerId=player,LocationId=id,
                            Track=(Catan.Core.M4.ImprovementTrack)((seat+row)%3)});
                    }
                    else knights.Add(new Catan.Core.M4.Knight{PlayerId=player,LocationId=id,Level=(row-5)/2+1,Active=row%2==0});
                }
            Vertices=vertices.ToArray();Settlements=settlements.ToArray();Cities=cities.ToArray();
            Walls=walls.ToArray();Knights=knights.ToArray();Metropolises=metros.ToArray();
        }
    }
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();var flag=Array.IndexOf(args,"--player-components-verify");
        if(flag<0)yield break;
        if(flag+1>=args.Length){Fail("Missing evidence directory");yield break;}
        var output=args[flag+1];Directory.CreateDirectory(output);
        // Hide the art gallery while its camera is used for the real renderer fixtures.
        var gallery=GetComponent<EntityAssetGallery>();
        if(gallery!=null){gallery.enabled=false;if(gallery.Current!=null)gallery.Current.SetActive(false);}
        var records=new List<Record>();var captured=new List<string>();var fixture=new Fixture();
        foreach(var mode in new[]{"base","seafarers","cities-knights","combined"})
        {
            var root=new GameObject("Public art fixture "+mode);
            try
            {
                if(mode=="base")
                {
                    var board=root.AddComponent<BoardRenderer>();board.Initialize(new Tile[0],fixture.Vertices,new Edge[0]);
                    board.Refresh(fixture.Settlements,new PiecePlacement[0],fixture.Cities.Where(c=>c.LocationId.StartsWith("V-art-1-")).ToArray(),null);
                }
                else if(mode=="seafarers")
                {
                    var board=root.AddComponent<M3BoardRenderer>();board.Initialize(new Tile[0],fixture.Vertices,new Edge[0]);
                    board.Refresh(fixture.Settlements,new PiecePlacement[0],fixture.Cities.Where(c=>c.LocationId.StartsWith("V-art-1-")).ToArray(),null);
                }
                else if(mode=="cities-knights")
                {
                    var board=root.AddComponent<M4BoardRenderer>();board.Initialize(new Tile[0],fixture.Vertices,new Edge[0]);
                    board.RefreshProjectedPieces(fixture.Settlements,new PiecePlacement[0],fixture.Cities,null,
                        fixture.Walls,fixture.Knights,fixture.Metropolises,null);
                }
                else
                {
                    var board=root.AddComponent<M5BoardRenderer>();board.Initialize(new Tile[0],fixture.Vertices,new Edge[0]);
                    board.RefreshProjectedPieces(fixture.Settlements,new PiecePlacement[0],fixture.Cities,null,
                        fixture.Walls,fixture.Knights.Select(k=>new Catan.Core.M5.Knight{PlayerId=k.PlayerId,LocationId=k.LocationId,Level=k.Level,Active=k.Active}).ToArray(),
                        fixture.Metropolises.Select(m=>new Catan.Core.M5.Metropolis{PlayerId=m.PlayerId,LocationId=m.LocationId,Track=(Catan.Core.M5.ImprovementTrack)(int)m.Track}).ToArray(),null);
                }
            }
            catch(Exception error){Destroy(root);Fail(error.ToString());yield break;}
            yield return null;yield return null;
            try
            {
                var pieces=root.transform.Find("Committed pieces");
                Require(pieces!=null,"No committed visual roots "+mode);
                foreach(var vertex in fixture.Vertices)
                {
                    var fields=vertex.Id.Split('-');var row=int.Parse(fields[2]);var seat=int.Parse(fields[3]);
                    if((mode=="base"||mode=="seafarers")&&row>1)continue;
                    var player="P"+(seat+1);var colour=new[]{"red","blue","orange","beige"}[seat];
                    var candidates=pieces.Cast<Transform>().Where(t=>t.name.Contains(vertex.Id)).ToArray();
                    Require(candidates.Length==1,"Duplicate or absent piece at "+mode+" "+vertex.Id+" count="+candidates.Length);
                    var piece=candidates[0];var kind=row==0?"village":row==1?"city":row==2?"city-with-wall":row==3?"metropolis":row==4?"metropolis-with-wall":"knight";
                    var level=row>=5?(row-5)/2+1:0;var active=row>=5&&row%2==0;
                    // Expected resources are written independently of the asset selector.
                    var resources=new List<string>();
                    if(seat==0)
                    {
                        if(row==0)resources.Add("PhotoSamples/v001/village");
                        else if(row<3)resources.Add("EntityAssets/v001/city/city-red");
                        else if(row<5)resources.Add("EntityAssets/v001/metropolis/metropolis-red");
                        else resources.Add("EntityAssets/v001/knights/knight-level-"+level+(active?"-active":"-resting"));
                        if(row==2||row==4)resources.Add("EntityAssets/v001/city-wall/city-wall-red");
                    }
                    else if(row<5)resources.Add("EntityAssets/v002/player-buildings/"+kind+"-"+colour);
                    else resources.Add("EntityAssets/v002/player-knights/knight-"+colour+"-level-"+level+(active?"-helmet":"-bare"));
                    var actualMeshes=piece.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).ToArray();
                    foreach(var resource in resources)
                    {
                        var source=Resources.Load<GameObject>(resource);Require(source!=null,"Missing expected source "+resource);
                        foreach(var mesh in source.GetComponentsInChildren<MeshFilter>())
                            Require(actualMeshes.Contains(mesh.sharedMesh),"Wrong source mesh "+mode+" "+vertex.Id+" expected "+resource);
                        foreach(var material in source.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials))
                            Require(piece.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Contains(material)),"Source material lost "+resource);
                    }
                    Require(piece.localScale==Vector3.one,"Non-metre visual root "+vertex.Id);
                    Require(Quaternion.Angle(piece.rotation,Quaternion.Euler(0,180,0))<.01f,"Photographed face points away from default board camera "+vertex.Id);
                    Require(piece.GetComponentsInChildren<Collider>().Length==0,"Visual collider "+vertex.Id);
                    var marker=true;var track=-1;
                    if(row>=5)
                    {
                        marker=piece.Find(active?"Active state: active":"Active state: resting")!=null;
                        Require(marker,"Missing independent active marker "+vertex.Id);
                        var rank=piece.Find("Knight level: "+level);
                        Require(rank!=null&&rank.childCount==level,"Missing independent level pips "+vertex.Id);
                    }
                    if(row==3||row==4)
                    {
                        track=(seat+row)%3;var label=new[]{"Trade","Politics","Science"}[track];
                        var visual=piece.Find("Metropolis track: "+label);Require(visual!=null,"Missing track label "+vertex.Id);
                        Require(visual.childCount==track+1,"Wrong track pips "+vertex.Id);
                    }
                    records.Add(new Record{renderer=mode,playerId=player,locationId=vertex.Id,kind=kind,resource=string.Join(" + ",resources),
                        level=level,active=active,wall=row==2||row==4,track=track,meshCount=actualMeshes.Length,uniqueRoot=true,metreScale=true,
                        noColliders=true,sourceMeshesMatched=true,explicitStateMarker=marker,explicitLevelMarker=row>=5});
                }
                Require(pieces.childCount==((mode=="base"||mode=="seafarers")?8:44),"Unexpected visual root count "+mode);
                Require(PlayerComponentAssets.ColourKey(new Color(.3f,1f,.75f))==null&&PlayerComponentAssets.ColourKey(new Color(1f,.22f,.2f))==null,"Preview became player colour");
                // The original preview code must still apply its green/red diagnostic colour.
                if(mode=="base")root.GetComponent<BoardRenderer>().Preview(fixture.Vertices[0].Id,false,true);
                else if(mode=="seafarers")root.GetComponent<M3BoardRenderer>().Preview(fixture.Vertices[0].Id,false,true);
                else if(mode=="cities-knights")root.GetComponent<M4BoardRenderer>().Preview(fixture.Vertices[0].Id,false,true);
                else root.GetComponent<M5BoardRenderer>().Preview(fixture.Vertices[0].Id,false,true);
                var ghost=root.transform.Cast<Transform>().Last(t=>t.name.Contains(fixture.Vertices[0].Id));
                var property=new MaterialPropertyBlock();ghost.GetComponentInChildren<Renderer>().GetPropertyBlock(property);
                Require(property.GetColor("_BaseColor")==new Color(.3f,1f,.75f),"Legal preview colour changed "+mode);
                if(mode=="base")root.GetComponent<BoardRenderer>().ClearPreview();
                else if(mode=="seafarers")root.GetComponent<M3BoardRenderer>().ClearPreview();
                else if(mode=="cities-knights")root.GetComponent<M4BoardRenderer>().ClearPreview();
                else root.GetComponent<M5BoardRenderer>().ClearPreview();
            }
            catch(Exception error){Destroy(root);Fail(error.ToString());yield break;}
            yield return null;
            if(mode=="cities-knights"||mode=="combined")
            {
                var folder=Path.Combine(output,mode);Directory.CreateDirectory(folder);
                foreach(var view in new[]{"game-camera","top","side","crowded-vertex"})
                {
                    var camera=Camera.main;camera.orthographic=true;camera.rect=new Rect(0,0,1,1);
                    var centre=view=="crowded-vertex"?root.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("Metropolis")&&t.name.Contains("V-art-4-2")).position:Vector3.zero;
                    var direction=view=="top"?new Vector3(0,1,-.0001f):view=="side"?new Vector3(0,.06f,-1):new Vector3(0,.95f,-1);
                    camera.transform.position=centre+direction.normalized*.9f;camera.transform.LookAt(centre+Vector3.up*.012f);
                    camera.orthographicSize=view=="crowded-vertex"?.068f:.29f;
                    yield return null;yield return new WaitForEndOfFrame();
                    var path=Path.Combine(folder,"unity-"+view+".png");
                    try{Capture(path);captured.Add(mode+"/"+view);}
                    catch(Exception error){Destroy(root);Fail(error.ToString());yield break;}
                }
            }
            root.SetActive(false);Destroy(root);yield return null;yield return null;
        }
        File.WriteAllText(Path.Combine(output,"player-components-verification.json"),JsonUtility.ToJson(new Report{
            status="passed",unityVersion=Application.unityVersion,checkedInstances=records.Count,previewColoursPreserved=true,
            instances=records.ToArray(),capturedViews=captured.ToArray()},true));
        Debug.Log("CATAN_PLAYER_COMPONENTS_OK instances="+records.Count+" captures="+captured.Count);
        Application.Quit(0);
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void Capture(string path)
    {
        var camera=Camera.main;var previous=RenderTexture.active;
        var target=new RenderTexture(1200,1000,24);target.Create();var image=new Texture2D(1200,1000,TextureFormat.RGB24,false);
        try
        {
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            Require(RenderPipeline.SupportsRenderRequest(camera,request),"URP capture unsupported");
            RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1200,1000),0,0);image.Apply();
            var pixels=image.GetPixels32();var background=pixels[0];
            Require(pixels.Count(p=>Mathf.Abs(p.r-background.r)+Mathf.Abs(p.g-background.g)+Mathf.Abs(p.b-background.b)>15)>500,"Empty camera capture "+path);
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;target.Release();Destroy(target);Destroy(image);}
    }
    static void Fail(string message){Debug.LogError("CATAN_PLAYER_COMPONENTS_FAILED: "+message);Application.Quit(1);}
}
