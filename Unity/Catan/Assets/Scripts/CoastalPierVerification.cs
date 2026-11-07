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
            var cargo=root.Find("Cargo_"+(port.Resource?.ToString().ToLowerInvariant()??"generic"));
            var filter=cargo.GetComponent<MeshFilter>();
            var finish=cargo.GetComponent<Renderer>().sharedMaterial;
            Require(finish.shader.isSupported && finish.shader.name=="Catan/HarborCargo" &&
                finish.mainTexture!=null && finish.GetTexture("_SurfaceMap")!=null,"货物缺少表面材质");
            var proxy=CoastalDecals.HitMesh(filter.sharedMesh);
            Require(proxy!=filter.sharedMesh && proxy.GetIndexCount(0)<filter.sharedMesh.GetIndexCount(0),"货堆没有独立拾取轮廓");
            var center=BoardCamera.WorldToScreenPoint(cargo.GetComponent<Renderer>().bounds.center);
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
            var piles = root.Find("PierDeck").GetComponent<Renderer>().bounds;
            Require(piles.min.y<TabletopOcean.WaterHeight,"桩底未入水");
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
            SaveOceanFrame("货物-"+kind+".png");
            var cargoRoot=root.Find("Cargo_"+(group.Key?.ToString().ToLowerInvariant()??"generic"));
            var center=cargoRoot.GetComponent<Renderer>().bounds.center;
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
            DetailFrame(center,"材质特写-"+kind,detailSize:.0094f,eyeOffset:eye);
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
        Require(Vector3.Dot(sign.transform.forward,-BoardCamera.transform.forward)>.999f,"玉签没有正对棋盘相机");
        var right=BoardCamera.transform.right;
        var expected=sign.transform.parent.TransformPoint(HarborSign.LocalAnchor)+right*HarborSign.BoardOffset.x
            +Vector3.Cross(right,Vector3.up)*HarborSign.BoardOffset.y;
        Require(Vector3.Distance(sign.transform.position,expected)<.00001f,
            "镜头转动导致玉签偏离本港");
        var rect=TabletopReferenceMarkers.SignBounds(sign.Face,BoardCamera);
        float width=rect.width*BoardCamera.pixelHeight/(2*BoardCamera.orthographicSize);
        Require(width*400/1024>=12,"比例字高低于十二像素");
        return rect;
    }
}
