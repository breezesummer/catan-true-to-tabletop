using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyOrePort(string[] args)
    {
        mode="combined";
        int save=Array.IndexOf(args,"--ore-port")+1;
        if (save<args.Length && !args[save].StartsWith("-")) app.ReadSave(args[save]);
        else app.StartGame(mode,"through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        yield return null;
        yield return null;
        for (int frame=0;!Table.Ocean.MaskReady && frame<900;frame++) yield return null;
        Require(Table.Ocean.MaskReady && app.PlayerCount==4 && app.ThreeKingdoms && app.Harbormaster &&
            app.Fishing && app.ScenarioId=="through-the-desert","矿石港四人验收对局未就绪");
        var markers=Table.transform.parent.GetComponentInChildren<TabletopReferenceMarkers>();
        var port=GetComponent<M5App>().M6View.Board.Ports.First(p=>p.Resource==Catan.Core.Resource.Ore);
        var root=markers.PortObject(port.Id).transform;
        var sign=root.GetComponentInChildren<HarborSign>();
        Require(sign && sign.name=="TradePlaque" && sign.PickVolume && sign.Footprint,"屋面矿徽或点选轮廓缺失");
        var mat=sign.Face.sharedMaterial;
        Require(mat.name=="OreHarbor" && mat.IsKeywordEnabled("_ORE_WHARF") && mat.shader.isSupported && mat.GetTexture("_BaseMap") &&
            mat.GetTexture("_BumpMap") && mat.GetTexture("_MetallicGlossMap"),"矿石港表面贴图缺失");
        foreach (string name in new[]{"HarborHouse","TradePlaque","PierDeck","OreCargo","LoadingCrane","OreSling"})
        {
            var part=root.Find(name);
            Require(part && part.GetComponent<Renderer>().sharedMaterial==mat,"矿港分件材质错误 "+name);
            var mesh=part.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh.subMeshCount==1 && !mesh.isReadable && mesh.lodCount>1 &&
                mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1),"矿港网格细节或天光数据错误 "+name);
        }
        Require(root.GetComponentsInChildren<Collider>().Length==0,"港口遮挡棋子操作");
        var shore=root.InverseTransformPoint((Position(port.Vertices[0])+Position(port.Vertices[1]))*.5f);
        var deck=root.Find("PierDeck").GetComponent<Renderer>();
        var house=root.Find("HarborHouse").GetComponent<Renderer>();
        var sling=root.Find("OreSling").GetComponent<Renderer>();
        var extent=CoastalDecals.Footprint(root).SelectMany(t=>t).Select(root.InverseTransformPoint).ToArray();
        Require(Mathf.Abs(shore.x)<.00005f && extent.Max(p=>p.z)>shore.z && extent.Max(p=>p.z)<shore.z+.0005f,
            "仓后栈板没有贴紧本港的实际岸线");
        Require(Mathf.Abs(extent.Min(p=>p.z)-CoastalDecals.PlatformEnd(port.Resource))<.00005f,
            "原矿栈桥外端与玩家商船泊位不一致");
        Require(deck.bounds.min.y<TabletopOcean.WaterHeight-.001f &&
            house.bounds.min.y>TabletopOcean.WaterHeight && sling.bounds.min.y>root.position.y+.0009f,
            "矿港桩脚吃水、仓脚或起吊高度异常");
        var contact=new MaterialPropertyBlock();
        root.Find("栈桥水影").GetComponent<Renderer>().GetPropertyBlock(contact);
        Require(contact.GetFloat("_OreWharf")==1,"矿港入水桩的水影错误");
        string authority=Authority;
        var original=sign.transform.rotation;
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();
        DetailFrame(Vector3.zero,"穿越沙漠-全盘",true);
        var sea=-root.forward;
        var focus=root.TransformPoint(new Vector3(0,.0059f,.0016f));
        DetailFrame(focus,"原矿商港-临海近景",detailSize:.0152f,
            eyeOffset:(sea+root.right*.45f+Vector3.up*.53f).normalized*.16f,matchCameraMsaa:true);
        DetailFrame(focus,"原矿商港-贴岸俯视",detailSize:.016f,eyeOffset:Vector3.up*.16f,
            viewUp:root.forward,matchCameraMsaa:true);
        DetailFrame(focus,"原矿商港-棋盘视距",detailSize:.049f,
            eyeOffset:(sea+Vector3.up*2.1f).normalized*.16f,matchCameraMsaa:true);
        if (args.Contains("--ore-berth"))
        {
            var edge=Edges.Single(e=>port.Vertices.All(e.Vertices.Contains));
            var pieces=GetComponent<M5App>().VerificationBoard.CommittedPieces;
            var ship=pieces.Cast<Transform>().SingleOrDefault(t=>t.name.StartsWith("Ship "+edge.Id+" [",StringComparison.Ordinal));
            Require(ship!=null,"本次泊位验收需要矿港同边的商船");
            var moored=root.InverseTransformPoint(ship.position);
            Require(Mathf.Abs(moored.x)<.00005f &&
                Mathf.Abs(moored.z-CoastalDecals.PlatformEnd(port.Resource)+ShipCoastLayout.HullBeam*.75f)<.00005f &&
                Mathf.Abs(Vector3.Dot(ship.forward,root.forward))<.01f,"矿港同边商船未沿短桥外端停靠");
            var wake=pieces.Find("船尾浪 "+edge.Id).GetComponent<MeshFilter>();
            var tail=wake.sharedMesh.vertices;
            var wakeStart=wake.transform.TransformPoint((tail[0]+tail[1])*.5f);
            Require(Mathf.Abs(Vector3.Dot(ship.forward,wakeStart-ship.position)+EntityBoardAssets.ShipLength*.48f)<.0001f,
                "矿港商船艉部与尾浪分离");
            DetailFrame(focus+sea*.003f,"原矿商港-商船靠泊",detailSize:.022f,
                eyeOffset:(sea+root.right*.63f+Vector3.up*.86f).normalized*.16f,matchCameraMsaa:true);
        }
        FocusDetail(root.position,false);
        var pointer=BoardCamera.pixelRect.center;
        var targets=new[]{new Vector3(-.00725f,.012f,.002f),new Vector3(.00837f,.0016f,-.00218f),
            new Vector3(-.00232f,.00172f,-.00318f),new Vector3(-.0076f,.00042f,-.0059f)};
        foreach (int pitch in new[]{30,66,90})
        {
            Table.SetPitch(pitch,false);
            for (int side=0;side<4;side++)
            {
                markers.AlignPortSigns(BoardCamera);
                foreach (var point in targets.Select(root.TransformPoint).Append(sign.Face.bounds.center))
                {
                    var pixel=BoardCamera.WorldToScreenPoint(point);
                    Require(markers.PickPort(new Vector2(pixel.x,Screen.height-pixel.y),BoardCamera)==port.Id,
                        "旋转棋盘后无法选中原矿商港");
                }
                for (int i=0;i<6;i++) Table.Wheel(1,true,pointer);
            }
        }
        Require(Quaternion.Angle(original,sign.transform.rotation)<.01f && Authority==authority,
            "点选改变了固定屋面牌或对局状态");
        configurations++;
    }
}
