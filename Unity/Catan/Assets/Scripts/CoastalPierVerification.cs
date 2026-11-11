using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyPierViews(string[] args)
    {
        int flag = Array.IndexOf(args, "--pier-views");
        mode = "combined";
        BoardArtPreference.Select(false, persist: false);
        app.ReadSave(args[flag + 1]);
        yield return null;
        yield return null;
        for (int i=0; i<600 && !Table.Ocean.MaskReady; i++) yield return null;
        Require(Table.Ocean.MaskReady && app.ScenarioId=="through-the-desert" && app.PlayerCount==4
            && app.ThreeKingdoms && app.Harbormaster && app.Fishing,"验收对局配置不符");
        yield return new WaitForSeconds(2);
        var markers = Table.transform.parent.GetComponentInChildren<TabletopReferenceMarkers>();
        var ports = GetComponent<M5App>().M6View.Board.Ports;
        string authority=Authority;
        if (args.Contains("--wood-berth"))
        {
            var board=GetComponent<M5App>().VerificationBoard;
            var port=ports.Single(p=>p.Resource==Catan.Core.Resource.Wood);
            var edge=Edges.Single(e=>port.Vertices.All(e.Vertices.Contains));
            Require(Mathf.Abs(EntityBoardAssets.ShipLength-CoastalDecals.PierModelLength)<.0002f,"船长与栈桥实长不符");
            var ship=GetComponent<M5App>().M6View.Board.Ships.SingleOrDefault(s=>s.LocationId==edge.Id);
            Require(ship!=null,"木材港同边缺少实船 "+edge.Id);
            var root=markers.PortObject(port.Id).transform;
            var actual=board.CommittedPieces.Cast<Transform>().Single(t=>t.name.StartsWith("Ship "+edge.Id+" [",StringComparison.Ordinal));
            var berth=root.InverseTransformPoint(actual.position);
            Require(Mathf.Abs(berth.x)<.00002f && Mathf.Abs(-berth.z-(CoastalDecals.DockLength-CoastalDecals.ShoreEnd)-ShipCoastLayout.HullBeam*.75f)<.00005f,"商船没有停靠在甲板外沿正中");
            Require(Mathf.Abs(Vector3.Dot(actual.forward,root.forward))<.01f,"船身未沿岸摆放");
            var wake=board.CommittedPieces.Find("船尾浪 "+edge.Id).GetComponent<MeshFilter>();
            var strip=wake.sharedMesh.vertices;
            var wakeStart=wake.transform.TransformPoint((strip[0]+strip[1])*.5f);
            Require(Mathf.Abs(Vector3.Dot(actual.forward,wakeStart-actual.position)+EntityBoardAssets.ShipLength*.48f)<.0001f,"尾浪未贴近艉部水线");
            var deck=actual.Find("主甲板最低点");
            // 栈桥配方的水平桥板从 0.53 毫米起，高 0.69 毫米。
            float pierY=root.TransformPoint(Vector3.up*.00122f).y;
            Require(deck!=null,"缺少主甲板测点");
            Require(Mathf.Abs(deck.position.y-pierY)<.00005f,"主甲板与港口桥面高度不符");
            Require(deck.position.y>TabletopOcean.WaterHeight&&actual.position.y<TabletopOcean.WaterHeight,"商船吃水异常");
            Debug.Log($"安达卢西亚号甲板 {deck.position.y*1000:F2} 毫米，港口桥面 {pierY*1000:F2} 毫米");
            foreach(string key in new[]{"P1","P2","P3","P4","pirate"})
            {
                var resource=key=="pirate"?EntityBoardAssets.PirateResource:EntityBoardAssets.ShipResource(key);
                var model=Instantiate(Resources.Load<GameObject>(resource),Vector3.zero,Quaternion.identity);
                var bounds=model.GetComponentInChildren<MeshRenderer>().bounds;
                Destroy(model);
                Require(Mathf.Abs(Mathf.Max(bounds.size.x,bounds.size.z)-(key=="pirate"?EntityBoardAssets.PirateLength:EntityBoardAssets.ShipLength))<.00005f,"船长与港口比例不符 "+key);
            }
            var sign=root.GetComponentInChildren<HarborSign>();
            var center=(actual.position+sign.Face.bounds.center)*.5f;
            var sea=-root.forward;
            var size=.021f;
            // 两帧从海侧看向岸上，保持画幅一致，让斜视帧里的船帆位于贸易牌前方。
            DetailFrame(center,"木材港靠泊-俯视",detailSize:size,eyeOffset:Vector3.up*.16f,viewUp:root.forward);
            if(args.Contains("--pier-shadow-probe"))
            {
                var pier=root.Find("PierDeck").GetComponent<Renderer>();
                var cast=pier.shadowCastingMode;
                try
                {
                    pier.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    DetailFrame(center,"栈桥投影对照-关闭",detailSize:size,eyeOffset:Vector3.up*.16f,viewUp:root.forward);
                }
                finally { pier.shadowCastingMode=cast; }
            }
            DetailFrame(center,"木材港靠泊-斜视45度",detailSize:size,eyeOffset:(sea+Vector3.up).normalized*.16f);
            DetailFrame(center,"木材港靠泊-低角度",detailSize:size,eyeOffset:(sea+Vector3.up*.22f).normalized*.16f);
            // 从船首外舷看帆腹和护舷，保留栈桥作为尺寸参照。
            var bowView=(actual.forward*.92f+sea*.79f+Vector3.up*.44f).normalized;
            DetailFrame(actual.position+Vector3.up*(EntityBoardAssets.ShipLength*.28f),"木材港靠泊-船首斜视",detailSize:.016f,eyeOffset:bowView*.16f,matchCameraMsaa:true);
            Require(authority==Authority,"靠泊取景改变了权威对局");
            configurations++;
            yield break;
        }
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForEndOfFrame();
        SaveOceanFrame("穿越沙漠-全盘.png");
        var fullView = new List<string>{"货物,屏幕中心X,屏幕中心Y"};
        markers.AlignPortSigns(BoardCamera);
        var signRects=new List<Rect>();
        var signPixels=new List<string>{"货物,屏幕中心X,屏幕中心Y,玉签宽像素,玉签高像素"};
        var pieces=GetComponent<M5App>().VerificationBoard.CommittedPieces.GetComponentsInChildren<Renderer>();
        var slips=Table.transform.parent.GetComponentsInChildren<JadeNumberSlip>().Where(slip=>slip.GetComponent<HarborSign>()==null).ToArray();
        foreach (var port in ports)
        {
            var root = markers.PortObject(port.Id).transform;
            string kind=port.Resource?.ToString()??"通用";
            bool painted=BoardArtPreference.Painted;
            Require(root.Find("PierDeck")!=null && !root.Cast<Transform>().Any(t=>t.name.StartsWith("Cargo_")),
                "港口未使用空甲板 "+kind);
            var center=BoardCamera.WorldToScreenPoint(root.GetComponentInChildren<HarborSign>().Face.bounds.center);
            fullView.Add(kind+","+Mathf.RoundToInt(center.x)+","+Mathf.RoundToInt(Screen.height-center.y));
            var sign=root.GetComponentInChildren<HarborSign>();
            Require(sign!=null && sign.Face.sharedMaterial.mainTexture==Resources.Load<Texture2D>(
                CoastalDecals.Root+"sign-"+(port.Resource?.ToString().ToLowerInvariant()??"generic")),"玉签货物图错误");
            var rect=CheckSignFrame(sign);
            Require(!signRects.Any(r=>r.Overlaps(rect)),"全盘玉签相互覆盖");
            foreach (var piece in pieces)
            {
                if (!piece.enabled) continue;
                var silhouette=TabletopReferenceMarkers.SignBounds(piece,BoardCamera);
                var inFront=Vector3.Dot(sign.Face.bounds.center-piece.bounds.center,BoardCamera.transform.forward)<0;
                Require(!inFront || !rect.Contains(silhouette.center),kind+"港玉签盖住棋子中心 "+piece.name);
            }
            Require(!slips.Any(slip=>rect.Contains(TabletopReferenceMarkers.SignBounds(slip.GetComponent<Renderer>(),BoardCamera).center)),
                kind+"港玉签遮住点数");
            signRects.Add(rect);
            var pixel=BoardCamera.WorldToScreenPoint(sign.Face.bounds.center);
            float px=BoardCamera.pixelHeight/(2*BoardCamera.orthographicSize);
            signPixels.Add(kind+","+Mathf.RoundToInt(pixel.x)+","+Mathf.RoundToInt(Screen.height-pixel.y)+","+
                Mathf.RoundToInt(rect.width*px)+","+Mathf.RoundToInt(rect.height*px));
            if(!painted) Require(root.Find("PierDeck").GetComponent<Renderer>().bounds.min.y<TabletopOcean.WaterHeight,"桩底未入水");
            var entry=root.TransformPoint(Vector3.forward*(CoastalDecals.ShoreEnd-CoastalDecals.DockInset));
            var mid=(Position(port.Vertices[0])+Position(port.Vertices[1]))*.5f;
            Require(Vector2.Distance(new Vector2(mid.x,mid.z),new Vector2(entry.x,entry.z))<.0001f,"桥头偏离海岸中点");
        }
        File.WriteAllLines(Path.Combine(output,"全盘货物坐标.csv"),fullView);
        File.WriteAllLines(Path.Combine(output,"全盘玉签坐标.csv"),signPixels);
        var detailPixels=new List<string>{"货物,玉签中心X,玉签中心Y,玉签宽,玉签高,货物中心X,货物中心Y"};
        // 每座港口均以玩家的缩放、旋转和俯角入口验收。
        foreach (var port in ports)
        {
            var root = markers.PortObject(port.Id).transform;
            var sign=root.GetComponentInChildren<HarborSign>();
            FocusDetail(root.position,false);
            var pointer = BoardCamera.pixelRect.center;
            foreach (var pitch in new[]{30,66,90})
            {
                Table.SetPitch(pitch,false);
                for (int i=0; i<24; i++)
                {
                    markers.AlignPortSigns(BoardCamera);
                    CheckSignFrame(sign);
                    var screen=BoardCamera.WorldToScreenPoint(sign.Face.bounds.center);
                    Require(markers.PickPort(new Vector2(screen.x,Screen.height-screen.y),BoardCamera)==port.Id,
                        "旋转后比例牌拾取失败");
                    bool capture=(pitch==30 && i==3)||(pitch==66 && i==0)||(pitch==90 && i==12);
                    if (port.Resource==Catan.Core.Resource.Wood && capture)
                    {
                        yield return new WaitForEndOfFrame();
                        SaveOceanFrame("木材港-"+pitch+"度-方位"+Mathf.RoundToInt(Table.Yaw)+".png");
                    }
                    Table.Wheel(1,true,pointer);
                }
            }
            yield return null;
        }
        foreach (var group in ports.GroupBy(p=>p.Resource))
        {
            var root=markers.PortObject(group.First().Id).transform;
            string kind=group.Key?.ToString()??"通用";
            Table.SetPitch(52,false);
            FocusDetail(root.position,false);
            yield return new WaitForEndOfFrame();
            SaveOceanFrame("港口-"+kind+".png");
            var center=root.GetComponentInChildren<HarborSign>().Face.bounds.center;
            var pixel=BoardCamera.WorldToScreenPoint(center);
            Require(markers.PickPort(new Vector2(pixel.x,Screen.height-pixel.y),BoardCamera)==group.First().Id,
                "货物的拾取轮廓没有命中贸易港");
            var tag=root.GetComponentInChildren<HarborSign>();
            var tagPixel=BoardCamera.WorldToScreenPoint(tag.Face.bounds.center);
            var tagRect=TabletopReferenceMarkers.SignBounds(tag.Face,BoardCamera);
            float px=BoardCamera.pixelHeight/(2*BoardCamera.orthographicSize);
            detailPixels.Add(kind+","+Mathf.RoundToInt(tagPixel.x)+","+Mathf.RoundToInt(Screen.height-tagPixel.y)+","+
                Mathf.RoundToInt(tagRect.width*px)+","+Mathf.RoundToInt(tagRect.height*px)+","+
                Mathf.RoundToInt(pixel.x)+","+Mathf.RoundToInt(Screen.height-pixel.y));
            foreach(float offset in new[]{-.29f,.21f})
            {
                var inkPixel=BoardCamera.WorldToScreenPoint(tag.Face.bounds.center+BoardCamera.transform.up*(tagRect.height*offset));
                Require(markers.PickPort(new Vector2(inkPixel.x,Screen.height-inkPixel.y),BoardCamera)==group.First().Id,
                    "点击资源图或比例未命中对应贸易港");
            }
            // 用同一场景的棋盘相机放大货堆，另存材质特写；玩家视距由上面的整窗图记录。
            var eye=(-root.forward-root.right*.78f+Vector3.up*1.45f).normalized*.16f;
            DetailFrame(center,"甲板特写-"+kind,detailSize:.022f,eyeOffset:eye);
            Table.SetPitch(90,false);
            yield return new WaitForEndOfFrame();
            SaveOceanFrame("俯视-"+kind+".png");
        }
        File.WriteAllLines(Path.Combine(output,"近景玉签坐标.csv"),detailPixels);
        Require(authority==Authority,"旋转与拾取改变了权威对局");
        configurations++;
    }

    Rect CheckSignFrame(HarborSign sign)
    {
        Require(sign.OnDeck && Vector3.Dot(sign.transform.forward,Vector3.up)>.999f,"贸易牌没有平放");
        var center=sign.transform.parent.InverseTransformPoint(sign.Face.bounds.center);
        Require(Mathf.Abs(center.x)<.00001f && Mathf.Abs(center.z-HarborSign.DeckCenterZ)<.00001f &&
            Mathf.Abs(sign.Face.bounds.min.y-sign.transform.parent.position.y-HarborSign.DeckHeight)<.00001f,
            "贸易牌偏离甲板");
        return TabletopReferenceMarkers.SignBounds(sign.Face,BoardCamera);
    }
}
