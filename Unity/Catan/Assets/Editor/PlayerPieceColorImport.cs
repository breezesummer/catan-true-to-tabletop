using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 只重绘材质。四色继续共享原网格，归属遮罩离线生成，运行时不扫描三角面。
public static class PlayerPieceColorImport
{
    const string Art="Assets/Art/PlayerColors/";
    const int Size=1024;
    static readonly string[] Kinds={"village","city","road","wall","metro-trade","metro-politics","metro-science",
        "knight-1-rest","knight-1-active","knight-2-rest","knight-2-active","knight-3-rest","knight-3-active"};

    [MenuItem("Catan/美术/统一玩家棋子归属色")]
    public static void Run()
    {
        ApplyKinds(Kinds);
        // 船帆已有独立材质，不改变木船和索具。
        for(int seat=0;seat<4;seat++)
        {
            var ship=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/"+EntityBoardAssets.ShipResource("P"+(seat+1))+".prefab");
            foreach(var material in ship.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct())
                if(EntityBoardAssets.IsSailMaterial(material.name))
                {material.SetColor("_BaseColor",EntityBoardAssets.SailColour(seat));EditorUtility.SetDirty(material);}
        }
        AssetDatabase.SaveAssets();
        StockModelPictures.Export();
        Debug.Log("CATAN_PLAYER_COLORS_OK: 屋顶、篷布、穹顶、城墙、道路、骑士与船帆已统一。");
    }

