using System;
using System.Collections.Generic;
using Catan.Core;
using UnityEngine;

// 只接收人类公开投影的已落成建筑。每个角对应两个60度自然区，合法距离规则保证不重叠。
public sealed class UltraPastureTile : MonoBehaviour
{
    readonly string[] seats=new string[6];
    readonly bool[] upgraded=new bool[6];
    readonly GameObject[] ranches=new GameObject[6],extras=new GameObject[6];
    public string TileId {get;private set;}
    public string[] VertexIds {get;private set;}
    public int DevelopedCount {get;private set;}
    public float DevelopedFraction=>DevelopedCount/3f;
    public void Bind(Tile tile,Dictionary<string,Vector3> positions)
    {
        TileId=tile.Id;VertexIds=new string[6];
        foreach(string id in tile.Vertices)
        {
            var d=positions[id]-transform.position;int slot=VertexSlot(d);
            if(VertexIds[slot]!=null)throw new InvalidOperationException("草原六角坐标重复。");VertexIds[slot]=id;
        }
    }
    public static int VertexSlot(Vector3 direction)=>((Mathf.RoundToInt((Mathf.Atan2(direction.z,direction.x)*Mathf.Rad2Deg-30)/60)%6)+6)%6;
    public string LayoutSignature
    {
        get {var parts=new string[6];for(int i=0;i<6;i++)parts[i]=(VertexIds?[i]??"")+":"+seats[i]+":"+upgraded[i];return string.Join("|",parts);}
    }
    public void Refresh(PiecePlacement[] settlements,PiecePlacement[] cities)
    {
        if(VertexIds==null)return;
        DevelopedCount=0;int covered=0;
        for(int i=0;i<6;i++)
        {
            string owner=null;bool city=false;
            foreach(var p in settlements)if(p.LocationId==VertexIds[i]){owner=p.PlayerId;break;}
            foreach(var p in cities)if(p.LocationId==VertexIds[i]){owner=p.PlayerId;city=true;break;}
            if(owner!=null){DevelopedCount++;covered|=(1<<i)|(1<<((i+5)%6));}
            if(owner==seats[i]&&city==upgraded[i])continue;
            Remove(ranches[i]);Remove(extras[i]);ranches[i]=null;extras[i]=null;seats[i]=owner;upgraded[i]=city;
            if(owner==null)continue;
            var color=BoardRenderer.SeatColor(owner);ranches[i]=UltraPastureAssets.Part("ranch-"+i,transform,color);
            if(city)extras[i]=UltraPastureAssets.Part("city-"+i,transform,color);
        }
        for(int i=0;i<6;i++)transform.Find("wild-"+i).gameObject.SetActive((covered&(1<<i))==0);
    }
    static void Remove(GameObject obj){if(obj!=null){obj.SetActive(false);Destroy(obj);}}
}

// 四种棋盘共用一份适配；没有逐帧轮询，不接受权威 GameState 或 AI PlayerView。
public sealed class UltraBoardEnvironment : MonoBehaviour
{
    UltraPastureTile[] pastures;
    public static UltraBoardEnvironment Create(Transform root,Tile[] tiles,Dictionary<string,Vector3> vertices)
    {
        if(!BoardThemeController.IsUltra)return null;
        var environment=root.gameObject.AddComponent<UltraBoardEnvironment>();var list=new List<UltraPastureTile>();
        foreach(var tile in tiles)
        {
            if(tile.Resource!="wool")continue;
            var model=root.Find(tile.Id)?.GetComponent<UltraPastureTile>();if(model==null)continue;
            model.Bind(tile,vertices);list.Add(model);
        }
        environment.pastures=list.ToArray();return environment;
    }
    public void Refresh(PiecePlacement[] settlements,PiecePlacement[] cities)
    {foreach(var tile in pastures)tile.Refresh(settlements,cities);}
}
