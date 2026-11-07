using System;
using System.Collections;
using System.Linq;
using Catan.Core;
using UnityEngine;
using M5=Catan.Core.M5;

// 只从显式验收入口调用，实局截图和临时四色投影分开保存。
public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyHarborPieces()
    {
        var presentation=GetComponent<M5App>();
        var board=presentation.VerificationBoard;
        var view=presentation.M6View;
        var state=view.Board;
        var ships=state.Ships;
        var normalZoom=Table.ZoomMultiplier;
        Transform Piece(string label)=>board.CommittedPieces.Cast<Transform>().Single(t=>t.name.StartsWith(label+" [",StringComparison.Ordinal));
        var pirateSource=Resources.Load<GameObject>(EntityBoardAssets.PirateResource);
        var pirateMesh=pirateSource.GetComponentInChildren<MeshFilter>().sharedMesh;
        var pirate=Piece("Pirate "+state.PirateTileId);
        Require(pirate.GetComponentInChildren<MeshFilter>().sharedMesh==pirateMesh,"海盗船资源错误");
        var pose=board.ReferenceMarkers.PiratePose(board.Position(state.PirateTileId));
        var pirateBottom=board.Position(state.PirateTileId)+pose.shift+new Vector3(.012f,EntityBoardAssets.BoardSurfaceHeight,0);
        Require(Vector3.Distance(pirate.position,EntityBoardAssets.ShipWaterline(pirateBottom))<.00002f,"海盗船吃水错误");
        var piratePreview=OperationPieceAssets.PointerPreview(new TabletopPointerPreview{Kind="pirate"},"P1",
            board.Position(state.PirateTileId)+pose.shift,Quaternion.identity,null,true,true,pose.facing);
        Require(piratePreview.GetComponentInChildren<MeshFilter>().sharedMesh==pirateMesh&&
            Vector3.Distance(piratePreview.transform.position,pirate.position)<.00002f&&Quaternion.Angle(piratePreview.transform.rotation,pirate.rotation)<.05f,"海盗落点预览与实物不符");
        Destroy(piratePreview);
        foreach(var ship in ships)
        {
            var actual=Piece("Ship "+ship.LocationId);
            Require(Vector3.Distance(actual.position,board.ShipPosition(ship.LocationId))<.00002f,"商船泊位发生偏移");
            Require(Mathf.Abs(actual.position.y-TabletopOcean.WaterHeight+.0004f)<.00002f,"商船吃水错误");
            var projected=BoardCamera.WorldToScreenPoint(actual.position+Vector3.up*.005f);
            Require(Vector2.Distance(new Vector2(projected.x,Screen.height-projected.y),board.ScreenPoint(ship.LocationId))<1,"商船命中点与泊位不符");
        }
        yield return new WaitForSeconds(3);yield return new WaitForEndOfFrame();SaveOceanFrame("实局全盘.png");
        var piratePosition=pirate.position;
        FocusDetail(piratePosition,false);
        yield return new WaitForEndOfFrame();SaveOceanFrame("海盗船-实局.png");
        var bay=(ships.OrderBy(s=>(board.ShipPosition(s.LocationId)-piratePosition).sqrMagnitude)
            .Take(6).Select(s=>board.ShipPosition(s.LocationId)).Aggregate(Vector3.zero,(a,b)=>a+b)+piratePosition)/7;
        FocusDetail(bay,false);
        ScrollBoard(-5,false,BoardCamera.pixelRect.center);
        yield return new WaitForEndOfFrame();SaveOceanFrame("实局海湾航线.png");
        if(Environment.GetCommandLineArgs().Contains("--ship-wakes"))
        {
            Require(app.PlayerCount==4 && app.ScenarioId=="through-the-desert" && app.ThreeKingdoms && app.Harbormaster && app.Fishing,"尾浪验收模式错误");
            var wakes=board.CommittedPieces.GetComponentsInChildren<ShipWake>();
            Require(wakes.Length==ships.Length && wakes.Length>1,"尾浪数量与已占船边不符");
            foreach(var ship in ships)
            {
                var hull=Piece("Ship "+ship.LocationId);
                var wake=wakes.Single(w=>w.name=="船尾浪 "+ship.LocationId);
                var bounds=hull.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                Require(Mathf.Abs(bounds.size.z*hull.lossyScale.z-.01121f)<.0002f,"船身比例不符");
                var strip=wake.GetComponent<MeshFilter>().sharedMesh;
                var stern=(strip.vertices[0]+strip.vertices[1])*.5f;
                Require(Vector3.Dot(stern-hull.position,-hull.forward)>.004f,"尾浪起点不在船艉");
                Require(wake.GetComponent<Collider>()==null && wake.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"尾浪拾取或材质异常");
            }
            board.RefreshSeafarers(ships,state.PirateTileId);
            Require(wakes.All(w=>w.gameObject.activeInHierarchy),"重复刷新重建了尾浪");
            for(int frame=0;frame<12;frame++)
            {
                yield return new WaitForSecondsRealtime(.10f);
                yield return new WaitForEndOfFrame();SaveOceanFrame("尾浪-"+frame.ToString("D2")+".png");
            }
            var blue=ships.Where(s=>s.PlayerId=="P2").Select(s=>board.ShipPosition(s.LocationId)).ToArray();
            Require(blue.Length>=2,"存档缺少蓝帆相邻航段");
            FocusDetail(blue.Aggregate(Vector3.zero,(sum,p)=>sum+p)/blue.Length,false);
            ScrollBoard(-4,false,BoardCamera.pixelRect.center);
            yield return new WaitForEndOfFrame();SaveOceanFrame("蓝帆航段.png");
            board.RefreshSeafarers(ships.Skip(1).ToArray(),state.PirateTileId);
            Require(board.CommittedPieces.GetComponentsInChildren<ShipWake>().Length==ships.Length-1,"移走船后残留尾浪");
            board.Refresh(view);
            Require(board.CommittedPieces.GetComponentsInChildren<ShipWake>().Length==ships.Length,"恢复棋局后尾浪数量错误");
            yield break;
        }
        var dockEdges=Edges.Where(e=>state.Ports.Any(p=>p.Vertices.All(e.Vertices.Contains))).Select(e=>e.Id).ToHashSet();
        var sample=ships.Where(s=>s.PlayerId=="P2").OrderByDescending(s=>dockEdges.Contains(s.LocationId))
            .ThenBy(s=>(board.ShipPosition(s.LocationId)-piratePosition).sqrMagnitude).First();
        FocusDetail(board.ShipPosition(sample.LocationId),false);
        yield return new WaitForEndOfFrame();SaveOceanFrame("商船-实局.png");
        var wall=state.Walls.First(w=>!state.Metropolises.Any(m=>m.LocationId==w.LocationId));
        var target=board.Position(wall.LocationId);
        var cityWall=Piece("City with wall "+wall.LocationId);
        var wallMesh=Resources.Load<GameObject>(PlayerComponentAssets.BuildingResource("city-wall",wall.PlayerId)).GetComponentInChildren<MeshFilter>().sharedMesh;
        Require(cityWall.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh==wallMesh),"带墙城市未使用新城墙");
        var entrance=cityWall.forward;
        FocusDetail(target,false);yield return new WaitForEndOfFrame();SaveOceanFrame("城墙-实局.png");
        FocusDetail(target,true);yield return new WaitForEndOfFrame();SaveOceanFrame("城墙-俯视.png");
        FocusDetail(target,false);
        var cam=BoardCamera;
        var rot=Quaternion.LookRotation(-entrance*Mathf.Cos(36*Mathf.Deg2Rad)-Vector3.up*Mathf.Sin(36*Mathf.Deg2Rad));
        cam.transform.SetPositionAndRotation(target+Vector3.up*.007f-rot*Vector3.forward*.20f,rot);
        yield return new WaitForEndOfFrame();SaveOceanFrame("门洞与院路.png");
        bool Connects(PiecePlacement road,PiecePlacement city)=>road.PlayerId==city.PlayerId&&
            Edges.Any(e=>e.Id==road.LocationId&&e.Vertices.Contains(city.LocationId));
        var roadWall=state.Walls.FirstOrDefault(w=>state.Roads.Any(r=>Connects(r,w)));
        var projectedWall=roadWall==null;
        if(projectedWall)
        {
            roadWall=state.Cities.First(c=>state.Roads.Any(r=>Connects(r,c)));
            board.RefreshProjectedPieces(state.Settlements,state.Roads,state.Cities,state.RobberTileId,
                state.Walls.Append(roadWall).ToArray(),state.Knights,state.Metropolises,view.MerchantTileId);
        }
        var firstRoad=state.Roads.First(r=>Connects(r,roadWall));
        var toward=board.Position(firstRoad.LocationId)-board.Position(roadWall.LocationId);toward.y=0;
        var gateMetro=state.Metropolises.FirstOrDefault(m=>m.LocationId==roadWall.LocationId);
        var gate=Piece((gateMetro==null?"City with wall ":"Metropolis "+gateMetro.Track+" ")+roadWall.LocationId);
        Require(Vector3.Angle(gate.forward,toward)<.1f,"城门与首条本方道路错向");
        FocusDetail(board.Position(roadWall.LocationId),false);
        yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame(projectedWall?"城门接道路-投影.png":"城门接道路-实局.png");
        var walls=state.Walls.Where(w=>w.LocationId!=state.Metropolises[0].LocationId)
            .Append(new PiecePlacement{LocationId=state.Metropolises[0].LocationId,PlayerId=state.Metropolises[0].PlayerId}).ToArray();
        foreach(var track in new[]{M5.ImprovementTrack.Trade,M5.ImprovementTrack.Politics,M5.ImprovementTrack.Science})
        {
            var metro=new M5.Metropolis{PlayerId=state.Metropolises[0].PlayerId,LocationId=state.Metropolises[0].LocationId,Track=track};
            board.RefreshProjectedPieces(state.Settlements,state.Roads,state.Cities,state.RobberTileId,walls,state.Knights,new[]{metro},view.MerchantTileId);
            var root=Piece("Metropolis "+track+" "+metro.LocationId);
            Require(root.GetComponentsInChildren<MeshFilter>().Count(f=>f.name!="幻想棋子画面")==2,"带墙大都会缺件");
            if(track==M5.ImprovementTrack.Trade)
            {
                FocusDetail(board.Position(metro.LocationId),false);
                yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame("带墙大都会-投影.png");
            }
        }
        var shipMesh=Piece("Ship "+sample.LocationId).GetComponentInChildren<MeshFilter>().sharedMesh;
        foreach(var seat in new[]{"P1","P2","P3","P4"})
        {
            var variants=ships.Select(s=>s.LocationId==sample.LocationId?new M5.ShipPlacement{PlayerId=seat,LocationId=s.LocationId}:s).ToArray();
            board.RefreshSeafarers(variants,state.PirateTileId);
            var actual=Piece("Ship "+sample.LocationId);
            Require(actual.GetComponentInChildren<MeshFilter>().sharedMesh==shipMesh,"四色商船未共享网格");
            var ghost=OperationPieceAssets.PointerPreview(new TabletopPointerPreview{Kind="wall"},seat,target,Quaternion.identity,null,true);
            Require(ghost.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh==wallMesh),"城墙预览网格不符");
            Destroy(ghost);
            Require(OperationPieceAssets.Texture("ship",seat)!=null&&OperationPieceAssets.Texture("wall",seat)!=null,"船墙库存缩略图缺失");
            FocusDetail(actual.position,false);yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame("同泊位-"+seat+".png");
        }
        var otherSea=state.Tiles.First(t=>t.Resource=="sea"&&t.Id!=state.PirateTileId);
        board.RefreshSeafarers(ships,otherSea.Id);
        Require(Piece("Pirate "+otherSea.Id).GetComponentInChildren<MeshFilter>().sharedMesh==pirateMesh,"海盗换格未恢复船模");
        board.Refresh(view);Table.ResetPan();SetTop(false);Table.Frame(BoardCamera,false,normalZoom);
    }
}
