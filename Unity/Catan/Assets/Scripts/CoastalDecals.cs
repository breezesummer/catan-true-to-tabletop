using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 棋盘平面上的画稿。栈桥使用与可见轮廓相同的两段拾取面，浮网没有碰撞体。
public static class CoastalDecals
{
    public const string Root = PaintedMap.Root + "Coast/";
    public const float DockLength=.023f;
    public const float DockInset=.0016f;
    static readonly Dictionary<string,Material> inks = new Dictionary<string,Material>();
    static Mesh dock, rectangle;
    static Material Ink(string name)
    {
        if (!inks.TryGetValue(name,out var ink) || ink == null)
        {
            ink = new Material(Resources.Load<Shader>(Root+"Decal")) { mainTexture=Resources.Load<Texture2D>(Root+name) };
            ink.renderQueue=name=="landing"?2999:name.StartsWith("ratio-")?3006:name=="sign-plank"?3005:3004;
            inks[name] = ink;
        }
        return ink;
    }
    static Mesh Surface(string name, Vector3[] points, Vector2[] uv, int[] triangles)
    {
        var mesh = new Mesh { name=name, vertices=points, uv=uv, triangles=triangles };
        mesh.RecalculateBounds();
        return mesh;
    }
    static readonly int[] Quad = {0,1,2,0,2,3};
    static Vector3 XZ(float x,float z) => new Vector3(x,0,z);
    public static Mesh Rectangle()
    {
        if(rectangle==null) rectangle=Surface("平面图签",
            new[]{XZ(-.5f,-.5f),XZ(-.5f,.5f),XZ(.5f,.5f),XZ(.5f,-.5f)},
            new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right},Quad);
        return rectangle;
    }
    static Transform Draw(Transform parent, string texture, Mesh mesh)
    {
        var go=new GameObject(texture);
        go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=Ink(texture);
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        return go.transform;
    }
    public static GameObject Dock(string resource, string label, Vector3 bottom, Quaternion rotation, Transform parent)
    {
        if(dock==null)
        {
            const float width=.0175f;
            var uv=new[]{new Vector2(0,0),new Vector2(0,.657f),new Vector2(1,.657f),new Vector2(1,0),
                new Vector2(.304f,.657f),new Vector2(.304f,1),new Vector2(.696f,1),new Vector2(.696f,.657f)};
            var points=new Vector3[uv.Length];
            for(int i=0;i<uv.Length;i++)points[i]=XZ((uv[i].x-.5f)*width,(uv[i].y-.5f)*DockLength);
            dock=Surface("栈桥与平台",points,uv,new[]{0,1,2,0,2,3,4,5,6,4,6,7});
        }
        string name=resource.Substring(resource.LastIndexOf('/')+1);
        var root=new GameObject(label).transform;
        root.SetParent(parent,false);
        root.SetPositionAndRotation(bottom,rotation);
        Draw(root,name,dock);
        var sign=Draw(root,name=="port-generic"?"ratio-3":"ratio-2",Rectangle());
        sign.localScale=new Vector3(.0125f,1,.00735f);
        sign.localPosition=new Vector3(.015f,.00018f,-.004f);
        sign.rotation=Quaternion.identity;
        var landing=Draw(root,"landing",Rectangle());
        landing.name="接岸石滩";
        landing.localScale=new Vector3(.010f,1,.010f);
        landing.localPosition=new Vector3(0,-.0006f,DockLength*.5f);
        var shadow=Draw(root,name,dock);
        shadow.name="栈桥投影";
        string shadowKey=name+"-shadow";
        if(!inks.TryGetValue(shadowKey,out var shadowInk)||shadowInk==null)
        {
            shadowInk=new Material(Ink(name));
            shadowInk.SetColor("_Tint",new Color(.015f,.04f,.035f,.32f));
            shadowInk.renderQueue=3003;inks[shadowKey]=shadowInk;
        }
        shadow.GetComponent<Renderer>().sharedMaterial=shadowInk;
        shadow.localPosition=new Vector3(.0007f,-.001f,-.0007f);
        var support=Draw(root,"sign-plank",Rectangle());
        support.name="比例牌托板";
        support.localScale=new Vector3(.0142f,1,.0088f);
        AlignSign(root);
        return root.gameObject;
    }
    public static void AlignSign(Transform root)
    {
        var sign=root.GetChild(1);
        sign.rotation=Quaternion.identity;
        var side=root.right;
        float plateHalf=.00625f*Mathf.Abs(side.x)+.003675f*Mathf.Abs(side.z);
        // 侧牌靠平台外端，给海岸两端的村庄与城市留出净空。
        sign.position=root.TransformPoint(new Vector3(.00875f+plateHalf+.00015f,.00018f,-.009f));
        var support=root.Find("比例牌托板");
        if(support!=null)support.SetPositionAndRotation(sign.position-Vector3.up*.00008f,Quaternion.identity);
    }

}
