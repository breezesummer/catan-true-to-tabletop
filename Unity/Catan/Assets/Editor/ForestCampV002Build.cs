using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// v002 独立样板导入，保留旧版及正式主题入口。
public static class ForestCampV002Build
{
    const string Folder="Assets/Resources/Ultra/ForestCamp/v002";
    public static void ImportAndBuild(){Import();M6Build.Build();}
    public static void Import()
    {
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        Directory.CreateDirectory(Folder);
        foreach(string name in new[]{"forest-color","forest-normal"})
            File.Copy(Path.Combine(repo,"art/source/ultra/forest-camp/v002/textures",name+".png"),Folder+"/"+name+".png",true);
        for(int i=0;i<3;i++)File.Copy(Path.Combine(repo,"art/exports/ultra/forest-camp/v002/forest-camp-lod"+i+".fbx"),Folder+"/forest-camp-lod"+i+".fbx",true);
        AssetDatabase.Refresh();
        foreach(string name in new[]{"forest-color","forest-normal"})
        {
            var imp=(TextureImporter)AssetImporter.GetAtPath(Folder+"/"+name+".png");
            imp.textureType=name=="forest-normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            imp.sRGBTexture=name!="forest-normal";imp.filterMode=FilterMode.Trilinear;imp.anisoLevel=4;
            imp.mipmapEnabled=true;imp.maxTextureSize=1024;imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.SaveAndReimport();
        }
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="森林图集"};
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/forest-color.png"));
        mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/forest-normal.png"));mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_BumpScale",.35f);mat.SetFloat("_Smoothness",.22f);mat.SetFloat("_Cull",0);mat.enableInstancing=true;
        string materialPath=Folder+"/Forest.mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(saved!=null){EditorUtility.CopySerialized(mat,saved);UnityEngine.Object.DestroyImmediate(mat);mat=saved;}else AssetDatabase.CreateAsset(mat,materialPath);
        var root=new GameObject("ForestCampV002");var lods=new LOD[3];var counts=new int[3];
        for(int i=0;i<3;i++)
        {
            string path=Folder+"/forest-camp-lod"+i+".fbx";var imp=(ModelImporter)AssetImporter.GetAtPath(path);
            imp.importCameras=false;imp.importLights=false;imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;
            imp.isReadable=true;imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.SaveAndReimport();
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
            model.name="LOD"+i;model.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>())r.sharedMaterials=Enumerable.Repeat(mat,r.GetComponent<MeshFilter>().sharedMesh.subMeshCount).ToArray();
            counts[i]=model.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
            lods[i]=new LOD(new[]{.42f,.18f,.015f}[i],model.GetComponentsInChildren<Renderer>());
        }
        if(!(counts[0]>counts[1]&&counts[1]>counts[2]))throw new Exception("森林LOD面数未逐档降低");
        var lod=root.AddComponent<LODGroup>();lod.SetLODs(lods);lod.RecalculateBounds();
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ForestCamp.prefab");
        File.WriteAllText(Path.Combine(repo,"art/previews/ultra/forest-camp/v002/unity-import.txt"),"Unity "+Application.unityVersion+"\nLOD三角形："+string.Join(",",counts)+"\n碰撞器："+root.GetComponentsInChildren<Collider>().Length+"\n材质：1张本色图集、1张非sRGB法线，三线性及MipMap，LODGroup已配置。");
        UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();Debug.Log("FOREST_V002_IMPORT_OK");
    }
}
