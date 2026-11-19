using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

// 在导入时从棋盘原画制作可拾取的低分辨率库存图，客户端无需回读或重新着色像素。
[ScriptedImporter(5,"fantasyicons")]
public sealed class FantasyThumbnailImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        foreach(var kind in File.ReadAllLines(ctx.assetPath))
        {
            if(string.IsNullOrWhiteSpace(kind))continue;
            // 上排为村、城、路，下排为三级骑士。船已有独立的侧面原画。
            int cell=Array.IndexOf(new[]{"village","city","road","knight-1","knight-2","knight-3"},kind);
            var path=cell>=0?"Assets/Art/FantasyPieces/stock-fronts.png":
                Path.GetDirectoryName(ctx.assetPath).Replace('\\','/')+"/"+kind+".png";
            ctx.DependsOnSourceAsset(path);
            var source=new Texture2D(2,2,TextureFormat.RGBA32,true);
            try
            {
                if(!source.LoadImage(File.ReadAllBytes(path)))throw new InvalidDataException(path);
                int width=192, height=cell>=0?width:Mathf.RoundToInt(width*source.height/(float)source.width);
                var rect=cell>=0?new Rect(cell%3/3f,(1-cell/3)*.5f,1/3f,.5f):new Rect(0,0,1,1);
                var mip=Mathf.Max(0,Mathf.RoundToInt(Mathf.Log(source.width*rect.width/width,2)));
                foreach(var colour in new[]{"blue","red","orange","beige"})
                {
                    var tex=new Texture2D(width,height,TextureFormat.RGBA32,true)
                        { name=kind+"-"+colour, wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear };
                    var pixels=new Color32[width*height];
                    var pigment=BoardRenderer.SeatColors[Array.IndexOf(new[]{"red","blue","orange","beige"},colour)].linear;
                    for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    {
                        var ink=source.GetPixelBilinear(rect.x+(x+.5f)/width*rect.width,
                            rect.y+(y+.5f)/height*rect.height,mip);
                        var lin=ink.linear;
                        float excess=(lin.b-Mathf.Max(lin.r,lin.g))/Mathf.Max(lin.b,.001f);
                        float mask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.09f,.35f,excess));
                        var dyed=Color.Lerp(lin,pigment*(.09f+1.22f*lin.b),mask).gamma;
                        dyed.a=ink.a;
                        ink=dyed;
                        pixels[y*width+x]=ink;
                    }
                    tex.SetPixels32(pixels);tex.Apply();
                    ctx.AddObjectToAsset(tex.name,tex);
                    if(kind=="village" && colour=="blue")ctx.SetMainObject(tex);
                }
            }
            finally { DestroyImmediate(source); }
        }
    }
}
