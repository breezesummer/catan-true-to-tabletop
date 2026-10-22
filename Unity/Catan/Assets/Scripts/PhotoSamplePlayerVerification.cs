using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Opt-in inspection of packaged resources and gallery controls in a real Windows player.
public sealed class PhotoSamplePlayerVerification : MonoBehaviour
{
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--art-verify");
        if(flag<0)yield break;
        string output=args[flag+1];Directory.CreateDirectory(output);
        var gallery=GetComponent<PhotoSampleGallery>();
        yield return null;yield return null;
        foreach(var id in new[]{"forest","road","village","resource-card"})
        {
            if(PhotoSampleAssets.Model(id)==null){Fail("Missing packaged prefab "+id);yield break;}
        }
        if(PhotoSampleAssets.Front==null||PhotoSampleAssets.Back==null){Fail("Missing card textures");yield break;}
        gallery.Rotate(45);gallery.Flip();gallery.Tint(new Color(.2f,.51f,.78f));
        if(gallery.Yaw!=45||!gallery.ShowBack||Mathf.Abs(Mathf.DeltaAngle(gallery.Village.transform.eulerAngles.y,45))>.01f)
        {Fail("Gallery controls did not update transforms");yield break;}
        gallery.Rotate(-45);gallery.Flip();gallery.Tint(new Color(.78f,.035f,.06f));
        var names=new[]{"game-camera","top","side","crowded-vertex"};
        for(int i=0;i<4;i++)
        {
            gallery.SetView(i);yield return null;yield return new WaitForEndOfFrame();
            var target=new RenderTexture(1600,1000,24);target.Create();var camera=Camera.main;var rect=camera.rect;
            camera.rect=new Rect(0,0,1,1);
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            if(!RenderPipeline.SupportsRenderRequest(camera,request)){Fail("URP capture unsupported");yield break;}
            RenderPipeline.SubmitRenderRequest(camera,request);camera.rect=rect;RenderTexture.active=target;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            if(image.GetPixels32().Count(p=>p.r>60&&p.g>40&&Math.Abs(p.r-p.g)>15)<1000){Fail("Blank art camera render");yield break;}
            File.WriteAllBytes(Path.Combine(output,"unity-"+names[i]+".png"),image.EncodeToPNG());
            RenderTexture.active=null;target.Release();Destroy(target);Destroy(image);
        }
        File.WriteAllText(Path.Combine(output,"player-verification.json"),"{\"status\":\"passed\",\"packagedPrefabs\":4,\"cardTextures\":2,\"views\":4,\"rotateFlipTint\":true,\"rulesChanged\":false}");
        Debug.Log("CATAN_PHOTO_PLAYER_OK: four prefabs, card front/back, rotate/flip/tint, four URP camera renders");
        Application.Quit(0);
    }
    static void Fail(string reason){Debug.LogError("CATAN_PHOTO_PLAYER_FAILED: "+reason);Application.Quit(1);}
}
