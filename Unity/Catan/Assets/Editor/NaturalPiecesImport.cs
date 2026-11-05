using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 只导入展示资产；四色共享三件网格和自然材质。
public static class NaturalPiecesImport
{
    const string Art="Assets/Art/NaturalPieces/v001/";
    const string Root="Assets/Resources/NaturalPieces/v001/";
    static readonly string[] Colours={"red","blue","orange","beige"};
    static readonly Color[] Tints={new Color(.80f,.27f,.17f),new Color(.27f,.48f,.77f),new Color(.98f,.54f,.18f),new Color(1,.98f,.89f)};
    [MenuItem("Catan/美术/导入自然四色棋子")]
    public static void Run()
    {
        Directory.CreateDirectory(Root);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string kind in new[]{"village","city","road"})
        {
            var textureImporter=(TextureImporter)AssetImporter.GetAtPath(Art+kind+"-surface.png");
            textureImporter.sRGBTexture=true;textureImporter.mipmapEnabled=true;textureImporter.maxTextureSize=2048;
            textureImporter.filterMode=FilterMode.Trilinear;textureImporter.anisoLevel=4;textureImporter.wrapMode=TextureWrapMode.Clamp;
            textureImporter.textureCompression=TextureImporterCompression.CompressedHQ;textureImporter.SaveAndReimport();
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+kind+"-surface.png");
            var neutral=MakeMaterial("Natural_Masonry_"+kind,Color.white,atlas);
            var contact=MakeContact();
            for(int seat=0;seat<4;seat++)
            {
                var player=MakeMaterial("Natural_Player_"+kind+"_"+Colours[seat],Tints[seat],atlas);
                var masonry=Colours[seat]=="beige"
                    ? MakeMaterial("Natural_Masonry_"+kind+"_ivory",new Color(.76f,.80f,.81f),atlas)
                    : neutral;
                string fbx=Art+kind+".fbx";
                if(seat==0)
                {
                    var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
                    importer.importAnimation=false;importer.addCollider=false;importer.isReadable=false;
                    importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
                    importer.meshCompression=ModelImporterMeshCompression.Off;importer.SaveAndReimport();
                }
                var root=new GameObject(kind+"-"+Colours[seat]);
                try
                {
                    var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx),root.transform);
                    model.name="自然棋子模型";
                    var renderers=model.GetComponentsInChildren<Renderer>();
                    foreach(var renderer in renderers)
                    {
                        renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m.name.StartsWith("Natural_Player")?player:m.name.StartsWith("Natural_Contact")?contact:masonry).ToArray();
                        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
                        renderer.receiveShadows=false;
                    }
                    var b=Bounds(renderers);float width=kind=="city"?.0205f:kind=="village"?.018f:.0396f;
                    float scale=width/Mathf.Max(b.size.x,b.size.z);
                    model.transform.localScale*=scale;model.transform.localPosition=new Vector3(-b.center.x,-b.min.y,-b.center.z)*scale;
                    b=Bounds(renderers);
                    if(Mathf.Abs(b.min.y)>.00001f||Mathf.Max(b.size.x,b.size.z)>width+.0001f||root.GetComponentsInChildren<Collider>().Length!=0)
                        throw new InvalidOperationException("自然棋子尺寸或碰撞检查失败："+kind);
                    if(kind=="city"&&(b.size.y<.025f||b.size.y>.030f))throw new InvalidOperationException("主城高度不正确");
                    if(kind=="road"&&b.size.z<b.size.x)throw new InvalidOperationException("道路长轴不正确");
                    PrefabUtility.SaveAsPrefabAsset(root,Root+kind+"-"+Colours[seat]+".prefab");
                    Debug.Log("CATAN_NATURAL_PIECE: "+kind+" "+Colours[seat]+" bounds="+b.size.ToString("F6"));
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                var thumbnail=(TextureImporter)AssetImporter.GetAtPath(Root+"thumbnail-"+kind+"-"+Colours[seat]+".png");
                thumbnail.isReadable=true;thumbnail.alphaIsTransparency=true;thumbnail.mipmapEnabled=true;
                thumbnail.maxTextureSize=512;thumbnail.wrapMode=TextureWrapMode.Clamp;thumbnail.SaveAndReimport();
            }
        }
        AssetDatabase.SaveAssets();Debug.Log("CATAN_NATURAL_IMPORT_OK: 12 prefabs, 3 shared meshes");
    }
    static Bounds Bounds(Renderer[] renderers){var b=renderers[0].bounds;foreach(var r in renderers.Skip(1))b.Encapsulate(r.bounds);return b;}
    static Material MakeContact()
    {
        const string path=Art+"Natural_Contact.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Catan/自然棋子贴地柔影"));AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_BaseMap",Resources.Load<Texture2D>("PaintedMap/Coast/gravel"));material.enableInstancing=true;
        EditorUtility.SetDirty(material);return material;
    }
    static Material MakeMaterial(string name,Color tint,Texture2D atlas)
    {
        string path=Art+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.name=name;material.SetTexture("_BaseMap",atlas);material.SetColor("_BaseColor",tint);
        material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.08f);
        material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.enableInstancing=true;
        // 厘米级模型以烘焙遮蔽表现缝隙，避免实时阴影在瓦边产生像素黑斑。
        material.SetFloat("_ReceiveShadows",0);material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        EditorUtility.SetDirty(material);return material;
    }
}
