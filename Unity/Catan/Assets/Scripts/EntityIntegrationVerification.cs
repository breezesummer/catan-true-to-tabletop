using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catan.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Opt-in acceptance of real rendering entrances using synthetic PUBLIC arrays only.
public sealed class EntityIntegrationVerification : MonoBehaviour
{
    [Serializable] public class Report
    {
        public string status,unityVersion;
        public int checkedSourceInstances,checkedPoliticalCards;
        public string[] views;
        public bool fogHasNoPhotoTexture,visualsHaveNoColliders,metreScale,previewColours,oreUsesFlatCardArt;
    }
    int checkedInstances;
    readonly List<string> views=new List<string>();
    readonly string[] resources={"wood","wool","wheat","brick","desert","sea","gold","ore","fog"};
    readonly string[] expected={"PhotoSamples/v001/forest","EntityAssets/v001/tiles/tile-pasture",
        "EntityAssets/v001/tiles/tile-fields","EntityAssets/v001/tiles/tile-hills","EntityAssets/v001/tiles/tile-desert",
        "EntityAssets/v001/tiles/tile-sea","EntityAssets/v001/tiles/tile-gold","EntityAssets/v004/ore-tile/tile-ore",null};
    Tile[] tiles;Vertex[] vertices;Edge[] edges;

    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();var flag=Array.IndexOf(args,"--entity-integration-verify");
        if(flag<0)yield break;
        var run=Verify(args[flag+1]);
        while(true)
        {
            object next;
            try{if(!run.MoveNext())break;next=run.Current;}
            catch(Exception error){Debug.LogError("CATAN_ENTITY_INTEGRATION_FAILED: "+error);Application.Quit(1);yield break;}
            yield return next;
        }
        Debug.Log("CATAN_ENTITY_INTEGRATION_OK instances="+checkedInstances);
        Application.Quit(0);
    }
    void Fixture()
    {
        var allTiles=new List<Tile>();var allVertices=new List<Vertex>();var allEdges=new List<Edge>();
        var dx=new[]{1,0,-1,-1,0,1};var dy=new[]{1,2,1,-1,-2,-1};
        for(var i=0;i<resources.Length;i++)
        {
            var ids=new string[6];
            for(var j=0;j<6;j++)
            {
                ids[j]="V-photo-"+i+"-"+j;
                allVertices.Add(new Vertex{Id=ids[j],X=(i%3-1)*3+dx[j],Y=(i/3-1)*6+dy[j]});
            }
            allTiles.Add(new Tile{Id="T-photo-"+i,Resource=resources[i],Vertices=ids});
            if(i<4)allEdges.Add(new Edge{Id="E-photo-"+i,Vertices=new[]{ids[2],ids[3]}});
        }
        tiles=allTiles.ToArray();vertices=allVertices.ToArray();edges=allEdges.ToArray();
    }
    IEnumerator Verify(string output)
    {
        Directory.CreateDirectory(output);Fixture();
        GetComponent<M6App>().enabled=false;
        foreach(var mode in new[]{"base","seafarers","cities-knights","combined"})
        {
            var root=new GameObject("Public photo fixture "+mode);
            var roads=edges.Select((e,i)=>new PiecePlacement{LocationId=e.Id,PlayerId="P"+(i+1)}).ToArray();
            var settlements=new[]{new PiecePlacement{LocationId=vertices[0].Id,PlayerId="P1"}};
            if(mode=="base")
            {
                var board=root.AddComponent<BoardRenderer>();board.Initialize(tiles,vertices,edges);
                board.Refresh(settlements,roads,new PiecePlacement[0],tiles[4].Id);
            }
            else if(mode=="seafarers")
            {
                var board=root.AddComponent<M3BoardRenderer>();board.Initialize(tiles,vertices,edges);
                board.Refresh(settlements,new PiecePlacement[0],new PiecePlacement[0],tiles[4].Id);
                board.RefreshSeafarers(edges.Select((e,i)=>new Catan.Core.M3.ShipPlacement{
                    LocationId=e.Id,PlayerId="P"+(i+1),IsWarship=i==0}).ToArray(),tiles[5].Id);
            }
            else if(mode=="cities-knights")
            {
                var board=root.AddComponent<M4BoardRenderer>();board.Initialize(tiles,vertices,edges);
                board.RefreshProjectedPieces(settlements,roads,new PiecePlacement[0],tiles[4].Id,
                    new PiecePlacement[0],new Catan.Core.M4.Knight[0],new Catan.Core.M4.Metropolis[0],tiles[2].Id,3);
            }
            else
            {
                var board=root.AddComponent<M5BoardRenderer>();board.Initialize(tiles,vertices,edges);
                board.RefreshProjectedPieces(settlements,new PiecePlacement[0],new PiecePlacement[0],tiles[4].Id,
                    new PiecePlacement[0],new Catan.Core.M5.Knight[0],new Catan.Core.M5.Metropolis[0],tiles[2].Id,barbarianPosition:3);
                board.RefreshSeafarers(edges.Select((e,i)=>new Catan.Core.M5.ShipPlacement{
                    LocationId=e.Id,PlayerId="P"+(i+1)}).ToArray(),tiles[5].Id);
            }
            yield return null;yield return null;
            for(var i=0;i<tiles.Length;i++)
            {
                var tile=root.transform.Find(tiles[i].Id);Require(tile!=null,"Missing tile "+mode+" "+tiles[i].Id);
                if(expected[i]!=null)Source(tile,expected[i]);
                else Require(tile.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m.mainTexture==null)),"Fog leaked terrain photo");
            }
            var pieces=root.transform.Find("Committed pieces");
            var ore=root.transform.Find(tiles[7].Id);
            var oreBounds=ore.GetComponentsInChildren<Renderer>()[0].bounds;
            foreach(var renderer in ore.GetComponentsInChildren<Renderer>())oreBounds.Encapsulate(renderer.bounds);
            Require(Mathf.Abs(oreBounds.size.y-.008f)<.000015f,"Ore still has a raised mountain silhouette");
            Require(ore.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>m.mainTexture!=null)),"Ore has no card artwork");
            if(mode=="base")root.GetComponent<BoardRenderer>().ShowTerrain(false);
            else if(mode=="seafarers")root.GetComponent<M3BoardRenderer>().ShowTerrain(false);
            else if(mode=="cities-knights")root.GetComponent<M4BoardRenderer>().ShowTerrain(false);
            else root.GetComponent<M5BoardRenderer>().ShowTerrain(false);
            Require(ore.gameObject.activeInHierarchy,"Flat ore board surface was hidden as a mountain decoration");
            Source(pieces.Cast<Transform>().Single(t=>t.name.StartsWith("Robber")),"EntityAssets/v001/robber/robber-black-group");
            if(mode=="seafarers"||mode=="combined")
            {
                var colour=new[]{"red","blue","orange","ivory"};
                for(var i=0;i<edges.Length;i++)
                    Source(pieces.Cast<Transform>().Single(t=>t.name.StartsWith("Ship "+edges[i].Id)),"EntityAssets/v001/ships/ship-"+colour[i]);
                Source(pieces.Cast<Transform>().Single(t=>t.name.StartsWith("Pirate")),"EntityAssets/v001/pirate/pirate-black");
            }
            if(mode=="cities-knights"||mode=="combined")
            {
                Source(pieces.Cast<Transform>().Single(t=>t.name.StartsWith("Merchant")),"EntityAssets/v001/merchant/merchant-neutral-cart");
                var track=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Barbarian track");
                Source(track,"EntityAssets/v001/barbarian-track/barbarian-track");
                Source(root.GetComponentsInChildren<Transform>().Single(t=>t.name.StartsWith("Barbarian ship")),"EntityAssets/v001/barbarian-ship/barbarian-ship");
                var ship=root.GetComponentsInChildren<Transform>().Single(t=>t.name.StartsWith("Barbarian ship"));var previous=ship.position;
                if(mode=="cities-knights")root.GetComponent<M4BoardRenderer>().RefreshBarbarians(6);
                else root.GetComponent<M5BoardRenderer>().RefreshBarbarians(6);
                yield return null;yield return null;
                var moved=root.GetComponentsInChildren<Transform>().Single(t=>t.name.StartsWith("Barbarian ship"));
                Require((moved.position-previous).magnitude>.005f,"Public barbarian position did not move sculpture");
            }
            if(mode=="base"||mode=="cities-knights")
            {
                foreach(var road in roads)
                    Source(pieces.Cast<Transform>().Single(t=>t.name==road.LocationId),"PhotoSamples/v001/road");
            }
            var folder=Path.Combine(output,mode);Directory.CreateDirectory(folder);
            foreach(var top in new[]{false,true})
            {
                if(mode=="base")root.GetComponent<BoardRenderer>().SetCamera(top);
                else if(mode=="seafarers")root.GetComponent<M3BoardRenderer>().SetCamera(top);
                else if(mode=="cities-knights")root.GetComponent<M4BoardRenderer>().SetCamera(top);
                else root.GetComponent<M5BoardRenderer>().SetCamera(top);
                yield return null;yield return new WaitForEndOfFrame();
                var name=top?"top":"game-camera";Capture(Path.Combine(folder,name+".png"));views.Add(mode+"/"+name);
            }
            foreach(var kind in new[]{"settlement","robber","ship"})
            {
                if(kind=="ship"&&mode!="seafarers"&&mode!="combined")continue;
                foreach(var legal in new[]{true,false})
                {
                    if(mode=="base")
                    {
                        var board=root.GetComponent<BoardRenderer>();board.ClearPreview();
                        if(kind=="robber")board.PreviewRobber(tiles[4].Id,legal);else board.Preview(vertices[0].Id,false,legal);
                    }
                    else if(mode=="seafarers")
                    {
                        var board=root.GetComponent<M3BoardRenderer>();board.ClearPreview();
                        if(kind=="robber")board.PreviewRobber(tiles[4].Id,legal);else if(kind=="ship")board.PreviewShip(edges[0].Id,legal);else board.Preview(vertices[0].Id,false,legal);
                    }
                    else if(mode=="cities-knights")
                    {
                        var board=root.GetComponent<M4BoardRenderer>();board.ClearPreview();
                        if(kind=="robber")board.PreviewRobber(tiles[4].Id,legal);else board.Preview(vertices[0].Id,false,legal);
                    }
                    else
                    {
                        var board=root.GetComponent<M5BoardRenderer>();board.ClearPreview();
                        if(kind=="robber")board.PreviewRobber(tiles[4].Id,legal);else if(kind=="ship")board.PreviewShip(edges[0].Id,legal);else board.Preview(vertices[0].Id,false,legal);
                    }
                    yield return null;yield return null;
                    var prefix=kind=="robber"?"Robber ":kind=="ship"?"Ship ":vertices[0].Id;
                    var ghost=root.transform.Cast<Transform>().Single(t=>t.name.StartsWith(prefix));
                    var colour=legal?new Color(.3f,1f,.75f):new Color(1f,.22f,.2f);
                    var block=new MaterialPropertyBlock();
                    foreach(var renderer in ghost.GetComponentsInChildren<Renderer>())
                    {
                        renderer.GetPropertyBlock(block);
                        Require(block.GetColor("_BaseColor")==colour||renderer.sharedMaterial.color==colour,"Unreadable preview tint "+mode+" "+kind);
                    }
                    Require(ghost.GetComponentsInChildren<Collider>().Length==0,"Preview collider blocks picking");
                }
            }
            root.SetActive(false);Destroy(root);yield return null;yield return null;
        }
        var names=new[]{"diplomacy","espionage","encouragement","intrigue","taxation","constitution","treason","wedding","sabotage"};
        var back=Resources.Load<Texture2D>("EntityAssets/v003/progress-politics/progress-politics-back-original");Require(back!=null,"Blue original photo back not loaded");
        foreach(var name in names)
        {
            var card=Resources.Load<GameObject>("EntityAssets/v003/progress-politics/progress-politics-"+name);
            Require(card!=null&&card.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>m.mainTexture==back)),"Political card still has neutral back "+name);
        }
        CheckCardFaces();
        File.WriteAllText(Path.Combine(output,"runtime-integration.json"),JsonUtility.ToJson(new Report{
            status="passed",unityVersion=Application.unityVersion,checkedSourceInstances=checkedInstances,checkedPoliticalCards=names.Length,
            fogHasNoPhotoTexture=true,visualsHaveNoColliders=true,metreScale=true,previewColours=true,oreUsesFlatCardArt=true,views=views.ToArray()},true));
    }
    static void CheckCardFaces()
    {
        var goods=new[]{"wood","brick","wool","grain","ore","cloth","coin","paper"};
        for(var i=0;i<goods.Length;i++)
        {
            var card=EntityCardAssets.Good(i);Require(EntityCardAssets.Texture(card.Front)!=null,"Missing good face "+goods[i]);
            Require(card.Id==goods[i],"Wrong good mapping "+goods[i]);
            if(i<5)Require(EntityCardAssets.Texture(card.Back)!=null,"Missing resource back");
            else Require(card.Back==null,"Unknown commodity back must not borrow a resource back");
        }
        for(var i=0;i<25;i++)Require(EntityCardAssets.ProgressFront(i)!=null,"Missing progress face "+i);
        for(var track=0;track<3;track++)
        {
            Require(EntityCardAssets.ProgressBack(track)!=null,"Missing provided progress back "+track);
            for(var level=1;level<=5;level++)
                Require(EntityCardAssets.ImprovementFront(track,level)!=null&&EntityCardAssets.ImprovementBack(track,level)!=null,"Missing improvement face");
        }
        Require(EntityCardAssets.Texture(EntityCardAssets.Guidance(false).Front)!=null&&EntityCardAssets.Texture(EntityCardAssets.Guidance(true).Front)!=null,"Missing improvement guidance");
        Require(EntityCardAssets.Texture(EntityCardAssets.Defender.Front)!=null,"Missing defender face");
        var display=new EntityCardAssets.Display();var privateFace=EntityCardAssets.Progress(0);
        display.Begin("seat-one");Require(!display.SelectVisible(privateFace,"private card"),"Unregistered face can be selected");
        display.Register(privateFace,"private card");Require(display.SelectVisible(privateFace,"private card")&&display.SelectionIsAuthorized,"Visible own face cannot be selected");
        display.Begin("seat-two");Require(!display.HasSelection,"Private preview survived seat change");
        display.Register(privateFace,"private card");display.SelectVisible(privateFace,"private card");
        display.Begin("seat-two");display.ValidateVisibility();Require(!display.HasSelection,"Private face survived removal from PlayerView");
        display.Register(privateFace,"private card");display.SelectVisible(privateFace,"private card");
        display.Begin(null);Require(!display.HasSelection,"Privacy curtain did not clear private face");
    }
    void Source(Transform instance,string resource)
    {
        Require(instance!=null,"No instance for "+resource);var source=Resources.Load<GameObject>(resource);Require(source!=null,"Missing expected resource "+resource);
        var meshes=instance.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).ToArray();
        Require(source.GetComponentsInChildren<MeshFilter>().All(m=>meshes.Contains(m.sharedMesh)),"Wrong mesh source "+resource);
        Require(source.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).All(m=>instance.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Contains(m))),"Wrong materials "+resource);
        Require(instance.localScale==Vector3.one,"Scale must remain metres "+resource);
        Require(instance.GetComponentsInChildren<Collider>().Length==0,"Artwork blocks picking "+resource);checkedInstances++;
    }
    static void Require(bool value,string error){if(!value)throw new InvalidOperationException(error);}
    static void Capture(string output)
    {
        var camera=Camera.main;var old=RenderTexture.active;
        var render=new RenderTexture(1440,1000,24);render.Create();var image=new Texture2D(1440,1000,TextureFormat.RGB24,false);
        try
        {
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=render};
            Require(RenderPipeline.SupportsRenderRequest(camera,request),"URP capture unsupported");
            RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=render;
            image.ReadPixels(new Rect(0,0,1440,1000),0,0);image.Apply();
            var pixels=image.GetPixels32();var first=pixels[0];
            Require(pixels.Count(p=>Mathf.Abs(p.r-first.r)+Mathf.Abs(p.g-first.g)+Mathf.Abs(p.b-first.b)>30)>2000,"Empty screenshot");
            File.WriteAllBytes(output,image.EncodeToPNG());
        }
        finally{RenderTexture.active=old;render.Release();Destroy(render);Destroy(image);}
    }
}
