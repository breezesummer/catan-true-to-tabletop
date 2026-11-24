using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyWoolPort(string[] args)
    {
        mode="combined";
        int save=Array.IndexOf(args,"--wool-port")+1;
        if (save<args.Length && !args[save].StartsWith("-")) app.ReadSave(args[save]);
        else app.StartGame(mode,"through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        yield return null;
        yield return null;
        for (int frame=0;frame<900 && !Table.Ocean.MaskReady;frame++) yield return null;
        Require(Table.Ocean.MaskReady && app.PlayerCount==4 && app.ThreeKingdoms && app.Harbormaster &&
            app.Fishing && app.ScenarioId=="through-the-desert","羊港验收局面或水面未就绪");
        var markers=Table.transform.parent.GetComponentInChildren<TabletopReferenceMarkers>();
        var trade=GetComponent<M5App>().M6View.Board.Ports.First(p=>p.Resource==Catan.Core.Resource.Wool);
        var root=markers.PortObject(trade.Id).transform;
        var sign=root.GetComponentInChildren<HarborSign>();
        Require(sign && sign.name=="TradePlaque" && sign.PickVolume && sign.Footprint,"羊头招牌或交互轮廓未加载");
        var mat=root.Find("HarborHouse").GetComponent<Renderer>().sharedMaterial;
        Require(mat.name=="WoolHarbor" && mat.shader.isSupported && mat.GetTexture("_BaseMap") &&
            mat.GetTexture("_BumpMap") && mat.GetTexture("_MetallicGlossMap"),"羊港材质或贴图未绑定");
        foreach (var part in new[]{"HarborHouse","TradePlaque","PierDeck","Hoist","WoolBales"})
        {
            var ob=root.Find(part);
            var mesh=ob.GetComponent<MeshFilter>().sharedMesh;
            Require(ob.GetComponent<Renderer>().sharedMaterials.Contains(mat) && !mesh.isReadable && mesh.subMeshCount==(ob.name=="TradePlaque" ? 2 : 1) &&
                mesh.lodCount>1,"羊港网格或视距级别错误 "+part);
        }
        var landing=root.Find("PierDeck").GetComponent<Renderer>();
        Require(landing.bounds.min.y<TabletopOcean.WaterHeight && landing.bounds.max.y>TabletopOcean.WaterHeight &&
            root.GetComponentsInChildren<Collider>().Length==0,"入水木桩或装卸面异常");
        var edge=(Position(trade.Vertices[0])+Position(trade.Vertices[1]))*.5f;
        var land=Tiles.First(t=>PaintedMap.Region(t.Resource)>=0 && PaintedMap.Region(t.Resource)<8 &&
            trade.Vertices.All(t.Vertices.Contains));
        Require(Vector3.Dot(root.forward,Vector3.ProjectOnPlane(Position(land.Id)-edge,Vector3.up).normalized)>.98f,
            "羊港后墙未朝向岸线");
        float shore=Vector3.Dot(edge-root.position,root.forward);
        var footprint=CoastalDecals.Footprint(root).SelectMany(t=>t).Select(p=>root.InverseTransformPoint(p).z).ToArray();
        float overlap=footprint.Max()-shore;
        Require(overlap>.0002f && overlap<.001f &&
            Mathf.Abs(footprint.Min()-CoastalDecals.PlatformEnd(trade.Resource))<.0001f,"接岸门槛或商船靠泊边缘错误");
        var state=Authority;
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();
        DetailFrame(Vector3.zero,"穿越沙漠-全盘",true);
        var focus=root.TransformPoint(new Vector3(0,.0059f,.0027f));
        var sea=-root.forward;
        if (args.Contains("--wool-berth"))
        {
            var sameEdge=Edges.Single(e=>trade.Vertices.All(e.Vertices.Contains));
            var pieces=GetComponent<M5App>().VerificationBoard.CommittedPieces;
            var ship=pieces.Cast<Transform>().Single(t=>t.name.StartsWith("Ship "+sameEdge.Id+" [",StringComparison.Ordinal));
            var berth=root.InverseTransformPoint(ship.position);
            Require(Mathf.Abs(berth.x)<.00005f && Mathf.Abs(berth.z-footprint.Min()+ShipCoastLayout.HullBeam*.75f)<.00006f &&
                Mathf.Abs(Vector3.Dot(ship.forward,root.forward))<.01f,"同岸商船未停在羊港桥头");
            DetailFrame(root.TransformPoint(new Vector3(0,.0045f,0)),"羊毛港-商船靠泊",detailSize:.0215f,
                eyeOffset:(sea+root.right*.35f+Vector3.up*1.20f).normalized*.16f,matchCameraMsaa:true);
        }
        DetailFrame(focus,"羊毛港-海侧近景",detailSize:.0139f,
            eyeOffset:(sea+root.right*.44f+Vector3.up*.46f).normalized*.16f,matchCameraMsaa:true);
        DetailFrame(focus,"羊毛港-贴岸俯视",detailSize:.0170f,eyeOffset:Vector3.up*.16f,
            viewUp:root.forward,matchCameraMsaa:true);
        DetailFrame(focus,"羊毛港-棋盘视距",detailSize:.046f,
            eyeOffset:(sea+Vector3.up*2.25f).normalized*.16f,matchCameraMsaa:true);
        FocusDetail(root.position,false);
        var pointer=BoardCamera.pixelRect.center;
        var pose=sign.transform.rotation;
        var targets=new[]{sign.Face.bounds.center,
            root.TransformPoint(new Vector3(-.0082f,.0114f,.0032f)),
            root.TransformPoint(new Vector3(.0091f,.0023f,-.0006f)),
            root.TransformPoint(new Vector3(-.00211f,.0020f,-.00196f)),
            root.TransformPoint(new Vector3(-.0085f,.00014f,-.0047f))};
        foreach (int pitch in new[]{30,66,90})
        {
            Table.SetPitch(pitch,false);
            for (int side=0;side<4;side++)
            {
                foreach (var point in targets)
                {
                    var screen=BoardCamera.WorldToScreenPoint(point);
                    Require(markers.PickPort(new Vector2(screen.x,Screen.height-screen.y),BoardCamera)==trade.Id,
                        "转动视角后未点中羊港");
                }
                for (int step=0;step<6;step++) Table.Wheel(1,true,pointer);
            }
        }
        Require(Quaternion.Angle(pose,sign.transform.rotation)<.01f && Authority==state,"羊港取景或拾取改变了招牌或对局状态");
        configurations++;
    }
}
