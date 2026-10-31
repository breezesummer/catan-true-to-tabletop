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
    public const string PhotoRoot="HarborFishing/v002/";
    public static string Resource(string name)
    {
        bool photo=name=="lake"||name=="old-boot"||name=="fish-1"||name=="fish-2"||name=="fish-3"||name.StartsWith("ground-",StringComparison.Ordinal);
        return (photo?PhotoRoot:Root)+name;
    }
    // 原照板的两组折边分别适配凸岸与凹岸；厚度不随海岸长度缩放。
    public const float CoastInset=.00075f;
    public const float GroundBottom=EntityBoardAssets.BoardSurfaceHeight+.0005f;
    sealed class GroundVisual
    {
        public GameObject Root;
        public Vector3[][] Shores;
    }
    readonly Dictionary<string,GroundVisual> grounds=new Dictionary<string,GroundVisual>();
    string layoutKey;
    readonly HashSet<string> retained=new HashSet<string>();
    static readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
    public static Texture2D Icon(string name)
    {
        if(!icons.TryGetValue(name,out var texture)){texture=Resources.Load<Texture2D>(Resource(name));icons[name]=texture;}
        return texture;
    }
    public static void DrawIcon(Rect area,string name)
    {
        var texture=Icon(name);if(texture!=null)GUI.DrawTexture(area,texture,ScaleMode.ScaleToFit,true);
    }
    public static void Refresh(Transform board,FishingView view,Tile[] tiles,Func<string,Vector3> position)
    {
        var layer=board.GetComponent<FishingTabletopAssets>();
        if(layer==null&&view==null)return;
        if(layer==null)layer=board.gameObject.AddComponent<FishingTabletopAssets>();
        layer.Apply(view,tiles,position);
    }
    GroundVisual CreateGround(FishingGround ground)
    {
        string name="ground-"+ground.Number;
        var root=new GameObject("渔场 "+ground.Id+" ["+name+"]");root.transform.SetParent(transform,false);
        var model=EntityBoardAssets.ReferenceMarker(Resource(name),name,Vector3.zero,Quaternion.identity,root.transform);
        if(model==null){Destroy(root);return null;}
        model.transform.localPosition=Vector3.zero;
        var nodes=model.GetComponentsInChildren<Transform>();
        var shores=new Vector3[2][];
        for(int side=0;side<2;side++)
        {
            shores[side]=new Vector3[3];
            for(int point=0;point<3;point++)
            {
                string prefix="coast-"+(side==0?"notch":"point")+"-"+point;
                // Blender的同名节点可有.001后缀；每个模型内部每个锚点唯一。
                var anchor=nodes.FirstOrDefault(node=>node.name==prefix||node.name.StartsWith(prefix+".",StringComparison.Ordinal));
                if(anchor==null){Debug.LogError("渔港缺少接岸锚点："+name+" / "+prefix);Destroy(root);return null;}
                shores[side][point]=Flat(root.transform.InverseTransformPoint(anchor.position));
            }
        }
        return new GroundVisual{Root=root,Shores=shores};
    }
    static Vector3 Flat(Vector3 value){value.y=0;return value;}
    static bool Land(Tile tile)=>tile.Resource!="sea"&&tile.Resource!="water"&&tile.Resource!="fog"&&tile.Resource!="hidden";
    static Vector3 SeaNormal(string a,string b,Tile[] tiles,Func<string,Vector3> position)
    {
        var tile=tiles.FirstOrDefault(t=>Land(t)&&t.Vertices.Contains(a)&&t.Vertices.Contains(b));
        return tile==null?Vector3.zero:Flat((position(a)+position(b))*.5f-position(tile.Id)).normalized;
    }
    static bool Fit(GroundVisual model,Vector3 a,Vector3 b,Vector3 c,Vector3 outward)
    {
        a=Flat(a);b=Flat(b);c=Flat(c);
        float length=Vector3.Distance(a,b)+Vector3.Distance(b,c),best=float.MaxValue,bestScale=1;
        Vector3 bestPosition=Vector3.zero;Quaternion bestRotation=Quaternion.identity;
        foreach(var shore in model.Shores)for(int reverse=0;reverse<2;reverse++)
        {
            var first=shore[reverse==0?0:2];var middle=shore[1];var last=shore[reverse==0?2:0];
            float source=Vector3.Distance(first,middle)+Vector3.Distance(last,middle);
            if(source<.000001f||length<.000001f)continue;
            float scale=length/source;
            var rotation=Quaternion.AngleAxis(Vector3.SignedAngle(first-middle,a-b,Vector3.up),Vector3.up);
            float error=Vector3.Distance(rotation*((first-middle)*scale),a-b)+Vector3.Distance(rotation*((last-middle)*scale),c-b);
            // 同一折线两侧都能重合，只有纸板主体落在海侧的那组折边可用；不翻转贴图。
            float sea=Vector3.Dot(rotation*(-middle*scale),outward);
            if(error>.0001f||sea<=.000001f||error>=best)continue;
            best=error;bestScale=scale;bestRotation=rotation;
            bestPosition=b-rotation*(middle*scale)-outward*CoastInset;
        }
        if(best==float.MaxValue)return false;
        bestPosition.y=GroundBottom;
        model.Root.transform.SetPositionAndRotation(bestPosition,bestRotation);
        model.Root.transform.localScale=new Vector3(bestScale,1,bestScale);
        return true;
    }
    void Apply(FishingView view,Tile[] tiles,Func<string,Vector3> position)
    {
        string next=view==null?"":string.Join("|",view.Grounds.Select(g=>g.Id+":"+g.Number+":"+string.Join(",",g.VertexIds)))+string.Join("|",tiles.Select(t=>t.Id+":"+t.Resource));
        if(next==layoutKey)return;layoutKey=next;
        retained.Clear();
        if(view!=null)foreach(var ground in view.Grounds)
        {
            var ids=ground.VertexIds;
            var outward=(SeaNormal(ids[0],ids[1],tiles,position)+SeaNormal(ids[1],ids[2],tiles,position)).normalized;
            if(outward.sqrMagnitude<.5f)continue;
            string key=ground.Id+":"+ground.Number;retained.Add(key);
            if(!grounds.TryGetValue(key,out var model))
            {
                model=CreateGround(ground);if(model==null)continue;grounds.Add(key,model);
            }
            // 固定关联的两条海岸边是唯一位置依据；允许在原港口之上显示。
            if(!Fit(model,position(ids[0]),position(ids[1]),position(ids[2]),outward))
                Debug.LogError("渔港接岸失败："+ground.Id+" / "+string.Join(",",ids));
        }
        var removed=new List<string>();foreach(var pair in grounds)if(!retained.Contains(pair.Key)){Destroy(pair.Value.Root);removed.Add(pair.Key);}
        foreach(var key in removed)grounds.Remove(key);
    }
}
