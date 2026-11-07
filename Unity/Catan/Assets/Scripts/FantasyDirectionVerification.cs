using System;
using System.Collections;
using System.Linq;
using Catan.Core;
using UnityEngine;
using M5=Catan.Core.M5;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyFantasyDirections(M5BoardRenderer board,M5.PlayerView view)
    {
        var state=view.Board;
        var land=state.Tiles.First(t=>t.Resource=="wood" && t.Vertices.Length==6);
        var ports=board.ReferenceMarkers.PublicPortPositions;
        var sea=state.Tiles.Where(t=>t.Resource=="sea" && t.Vertices.Length==6)
            .OrderByDescending(t=>ports.Min(p=>(p-board.Position(t.Id)).sqrMagnitude)).First();
        var empty=Array.Empty<PiecePlacement>();
        var seaCenter=board.Position(sea.Id);
        void Detail(Vector3 point)
        {
            FocusDetail(point,false);
            ScrollBoard(-4,false,BoardCamera.pixelRect.center);
        }
        void Project(PiecePlacement[] roads,M5.Metropolis[] metro,M5.Knight[] knights,string robber=null,string merchant=null,string pirate=null)
        {
            board.RefreshProjectedPieces(empty,roads,empty,robber,empty,knights,metro,merchant,
                Array.Empty<M5.ShipPlacement>(),pirate);
        }
        var noMetro=Array.Empty<M5.Metropolis>();
        var noKnights=Array.Empty<M5.Knight>();
        foreach(var s in state.Ships)
        {
            var live=board.CommittedPieces.Cast<Transform>().Single(t=>t.name.StartsWith("Ship "+s.LocationId+" [",StringComparison.Ordinal));
            board.PreviewPiece(s.LocationId,new TabletopPointerPreview{Kind="ship"},s.PlayerId,true);
            var preview=board.VerificationPointerPreview;
            Require(preview.GetComponent<FantasyBoardPiece>().Frame==live.GetComponent<FantasyBoardPiece>().Frame &&
                Vector3.Distance(preview.transform.position,live.position)<.00002f,"船只预览与实物的朝向或泊位不一致");
            var wake=board.CommittedPieces.GetComponentsInChildren<ShipWake>().Single(w=>w.name=="船尾浪 "+s.LocationId);
            var wakeVertices=wake.GetComponent<MeshFilter>().sharedMesh.vertices;
            Require(Vector3.Dot((wakeVertices[0]+wakeVertices[1])*.5f-live.position,live.forward)<-.004f,"实际尾浪未接船艉");
            board.ClearPreview();
        }
        Project(empty,noMetro,noKnights);
        var stand=new GameObject("六向投影验收");
        stand.transform.SetParent(board.transform,false);
        var names=new[]{"北","东北","东南","南","西南","西北"};
        for(int i=0;i<6;i++)
        {
            var rotation=Quaternion.Euler(0,60*i,0);
            var loc=seaCenter+Vector3.up*EntityBoardAssets.BoardSurfaceHeight;
            var ship=EntityBoardAssets.Ship("方向船",BoardRenderer.SeatColors[1],EntityBoardAssets.ShipWaterline(loc),rotation,stand.transform);
            var ink=ship.GetComponent<FantasyBoardPiece>();
            Require(ink.Frame==i,"船首方向错误："+names[i]);
            Require(ink.transform.Find("幻想棋子画面").up.y>.999f,"水面投影倾斜");
            var road=PlayerComponentAssets.Road("P2","方向路",loc,rotation,stand.transform);
            Require(road.GetComponent<FantasyBoardPiece>().Frame==i,"道路方向错误："+names[i]);
            var roadPlane=road.transform.Find("幻想棋子画面");
            Require(Vector3.Dot(roadPlane.right,rotation*Vector3.forward)>.999f,"道路两端与边不共线");
            road.SetActive(false);Destroy(road);
            Detail(loc);
            yield return new WaitForEndOfFrame();SaveOceanFrame("同泊位-"+names[i]+".png");
            ship.SetActive(false);Destroy(ship);
        }
        // 沿真实海格的六边顺时针摆放，检验相对岸线和各条船的艉部。
        for(int i=0;i<6;i++)
        {
            var a=board.Position(sea.Vertices[i]);var b=board.Position(sea.Vertices[(i+1)%6]);
            var edgeId=state.Edges.Single(e=>e.Vertices.Contains(sea.Vertices[i])&&e.Vertices.Contains(sea.Vertices[(i+1)%6])).Id;
            var center=board.ShipPosition(edgeId);
            var ship=EntityBoardAssets.Ship("航向对照",BoardRenderer.SeatColors[1],center,Quaternion.LookRotation(b-a),stand.transform);
            var aft=center+(a-b)*.5f;
            ShipWake.Create(stand.transform,"对照"+i,Table.Ocean.WakeMaterial,center,aft,aft);
            Require(Vector3.Dot(ship.transform.forward,(b-a).normalized)>.999f,"船体与航段不共线");
        }
        Detail(seaCenter);
        yield return new WaitForEndOfFrame();SaveOceanFrame("六向商船-棋盘投影.png");
        stand.SetActive(false);Destroy(stand);
        var ring=land.Vertices;
        var metro=Enumerable.Range(0,3).Select(i=>new M5.Metropolis{Track=(M5.ImprovementTrack)i,
            LocationId=ring[i*2],PlayerId="P"+(i+1)}).ToArray();
        var roads=metro.Select((m,i)=>new PiecePlacement{PlayerId=m.PlayerId,LocationId=state.Edges
            .Single(e=>e.Vertices.Contains(m.LocationId)&&e.Vertices.Contains(ring[i*2+1])).Id}).ToArray();
        Project(roads,metro,noKnights);
        foreach(var m in metro)
        {
            var piece=board.CommittedPieces.Cast<Transform>().Single(p=>p.name.StartsWith("Metropolis "+m.Track+" ",StringComparison.Ordinal));
            Require(piece.GetComponent<FantasyBoardPiece>().Frame==FantasyPieceFrames.Heading(piece.forward),"大都会门向错误");
            board.PreviewPiece(m.LocationId,new TabletopPointerPreview{Kind="metropolis",Track=(int)m.Track,Wall=true},m.PlayerId,true);
            var preview=board.VerificationPointerPreview;
            Require(preview.GetComponentInChildren<FantasyBoardPiece>().Frame==piece.GetComponent<FantasyBoardPiece>().Frame &&
                preview.GetComponentsInChildren<MeshFilter>().Count(f=>f.name!="幻想棋子画面")==2,"带城墙大都会预览与城门错位");
            board.ClearPreview();
        }
        Detail(board.Position(land.Id));
        yield return new WaitForEndOfFrame();SaveOceanFrame("三类大都会-棋盘投影.png");
        var knights=Enumerable.Range(0,6).Select(i=>new M5.Knight{LocationId=ring[i],PlayerId="P2",Level=i%3+1,Active=i>=3}).ToArray();
        Project(empty,noMetro,knights);
        var frames=board.CommittedPieces.GetComponentsInChildren<FantasyBoardPiece>().Select(p=>p.Frame);
        Require(frames.Distinct().Count()==6,"骑士六种状态未完整显示");
        Detail(board.Position(land.Id));
        yield return new WaitForEndOfFrame();SaveOceanFrame("骑士三级与状态-棋盘投影.png");
        var other=state.Tiles.Where(t=>t.Vertices.Contains(ring[0])&&t.Id!=land.Id&&t.Resource!="sea").FirstOrDefault()
            ??state.Tiles.Where(t=>t.Id!=land.Id&&t.Resource!="sea"&&t.Vertices.Length==6).OrderBy(t=>(board.Position(t.Id)-board.Position(land.Id)).sqrMagnitude).First();
        Project(empty,noMetro,noKnights,land.Id,other.Id,sea.Id);
        foreach(string kind in new[]{"robber","merchant","pirate"})
            Require(board.CommittedPieces.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled&&r.sharedMaterial.name=="幻想棋子-"+kind+"-neutral"),kind+"画稿未启用");
        Detail((board.Position(land.Id)+board.Position(other.Id))*.5f);
        yield return new WaitForEndOfFrame();SaveOceanFrame("强盗与商人-棋盘投影.png");
        Detail(seaCenter);
        yield return new WaitForEndOfFrame();SaveOceanFrame("海盗船-棋盘投影.png");
        board.PreviewPiece(sea.Id,new TabletopPointerPreview{Kind="pirate"},"P1",true);
        var raider=board.CommittedPieces.Cast<Transform>().Single(p=>p.name.StartsWith("Pirate ",StringComparison.Ordinal));
        Require(Vector3.Distance(board.VerificationPointerPreview.transform.position,raider.position)<.00002f &&
            Quaternion.Angle(board.VerificationPointerPreview.transform.rotation,raider.rotation)<.01f,"海盗预览未对齐实物");
        board.ClearPreview();
        board.Refresh(view);
    }
}
