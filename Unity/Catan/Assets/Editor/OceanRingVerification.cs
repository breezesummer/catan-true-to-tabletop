using System;
using System.Linq;
using Catan.Core.M2;
using UnityEngine;

// 外圈仅为显示：无港口仍完整、重复刷新复用模型、规则状态不变。
public static class OceanRingVerification
{
    public static void Run()
    {
        var game=BaseGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text,4,97);
        var view=game.GetPlayerView("P1");string saved=game.Save();
        var root=new GameObject("海洋外圈检查");
        try
        {
            var points=view.Board.Vertices.ToDictionary(v=>v.Id,v=>new Vector3(v.X*Mathf.Sqrt(3)*.02f,.008f,-v.Y*.02f));
            var markers=TabletopReferenceMarkers.Create(root.transform,view.Board.Tiles,points,view.Board.Edges);
            markers.Refresh(view.Board.Tiles,Array.Empty<TabletopReferenceMarkers.PortDisplay>());
            if(markers.SupplementalOceanTileCount!=18)throw new Exception("标准19格地图必须完整补齐18块海板");
            var instances=root.GetComponentsInChildren<MeshFilter>().Select(m=>m.GetInstanceID()).ToArray();
            for(int i=0;i<30;i++)markers.Refresh(view.Board.Tiles,Array.Empty<TabletopReferenceMarkers.PortDisplay>());
            if(!instances.SequenceEqual(root.GetComponentsInChildren<MeshFilter>().Select(m=>m.GetInstanceID())))throw new Exception("重复刷新重建海板");
            if(root.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("装饰海板阻挡拾取");
            if(game.Save()!=saved)throw new Exception("补海改变规则状态");
            if(Resources.Load<Texture2D>("HarborFishing/Cards/fish-icon")==null)throw new Exception("鱼图标未导入");
            Debug.Log("CATAN_OCEAN_RING_OK: 18块海板、无港口、30次复用、无碰撞体、状态不变、鱼图标导入通过");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
