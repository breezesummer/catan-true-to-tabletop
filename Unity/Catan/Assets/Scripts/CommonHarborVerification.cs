using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyCommonHarbor(string[] args)
    {
        mode="combined";
        int save=Array.IndexOf(args,"--common-port")+1;
        if(save<args.Length && !args[save].StartsWith("-")) app.ReadSave(args[save]);
        else app.StartGame(mode,"through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        yield return null;
        yield return null;
        for(int i=0;i<900 && !Table.Ocean.MaskReady;i++) yield return null;
        Require(Table.Ocean.MaskReady && app.PlayerCount==4 && app.ThreeKingdoms && app.Fishing && app.Harbormaster &&
            app.ScenarioId=="through-the-desert","商港验收棋盘或水面未就绪");
        var markers=Table.transform.parent.GetComponentInChildren<TabletopReferenceMarkers>();
        var ports=GetComponent<M5App>().M6View.Board.Ports.Where(p=>!p.Resource.HasValue).ToArray();
        Require(ports.Length>0,"缺少通用贸易港");
        string initial=Authority;
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForEndOfFrame();
        foreach(var port in ports)
        {
            var root=markers.PortObject(port.Id).transform;
            var midpoint=(Position(port.Vertices[0])+Position(port.Vertices[1]))*.5f;
            var tile=Tiles.First(t=>PaintedMap.Region(t.Resource)>=0 && PaintedMap.Region(t.Resource)<8 && port.Vertices.All(t.Vertices.Contains));
            var land=Vector3.ProjectOnPlane(Position(tile.Id)-midpoint,Vector3.up).normalized;
            float shore=Vector3.Dot(midpoint-root.position,root.forward);
            Require(Vector3.Dot(root.forward,land)>.99f,"商港后墙未朝向陆地");
            var sign=root.GetComponentInChildren<HarborSign>();
            Require(sign && sign.name=="TradePlaque" && sign.PickVolume && sign.Footprint,"交易牌或拾取体缺失");
            var plaque=root.InverseTransformPoint(sign.Face.transform.TransformPoint(sign.Face.localBounds.center));
            Require(Mathf.Abs(plaque.x-.00433f)<.00015f && Mathf.Abs(plaque.y-.00865f)<.00025f &&
                Mathf.Abs(plaque.z-.00285f)<.00025f,"3:1 铭牌未贴在仓门上方的临海屋坡 "+plaque.ToString("F5"));
            var material=root.Find("HarborHouse").GetComponent<Renderer>().sharedMaterial;
            Require(material.name=="CommonHarbor" && material.shader.isSupported &&
                material.GetTexture("_BaseMap") && material.GetTexture("_BumpMap") && material.GetTexture("_MetallicGlossMap"),"商港表面贴图未绑定");
            var surfaces=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled && (r==sign.Face || r.sharedMaterial==material)).ToArray();
            Require(surfaces.Length==5 && root.GetComponentsInChildren<Collider>().Length==0,"商港绘制或碰撞组件异常");
            var shape=CoastalDecals.Footprint(root).SelectMany(t=>t).Select(root.InverseTransformPoint).ToArray();
            float back=shape.Max(p=>p.z), tip=shape.Min(p=>p.z);
            var edge=Edges.Single(e=>port.Vertices.All(e.Vertices.Contains));
            Debug.Log($"CATAN_COMMON_HARBOR {edge.Id} 后檐出挑={(back-shore)*1000:F2}mm 岸至栈桥={(shore-tip)*1000:F2}mm");
            // 石砌后墙贴岸，最靠陆的轮廓是出挑 0.42 毫米的后檐。
            Require(Mathf.Abs(back-shore-.00042f)<.00012f && Mathf.Abs(tip-CoastalDecals.PlatformEnd(null))<.0001f,
                "商港后檐与岸边或栈桥与泊位没有接合");
            var house=root.Find("HarborHouse").GetComponent<Renderer>();
            var deck=root.Find("PierDeck").GetComponent<Renderer>();
            Require(house.bounds.min.y<TabletopOcean.WaterHeight && deck.bounds.min.y<TabletopOcean.WaterHeight &&
                sign.Face.bounds.min.y>TabletopOcean.WaterHeight,"石基与桥桩或铭牌水位错误");
            foreach(var surface in surfaces)
            {
                var mesh=surface.GetComponent<MeshFilter>().sharedMesh;
                Require(!mesh.isReadable && mesh.subMeshCount==1 && mesh.lodCount>1,"可见网格导入异常 "+surface.name);
                var point=surface.name switch
                {
                    "HarborHouse" => root.TransformPoint(new Vector3(-.00765f,.01562f,.005f)),
                    "HarborCrane" => root.TransformPoint(new Vector3(-.00216f,.00312f,-.00403f)),
                    "DockCargo" => root.TransformPoint(new Vector3(.00664f,.00351f,-.00207f)),
                    "PierDeck" => root.TransformPoint(new Vector3(-.00765f,.00051f,-.010f)),
                    _ => sign.Face.bounds.center
                };
                var pixel=BoardCamera.WorldToScreenPoint(point);
                Require(markers.PickPort(new Vector2(pixel.x,Screen.height-pixel.y),BoardCamera)==port.Id,
                    "点选商港部件未命中 "+surface.name);
            }
        }
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();
        DetailFrame(Vector3.zero,"穿越沙漠-全盘",true);
        for(int i=0;i<ports.Length;i++)
        {
            var root=markers.PortObject(ports[i].Id).transform;
            var center=root.TransformPoint(new Vector3(0,.0063f,-.0004f));
            var eye=(-root.forward+root.right*.40f+Vector3.up*.50f).normalized*.16f;
            DetailFrame(center,"吊楼商港-海侧"+(i+1),detailSize:.0168f,eyeOffset:eye,matchCameraMsaa:true);
            DetailFrame(root.TransformPoint(new Vector3(0,0,-.001f)),"吊楼商港-贴岸"+(i+1),detailSize:.0225f,
                eyeOffset:Vector3.up*.16f,viewUp:root.forward,matchCameraMsaa:true);
            if(i==0) DetailFrame(center,"吊楼商港-棋盘视距",detailSize:.059f,
                eyeOffset:(-root.forward+Vector3.up*2.3f).normalized*.16f,matchCameraMsaa:true);
            if(i==ports.Length-1) DetailFrame(center,"吊楼商港-正面",detailSize:.0168f,
                eyeOffset:(-root.forward+Vector3.up*.23f).normalized*.16f,matchCameraMsaa:true);
        }
        Require(initial==Authority,"观察商港时对局状态发生变化");
        configurations++;
    }
}
