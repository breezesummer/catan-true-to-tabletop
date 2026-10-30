using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using Catan.Core.TradeModules;
using UnityEngine;

// 仅消费公开渔场；每次授权视图更新核对一次，静止帧不创建网格、材质或网络消息。
public sealed class FishingTabletopAssets : MonoBehaviour
{
    public const string Root="HarborFishing/v001/";
    readonly Dictionary<string,GameObject> grounds=new Dictionary<string,GameObject>();
    string layoutKey;
    readonly List<Vector3> occupied=new List<Vector3>();
    readonly HashSet<string> retained=new HashSet<string>();
    static readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
    public static Texture2D Icon(string name)
    {
        if(!icons.TryGetValue(name,out var texture)){texture=Resources.Load<Texture2D>(Root+name);icons[name]=texture;}
        return texture;
    }
    public static void DrawIcon(Rect area,string name)
    {
        var texture=Icon(name);if(texture!=null)GUI.DrawTexture(area,texture,ScaleMode.ScaleToFit,true);
    }
    public static void Refresh(Transform board,FishingView view,Tile[] tiles,Func<string,Vector3> position,Vector3[] ports)
    {
        var layer=board.GetComponent<FishingTabletopAssets>();
        if(layer==null&&view==null)return;
        if(layer==null)layer=board.gameObject.AddComponent<FishingTabletopAssets>();
        layer.Apply(view,tiles,position,ports);
    }
    Vector3 Place(Vector3 anchor,Vector3 outward,Tile[] tiles,Func<string,Vector3> position,Vector3[] ports)
    {
        var tangent=Vector3.Cross(Vector3.up,outward);
        // 有界候选仅调整装饰，不改变渔场关联顶点。窄水道中的两处渔场不能叠在一起。
        for(int ring=0;ring<4;ring++)foreach(int side in new[]{0,-1,1,-2,2})
        {
            var p=anchor+outward*(.023f+ring*.015f)+tangent*(side*.017f);
            if(occupied.Any(o=>FlatDistance(p,o)<.029f)||ports.Any(o=>FlatDistance(p,o)<.029f))continue;
            bool land=false;
            foreach(var tile in tiles)
            {
                if(tile.Resource=="sea"||tile.Resource=="water"||tile.Resource=="fog"||tile.Resource=="hidden")continue;
                // 凸六边形的同侧测试；仅使用公开顶点，湖泊也保留地形面。
                bool positive=false,negative=false;
                for(int i=0;i<tile.Vertices.Length;i++)
                {
                    var a=position(tile.Vertices[i]);var b=position(tile.Vertices[(i+1)%tile.Vertices.Length]);
                    float cross=(b.x-a.x)*(p.z-a.z)-(b.z-a.z)*(p.x-a.x);
                    positive|=cross>0;negative|=cross<0;
                }
                if(!(positive&&negative)){land=true;break;}
            }
            if(!land)return p;
        }
        return anchor+outward*.083f;
    }
    static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    void Apply(FishingView view,Tile[] tiles,Func<string,Vector3> position,Vector3[] ports)
    {
        string next=view==null?"":string.Join("|",view.Grounds.Select(g=>g.Id+":"+g.Number+":"+string.Join(",",g.VertexIds)))+string.Join("|",tiles.Select(t=>t.Id+":"+t.Resource))+string.Join("|",ports.Select(p=>p.x.ToString("R")+":"+p.z.ToString("R")));
        if(next==layoutKey)return;layoutKey=next;
        retained.Clear();occupied.Clear();
        if(view!=null)foreach(var ground in view.Grounds)
        {
            // 凹岸与凸岸都按相邻公开陆地中心求朝海方向；不以三顶点弯曲方向猜测。
            var b=position(ground.VertexIds[1]);
            var land=tiles.Where(t=>t.Resource!="sea"&&t.Resource!="water"&&t.Resource!="fog"&&t.Resource!="hidden"&&t.Vertices.Contains(ground.VertexIds[1])&&(t.Vertices.Contains(ground.VertexIds[0])||t.Vertices.Contains(ground.VertexIds[2]))).ToArray();
            if(land.Length==0)continue;
            var inland=Vector3.zero;foreach(var tile in land)inland+=position(tile.Id);inland/=land.Length;
            var outward=b-inland;outward.y=0;outward.Normalize();
            var center=Place(b,outward,tiles,position,ports);occupied.Add(center);center.y=EntityBoardAssets.BoardSurfaceHeight+.0005f;
            string key=ground.Id+":"+ground.Number;retained.Add(key);
            if(!grounds.TryGetValue(key,out var instance))
            {
                instance=EntityBoardAssets.ReferenceMarker(Root+"ground-"+ground.Number,"渔场 "+ground.Id,center,Quaternion.identity,transform);
                if(instance==null)continue;grounds.Add(key,instance);
            }
            instance.transform.position=center;
        }
        var removed=new List<string>();foreach(var pair in grounds)if(!retained.Contains(pair.Key)){Destroy(pair.Value);removed.Add(pair.Key);}
        foreach(var key in removed)grounds.Remove(key);
    }
}
