using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

// 按批准的原图切片导入；处理仅发生在编辑器，运行时直接加载独立贴图。
[ScriptedImporter(2, "resourcecard")]
public sealed class ResourceCardSheetImporter : ScriptedImporter
{
    [Serializable] sealed class Slice { public string source; public int x, y, width, height; public bool rotate180; }
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var slice=JsonUtility.FromJson<Slice>(File.ReadAllText(ctx.assetPath));
        ctx.DependsOnSourceAsset(slice.source);
        var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!sheet.LoadImage(File.ReadAllBytes(slice.source)))throw new InvalidOperationException("资源牌原图读取失败");
            if(slice.x<0||slice.y<0||slice.x+slice.width>sheet.width||slice.y+slice.height>sheet.height)
                throw new InvalidOperationException("资源牌切片越界");
            var pixels=sheet.GetPixels(slice.x,sheet.height-slice.y-slice.height,slice.width,slice.height);
            if(slice.rotate180)Array.Reverse(pixels);
            // 原图的灰色展示背景只存在于圆角外侧；透明圆角保留卡面本身。
            const float radius=39;
            for(int y=0;y<slice.height;y++)for(int x=0;x<slice.width;x++)
            {
                float dx=Mathf.Max(radius-x-.5f,x+.5f-(slice.width-radius));
                float dy=Mathf.Max(radius-y-.5f,y+.5f-(slice.height-radius));
                float distance=Mathf.Sqrt(Mathf.Max(0,dx)*Mathf.Max(0,dx)+Mathf.Max(0,dy)*Mathf.Max(0,dy));
                pixels[y*slice.width+x].a*=Mathf.Clamp01(radius-distance);
            }
            var card=new Texture2D(slice.width,slice.height,TextureFormat.RGBA32,true){name=Path.GetFileNameWithoutExtension(ctx.assetPath),filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
            card.SetPixels(pixels);card.Apply(true,true);
            ctx.AddObjectToAsset("card",card);ctx.SetMainObject(card);
        }
        finally { DestroyImmediate(sheet); }
    }
}
