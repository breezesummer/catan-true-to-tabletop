using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class StockModelPictureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/StockModels/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.isReadable=true;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=true;
        importer.filterMode=FilterMode.Trilinear;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
    }
    public override uint GetVersion()=>1;
}

// 直接从棋盘 prefab 导出正交库存图，换模型时只需重跑此菜单。
public static class StockModelPictures
{
    [MenuItem("Catan/美术/导出库存模型正视图")]
    public static void Export() => ExportKinds(new[]{"settlement","city","road","ship","wall","knight-1","knight-2","knight-3"});
    public static void ExportShips() => ExportKinds(new[]{"ship"});
    static void ExportKinds(string[] kinds)
    {
        const string folder="Assets/Resources/StockModels";
        Directory.CreateDirectory(folder);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("库存正视图").AddComponent<Camera>();
        camera.orthographic=true;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=Color.clear;
        camera.nearClipPlane=.001f;
        camera.farClipPlane=2;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var light=new GameObject("柔光").AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=1.45f;
        light.transform.rotation=Quaternion.Euler(40,-145,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
        var target=RenderTexture.GetTemporary(256,256,24,RenderTextureFormat.ARGB32);
        var image=new Texture2D(256,256,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;
        try
        {
            camera.targetTexture=target;
            for(int seat=1;seat<=4;seat++)
            foreach(var kind in kinds)
            {
                string player="P"+seat;
                string path=kind=="road"?PlayerComponentAssets.RoadResource(player):
                    kind=="ship"?EntityBoardAssets.ShipResource(player):
                    kind.StartsWith("knight")?PlayerComponentAssets.KnightResource(player,kind[7]-'0',false):
                    PlayerComponentAssets.BuildingResource(kind=="settlement"?"village":kind=="wall"?"city-wall":kind,player);
                var piece=Object.Instantiate(PlayerComponentAssets.Model(path));
                try
                {
                    var renderers=piece.GetComponentsInChildren<Renderer>();
                    var bounds=renderers[0].bounds;
                    foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                    bool lateral=kind=="ship"||kind=="road";
                    camera.transform.position=bounds.center+(kind=="ship"?new Vector3(1,.20f,.48f).normalized:lateral?Vector3.right:Vector3.forward)*.5f;
                    camera.transform.LookAt(bounds.center);
                    camera.orthographicSize=.54f*Mathf.Max(bounds.size.y,lateral?bounds.size.z:bounds.size.x);
                    camera.Render();RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
                    var pixels=image.GetPixels32();
                    int left=256,right=-1,bottom=256,top=-1;
                    for(int i=0;i<pixels.Length;i++)if(pixels[i].a>8)
                    {left=Mathf.Min(left,i%256);right=Mathf.Max(right,i%256);bottom=Mathf.Min(bottom,i/256);top=Mathf.Max(top,i/256);}
                    if(right<=left||top<=bottom)throw new System.InvalidOperationException("模型正视图为空 "+path);
                    var cropped=new Texture2D(right-left+1,top-bottom+1,TextureFormat.RGBA32,false);
                    cropped.SetPixels(image.GetPixels(left,bottom,cropped.width,cropped.height));cropped.Apply();
                    File.WriteAllBytes(folder+"/"+kind+"-"+PlayerComponentAssets.ColourKey(player)+".png",cropped.EncodeToPNG());
                    Object.DestroyImmediate(cropped);
                }
                finally {Object.DestroyImmediate(piece);}
            }
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(image);Object.DestroyImmediate(camera.gameObject);Object.DestroyImmediate(light.gameObject);
        }
        AssetDatabase.Refresh();
    }
}
