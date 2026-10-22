using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 只创建独立美术样板，不修改棋盘规则和地形解析入口。
public static class ForestCampAssetBuild
{
    const string Folder="Assets/Resources/Ultra/ForestCamp/v001";
    public static void Import()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        Directory.CreateDirectory(Folder);
        File.Copy(Path.Combine(repo,"art/exports/ultra/forest-camp/v001/forest-camp.fbx"),Folder+"/forest-camp.fbx",true);
        foreach(string name in new[]{"forest-atlas","leaf-cluster","detail-atlas"})
            File.Copy(Path.Combine(repo,"art/source/ultra/forest-camp/v001/textures/"+name+".png"),Folder+"/"+name+".png",true);
        AssetDatabase.Refresh();
        foreach(string name in new[]{"forest-atlas","leaf-cluster","detail-atlas"})
        {
            var imp=(TextureImporter)AssetImporter.GetAtPath(Folder+"/"+name+".png");
            imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=2048;
            imp.filterMode=FilterMode.Point;imp.mipmapEnabled=true;imp.alphaIsTransparency=name=="leaf-cluster";
            imp.wrapMode=TextureWrapMode.Clamp;imp.SaveAndReimport();
        }
        var importer=(ModelImporter)AssetImporter.GetAtPath(Folder+"/forest-camp.fbx");
        importer.importCameras=false;importer.importLights=false;importer.isReadable=true;
        importer.importNormals=ModelImporterNormals.Import;importer.SaveAndReimport();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/forest-camp.fbx");
        var model=(GameObject)PrefabUtility.InstantiatePrefab(source);
        model.name="森林伐木营地";
        // FBX坐标转换后统一营地在Unity的右前侧，保持与Blender验收镜头一致。
        model.transform.rotation=Quaternion.Euler(0,180,0);
        var cache=new Dictionary<string,Material>();
        var colors=new Dictionary<string,Color>{
            {"浅木",new Color(.55f,.31f,.10f)}, {"泥土",new Color(.24f,.14f,.065f)},
            {"苔石",new Color(.29f,.33f,.20f)}, {"绳",new Color(.59f,.45f,.24f)},
            {"铁",new Color(.18f,.22f,.22f)}, {"衣",new Color(.60f,.55f,.40f)},
            {"裤",new Color(.17f,.20f,.15f)}, {"肤",new Color(.60f,.34f,.17f)}, {"亮叶",new Color(.33f,.52f,.045f)}};
        foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            var mapped=new List<Material>();
            foreach(var old in renderer.sharedMaterials)
            {
                string name=old.name;
                if(!cache.TryGetValue(name,out var material))
                {
                    material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
                    material.SetFloat("_Smoothness",.12f);
                    bool leaves=name=="叶簇";
                    if(leaves||new[]{"叶片","树皮","红布","林地"}.Contains(name))
                        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+(leaves?"leaf-cluster":"forest-atlas")+".png"));
                    else if(new[]{"浅木","泥土","苔石","衣"}.Contains(name))
                        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/detail-atlas.png"));
                    else if(colors.TryGetValue(name,out var color))material.SetColor("_BaseColor",color);
                    if(leaves)
                    {
                        material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.5f);material.SetFloat("_Cull",0);
                        material.EnableKeyword("_ALPHATEST_ON");material.renderQueue=(int)RenderQueue.AlphaTest;
                        material.SetOverrideTag("RenderType","TransparentCutout");
                    }
                    if(name=="亮叶")material.SetFloat("_Cull",0);
                    string path=Folder+"/"+name+".mat";
                    var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(saved!=null){EditorUtility.CopySerialized(material,saved);UnityEngine.Object.DestroyImmediate(material);material=saved;}
                    else AssetDatabase.CreateAsset(material,path);
                    cache.Add(name,material);
                }
                mapped.Add(material);
            }
            renderer.sharedMaterials=mapped.ToArray();
        }
        int triangles=model.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
        if(triangles>6000)throw new Exception("Unity导入面数超限："+triangles);
        // 按材质合并，降低每块地形的Renderer数；源FBX仍保留全部分件。
        var root=new GameObject("ForestCamp");
        foreach(var pair in cache)
        {
            var combine=new List<CombineInstance>();
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                for(int i=0;i<r.sharedMaterials.Length;i++)if(r.sharedMaterials[i]==pair.Value)
                    combine.Add(new CombineInstance{mesh=mesh,subMeshIndex=i,transform=r.transform.localToWorldMatrix});
            }
            if(combine.Count==0)continue;
            var merged=new Mesh{name=pair.Key};merged.CombineMeshes(combine.ToArray(),true,true);merged.RecalculateBounds();
            string path=Folder+"/"+pair.Key+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old!=null){EditorUtility.CopySerialized(merged,old);UnityEngine.Object.DestroyImmediate(merged);merged=old;}
            else AssetDatabase.CreateAsset(merged,path);
            var child=new GameObject(pair.Key);child.transform.SetParent(root.transform,false);
            child.AddComponent<MeshFilter>().sharedMesh=merged;child.AddComponent<MeshRenderer>().sharedMaterial=pair.Value;
        }
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ForestCamp.prefab");
        var bounds=new Bounds();foreach(var r in root.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
        string evidence=Path.Combine(repo,"art/previews/ultra/forest-camp/v001/unity-import.txt");
        File.WriteAllText(evidence,"Unity版本："+Application.unityVersion+"\n三角形："+triangles+"\nRenderer："+root.GetComponentsInChildren<Renderer>().Length+"\n包围盒："+bounds+"\n碰撞器："+root.GetComponentsInChildren<Collider>().Length);
        UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(model);AssetDatabase.SaveAssets();
        Debug.Log("FOREST_CAMP_IMPORT_OK tris="+triangles);
    }
}
