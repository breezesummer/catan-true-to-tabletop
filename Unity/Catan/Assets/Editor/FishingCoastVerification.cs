using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using Catan.Core.M2;
using Catan.Core.TradeModules;
using UnityEngine;

// 从实际导入FBX的锚点独立量测两条接岸边，不把“出现六个模型”当作贴合通过。
public static class FishingCoastVerification
{
    [Serializable] public sealed class Record
    {
        public string 模式,渔港,接岸侧;
        public float 最大顶点误差米,最大边长误差米,海侧距离米;
    }
    public static readonly List<Record> Records=new List<Record>();
    public static int GeometryCases {get;private set;}
    public static float GeometryMaxError {get;private set;}
    static readonly string[] sides={"notch","point"};
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static Vector3 Flat(Vector3 p){p.y=0;return p;}
    public static Transform Anchor(Transform root,string side,int index)
    {
        string prefix="coast-"+side+"-"+index;
        return root.GetComponentsInChildren<Transform>().Single(t=>t.name==prefix||t.name.StartsWith(prefix+".",StringComparison.Ordinal));
    }
    public static string Side(Transform root,Vector3 middle)=>sides.OrderBy(side=>Vector3.Distance(Flat(Anchor(root,side,1).position),Flat(middle))).First();

    public static void Reset(){Records.Clear();GeometryCases=0;GeometryMaxError=0;}
    public static void Geometry()
    {
        var game=BaseGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text,4,97,tradeModules:new TradeModuleOptions{Fishing=true});
        var view=game.GetPlayerView("P1");
        string before=game.Save();
        var points=view.Board.Vertices.ToDictionary(v=>v.Id,v=>new Vector3(v.X*Mathf.Sqrt(3)*.02f,.008f,-v.Y*.02f));
        foreach(var tile in view.Board.Tiles)points.Add(tile.Id,tile.Vertices.Select(id=>points[id]).Aggregate(Vector3.zero,(sum,p)=>sum+p)/tile.Vertices.Length);
        var fish=view.TradeModules.Fishing;
        var original=fish.Grounds.Select(g=>g.VertexIds.ToArray()).ToArray();
        var coast=view.Board.Edges.Where(edge=>view.Board.Tiles.Count(tile=>edge.Vertices.All(tile.Vertices.Contains))==1).ToArray();
        var convex=new List<string[]>();
        foreach(string middle in coast.SelectMany(edge=>edge.Vertices).Distinct().OrderBy(id=>id,StringComparer.Ordinal))
        {
            var ends=coast.Where(edge=>edge.Vertices.Contains(middle)).SelectMany(edge=>edge.Vertices).Where(id=>id!=middle).Distinct().ToArray();
            if(ends.Length==2&&view.Board.Tiles.Any(tile=>tile.Vertices.Contains(middle)&&ends.All(tile.Vertices.Contains)))convex.Add(new[]{ends[0],middle,ends[1]});
        }
        Require(convex.Count>=6,"独立凸岸夹具不足");
        var orientations=new HashSet<string>();
        for(int fold=0;fold<2;fold++)foreach(float scale in new[]{.7f,1,1.6f})for(int turn=0;turn<12;turn++)
        {
            for(int i=0;i<fish.Grounds.Length;i++)fish.Grounds[i].VertexIds=fold==0?original[i]:convex[i];
            var rotation=Quaternion.Euler(0,turn*30,0);var offset=new Vector3(.21f,0,-.13f);
            Func<string,Vector3> position=id=>rotation*(points[id]*scale)+offset;
            var root=new GameObject("渔港贴合独立几何案例");
            try
            {
                FishingTabletopAssets.Refresh(root.transform,view.TradeModules.Fishing,view.Board.Tiles,position);
                var rows=Validate("缩放"+scale+"/旋转"+turn,root.transform,view.TradeModules.Fishing,view.Board.Tiles,position,false);
                foreach(var row in rows){GeometryCases++;GeometryMaxError=Mathf.Max(GeometryMaxError,row.最大顶点误差米);orientations.Add(row.接岸侧);}
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        Require(game.Save()==before,"调整独立显示夹具修改了权威规则状态");
        Require(orientations.Count==2,"没有独立覆盖凹岸与凸岸两种接法");
    }

    public static Record[] Validate(string mode,Transform board,FishingView fish,Tile[] tiles,Func<string,Vector3> position,bool retain=true)
    {
        var layer=board.GetComponent<FishingTabletopAssets>();Require(layer!=null,"缺少渔港层");
        var roots=layer.transform.Cast<Transform>().Where(t=>t.name.StartsWith("渔场 ")).ToArray();
        Require(roots.Length==fish.Grounds.Length,"渔港数与公开规则视图不一致");
        var rows=new List<Record>();
        foreach(var ground in fish.Grounds)
        {
            var root=roots.Single(t=>t.name.StartsWith("渔场 "+ground.Id+" "));
            var wanted=ground.VertexIds.Select(id=>Flat(position(id))).ToArray();
            string side=Side(root,wanted[1]);
            var actual=Enumerable.Range(0,3).Select(i=>Flat(Anchor(root,side,i).position)).ToArray();
            if(Vector3.Distance(actual[0],wanted[0])>Vector3.Distance(actual[2],wanted[0]))Array.Reverse(actual);
            float error=Enumerable.Range(0,3).Max(i=>Vector3.Distance(actual[i],wanted[i]));
            Require(error<.0008f,mode+" "+ground.Id+" 没有贴合三个海岸顶点，误差="+error);
            float lengthError=Mathf.Max(Mathf.Abs(Vector3.Distance(actual[0],actual[1])-Vector3.Distance(wanted[0],wanted[1])),Mathf.Abs(Vector3.Distance(actual[1],actual[2])-Vector3.Distance(wanted[1],wanted[2])));
            Require(lengthError<.00001f,"两条接岸边没有自适应长度："+ground.Id);
            var adjacent=tiles.Where(t=>t.Resource!="sea"&&t.Resource!="water"&&t.Resource!="fog"&&t.Resource!="hidden"&&t.Vertices.Contains(ground.VertexIds[1])&&(t.Vertices.Contains(ground.VertexIds[0])||t.Vertices.Contains(ground.VertexIds[2]))).ToArray();
            var land=adjacent.Select(t=>Flat(position(t.Id))).Aggregate(Vector3.zero,(sum,p)=>sum+p)/adjacent.Length;
            var outward=(wanted[1]-land).normalized;
            bool convex=Vector3.Dot(wanted[1]-(wanted[0]+wanted[2])*.5f,outward)>0;
            Require(side==(convex?"notch":"point"),"凹凸岸使用了错误的接岸侧："+ground.Id);
            float sea=Vector3.Dot(Flat(root.position)-wanted[1],outward);
            Require(sea>0,"渔港主体落入陆地："+ground.Id);
            Require(root.localScale.x>0&&root.localScale.z>0,"贴图被镜像翻转");
            Require(root.GetComponentsInChildren<Collider>().Length==0,"装饰阻挡棋盘拾取");
            var bounds=root.GetComponentInChildren<Renderer>().bounds;
            Require(Mathf.Abs(bounds.min.y-FishingTabletopAssets.GroundBottom)<.00001f&&Mathf.Abs(bounds.size.y-.0015f)<.00001f,"海岸缩放改变了纸板厚度");
            rows.Add(new Record{模式=mode,渔港=ground.Id,接岸侧=convex?"凸岸凹口":"凹岸外尖",最大顶点误差米=error,最大边长误差米=lengthError,海侧距离米=sea});
        }
        if(retain)Records.AddRange(rows);return rows.ToArray();
    }

    public static int OverPorts(Transform board)
    {
        var layer=board.GetComponent<FishingTabletopAssets>();
        var ports=board.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("港口 ")&&t.GetComponentInChildren<Renderer>()!=null).ToArray();
        int count=0;
        foreach(Transform ground in layer.transform)
        {
            if(!ground.name.StartsWith("渔场 "))continue;
            var polygon=new[]{Anchor(ground,"notch",0).position,Anchor(ground,"notch",1).position,Anchor(ground,"notch",2).position,Anchor(ground,"point",2).position,Anchor(ground,"point",1).position,Anchor(ground,"point",0).position};
            float top=ground.GetComponentInChildren<Renderer>().bounds.max.y;
            foreach(var port in ports)
            {
                var bounds=port.GetComponentInChildren<Renderer>().bounds;
                if(!Contains(polygon,bounds.center))continue;
                Require(top>bounds.max.y+.0001f,"渔港与港口重叠处没有处于上层");count++;
            }
        }
        return count;
    }
    static bool Contains(Vector3[] polygon,Vector3 point)
    {
        bool inside=false;
        for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
        {
            var a=polygon[i];var b=polygon[j];
            if((a.z>point.z)!=(b.z>point.z)&&point.x<(b.x-a.x)*(point.z-a.z)/(b.z-a.z)+a.x)inside=!inside;
        }
        return inside;
    }
}
