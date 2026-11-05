using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.Core;
using UnityEngine;
using M5=Catan.Core.M5;

// 显式开发入口：先恢复正常规则推进的双扩展对局，再短暂排布公开投影以检查稀有棋子。
public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyExpansionPieces()
    {
        mode="combined";
        string seat=File.ReadAllText(Path.Combine(output,"seat.txt")).Trim();
        app.StartGame(mode,"heading-for-new-shores",4,seat,true,File.ReadAllText(Path.Combine(output,"authority.json")));
        yield return null;yield return null;
        var presentation=GetComponent<M5App>();
        var board=presentation.VerificationBoard;
        var view=presentation.M6View;
        for(int i=0;i<600&&!Table.Ocean.MaskReady;i++)yield return null;
        Require(Table.Ocean.MaskReady,"地图未就绪");
        string authority=Authority;
        string[] parts={"ship","wall","knight-1-rest","knight-1-active","knight-2-rest","knight-2-active","knight-3-rest","knight-3-active","metro-trade","metro-politics","metro-science"};
        foreach(var part in parts)
        {
            Mesh first=null;
            foreach(var colour in new[]{"red","blue","orange","beige"})
            {
                var model=Resources.Load<GameObject>("ExpansionPieces/v001/"+part+"-"+colour);
                Require(model!=null,"缺少扩展棋子 "+part);
                var filter=model.GetComponentsInChildren<MeshFilter>().Single();
                if(first==null)first=filter.sharedMesh;
                Require(filter.sharedMesh==first,"四色共享网格失败 "+part);
                var materials=filter.GetComponent<Renderer>().sharedMaterials;
                Require(materials.All(m=>m!=null&&m.mainTexture!=null&&m.shader.isSupported),"无效材质 "+part);
                Require(materials.Any(m=>m.name.EndsWith("_"+colour)),"玩家色丢失 "+part);
                Require(model.GetComponentsInChildren<Collider>().Length==0,"新增棋子阻挡输入");
            }
        }
        foreach(var id in new[]{"P1","P2","P3","P4"})
        {
            foreach(var icon in new[]{"ship","wall","knight"})
            for(int level=1;level<=(icon=="knight"?3:1);level++)
            {
                var thumbnail=OperationPieceAssets.Texture(icon,id,level);
                Require(thumbnail!=null&&thumbnail.isReadable,"操作缩略图缺失");
            }
            foreach(string kind in new[]{"ship","wall","knight","metropolis"})
            foreach(bool legal in new[]{true,false})
            {
                var spec=new TabletopPointerPreview{Kind=kind,Level=3,Active=true,Track=2,Wall=kind=="metropolis"};
                var ghost=OperationPieceAssets.PointerPreview(spec,id,Vector3.zero,Quaternion.identity,null,legal);
                var mesh=kind=="ship"?EntityBoardAssets.ShipResource(id):kind=="wall"?PlayerComponentAssets.BuildingResource("city-wall",id):
                    kind=="knight"?PlayerComponentAssets.KnightResource(id,3,true):PlayerComponentAssets.BuildingResource("metropolis",id,2);
                var expected=Resources.Load<GameObject>(mesh).GetComponentInChildren<MeshFilter>().sharedMesh;
                Require(ghost.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh==expected),"建造预览模型不符");
                var block=new MaterialPropertyBlock();ghost.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
                Require(block.GetColor("_BaseColor")== (legal?new Color(.3f,1f,.75f):new Color(1f,.22f,.2f)),"预览颜色不符");
                Destroy(ghost);
            }
        }
        Require(view.Board.Ships.Length>0&&view.Board.Walls.Length>0&&view.Board.Metropolises.Length>0&&view.Board.Knights.Length>0,"对局缺少扩展棋子");
        foreach(var ship in view.Board.Ships)
        {
            var hull=board.CommittedPieces.Cast<Transform>().Single(t=>t.name.StartsWith("Ship "+ship.LocationId+" ["));
            Require(Vector3.Distance(hull.position,board.ShipPosition(ship.LocationId))<.00005f,"商船泊位异常");
            Require(Mathf.Abs(hull.position.y-TabletopOcean.WaterHeight+.0004f)<.00005f,"商船水线异常");
            var pixel=BoardCamera.WorldToScreenPoint(hull.position+Vector3.up*.005f);
            Require(Vector2.Distance(board.ScreenPoint(ship.LocationId),new Vector2(pixel.x,Screen.height-pixel.y))<1,"商船选择与显示位置不符");
        }
        yield return new WaitForSeconds(3);
        Table.Frame(BoardCamera,false,.76f);yield return new WaitForEndOfFrame();SaveOceanFrame("实际双扩展对局.png");
        foreach(var pair in new[]{("实局商船",view.Board.Ships[0].LocationId),("实局城墙",view.Board.Walls[0].LocationId),
            ("实局骑士",view.Board.Knights[0].LocationId),("实局大都会",view.Board.Metropolises[0].LocationId)})
        {
            FocusDetail(pair.Item1=="实局商船"?board.ShipPosition(pair.Item2):Position(pair.Item2),false);yield return new WaitForEndOfFrame();SaveOceanFrame(pair.Item1+".png");
        }
        // 同一顶点、同一相机、同一座位比较：只传入公开投影，比较拍摄结束后恢复真实 PlayerView。
        var city=view.Board.Cities.First();
        foreach(var direction in new[]{M5.ImprovementTrack.Trade,M5.ImprovementTrack.Politics,M5.ImprovementTrack.Science})
        {
            var metro=new M5.Metropolis{PlayerId=city.PlayerId,LocationId=city.LocationId,Track=direction};
            board.RefreshProjectedPieces(view.Board.Settlements,view.Board.Roads,view.Board.Cities,view.Board.RobberTileId,
                view.Board.Walls.Where(w=>w.LocationId!=city.LocationId).ToArray(),view.Board.Knights,new[]{metro},view.MerchantTileId);
            FocusDetail(Position(city.LocationId),false);yield return null;yield return new WaitForEndOfFrame();
            SaveOceanFrame("投影对照-大都会-"+direction+".png");
        }
        var knight=view.Board.Knights.OrderByDescending(k=>view.Board.Tiles.Count(t=>t.Resource!="sea"&&t.Vertices.Contains(k.LocationId))).First();
        for(int level=1;level<=3;level++)
        {
            var pose=new M5.Knight{PlayerId=knight.PlayerId,LocationId=knight.LocationId,Level=level,Active=true};
            board.RefreshProjectedPieces(view.Board.Settlements,view.Board.Roads,view.Board.Cities,view.Board.RobberTileId,
                view.Board.Walls,new[]{pose},view.Board.Metropolises,view.MerchantTileId);
            FocusDetail(Position(knight.LocationId),false);yield return null;yield return new WaitForEndOfFrame();
            SaveOceanFrame("投影对照-骑士-"+level+".png");
        }
        board.Refresh(view);
        Require(Authority==authority,"显示改变了规则状态");
        Table.ResetPan();Table.Frame(BoardCamera,false,.76f);
        yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame("验收窗口.png");
        app.WriteSave(Path.Combine(output,"扩展棋子验收局.json"));configurations++;
    }
}

