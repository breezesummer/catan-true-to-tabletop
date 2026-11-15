using System.Collections.Generic;
using UnityEngine;

// Transparent thumbnails rendered from the retained physical-piece meshes.
// Runtime never creates a camera, changes layers, or reads hidden game state.
public static class OperationPieceAssets
{
    static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
    static Dictionary<string, Texture2D> paintedStock;

    public static void DrawStock(Rect area,Texture2D texture)
    {
        // 实体道路从水平面看很薄，库存尺寸下保留可辨认的侧面高度。
        if(texture.width>texture.height*10)
        {
            float height=area.width*.10f;
            GUI.DrawTexture(new Rect(area.x,area.center.y-height/2,area.width,height),texture);
        }
        else GUI.DrawTexture(area,texture,ScaleMode.ScaleToFit);
    }

    public static bool Contains(Texture2D texture, Rect rect, Vector2 point)
    {
        float width=Mathf.Min(rect.width,rect.height*texture.width/texture.height),height=width*texture.height/texture.width;
        var fitted=new Rect(rect.center.x-width/2,rect.center.y-height/2,width,height);
        if(!fitted.Contains(point))return false;
        return texture.isReadable&&texture.GetPixelBilinear((point.x-fitted.x)/width,1-(point.y-fitted.y)/height).a>.12f;
    }

    public static Texture2D Texture(string icon, string seat, int level = 1)
    {
        var colour = PlayerComponentAssets.ColourKey(seat);
        if (colour == null || string.IsNullOrEmpty(icon)) return null;
        icon = icon.ToLowerInvariant();
        string kind;
        if (icon.Contains("road")) kind = "road";
        else if (icon.Contains("ship")) kind = "ship";
        else if (icon.Contains("knight")) kind = "knight-" + Mathf.Clamp(level, 1, 3);
        else if (icon.Contains("wall")) kind = "wall";
        else if (icon.Contains("city")) kind = "city";
        else if (icon.Contains("settlement") || icon.Contains("village")) kind = "settlement";
        else return null;
        if(BoardArtPreference.Painted && kind!="wall")
        {
            if(paintedStock==null)
            {
                paintedStock=new Dictionary<string,Texture2D>();
                foreach(var iconTexture in Resources.LoadAll<Texture2D>("FantasyPieces/stock"))
                    paintedStock[iconTexture.name]=iconTexture;
            }
            if(paintedStock.TryGetValue((kind=="settlement"?"village":kind)+"-"+colour,out var painted))return painted;
        }
        string path="StockModels/"+kind+"-"+colour;
        if (!Textures.TryGetValue(path, out var texture))
        {
            texture = Resources.Load<Texture2D>(path);
            Textures[path] = texture;
        }
        return texture;
    }

    public static GameObject PointerPreview(TabletopPointerPreview preview,string seat,Vector3 bottom,Quaternion rotation,Transform parent,bool legal,bool ocean=true,Quaternion? pirateFacing=null)
    {
        var colour=legal?ProgressTableUI.Gold:new Color(.64f,.29f,.19f);
        string label="鼠标预览 · "+preview.Kind;
        GameObject instance=null;
        // 新模型底面即落地点，移除旧顶点棋子的四毫米展示余量。
        if(preview.Kind=="settlement"||preview.Kind=="city"||preview.Kind=="wall"||preview.Kind=="metropolis"||preview.Kind=="knight")bottom-=Vector3.up*.004f;
        switch(preview.Kind)
        {
            case "settlement": instance=PlayerComponentAssets.Building("village",seat,label,bottom,parent);break;
            case "city": instance=PlayerComponentAssets.Building("city",seat,label,bottom,parent);break;
            case "wall": instance=PlayerComponentAssets.Building("city-with-wall",seat,label,bottom,parent);break;
            case "metropolis": instance=PlayerComponentAssets.Building(preview.Wall?"metropolis-with-wall":"metropolis",seat,label,bottom,parent,preview.Track);break;
            case "knight": instance=PlayerComponentAssets.Knight(seat,preview.Level,preview.Active,label,bottom,parent);break;
            case "road": instance=PlayerComponentAssets.Road(seat,label,bottom,rotation,parent);break;
            case "ship": int seatIndex=System.Array.IndexOf(new[]{"P1","P2","P3","P4"},seat);instance=EntityBoardAssets.Ship(label,BoardRenderer.SeatColors[Mathf.Max(0,seatIndex)],ocean?EntityBoardAssets.ShipWaterline(bottom):bottom,rotation,parent);break;
            case "robber": instance=EntityBoardAssets.Robber(label,bottom+new Vector3(.012f,EntityBoardAssets.BoardSurfaceHeight,0),parent);break;
            case "pirate": instance=EntityBoardAssets.Pirate(label,bottom+new Vector3(.012f,EntityBoardAssets.BoardSurfaceHeight,0),parent,ocean,pirateFacing);break;
            case "merchant": instance=EntityBoardAssets.Merchant(label,bottom+new Vector3(-.013f,EntityBoardAssets.BoardSurfaceHeight,0),parent);break;
            case "port": instance=EntityBoardAssets.ReferenceMarker(EntityBoardAssets.PortResource(null),label,bottom,rotation,parent);break;
        }
        if(instance==null)
        {
            instance=GameObject.CreatePrimitive(PrimitiveType.Cylinder);instance.name=label;
            instance.transform.SetParent(parent,false);instance.transform.position=bottom+Vector3.up*.011f;
            instance.transform.localScale=new Vector3(.022f,.0009f,.022f);
            instance.GetComponent<Renderer>().sharedMaterial=PointerMaterial;
        }
        var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",colour);block.SetColor("_Color",colour);
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if(preview.Kind!="settlement")
            {
                renderer.SetPropertyBlock(block);
                if(preview.Kind=="ship")
                {
                    var materials=renderer.sharedMaterials;
                    var sailBlock=new MaterialPropertyBlock();
                    for(int i=0;i<materials.Length;i++)
                        if(materials[i]!=null && EntityBoardAssets.IsSailMaterial(materials[i].name))
                        {
                            sailBlock.SetColor("_BaseColor",materials[i].GetColor("_BaseColor"));
                            renderer.SetPropertyBlock(sailBlock,i);
                        }
                }
                continue;
            }
            for(int i=0;i<renderer.sharedMaterials.Length;i++)
                if(renderer.sharedMaterials[i]==null || !renderer.sharedMaterials[i].name.StartsWith("Village_Seat"))
                    renderer.SetPropertyBlock(block,i);
        }
        foreach(var collider in instance.GetComponentsInChildren<Collider>())Object.Destroy(collider);
        instance.AddComponent<SelectionArrival>();
        return instance;
    }
    static Material pointerMaterial;
    static Material PointerMaterial=>pointerMaterial!=null?pointerMaterial:(pointerMaterial=BoardRenderer.Material(Color.white));
}
