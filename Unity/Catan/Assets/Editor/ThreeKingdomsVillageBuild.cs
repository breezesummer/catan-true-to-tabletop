using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 独立美术导入：不更改规则、存档或节点碰撞。
public static class ThreeKingdomsVillageBuild
{
    const string Root="Assets/Resources/ThreeKingdoms/v001/";
    const string Art="Assets/Art/ThreeKingdoms/v001/";
    public static void Import()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        string source=Path.Combine(repo,"art/exports/three-kingdoms-pieces/v001");
        string previews=Path.Combine(repo,"art/previews/three-kingdoms-pieces/v001");
        Directory.CreateDirectory(Root); Directory.CreateDirectory(Art);
        File.Copy(Path.Combine(source,"textures/village-atlas.png"),Art+"village-atlas.png",true);
        string[] colours={"red","blue","orange","beige"};
        foreach(string colour in colours)
        {
            File.Copy(Path.Combine(source,"tk-village-"+colour+".fbx"),Art+"tk-village-"+colour+".fbx",true);
            File.Copy(Path.Combine(previews,"thumbnail-"+colour+".png"),Root+"thumbnail-"+colour+".png",true);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string path in new[]{Art+"village-atlas.png"}.Concat(colours.Select(c=>Root+"thumbnail-"+c+".png")))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=true;
            importer.mipmapEnabled=true;importer.isReadable=path.Contains("thumbnail");
            importer.alphaIsTransparency=path.Contains("thumbnail");importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Trilinear;importer.maxTextureSize=1024;importer.SaveAndReimport();
        }
        var atlas=Material("Village_Atlas",Color.white);atlas.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"village-atlas.png");
        atlas.SetFloat("_Smoothness",.2f);EditorUtility.SetDirty(atlas);
        var evidence=new List<string>();
        for(int i=0;i<4;i++)
        {
            string name="tk-village-"+colours[i];string path=Art+name+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.globalScale=1;importer.useFileScale=true;
            importer.importAnimation=false;importer.addCollider=false;importer.isReadable=true;importer.SaveAndReimport();
            var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));obj.name=name;
            var paint=Material("Village_Seat_"+colours[i],BoardRenderer.SeatColors[i]);
            foreach(var r in obj.GetComponentsInChildren<Renderer>())
                r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.StartsWith("Village_Seat")?paint:atlas).ToArray();
            var renderers=obj.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            int tris=obj.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
            if(Mathf.Abs(bounds.min.y)>.00001f || bounds.size.x>.019f || bounds.size.z>.019f || bounds.size.y>.020f)
                throw new Exception("村庄米制尺寸不符："+bounds);
            if(obj.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("村庄混入拾取碰撞");
            PrefabUtility.SaveAsPrefabAsset(obj,Root+name+".prefab");
            evidence.Add(name+"：尺寸米="+bounds.size.ToString("F6")+"，三角形="+tris+"，渲染器="+renderers.Length+"，材质="+renderers.Sum(r=>r.sharedMaterials.Length));
            UnityEngine.Object.DestroyImmediate(obj);
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        string output=Path.Combine(repo,"docs/verification/three-kingdoms-pieces/v001");Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output,"Unity导入.txt"),evidence);
        Debug.Log("CATAN_THREE_KINGDOMS_IMPORT_OK");
    }
    static Material Material(string name,Color color)
    {
        string path=Art+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.color=color;material.SetFloat("_Smoothness",.35f);EditorUtility.SetDirty(material);return material;
    }
}
