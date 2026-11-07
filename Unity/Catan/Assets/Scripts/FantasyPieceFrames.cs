using System.Collections.Generic;
using UnityEngine;

// 原画每格为 512 像素。位置以格内左上角为原点；棋盘以 +Z 为北。
internal static class FantasyPieceFrames
{
    static readonly Dictionary<(string,int),Mesh> meshes = new Dictionary<(string,int),Mesh>();
    static readonly float pitchSin = Mathf.Sin(TabletopCameraModel.DefaultAngle*Mathf.Deg2Rad);
    internal const float ShipLength = .016f;
    // 在船体水线标定艉、艏。桅顶和龙头高于水面，不能用轮廓包围盒作为中心。
    static readonly Vector2[] shipStern = {new(260,463),new(96,423),new(111,153),new(259,78),new(405,109),new(390,408)};
    static readonly Vector2[] shipBow = {new(260,54),new(438,111),new(427,379),new(259,461),new(63,387),new(86,113)};
    static readonly Vector2[] pirateStern = {new(256,477),new(99,407),new(102,145),new(254,85),new(422,115),new(397,399)};
    static readonly Vector2[] pirateBow = {new(256,48),new(447,101),new(431,373),new(254,466),new(65,380),new(77,113)};
    // 石阶最外端。背面不可见的入口按石基后沿标定；西南城市使用翻面之后的坐标。
    static readonly Dictionary<string,Vector2[]> stairs=new Dictionary<string,Vector2[]> {
        {"village",new Vector2[]{new(257,108),new(388,197),new(353,423),new(254,464),new(107,412),new(116,190)}},
        {"city",new Vector2[]{new(256,117),new(397,189),new(355,399),new(255,437),new(157,399),new(119,192)}},
        {"metro-trade",new Vector2[]{new(256,93),new(403,180),new(403,402),new(255,437),new(100,418),new(109,197)}},
        {"metro-politics",new Vector2[]{new(256,96),new(397,179),new(386,409),new(254,479),new(109,435),new(119,189)}},
        {"metro-science",new Vector2[]{new(256,104),new(391,195),new(365,442),new(255,468),new(99,447),new(119,192)}}
    };

    internal static int Heading(Vector3 forward) =>
        ((Mathf.RoundToInt(Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg/60)%6)+6)%6;

    internal static bool IsBuilding(string kind) =>
        kind=="city" || kind=="village" || kind.StartsWith("metro-",System.StringComparison.Ordinal);

    internal static Mesh Get(string kind,int frame,Texture texture)
    {
        if(meshes.TryGetValue((kind,frame),out var mesh))return mesh;
        bool ship=kind=="ship" || kind=="pirate";
        bool building=IsBuilding(kind);
        bool atlas=ship || building || kind=="knights";
        int cell=frame;
        // 城市西南门使用东南门的左右对称面，确保门洞落在接路侧。
        bool mirrored=kind=="city" && frame==4;
        if(mirrored)cell=2;
        float width=kind switch {
            "road"=>.039f, "village"=>.025f, "city"=>.030f, "knights"=>.026f,
            "robber"=>.021f, "merchant"=>.022f, _=>.034f
        };
        float scaleX=width/512, scaleZ=scaleX/pitchSin, shear=0;
        Vector2 pivot=new Vector2(256,building?291:kind=="knights"?323:256);
        if(ship)
        {
            Vector2 stern=kind=="ship"?shipStern[frame]:pirateStern[frame];
            Vector2 bow=kind=="ship"?shipBow[frame]:pirateBow[frame];
            pivot=(stern+bow)*.5f;
            Vector2 axis=bow-stern;
            Vector3 span=Quaternion.Euler(0,frame*60,0)*Vector3.forward*(kind=="ship"?ShipLength:.023f);
            if(Mathf.Abs(axis.x)>1)
            {
                scaleX=span.x/axis.x;
                scaleZ=scaleX/pitchSin;
                shear=(span.z+axis.y*scaleZ)/axis.x;
            }
            else
            {
                scaleZ=-span.z/axis.y;
                scaleX=scaleZ*pitchSin;
            }
        }
        else if(building)
        {
            var facing=Quaternion.Euler(0,frame*60,0)*Vector3.forward;
            float radius=kind=="village"?153:166;
            pivot=stairs[kind][frame]-new Vector2(facing.x,-facing.z*pitchSin)*radius;
        }
        else if(!atlas)
        {
            // 道路颜料平铺在地面，短边随相机自然缩短。
            scaleZ=scaleX*texture.height/texture.width/(kind=="road"?1:pitchSin);
        }
        Vector3 Project(float x,float y) =>
            new Vector3((x-pivot.x)*scaleX,0,(pivot.y-y)*scaleZ+(x-pivot.x)*shear);
        var uv=atlas?new Rect((cell%3)/3f,(1-cell/3)/2f,1/3f,.5f):new Rect(0,0,1,1);
        float left=mirrored?uv.xMax:uv.xMin, right=mirrored?uv.xMin:uv.xMax;
        mesh=new Mesh{name="幻想棋子-"+kind+"-"+frame};
        mesh.vertices=new[]{Project(0,512),Project(0,0),Project(512,0),Project(512,512)};
        mesh.uv=new[]{new Vector2(left,uv.yMin),new Vector2(left,uv.yMax),
            new Vector2(right,uv.yMax),new Vector2(right,uv.yMin)};
        mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
        meshes.Add((kind,frame),mesh);
        return mesh;
    }
}
