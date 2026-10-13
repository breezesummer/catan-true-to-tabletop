using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class EntityAssetPlayerVerification : MonoBehaviour
{
    [Serializable] class Report {public string status;public int packagedPrefabs,types;public bool rotateFlip,wallAssembly;public string controlCheckMethod="runtime method calls";public string[] capturedViews;}
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();var flag=Array.IndexOf(args,"--entity-verify");
        if(flag<0) yield break;
        if(flag+1>=args.Length){Fail("Missing evidence directory");yield break;}
        var output=args[flag+1];Directory.CreateDirectory(output);
        var gallery=GetComponent<EntityAssetGallery>(); yield return null; yield return null;
        var captured=new List<string>();
        for(var index=0;index<gallery.Catalog.assets.Length;index++)
        {
            var entry=gallery.Catalog.assets[index]; gallery.Show(index); yield return null;
            if(gallery.Current.GetComponentsInChildren<Collider>().Length>0){Fail("Art unexpectedly has colliders: "+entry.id);yield break;}
            foreach(var renderer in gallery.Current.GetComponentsInChildren<Renderer>())
            {
                if(renderer.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name!="Universal Render Pipeline/Lit"))
                {Fail("Missing URP material: "+entry.id);yield break;}
            }
            var yaw=gallery.Yaw;gallery.Rotate(45);gallery.Flip();
            if(!gallery.Back||Mathf.Abs(gallery.Yaw-yaw-45)>.01f||Quaternion.Angle(gallery.Current.transform.rotation,Quaternion.Euler(0,yaw+45,180))>.01f)
            {Fail("Rotate/flip failed: "+entry.id);yield break;}
            gallery.Rotate(-45);gallery.Flip();
            gallery.SetView(1);yield return null;yield return new WaitForEndOfFrame();
            var assetFolder=Path.Combine(output,"assets",entry.id);Directory.CreateDirectory(assetFolder);
            if(!gallery.CatalogResource.Contains("/v002/"))
            {
                if(!Capture(Path.Combine(assetFolder,"unity-top.png"))){yield break;}
                captured.Add("assets/"+entry.id+"/top");
            }
            // Every prefab loads in a standalone player. Capture every type, all four views.
            if(!gallery.CatalogResource.Contains("/v002/")&&index>0&&gallery.Catalog.assets.Take(index).Any(e=>e.typeId==entry.typeId))continue;
            var folder=Path.Combine(output,gallery.CatalogResource.Contains("/v002/")?"assets/"+entry.id:entry.typeId);Directory.CreateDirectory(folder);
            var names=new[]{"game-camera","top","side","crowded-vertex"};
            for(var view=0;view<4;view++)
            {
                gallery.SetView(view);yield return null;yield return new WaitForEndOfFrame();
                if(!Capture(Path.Combine(folder,"unity-"+names[view]+".png"))){yield break;}
                captured.Add((gallery.CatalogResource.Contains("/v002/")?"assets/"+entry.id:entry.typeId)+"/"+names[view]);
            }
            if(entry.kind=="card"||entry.kind=="panel"||entry.typeId=="barbarian-track")
            {
                gallery.SetView(1);gallery.Flip();yield return null;yield return new WaitForEndOfFrame();
                if(!Capture(Path.Combine(folder,"unity-back.png"))){yield break;}gallery.Flip();
                captured.Add(entry.typeId+"/back");
            }
        }
        var wall=Array.FindIndex(gallery.Catalog.assets,a=>a.typeId=="city-wall");
        var assembly=wall>=0&&gallery.Catalog.assets.Any(a=>a.id=="city-red")&&gallery.Catalog.assets.Any(a=>a.id=="metropolis-red");
        if(assembly)
        {
            foreach(var id in new[]{"city-red","metropolis-red"})
            {
                gallery.Show(wall);gallery.SetView(0);gallery.AddInsert(id);
                yield return null;yield return new WaitForEndOfFrame();
                if(!Capture(Path.Combine(output,"wall-with-"+id+".png"))){yield break;}
                captured.Add("wall-with-"+id);
            }
        }
        File.WriteAllText(Path.Combine(output,"player-verification.json"),JsonUtility.ToJson(new Report {status="passed",
            packagedPrefabs=gallery.Catalog.assets.Length,types=gallery.Catalog.assets.Select(a=>a.typeId).Distinct().Count(),
            rotateFlip=true,wallAssembly=assembly,capturedViews=captured.ToArray()},true));
        Debug.Log("CATAN_ENTITY_PLAYER_OK assets="+gallery.Catalog.assets.Length+" captures="+captured.Count);
        Application.Quit(0);
    }
    static bool Capture(string path)
    {
        var camera=Camera.main;var rect=camera.rect;var previous=RenderTexture.active;
        var target=new RenderTexture(1200,800,24);target.Create();
        var image=new Texture2D(1200,800,TextureFormat.RGB24,false);
        try
        {
            camera.rect=new Rect(0,0,1,1);
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            if(!RenderPipeline.SupportsRenderRequest(camera,request)){Fail("URP capture unsupported");return false;}
            RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();
            var pixels=image.GetPixels32();var background=pixels[0];
            if(pixels.Count(p=>Mathf.Abs(p.r-background.r)+Mathf.Abs(p.g-background.g)+Mathf.Abs(p.b-background.b)>15)<50)
            {Fail("Empty camera capture: "+path);return false;}
            File.WriteAllBytes(path,image.EncodeToPNG());return true;
        }
        finally {camera.rect=rect;RenderTexture.active=previous;target.Release();Destroy(target);Destroy(image);}
    }
    static void Fail(string message){Debug.LogError("CATAN_ENTITY_PLAYER_FAILED: "+message);Application.Quit(1);}
}
