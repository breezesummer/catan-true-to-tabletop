using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 悬浮薄玉牌的静态信息层；数字和概率点只来自当前可见地图。
public sealed class UltraNumberTokens : MonoBehaviour
{
    readonly List<Transform> tokens=new List<Transform>();
    readonly List<Material> materials=new List<Material>();
    Dictionary<string,Vector3> positions;
    string signature;
    Camera boardCamera;
    Mesh plate;
    Material jade,gold;
    Font font;
    public void Initialize(Dictionary<string,Vector3> locations,Camera camera=null)
    {
        positions=locations;boardCamera=camera!=null?camera:Camera.main;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        jade=BoardRenderer.Material(new Color(.90f,.95f,.81f));jade.name="ultra 暖白薄玉";jade.SetFloat("_Smoothness",.38f);
        gold=BoardRenderer.Material(new Color(.71f,.50f,.20f),true);gold.name="ultra 旧金细边";
        materials.Add(jade);materials.Add(gold);plate=RoundedPlate();
    }
    public void Refresh(Tile[] tiles)
    {
        if(tiles==null)return;
        var visible=tiles.Where(t=>t.Number.HasValue&&t.Number>=2&&t.Number<=12&&t.Number!=7&&
            t.Resource!="fog"&&t.Resource!="hidden"&&t.Resource!="sea"&&t.Resource!="water"&&t.Resource!="desert").OrderBy(t=>t.Id,StringComparer.Ordinal).ToArray();
        string next=string.Join("|",visible.Select(t=>t.Id+":"+t.Number));if(next==signature)return;signature=next;
        foreach(var token in tokens){token.gameObject.SetActive(false);Destroy(token.gameObject);}tokens.Clear();
        foreach(var tile in visible)
        {
            int number=tile.Number.Value;var root=new GameObject("薄玉牌 · "+tile.Id+" · "+number).transform;root.SetParent(transform,false);
            root.position=positions[tile.Id]+Vector3.up*.029f;tokens.Add(root);
            Plate(root,"旧金边",gold,1,0);Plate(root,"玉面",jade,.94f,-.00016f);
            var ink=number==6||number==8?new Color(.59f,.10f,.065f):new Color(.21f,.16f,.10f);
            Text(root,number.ToString(),new Vector3(0,.0022f,-.00053f),.013f,ink);
            int probability=6-Math.Abs(7-number);
            Text(root,new string('•',probability),new Vector3(0,-.0058f,-.00053f),.0038f,ink);
        }
        FaceCamera();
    }
    void Plate(Transform root,string label,Material material,float scale,float depth)
    {
        var part=new GameObject(label);part.transform.SetParent(root,false);part.transform.localScale=new Vector3(scale,scale,1);part.transform.localPosition=new Vector3(0,0,depth);
        part.AddComponent<MeshFilter>().sharedMesh=plate;var r=part.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    void Text(Transform root,string label,Vector3 position,float size,Color color)
    {
        var obj=new GameObject(label);obj.transform.SetParent(root,false);obj.transform.localPosition=position;
        var text=obj.AddComponent<TextMesh>();text.font=font;text.fontSize=96;text.characterSize=size*10/96;text.anchor=TextAnchor.MiddleCenter;
        text.alignment=TextAlignment.Center;text.color=color;text.text=label;
        obj.GetComponent<MeshRenderer>().sharedMaterial=font.material;
    }
    void LateUpdate()=>FaceCamera();
    void FaceCamera()
    {
        if(boardCamera==null)return;
        var rotation=boardCamera.transform.rotation;
        // 正交镜头下随缩放有限变化；数字不随地形LOD消失，玉牌没有拾取碰撞。
        float scale=boardCamera.orthographic?Mathf.Clamp(boardCamera.orthographicSize/.255f,.8f,1.15f):1;
        foreach(var token in tokens){token.rotation=rotation;token.localScale=Vector3.one*scale;}
    }
    static Mesh RoundedPlate()
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        const int count=24;const float halfX=.0078f,halfY=.010f,radius=.0018f;
        for(int side=0;side<2;side++)for(int i=0;i<count;i++)
        {
            int corner=i/6;float a=(corner*90+i%6*18)*Mathf.Deg2Rad;
            float x=(corner==0||corner==3?1:-1)*(halfX-radius)+Mathf.Cos(a)*radius;
            float y=(corner<2?1:-1)*(halfY-radius)+Mathf.Sin(a)*radius;
            vertices.Add(new Vector3(x,y,side==0?-.0003f:.0003f));
        }
        for(int i=1;i<count-1;i++){triangles.AddRange(new[]{0,i+1,i,count,count+i,count+i+1});}
        for(int i=0;i<count;i++){int j=(i+1)%count;triangles.AddRange(new[]{i,j,count+j,i,count+j,count+i});}
        var mesh=new Mesh{name="薄玉牌闭合圆角网格"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    void OnDestroy(){foreach(var material in materials)Destroy(material);if(plate!=null)Destroy(plate);}
}
