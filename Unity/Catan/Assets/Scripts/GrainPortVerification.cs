using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyGrainPort(string[] args)
    {
        mode="combined";
        int index=Array.IndexOf(args,"--grain-port")+1;
        if (index<args.Length && !args[index].StartsWith("-")) app.ReadSave(args[index]);
        else app.StartGame(mode,"through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        yield return null;
        yield return null;
        for(int i=0;i<900 && !Table.Ocean.MaskReady;i++)yield return null;
        Require(Table.Ocean.MaskReady && app.PlayerCount==4 && app.ThreeKingdoms && app.Harbormaster &&
            app.Fishing && app.ScenarioId=="through-the-desert","麦港验收棋盘未就绪");
        var markers=Table.transform.parent.GetComponentInChildren<TabletopReferenceMarkers>();
        var port=GetComponent<M5App>().M6View.Board.Ports.First(p=>p.Resource==Catan.Core.Resource.Wheat);
        var root=markers.PortObject(port.Id).transform;
        var sign=root.GetComponentInChildren<HarborSign>();
        Require(sign && sign.name=="TradePlaque" && sign.PickVolume && sign.Footprint,"屋牌或交互简模缺失");
        var mat=root.Find("HarborHouse").GetComponent<Renderer>().sharedMaterial;
        Require(mat.name=="GrainHarbor" && mat.GetTexture("_BaseMap") && mat.GetTexture("_BumpMap") &&
            mat.GetTexture("_MetallicGlossMap"),"麦港表面图集未完整绑定");
        foreach(var name in new[]{"HarborHouse","TradePlaque","PierDeck","GrainCargo","Hoist"})
        {
            var part=root.Find(name);
            Require(part && part.GetComponent<Renderer>().sharedMaterials.Contains(mat),"正式分件缺失 "+name);
            var mesh=part.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh.subMeshCount==(part.name=="TradePlaque" ? 2 : 1) && !mesh.isReadable && mesh.lodCount>1,"网格细节级别错误 "+name);
        }
        Require(root.GetComponentsInChildren<Collider>().Length==0 && root.Find("HarborMast")==null,
            "旧桅杆或碰撞器仍在港内");
        var localShore=root.InverseTransformPoint((Position(port.Vertices[0])+Position(port.Vertices[1]))*.5f);
        float shore=CoastalDecals.ShoreEnd-CoastalDecals.DockInset;
        Require(Mathf.Abs(localShore.x)<.00004f && Mathf.Abs(localShore.z-shore)<.00004f,"谷仓偏离当前海岸");
        var roof=root.Find("HarborHouse").GetComponent<Renderer>();
        var deck=root.Find("PierDeck").GetComponent<Renderer>();
        Require(deck.bounds.min.y<TabletopOcean.WaterHeight-.0008f &&
            roof.bounds.min.y<TabletopOcean.WaterHeight && roof.bounds.max.y>EntityBoardAssets.BoardSurfaceHeight+.014f,
            "仓墙与码头的水线不符");
        var outline=CoastalDecals.Footprint(root).SelectMany(tri=>tri).Select(root.InverseTransformPoint).ToArray();
        Require(Mathf.Abs(outline.Min(p=>p.z)-CoastalDecals.PlatformEnd(port.Resource))<.00005f &&
            outline.Max(p=>p.z)>localShore.z && outline.Max(p=>p.z)<localShore.z+.0013f,
            "挑桥外沿、后檐与岸边未对齐");
        string before=Authority;
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForSecondsRealtime(.8f);
        yield return new WaitForEndOfFrame();
        DetailFrame(Vector3.zero,"穿越沙漠-全盘",true);
        var sea=-root.forward;
        var focus=root.TransformPoint(new Vector3(0,.0058f,.0029f));
        DetailFrame(focus,"谷物商港-临海近景",detailSize:.0146f,
            eyeOffset:(sea+root.right*.43f+Vector3.up*.49f).normalized*.16f,matchCameraMsaa:true);
        DetailFrame(focus,"谷物商港-贴岸俯视",detailSize:.015f,eyeOffset:Vector3.up*.16f,
            viewUp:root.forward,matchCameraMsaa:true);
        DetailFrame(focus,"谷物商港-棋盘视距",detailSize:.048f,
            eyeOffset:(sea+Vector3.up*2.24f).normalized*.16f,matchCameraMsaa:true);
        if(args.Contains("--grain-berth"))
        {
            var edge=Edges.Single(e=>port.Vertices.All(e.Vertices.Contains));
            var board=GetComponent<M5App>().VerificationBoard;
            var ship=board.CommittedPieces.Cast<Transform>().SingleOrDefault(
                t=>t.name.StartsWith("Ship "+edge.Id+" [",StringComparison.Ordinal));
            Require(ship && Mathf.Abs(Vector3.Dot(ship.forward,root.forward))<.01f,"麦港同边商船缺失或船身未沿岸");
            var center=root.InverseTransformPoint(ship.position);
            Require(Mathf.Abs(center.z+ShipCoastLayout.HullBeam*.75f-CoastalDecals.PlatformEnd(port.Resource))<.00005f &&
                Mathf.Abs(center.x)<.00005f,"商船未停在挑桥外沿");
            DetailFrame((focus+ship.position)*.5f,"谷物商港-商船靠泊",detailSize:.023f,
                eyeOffset:(sea+root.right*.34f+Vector3.up).normalized*.16f,matchCameraMsaa:true);
        }
        // 从四个方位、三种俯角点选交易比例；另测货袋、塔楼、吊梁和左挑台。
        var defRotation=sign.transform.rotation;
        var pointer=BoardCamera.pixelRect.center;
        FocusDetail(root.position,false);
        foreach(var angle in new[]{30,66,90})
        {
            Table.SetPitch(angle,false);
            for(int side=0;side<4;side++)
            {
                markers.AlignPortSigns(BoardCamera);
                var points=new[]{sign.Face.bounds.center,
                    root.TransformPoint(new Vector3(-.0076f,.012f,.0031f)),
                    root.TransformPoint(new Vector3(.0071f,.0026f,-.00042f)),
                    root.TransformPoint(new Vector3(-.0041f,.0091f,-.0012f)),
                    root.TransformPoint(new Vector3(-.0081f,.00051f,-.0045f))};
                foreach(var point in points)
                {
                    var p=BoardCamera.WorldToScreenPoint(point);
                    Require(markers.PickPort(new Vector2(p.x,Screen.height-p.y),BoardCamera)==port.Id,
                        "旋转棋盘后点选麦港失败");
                }
                for(int step=0;step<6;step++)Table.Wheel(1,true,pointer);
            }
        }
        Require(Quaternion.Angle(defRotation,sign.transform.rotation)<.01f && Authority==before,
            "取景或点选改变了屋牌朝向或对局状态");
        configurations++;
    }
}

