using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyPlayerColors(string[] args)
    {
        mode="combined";
        app.ReadSave(args[Array.IndexOf(args,"--player-colors")+1]);
        app.SetPaused(true);
        yield return null;yield return null;
        Require(app.PlayerCount==4 && app.ThreeKingdoms && app.Harbormaster && app.Fishing && app.ScenarioId=="through-the-desert","归属色验收配置错误");
        for(int i=0;i<900&&!Table.Ocean.MaskReady;i++)yield return null;
        Require(Table.Ocean.MaskReady,"棋盘尚未就绪");
        string before=Authority;
        bool painted=BoardArtPreference.Painted;
        BoardArtPreference.Select(false,false);
        Table.SetPitch(45,false);Table.Frame(BoardCamera,false,1);
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        yield return new WaitForSecondsRealtime(2);
        yield return new WaitForEndOfFrame();CaptureCamera("四色实局-全盘.png");
        var board=GetComponent<M5App>().VerificationBoard;
        bool sailsOnly=args.Contains("--ship-sails");
        var stand=new GameObject("四色棋子公开对照");
        stand.transform.SetParent(board.transform,false);
        var point=board.Position(Tiles.First(t=>t.Resource==(sailsOnly?"sea":"wood")).Id);
        point.y=EntityBoardAssets.BoardSurfaceHeight;
        var kinds=new[]{"village","city","city-with-wall","metro-trade","metro-politics","metro-science","road","ship","knight-1","knight-2","knight-3"};
        var labels=new[]{"村庄","主城","带墙主城","贸易大都会","政治大都会","科学大都会","道路","宝船","一级骑士","二级骑士","三级骑士"};
        var csv=new System.Text.StringBuilder("棋子,玩家,归属材质数,网格数\n");
        try
        {
            board.CommittedPieces.gameObject.SetActive(false);
            for(int row=0;row<kinds.Length;row++)
            {
                string kind=kinds[row];
                if(sailsOnly && kind!="ship")continue;
                Mesh shared=null;
                for(int seat=0;seat<4;seat++)
                {
                    string id="P"+(seat+1);
                    Vector3 pos=point+Vector3.right*(seat-1.5f)*.039f;
                    var piece=kind=="road"?PlayerComponentAssets.Road(id,"四色道路",pos,Quaternion.Euler(0,65,0),stand.transform):
                        kind=="ship"?EntityBoardAssets.Ship("四色商船",BoardRenderer.SeatColors[seat],pos,Quaternion.Euler(0,145,0),stand.transform):
                        kind.StartsWith("knight-")?PlayerComponentAssets.Knight(id,kind[7]-'0',false,"四色骑士",pos,stand.transform):
                        PlayerComponentAssets.Building(kind.StartsWith("metro-")?"metropolis":kind,id,"四色建筑",pos,stand.transform,Mathf.Max(0,row-3));
                    Require(piece!=null,"四色棋子缺失："+kind);
                    var solids=piece.GetComponentsInChildren<MeshFilter>().Where(f=>f.name!="幻想棋子画面").ToArray();
                    if(shared==null)shared=solids.Last().sharedMesh;
                    Require(solids.Any(f=>f.sharedMesh==shared),"四色没有共享原网格："+kind);
                    var materials=piece.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).ToArray();
                    var owned=materials.Where(m=>m.shader.name=="Catan/玩家归属色"||EntityBoardAssets.IsSailMaterial(m.name)).Distinct().ToArray();
                    var expected=kind=="ship"&&seat==3?Color.white:BoardRenderer.SeatColors[seat];
                    Require(owned.Length>0 && owned.All(m=>m.GetColor("_BaseColor")==expected),"归属材质与玩家颜色不一致："+kind+" "+id);
                    if(kind=="ship")
                    {
                        Require(owned.Length==3 && owned.All(m=>m.GetTexture("_BumpMap")!=null),"横帆、三角帆或旗布缺失");
                        foreach(bool legal in new[]{true,false})
                        {
                            var ghost=OperationPieceAssets.PointerPreview(new TabletopPointerPreview{Kind="ship"},id,pos,Quaternion.identity,null,legal);
                            var hull=ghost.GetComponentsInChildren<MeshFilter>().Single(f=>f.sharedMesh==shared).GetComponent<Renderer>();
                            var slots=hull.sharedMaterials;
                            var properties=new MaterialPropertyBlock();
                            foreach(var m in owned)
                            {
                                int slot=Array.IndexOf(slots,m);
                                Require(slot>=0,"预览帆面材质不符");
                                hull.GetPropertyBlock(properties,slot);
                                Require(properties.GetColor("_BaseColor")==expected,"预览帆色不符");
                            }
                            Destroy(ghost);
                        }
                    }
                    Require(materials.All(m=>m.shader.isSupported),"棋子着色器不支持："+kind);
                    Require(owned.All(m=>m.enableInstancing||kind=="ship"),"归属材质未共享实例配置");
                    Require(piece.GetComponentsInChildren<Collider>().Length==0,"染色增加了碰撞器");
                    Require(FantasyBoardPiece.Pigment(PlayerComponentAssets.ColourKey(id))==BoardRenderer.SeatColors[seat],"绘制视图色值不一致");
                    foreach(var r in piece.GetComponentsInChildren<Renderer>())
                        Require(r.name=="幻想棋子画面"?r.forceRenderingOff:!r.forceRenderingOff,"实体模式显示异常");
                    csv.AppendLine(labels[row]+","+id+","+owned.Length+","+solids.Length);
                    if(row<6)
                    {
                        var spec=new TabletopPointerPreview{Kind=row==0?"settlement":row<3?"city":"metropolis",Wall=row==2,Track=Mathf.Max(0,row-3)};
                        var ghost=OperationPieceAssets.PointerPreview(spec,id,pos,Quaternion.identity,null,true);
                        Require(ghost.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh==shared),"预览没有使用同源网格");
                        Destroy(ghost);
                    }
                }
                var facing=Quaternion.Euler(45,0,0);
                var aim=point+Vector3.up*.019f+Vector3.right*.010f;
                BoardCamera.transform.SetPositionAndRotation(aim-facing*Vector3.forward*.3f,facing);
                BoardCamera.orthographicSize=.069f;
                yield return null;yield return new WaitForEndOfFrame();
                CaptureCamera(labels[row]+"-四色.png");
                if(row>=3&&row<=5)
                {
                    facing=Quaternion.Euler(82,0,0);
                    BoardCamera.transform.SetPositionAndRotation(aim-facing*Vector3.forward*.3f,facing);
                    yield return new WaitForEndOfFrame();CaptureCamera(labels[row]+"-四色俯视.png");
                }
                foreach(Transform child in stand.transform)child.gameObject.SetActive(false);
                foreach(Transform child in stand.transform)Destroy(child.gameObject);
                yield return null;
            }
            // 实局中的玩家显示、库存和网格颜色均使用同一套席位色。
            foreach(string id in new[]{"P1","P2","P3","P4"})
                foreach(string kind in sailsOnly?new[]{"ship"}:new[]{"village","city","road","ship","wall","knight"})
                    Require(OperationPieceAssets.Texture(kind,id)!=null,"库存缺少四色模型图");
            File.WriteAllText(Path.Combine(output,"归属材质检查.csv"),csv.ToString());
        }
        finally
        {
            Destroy(stand);board.CommittedPieces.gameObject.SetActive(true);
            BoardArtPreference.Select(painted,false);
            Table.ResetPan();Table.SetPitch(45,false);Table.Frame(BoardCamera,false,1);
        }
        Require(Authority==before,"染色或对照改变了权威对局状态");
        configurations++;
    }
}
