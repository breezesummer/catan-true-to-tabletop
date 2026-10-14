using System.Collections.Generic;
using System.Linq;
using Catan.Core;
using UnityEngine;

// 点数来自公开地图。正面保留规则排字，背面使用实体照片；闭合厚板参与普通深度测试。
public sealed class TabletopNumberTokens : MonoBehaviour
{
    readonly List<Material> materials=new List<Material>();
    Mesh tokenMesh;
    Dictionary<string,Vector3> positions;
    string signature;
    public static TabletopNumberTokens Create(Transform parent,Tile[] tiles,Dictionary<string,Vector3> positions)
    {
        var root=new GameObject("实体点数牌 v008");root.transform.SetParent(parent,false);
        var tokens=root.AddComponent<TabletopNumberTokens>();tokens.Initialize(tiles,positions);root.SetActive(false);return tokens;
    }
    void Initialize(Tile[] tiles,Dictionary<string,Vector3> positions)
    {
        this.positions=positions;tokenMesh=CreateMesh();Refresh(tiles);
    }
    public void Refresh(Tile[] tiles)
    {
        if(tiles==null)return;
        var visible=tiles.Where(t=>t.Number.HasValue&&t.Number.Value>=2&&t.Number.Value<=12&&t.Number.Value!=7&&
            t.Resource!="desert"&&t.Resource!="fog"&&t.Resource!="hidden"&&t.Resource!="sea"&&t.Resource!="water")
            .OrderBy(t=>t.Id,System.StringComparer.Ordinal).ToArray();
        string next=string.Join("|",visible.Select(t=>t.Id+":"+t.Number.Value));
        if(next==signature)return;
        signature=next;
        // 发明家换点数或探索揭示后才更新。旧对象立即隐藏，延迟释放不会产生重叠的牌面。
        foreach(Transform child in transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        foreach(var material in materials)Destroy(material);
        materials.Clear();
        // 所有点数共用一张圆形厚板网格，数字变化无需重复分配网格；组件销毁时统一释放。
        var edge=BoardRenderer.Material(new Color(.69f,.55f,.31f));edge.name="点数牌纸芯 v008";materials.Add(edge);
        var back=BoardRenderer.Material(Color.white);back.name="点数牌原照背面 v005";
        back.SetTexture("_BaseMap",Resources.Load<Texture2D>("EntityAssets/v005/tabletop-markers/number-token-back"));
        back.SetFloat("_Smoothness",.16f);back.SetFloat("_Metallic",0);materials.Add(back);
        var faces=new Dictionary<int,Material>();
        foreach(var tile in visible)
        {
            int number=tile.Number.Value;
            if(!faces.TryGetValue(number,out var face))
            {
                face=BoardRenderer.Material(Color.white);face.name="点数牌 "+number;
                face.SetTexture("_BaseMap",Resources.Load<Texture2D>("TabletopUI/v008/NumberTokens/number-"+number));
                face.SetFloat("_Smoothness",.16f);face.SetFloat("_Metallic",0);materials.Add(face);faces.Add(number,face);
            }
            var obj=new GameObject("点数 "+number+" · "+tile.Id);obj.transform.SetParent(transform,false);
            obj.transform.position=positions[tile.Id]+Vector3.up*(EntityBoardAssets.BoardSurfaceHeight+.00008f);
            obj.AddComponent<MeshFilter>().sharedMesh=tokenMesh;
            var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{face,edge,back};
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
    static Mesh CreateMesh()
    {
        // 21 mm 直径置于板块中心，远离边与顶点建筑；薄纸牌不会遮住建筑轮廓。
        const int segments=64;const float radius=.0105f,thickness=.00045f;
        var vertices=new List<Vector3>{new Vector3(0,thickness,0)};var uv=new List<Vector2>{new Vector2(.5f,.5f)};
        var face=new List<int>();var edge=new List<int>();var back=new List<int>();
        for(int i=0;i<segments;i++)
        {
            float radians=i*2*Mathf.PI/segments;float x=Mathf.Cos(radians),z=Mathf.Sin(radians);
            vertices.Add(new Vector3(x*radius,thickness,z*radius));uv.Add(new Vector2((x+1)/2,(z+1)/2));
            face.AddRange(new[]{0,(i+1)%segments+1,i+1});
        }
        for(int i=0;i<segments;i++)
        {
            int start=vertices.Count;float a=i*2*Mathf.PI/segments,b=(i+1)*2*Mathf.PI/segments;
            var left=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);var right=new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
            vertices.AddRange(new[]{left,right,left+Vector3.up*thickness,right+Vector3.up*thickness});
            uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one});
            edge.AddRange(new[]{start,start+2,start+3,start,start+3,start+1});
        }
        int bottom=vertices.Count;vertices.Add(Vector3.zero);uv.Add(new Vector2(.5f,.5f));
        for(int i=0;i<segments;i++)
        {
            float radians=i*2*Mathf.PI/segments;float x=Mathf.Cos(radians),z=Mathf.Sin(radians);
            vertices.Add(new Vector3(x*radius,0,z*radius));uv.Add(new Vector2((1-x)/2,(z+1)/2));
            back.AddRange(new[]{bottom,bottom+i+1,bottom+(i+1)%segments+1});
        }
        var mesh=new Mesh{name="点数牌双面闭合厚纸板 v005"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=3;
        mesh.SetTriangles(face,0);mesh.SetTriangles(edge,1);mesh.SetTriangles(back,2);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    void OnDestroy(){foreach(var material in materials)Destroy(material);if(tokenMesh!=null)Destroy(tokenMesh);}
}