    public static void ApplyKinds(string[] kinds)
    {
        Directory.CreateDirectory(Art);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Art+"PlayerColor.shader");
        if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("归属色着色器未正确导入");
        foreach(string kind in kinds)
        {
            bool natural=kind=="village"||kind=="city"||kind=="road";
            string resource=(natural?"NaturalPieces/v001/":PlayerComponentAssets.ExpansionRoot)+kind;
            var first=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/"+resource+"-red.prefab");
            if(first==null)throw new InvalidOperationException("缺少归属色源棋子："+kind);
            // 再次执行始终读取原始材质，避免对已染色的贴图重复叠色。
            // 两类建筑的遮罩来自各自的陶瓦釉层和板岩风化纹。
            bool observatory=kind=="metro-science";
            bool tintSeatSlotOnly=kind=="metro-trade" || kind=="metro-politics" || kind=="wall";
            bool buildingSurface=kind=="city" || tintSeatSlotOnly || observatory;
            var mask=kind=="village" || buildingSurface
                ? AssetDatabase.LoadAssetAtPath<Texture2D>(Art+kind+"-mask.png") : MakeMask(first,kind);
            if(mask==null)throw new InvalidOperationException("缺少归属色遮罩："+kind);
            for(int seat=0;seat<4;seat++)
            {
                string key=PlayerComponentAssets.ColourKey("P"+(seat+1));
                string prefabPath="Assets/Resources/"+resource+"-"+key+".prefab";
                var root=PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                    {
                        var originals=renderer.sharedMaterials;
                        for(int slot=0;slot<originals.Length;slot++)
                        {
                            var original=SourceMaterial(originals[slot],kind,natural);
                            if(original==null || original.name.Contains("Contact") || original.name.Contains("Metal"))continue;
                            bool player=original.name.Contains("Player")||original.name.Contains("Seat");
                            if(!player && (tintSeatSlotOnly || (kind!="road" && !kind.StartsWith("metro-"))))
                            {originals[slot]=original;continue;}
                            string path=Art+kind+"-"+slot+"-"+key+".mat";
                            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                            if(material==null){material=new Material(original);AssetDatabase.CreateAsset(material,path);}
                            else material.CopyPropertiesFromMaterial(original);
                            material.shader=kind=="village" || buildingSurface?original.shader:shader;
                            if(kind=="village")material.EnableKeyword("_VILLAGE_GLAZE");
                            if(buildingSurface)material.EnableKeyword("_CITY_TINTED_SLATE");
                            material.name="玩家归属-"+kind+"-"+slot+"-"+key;
                            material.SetColor("_BaseColor",BoardRenderer.SeatColors[seat]);
                            material.SetTexture("_DetailMask",observatory?original.GetTexture("_DetailMask"):mask);
                            material.DisableKeyword("_DETAIL_MULX2");material.DisableKeyword("_DETAIL_SCALED");
                            material.enableInstancing=true;
                            EditorUtility.SetDirty(material);
                            originals[slot]=material;
                        }
                        renderer.sharedMaterials=originals;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        AssetDatabase.SaveAssets();
    }

    static Material SourceMaterial(Material material,string kind,bool natural)
    {
        if(material==null || !material.name.StartsWith("玩家归属-"))return material;
        // 归属材质名中的槽位保持源 FBX 的顺序；源文件始终保留未染色贴图。
        var model=AssetDatabase.LoadAssetAtPath<GameObject>((natural?"Assets/Art/NaturalPieces/v001/":"Assets/Art/ExpansionPieces/v001/")+kind+".fbx");
        int slot=int.Parse(material.name.Substring(("玩家归属-"+kind+"-").Length).Split('-')[0]);
        var source=model.GetComponentInChildren<MeshRenderer>().sharedMaterials[slot];
        if(source.name.Contains("Contact"))return AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/NaturalPieces/v001/Natural_Contact.mat");
        string path=natural?"Assets/Art/NaturalPieces/v001/"+(source.name.Contains("Player")?"Natural_Player_"+kind+"_red":"Natural_Masonry_"+kind)+".mat":
            "Assets/Art/ExpansionPieces/v001/"+(source.name.Contains("Seat")?"Expansion_Seat_"+kind+"_red":source.name.Contains("Metal")?"Expansion_Metal_"+kind:"Expansion_Natural_"+kind)+".mat";
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static Texture2D MakeMask(GameObject prefab,string kind)
    {
        bool natural=kind=="village"||kind=="city"||kind=="road";
        bool metro=kind.StartsWith("metro-");
        var root=UnityEngine.Object.Instantiate(prefab);
        var pixels=new Color32[Size*Size];
        var covered=new bool[pixels.Length];
        int roofFaces=0;
        try
        {
            var bounds=root.GetComponentInChildren<MeshRenderer>().bounds;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;
                var vertices=mesh.vertices.Select(v=>root.transform.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
                // FBX 根变换包含轴向转换，不能用变换后三点的叉积代替表面法线。
                var normalMatrix=(root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix).inverse.transpose;
                var normals=mesh.normals.Select(n=>normalMatrix.MultiplyVector(n).normalized).ToArray();
                var uv=mesh.uv;
                var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;
                for(int slot=0;slot<mesh.subMeshCount;slot++)
                {
                    var material=SourceMaterial(materials[slot],kind,natural);
                    string name=material.name;
                    if(name.Contains("Contact"))continue;
                    bool player=name.Contains("Player")||name.Contains("Seat");
                    bool metal=name.Contains("Metal");
                    var indices=mesh.GetTriangles(slot);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        int a=indices[i],b=indices[i+1],c=indices[i+2];
                        var center=(vertices[a]+vertices[b]+vertices[c])/3;
                        float height=(center.y-bounds.min.y)/bounds.size.y;
                        var normal=(normals[a]+normals[b]+normals[c]).normalized;
                        // 主屋顶、金帐、穹顶连成完整色面；金属仪器和石基保留原材质。
                        bool roof=metro && height>.23f && normal.y>.18f && !metal;
                        if(roof&&!player)roofFaces++;
                        bool owned=player||roof||kind=="road";
                        Raster(uv[a]*Size,uv[b]*Size,uv[c]*Size,owned,pixels,covered);
                    }
                }
            }
            if(metro && roofFaces<100)throw new InvalidOperationException("大都会屋顶归属遮罩缺失："+kind);
            Debug.Log("CATAN_PLAYER_COLOR_MASK: "+kind+" neutralRoofTriangles="+roofFaces);
            // 补足烘焙 UV 岛的留白，缩小与斜视时不露出原色细边。
            for(int pass=0;pass<4;pass++)
            {
                var next=(Color32[])pixels.Clone();var fill=(bool[])covered.Clone();
                for(int y=1;y<Size-1;y++)for(int x=1;x<Size-1;x++)
                {
                    int p=y*Size+x;if(covered[p])continue;
                    foreach(int d in new[]{-1,1,-Size,Size})if(covered[p+d])
                    {next[p]=pixels[p+d];fill[p]=true;break;}
                }
                pixels=next;covered=fill;
            }
            var image=new Texture2D(Size,Size,TextureFormat.RGBA32,false,true);
            image.SetPixels32(pixels);image.Apply();
            string path=Art+kind+"-mask.png";
            File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Trilinear;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }

    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static void Raster(Vector2 a,Vector2 b,Vector2 c,bool owned,Color32[] pixels,bool[] covered)
    {
        float area=Cross(b-a,c-a);if(Mathf.Abs(area)<.00001f)return;
        int minX=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,Size-1);
        int maxX=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,Size-1);
        int minY=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,Size-1);
        int maxY=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,Size-1);
        byte value=owned?(byte)255:(byte)0;
        for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
        {
            var p=new Vector2(x+.5f,y+.5f);
            float u=Cross(b-p,c-p)/area,v=Cross(c-p,a-p)/area,w=1-u-v;
            if(u<-.001f||v<-.001f||w<-.001f)continue;
            int index=y*Size+x;
            pixels[index]=new Color32(value,value,value,255);covered[index]=true;
        }
    }
}
