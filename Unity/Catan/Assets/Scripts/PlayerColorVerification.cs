using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyHarborGlints(string[] args)
    {
        mode="combined";
        app.ReadSave(args[Array.IndexOf(args,"--harbor-glints")+1]);
        app.SetPaused(true);
        yield return null;yield return null;
        Require(app.PlayerCount==4 && app.ThreeKingdoms && app.Harbormaster && app.Fishing &&
            app.ScenarioId=="through-the-desert","港口铜边验收配置错误");
        for(int i=0;i<900&&!Table.Ocean.MaskReady;i++) yield return null;
        Require(Table.Ocean.MaskReady,"海岸尚未就绪");
        var game=GetComponent<M5App>();
        var markers=game.VerificationBoard.ReferenceMarkers;
        var ports=game.M6View.Board.Ports.GroupBy(p=>p.Resource).Select(g=>g.First()).ToArray();
        Require(ports.Length==6,"未找到六种港口");
        var oreFace=Resources.Load<GameObject>(CoastalDecals.Root+"ore-harbor").transform
            .Find("TradePlaque").GetComponent<Renderer>().sharedMaterial;
        var allowed=new System.Collections.Generic.HashSet<string>();
        var properties=new MaterialPropertyBlock();
        string authority=Authority;
        foreach(var p in ports)
        {
            var root=markers.PortObject(p.Id).transform;
            var sign=root.GetComponentInChildren<HarborSign>();
            Require(sign.GetComponent<MeshFilter>().sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2),
                "交易牌缺少框条遮罩 "+p.Id);
            var materials=sign.Face.sharedMaterials;
            Require(materials.Contains(oreFace),"铭牌未使用矿石港的漆面与铜色 "+p.Id);
            var house=root.Find("HarborHouse").GetComponent<Renderer>().sharedMaterial;
            bool hasEmblemMaterial=p.Resource.HasValue && p.Resource!=Catan.Core.Resource.Ore;
            Require(materials.Length==(hasEmblemMaterial ? 2 : 1) && (!hasEmblemMaterial || materials.Contains(house)),
                "交易牌的货物徽记材质不符 "+p.Id);
            var center=sign.Face.bounds.center;
            var eye=(-root.forward+Vector3.up*1.2f).normalized*.16f;
            var pixel=BoardCamera.WorldToScreenPoint(center);
            var pointer=new Vector2(pixel.x,Screen.height-pixel.y);
            string name=p.Resource.HasValue?new[]{"木材","砖","羊毛","小麦","矿石"}[(int)p.Resource.Value]:"通用";
            void Capture(string state)=>DetailFrame(center,name+"港-"+state,detailSize:.009f,eyeOffset:eye,matchCameraMsaa:true);
            allowed.Clear();
            markers.ShowTradeGlints(allowed,BoardCamera,pointer,true);
            Capture("原色");
            allowed.Add(p.Id);
            markers.ShowTradeGlints(allowed,BoardCamera,pointer,false);
            sign.Face.GetPropertyBlock(properties);
            Require(properties.GetVector("_PlaqueGlint").x==1 && properties.GetVector("_PlaqueGlint").y==0,"常态铜边错误 "+p.Id);
            Capture("可交易");
            markers.ShowTradeGlints(allowed,BoardCamera,pointer,true);
            sign.Face.GetPropertyBlock(properties);
            Require(properties.GetVector("_PlaqueGlint").y==1,"指向港口未亮起 "+p.Id);
            Capture("指向");
            markers.PressTradePlaque(p.Id);
            sign.Face.GetPropertyBlock(properties);
            Require(properties.GetVector("_PlaqueGlint").w>Time.time,"点按铜边未响应 "+p.Id);
            Capture("点按");
        }
        Require(Authority==authority,"光效改变了对局状态");
        // 切换到临港玩家，用正常回合和点击入口再核对亮起的港口与实际交易权限。
        var state=game.M6View.Board;
        var connected=state.Settlements.Concat(state.Cities)
            .Where(b=>state.Ports.Any(p=>p.Vertices.Contains(b.LocationId)))
            .OrderByDescending(b=>b.PlayerId==game.M6View.ActivePlayerId).First();
        app.StartGame(mode,"through-the-desert",4,connected.PlayerId,true,authority,
            threeKingdoms:true,harbormaster:true,fishing:true);
        yield return null;yield return null;
        for(int step=0;step<200 && (!Snapshot.CanTrade || Snapshot.TradePorts.Count==0);step++)
        {
            Advance();
            yield return null;
        }
        Require(Snapshot.CanTrade && Snapshot.TradePorts.Count>0,"临港玩家未到可交易阶段");
        // 行动阶段的公开投影先到，掷骰和发牌动画在随后的绘制帧才锁定输入。
        yield return new WaitForSecondsRealtime(.3f);
        for(int i=0;i<900 && !Dock.CanOpenTrade(Snapshot);i++) yield return null;
        Require(Dock.CanOpenTrade(Snapshot),"掷骰与发牌结束后未恢复交易入口");
        Table.Frame(BoardCamera,false,1);
        yield return null;
        yield return new WaitForEndOfFrame();
        game=GetComponent<M5App>();
        markers=game.VerificationBoard.ReferenceMarkers;
        foreach(var p in game.M6View.Board.Ports)
        {
            var sign=markers.PortObject(p.Id).GetComponentInChildren<HarborSign>();
            sign.Face.GetPropertyBlock(properties);
            Require((properties.GetVector("_PlaqueGlint").x>0)==Snapshot.TradePorts.Contains(p.Id),
                $"真实回合的港口提示与权限不符 {p.Id} 交易={Dock.CanOpenTrade(Snapshot)} 弹窗={Dock.HasOverlay} 菜单={app.TableMenuOpen} 历史={Hud.HistoryOpen} 隐藏={Hud.OperationHidden}");
        }
        var portId=Snapshot.TradePorts.First();
        var owned=markers.PortObject(portId).GetComponentInChildren<HarborSign>();
        var point=BoardCamera.WorldToScreenPoint(owned.Face.bounds.center);
        var mouse=new Vector2(point.x,Screen.height-point.y);
        Require(markers.PickPort(mouse,BoardCamera)==portId,"实局未拾取到目标交易牌 "+portId);
        var pick=game.GetType().GetMethod("PickTabletopPort",PrivateInstance);
        Require((bool)pick.Invoke(game,new object[]{mouse}),"实局港口未接受点击 "+portId);
        Require(Dock.IsPortTrade,"点选亮起的港口未打开交易 "+portId);
        owned.Face.GetPropertyBlock(properties);
        Require(properties.GetVector("_PlaqueGlint").w>Time.time,"实际交易入口未触发点按反馈");
        configurations++;
    }

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
        Table.SetPitch(45,false);Table.Frame(BoardCamera,false,1);
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        yield return new WaitForSecondsRealtime(2);
        yield return new WaitForEndOfFrame();CaptureCamera("四色实局-全盘.png");
        var board=GetComponent<M5App>().VerificationBoard;
        VerifyHarborColors(board);
        foreach(string kind in new[]{"village","city"}.Where(k=>args.Contains("--"+k+"-views")))
        {
            var state=GetComponent<M5App>().M6View.Board;
            var edges=Edges.ToDictionary(e=>e.Id);
            bool city=kind=="city";
            var locations=city?state.Cities.Where(c=>!state.Walls.Any(w=>w.LocationId==c.LocationId) &&
                !state.Metropolises.Any(m=>m.LocationId==c.LocationId)):state.Settlements.AsEnumerable();
            var building=locations.OrderByDescending(v=>state.Roads.Count(r=>
                edges[r.LocationId].Vertices.Contains(v.LocationId))).First();
            var model=PlayerComponentAssets.Model(PlayerComponentAssets.BuildingResource(kind,building.PlayerId));
            var shared=model.GetComponentInChildren<MeshFilter>().sharedMesh;
            var vertex=board.Position(building.LocationId);
            var placed=board.CommittedPieces.Cast<Transform>().First(p=>
                (new Vector2(p.position.x-vertex.x,p.position.z-vertex.z)).sqrMagnitude<1e-8f &&
                p.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh==shared));
            var bounds=placed.GetComponentInChildren<MeshRenderer>().bounds;
            Require(Mathf.Abs(bounds.min.y-EntityBoardAssets.BoardSurfaceHeight)<.0002f,"建筑墙脚未落在棋盘表面");
            if(city)
            {
                var surfaces=placed.GetComponentInChildren<MeshRenderer>().sharedMaterials;
                Require(shared.lodCount>1 && shared.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1) &&
                    surfaces.Length==2 && surfaces.All(m=>
                    m.GetTexture("_BumpMap")!=null && m.GetTexture("_MetallicGlossMap")!=null),"王城缺少远景网格或表面贴图");
                var bookImage=Resources.Load<Texture2D>("OperationUI/v002/pieces/city-red");
                Require(bookImage!=null && bookImage.width>=768,"百科王城图缺失或被压缩到旧缩略图尺寸");
            }
            var aim=bounds.center;
            var direction=(placed.forward-.40f*placed.right+.78f*Vector3.up).normalized;
            BoardCamera.transform.position=aim+direction*.18f;
            BoardCamera.transform.LookAt(aim);BoardCamera.orthographicSize=.029f;
            yield return new WaitForEndOfFrame();CaptureCamera(city?"王城实局路口.png":"村庄实局路口.png");
            Table.SetPitch(45,false);Table.Frame(BoardCamera,false,1);
        }
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
                    var solids=piece.GetComponentsInChildren<MeshFilter>();
                    if(shared==null)shared=solids.Last().sharedMesh;
                    Require(solids.Any(f=>f.sharedMesh==shared),"四色没有共享原网格："+kind);
                    var materials=piece.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).ToArray();
                    var owned=materials.Where(m=>m.shader.name=="Catan/玩家归属色"||
                        ((m.shader.name=="Catan/圆门村庄" || m.shader.name=="Catan/王城石材与板岩") && m.name.StartsWith("玩家归属-"))||
                        EntityBoardAssets.IsSailMaterial(m.name)).Distinct().ToArray();
                    if(kind=="village")
                    {
                        Require(shared.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1),"村庄缺少整屋接触 UV");
                        Require(solids.Length==1 && materials.Length==2 && owned.Length==1,"村庄的共享网格或材质分区异常");
                        Require(owned[0].IsKeywordEnabled("_VILLAGE_GLAZE") && materials.Count(m=>m.IsKeywordEnabled("_VILLAGE_GLAZE"))==1,"陶瓦釉色分区异常");
                        Require(materials.All(m=>m.shader.name=="Catan/圆门村庄" && m.GetTexture("_OcclusionMap")!=null &&
                            m.GetTexture("_OcclusionMap").name=="village-contact"),"整屋接触图没有接入两个村庄材质");
                    }
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
                    foreach(var r in piece.GetComponentsInChildren<Renderer>())
                        Require(!r.forceRenderingOff,"实体模式显示异常");
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
            Table.ResetPan();Table.SetPitch(45,false);Table.Frame(BoardCamera,false,1);
        }
        Require(Authority==before,"染色或对照改变了权威对局状态");
        configurations++;
    }

    void VerifyHarborColors(M5BoardRenderer board)
    {
        var view=GetComponent<M5App>().M6View;
        var ports=view.Board.Ports.Select(p=>new TabletopReferenceMarkers.PortDisplay
            {Id=p.Id,Vertices=p.Vertices,Resource=p.Resource}).ToArray();
        var markers=board.ReferenceMarkers;
        var block=new MaterialPropertyBlock();
        Color Roof(TabletopReferenceMarkers.PortDisplay port)
        {
            var house=markers.PortObject(port.Id).transform.Find("HarborHouse").GetComponent<Renderer>();
            Require(house.sharedMaterial.HasProperty("_RoofColor") && house.sharedMaterial.shader.isSupported,"港仓缺少屋瓦归属色 "+port.Id);
            house.GetPropertyBlock(block);
            return block.GetColor("_RoofColor");
        }
        var occupied=view.Board.Settlements.Where(b=>view.ThreeKingdoms?.BoardAssets?.Any(a=>
            a.LocationId==b.LocationId && a.Kind=="Settlement" && a.Damaged)!=true).Concat(view.Board.Cities).ToArray();
        var livePort=ports.Select(p=>(port:p,owner:occupied.FirstOrDefault(b=>p.Vertices.Contains(b.LocationId))?.PlayerId))
            .OrderBy(p=>p.owner,StringComparer.Ordinal).ToArray();
        foreach(var p in livePort)
            Require(Roof(p.port)==(p.owner==null?Color.clear:BoardRenderer.SeatColor(p.owner)),"实局港口与岸边建筑归属不符 "+p.port.Id);
        var claimed=livePort.FirstOrDefault(p=>p.owner!=null).port;
        if(claimed!=null)
        {
            var root=markers.PortObject(claimed.Id).transform;
            DetailFrame(root.TransformPoint(new Vector3(0,.005f,0)),"港口占据配色",detailSize:.029f,
                eyeOffset:(-root.forward+root.right*.35f+Vector3.up*1.35f).normalized*.16f,matchCameraMsaa:true);
        }
        var buildings=ports.Select((p,i)=>new Catan.Core.PiecePlacement
            {LocationId=p.Vertices[i%2],PlayerId="P"+(1+i%4)}).ToArray();
        try
        {
            markers.Refresh(Tiles,ports,table:Table,settlements:buildings);
            for(int i=0;i<ports.Length;i++)
                Require(Roof(ports[i])==BoardRenderer.SeatColor(buildings[i].PlayerId),"岸边村庄与港口颜色不符 "+ports[i].Id);
            markers.Refresh(Tiles,ports,table:Table,cities:buildings);
            for(int i=0;i<ports.Length;i++)
                Require(Roof(ports[i])==BoardRenderer.SeatColor(buildings[i].PlayerId),"岸边城市与港口颜色不符 "+ports[i].Id);
            markers.Refresh(Tiles,ports,table:Table);
            foreach(var port in ports) Require(Roof(port)==Color.clear,"空港未恢复原色 "+port.Id);
        }
        finally { board.RefreshReferenceMarkers(view); }
    }
}
