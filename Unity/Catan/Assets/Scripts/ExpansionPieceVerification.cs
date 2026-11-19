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
    IEnumerator VerifyKnightUpdraft()
    {
        mode="combined";
        app.StartGame(mode,"through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        yield return new WaitForSeconds(2);
        var app5=GetComponent<M5App>();
        var board=app5.VerificationBoard;
        var view=app5.M6View;
        var state=view.Board;
        var edge=Edges.Where(e=>e.Vertices.All(v=>Tiles.Count(t=>t.Vertices.Contains(v)&&t.Resource!="sea")>=2))
            .OrderBy(e=>Mathf.Abs((board.Position(e.Vertices[0])-board.Position(e.Vertices[1])).z)).First();
        var vertices=edge.Vertices.OrderBy(v=>board.Position(v).x).ToArray();
        var cam=BoardCamera;
        for(int level=1;level<=3;level++)
        {
            var poses=vertices.Select((v,i)=>new M5.Knight{PlayerId="P2",Level=level,Active=i==1,LocationId=v}).ToArray();
            board.RefreshProjectedPieces(state.Settlements,state.Roads,state.Cities,state.RobberTileId,state.Walls,poses,
                state.Metropolises,view.MerchantTileId);
            yield return new WaitForSeconds(1.8f);
            var roots=board.CommittedPieces.Cast<Transform>();
            var pair=vertices.Select(v=>roots.Single(p=>p.name.StartsWith("Knight L"+level+" "+v+" ["))).ToArray();
            var wind=pair[1].GetComponentInChildren<ParticleSystem>();
            Require(wind!=null && wind.particleCount>0 && pair[0].GetComponentInChildren<ParticleSystem>()==null,"激活与休整气流不符");
            var rot=Quaternion.Euler(32,0,0);
            var center=(pair[0].position+pair[1].position)*.5f+Vector3.up*(level==3?.015f:.012f);
            cam.transform.SetPositionAndRotation(center-rot*Vector3.forward*.24f,rot);
            cam.orthographicSize=level==3?.032f:.025f;
            var rt=RenderTexture.GetTemporary(1440,900,24);
            var prior=cam.targetTexture;
            var active=RenderTexture.active;
            var rect=cam.rect;
            var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try
            {
                cam.rect=new Rect(0,0,1,1);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(output,"knight-level-"+level+".png"),tex.EncodeToPNG());
                cameraImages++;
            }
            finally {cam.rect=rect;cam.targetTexture=prior;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(tex);}
        }
    }
    IEnumerator VerifyExpansionPieces()
    {
        mode="combined";
        var args=Environment.GetCommandLineArgs();
        int saveArg=Array.IndexOf(args,"--expansion-save");
        if(saveArg>=0)app.ReadSave(args[saveArg+1]);
        else
        {
            string seat=File.ReadAllText(Path.Combine(output,"seat.txt")).Trim();
            app.StartGame(mode,"heading-for-new-shores",4,seat,true,File.ReadAllText(Path.Combine(output,"authority.json")));
        }
        yield return null;yield return null;
        var presentation=GetComponent<M5App>();
        var board=presentation.VerificationBoard;
        var view=presentation.M6View;
        for(int i=0;i<600&&!Table.Ocean.MaskReady;i++)yield return null;
        Require(Table.Ocean.MaskReady,"地图未就绪");
        string authority=Authority;
        if(args.Contains("--neutral-pieces"))
        {
            Require(app.PlayerCount==4 && app.ScenarioId=="through-the-desert" && app.ThreeKingdoms && app.Harbormaster && app.Fishing,"中立棋子验收模式不符");
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return new WaitForSeconds(2);
            var state=view.Board;
            var robberTile=state.RobberTileId ?? state.Tiles.First(t=>t.Resource=="ore").Id;
            var merchantTile=view.MerchantTileId ?? state.Tiles.First(t=>t.Resource=="wheat" && t.Id!=robberTile).Id;
            if(view.MerchantTileId==null || state.RobberTileId==null)
                board.RefreshProjectedPieces(state.Settlements,state.Roads,state.Cities,robberTile,state.Walls,state.Knights,state.Metropolises,merchantTile,skillAssets:view.ThreeKingdoms?.BoardAssets);
            yield return new WaitForEndOfFrame();CaptureCamera("实局全盘.png");
            foreach(var kind in new[]{"robber","merchant"})
            {
                bool robber=kind=="robber";
                var id=robber?robberTile:merchantTile;
                var piece=board.CommittedPieces.Cast<Transform>().Single(t=>t.name.StartsWith((robber?"Robber ":"Merchant ")+id+" [",StringComparison.Ordinal));
                var renderer=piece.GetComponentInChildren<MeshRenderer>();
                var bound=renderer.bounds;
                Require(Mathf.Abs(bound.min.y-piece.position.y)<.0002f && Mathf.Abs(bound.size.y-(robber ? .02036f : .01576f))<.0003f,"中立棋子高度或轴向不符："+kind+" "+bound);
                Require(renderer.sharedMaterial.mainTexture==Resources.Load<Texture2D>(robber?"NeutralPieces/v001/robber-surface":"NeutralPieces/v001/peddler-surface") && renderer.sharedMaterial.shader.isSupported,"中立棋子调色材质缺失："+kind);
                Require(piece.GetComponentsInChildren<Collider>().Length==0,"中立棋子阻挡地块拾取");
                var preview=OperationPieceAssets.PointerPreview(new TabletopPointerPreview{Kind=kind},"P1",board.Position(id),Quaternion.identity,null,true);
                Require(preview.GetComponentInChildren<MeshFilter>().sharedMesh==piece.GetComponentInChildren<MeshFilter>().sharedMesh && Vector3.Distance(preview.transform.position,piece.position)<.00001f && Quaternion.Angle(preview.transform.rotation,piece.rotation)<.01f,"中立棋子落点预览不一致");
                Destroy(preview);
                FocusDetail(piece.position,false);
                yield return null;yield return new WaitForEndOfFrame();
                var title=(robber?"强盗":"货郎")+((robber?state.RobberTileId:view.MerchantTileId)==null?"-公开投影":"-实局");
                CaptureCamera(title+".png");
                if(robber)
                {
                    Require(!renderer.forceRenderingOff &&
                        renderer.sharedMaterial.GetTexture("_BumpMap")==Resources.Load<Texture2D>("NeutralPieces/v001/robber-normal") &&
                        renderer.sharedMaterial.GetTexture("_MetallicGlossMap")==Resources.Load<Texture2D>("NeutralPieces/v001/robber-metal"),"强盗实体材质未完整加载");
                }
                var facing=robber?Quaternion.Euler(30,0,0):Quaternion.Euler(18,-12,0);
                var centre=piece.position+Vector3.up*.009f;
                BoardCamera.transform.SetPositionAndRotation(centre-facing*Vector3.forward*.18f,facing);
                BoardCamera.orthographicSize=robber?.040f:.030f;
                yield return new WaitForEndOfFrame();CaptureCamera(title+"-特写.png");
            }
            if(args.Contains("--detailed-pirate"))
            {
                var sea=state.PirateTileId ?? state.Tiles.First(t=>t.Resource=="sea").Id;
                board.RefreshSeafarers(state.Ships,sea);
                var pirate=board.CommittedPieces.Cast<Transform>().Single(t=>t.name.StartsWith("Pirate "+sea+" [",StringComparison.Ordinal));
                var mesh=pirate.GetComponentInChildren<MeshFilter>().sharedMesh;
                var pose=board.ReferenceMarkers.PiratePose(board.Position(sea));
                var ghost=OperationPieceAssets.PointerPreview(new TabletopPointerPreview{Kind="pirate"},"P1",board.Position(sea)+pose.shift,Quaternion.identity,null,true,true,pose.facing);
                Require(mesh!=null && mesh.vertexCount>0 && mesh==Resources.Load<GameObject>(EntityBoardAssets.PirateResource).GetComponentInChildren<MeshFilter>().sharedMesh,"海盗网格加载错误");
                Require(ghost.GetComponentInChildren<MeshFilter>().sharedMesh==mesh && Vector3.Distance(ghost.transform.position,pirate.position)<.00002f,"海盗落点预览错误");
                Destroy(ghost);
                FocusDetail(pirate.position,false);
                yield return null;yield return new WaitForEndOfFrame();CaptureCamera("海盗-棋盘.png");
                var facing=Quaternion.LookRotation(-(pirate.rotation*new Vector3(.62f,.64f,1f)),Vector3.up);
                var target=pirate.position+Vector3.up*.0045f;
                BoardCamera.transform.SetPositionAndRotation(target-facing*Vector3.forward*.18f,facing);
                BoardCamera.orthographicSize=.022f;
                yield return new WaitForEndOfFrame();CaptureCamera("海盗-特写.png");
            }
            board.Refresh(view);
            Require(Authority==authority,"棋子投影影响存档");
            configurations++;
            yield break;
        }
        if(args.Contains("--harbor-study"))
        {
            var study=VerifyHarborPieces();
            while(study.MoveNext())yield return study.Current;
            Require(Authority==authority,"海湾棋子显示检查改变了对局");
            configurations++;
            yield break;
        }
        if(args.Contains("--jade-numbers"))
        {
            Require(app.PlayerCount==4 && app.ScenarioId=="through-the-desert" && app.ThreeKingdoms && app.Harbormaster && app.Fishing,"验收模式不符");
            var tiles=board.GetComponentInChildren<TabletopNumberTokens>();
            var fish=view.TradeModules.Fishing;
            var lake=board.transform.Find("湖泊点数");
            Require(lake!=null && lake.childCount==fish.LakeNumbers.Length,"湖泊玉签与公开点数不符");
            Require(board.GetComponentsInChildren<FishingNumberDisplay>().Length==fish.Grounds.Length,"沿岸渔场玉签数量不符");
            foreach(var slip in board.GetComponentsInChildren<JadeNumberSlip>())
            {
                Require(slip.GetComponent<Collider>()==null && slip.GetComponent<Renderer>().bounds.min.y>EntityBoardAssets.BoardSurfaceHeight,"玉签阻挡拾取或嵌入地面");
                Require(slip.GetComponent<Renderer>().sharedMaterial.mainTexture!=null,"点数缺少签面");
            }
            var forest=view.Board.Tiles.First(t=>t.Resource=="wood" && t.Number==6);
            var token=tiles.transform.Find("点数 6 · "+forest.Id);
            Require(token!=null,"森林六点玉签缺失");
            CheckNumberTokenUpdates();
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return new WaitForSeconds(2);
            yield return new WaitForEndOfFrame();SaveOceanFrame("默认视距.png");
            FocusDetail(Position(forest.Id),false);
            yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame("森林六点.png");
            FocusDetail(Position(fish.LakeTileId),false);
            yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame("湖泊.png");
            var ground=board.GetComponentsInChildren<FishingNumberDisplay>().First(g=>g.GetComponentInChildren<JadeNumberSlip>()!=null);
            FocusDetail(ground.GetComponentInChildren<JadeNumberSlip>().transform.position,false);
            yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame("沿岸渔场.png");
            Table.Wheel(3,true,BoardCamera.pixelRect.center);
            yield return null;yield return new WaitForEndOfFrame();
            Require(Vector3.Dot(token.forward,-BoardCamera.transform.forward)>.999f,"转动棋盘后签面方向错误");
            Require(Authority==authority,"点数显示改变了对局");
            configurations++;
            yield break;
        }
        if(args.Contains("--metropolis-study"))
        {
            var study=VerifyMetropolisViews();
            while(study.MoveNext())yield return study.Current;
            Require(Authority==authority,"大都会外观检查改变了对局");
            configurations++;
            yield break;
        }
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
                Require(materials.All(m=>m!=null&&(part=="ship"||m.mainTexture!=null)&&m.shader.isSupported),"无效材质 "+part);
                Require(materials.Any(m=>part=="ship"?m.name=="归属旗布-"+colour:m.name.EndsWith("_"+colour)),"玩家色丢失 "+part);
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
        if(saveArg>=0){yield return new WaitForEndOfFrame();SaveOceanFrame("读档原视距.png");}
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
        bool portraits=args.Contains("--knight-portraits");
        string displaySeat=portraits?"P1":knight.PlayerId;
        string knightVertex=knight.LocationId;
        if(portraits)
        {
            var occupied=view.Board.Cities.Concat(view.Board.Settlements).Select(p=>p.LocationId).ToHashSet();
            var roadIds=view.Board.Roads.Select(p=>p.LocationId).ToHashSet();
            var roadEnds=Edges.Where(e=>roadIds.Contains(e.Id)).SelectMany(e=>e.Vertices).Distinct();
            knightVertex=roadEnds.Where(id=>!occupied.Contains(id))
                .OrderByDescending(id=>view.Board.Tiles.Count(t=>t.Resource!="sea"&&t.Vertices.Contains(id)))
                .ThenByDescending(id=>occupied.Min(other=>(Position(other)-Position(id)).sqrMagnitude))
                .FirstOrDefault()??knightVertex;
        }
        for(int level=1;level<=3;level++)
        foreach(bool active in new[]{false,true})
        {
            var pose=new M5.Knight{PlayerId=displaySeat,LocationId=knightVertex,Level=level,Active=active};
            board.RefreshProjectedPieces(view.Board.Settlements,view.Board.Roads,view.Board.Cities,view.Board.RobberTileId,
                view.Board.Walls,new[]{pose},view.Board.Metropolises,view.MerchantTileId);
            FocusDetail(Position(knightVertex),false);yield return null;yield return new WaitForEndOfFrame();
            var resource=PlayerComponentAssets.KnightResource(displaySeat,level,active);
            var expected=Resources.Load<GameObject>(resource).GetComponentInChildren<MeshFilter>().sharedMesh;
            Require(board.CommittedPieces.GetComponentsInChildren<MeshFilter>().Any(f=>f.sharedMesh==expected),"棋盘骑士姿态不符");
            SaveOceanFrame("投影对照-骑士-"+level+(active?"":"-休整")+".png");
            if(portraits&&active)
            {
                var piece=board.CommittedPieces.GetComponentsInChildren<MeshFilter>().Single(f=>f.sharedMesh==expected);
                var bounds=piece.GetComponent<Renderer>().bounds;
                var cam=BoardCamera;
                var previousPosition=cam.transform.position;
                var previousRotation=cam.transform.rotation;
                float previousSize=cam.orthographicSize;
                try
                {
                    var angle=Quaternion.Euler(30,0,0);
                    var center=bounds.center-Vector3.up*bounds.size.y*.06f;
                    cam.transform.SetPositionAndRotation(center-angle*Vector3.forward*.20f,angle);
                    cam.orthographicSize=Mathf.Max(bounds.size.y*.72f+bounds.size.z*.32f,bounds.size.x*.48f);
                    yield return new WaitForEndOfFrame();
                    SaveOceanFrame("参考俯角-骑士-"+level+".png");
                }
                finally
                {
                    cam.transform.SetPositionAndRotation(previousPosition,previousRotation);
                    cam.orthographicSize=previousSize;
                }
            }
        }
        board.Refresh(view);
        Require(Authority==authority,"显示改变了规则状态");
        Table.ResetPan();Table.Frame(BoardCamera,false,.76f);
        yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame("验收窗口.png");
        app.WriteSave(Path.Combine(output,"扩展棋子验收局.json"));configurations++;
    }

    IEnumerator VerifyMetropolisViews()
    {
        var appView=GetComponent<M5App>();
        var board=appView.VerificationBoard;
        var view=appView.M6View;
        var pieces=view.Board;
        var trade=pieces.Metropolises.First(m=>m.Track==M5.ImprovementTrack.Trade);
        var city=pieces.Cities.Single(c=>c.LocationId==trade.LocationId);
        var others=pieces.Metropolises.Where(m=>m.LocationId!=city.LocationId).ToArray();
        var noWall=pieces.Walls.Where(w=>w.LocationId!=city.LocationId).ToArray();
        var withWall=noWall.Append(new PiecePlacement{PlayerId=city.PlayerId,LocationId=city.LocationId}).ToArray();
        var normalZoom=Table.ZoomMultiplier;
        yield return new WaitForSeconds(3);
        yield return new WaitForEndOfFrame();SaveOceanFrame("实际对局-默认视距.png");
        var target=Position(city.LocationId);
        var center=BoardCamera.WorldToScreenPoint(target+Vector3.up*.005f);
        File.WriteAllText(Path.Combine(output,"对照位置.txt"),Mathf.RoundToInt(center.x)+","+Mathf.RoundToInt(Screen.height-center.y));
        void Project(PiecePlacement[] walls,M5.Metropolis[] metros)
        {
            board.RefreshProjectedPieces(pieces.Settlements,pieces.Roads,pieces.Cities,pieces.RobberTileId,
                walls,pieces.Knights,metros,view.MerchantTileId);
        }
        Project(noWall,others);
        yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame("普通主城-全盘.png");
        FocusDetail(target,false);
        yield return new WaitForEndOfFrame();SaveOceanFrame("普通主城-近景.png");
        var ordinary=board.CommittedPieces.Cast<Transform>().Single(t=>t.name.StartsWith("City "+city.LocationId+" ["));
        float cityHeight=ordinary.GetComponentInChildren<Renderer>().bounds.size.y;
        var heights=new System.Text.StringBuilder("建筑,高度毫米\n");
        void RecordHeight(string label,float meters) => heights.AppendLine(label+","+Mathf.RoundToInt(meters*100000f)/100f);
        RecordHeight("普通主城",cityHeight);
        var streetAngle=Quaternion.LookRotation(-ordinary.forward*Mathf.Cos(32*Mathf.Deg2Rad)-Vector3.up*Mathf.Sin(32*Mathf.Deg2Rad));
        void FaceStreet()
        {
            var aim=target+Vector3.up*.013f;
            BoardCamera.transform.SetPositionAndRotation(aim-streetAngle*Vector3.forward*.24f,streetAngle);
        }
        FaceStreet();yield return new WaitForEndOfFrame();SaveOceanFrame("普通主城-沿街.png");
        foreach(var track in new[]{M5.ImprovementTrack.Trade,M5.ImprovementTrack.Politics,M5.ImprovementTrack.Science})
        {
            string label=track==M5.ImprovementTrack.Trade?"贸易":track==M5.ImprovementTrack.Politics?"政治":"科学";
            var metros=others.Append(new M5.Metropolis{PlayerId=city.PlayerId,LocationId=city.LocationId,Track=track}).ToArray();
            var model=Resources.Load<GameObject>(PlayerComponentAssets.BuildingResource("metropolis",city.PlayerId,(int)track));
            var mesh=model.GetComponentInChildren<MeshFilter>().sharedMesh;
            foreach(var seat in new[]{"P1","P2","P3","P4"})
            {
                var variant=PlayerComponentAssets.Model(PlayerComponentAssets.BuildingResource("metropolis",seat,(int)track));
                Require(variant.GetComponentInChildren<MeshFilter>().sharedMesh==mesh,"大都会四色未共享几何");
                Require(variant.GetComponentInChildren<Renderer>().sharedMaterials.All(m=>m!=null&&m.mainTexture!=null&&m.shader.isSupported),"大都会材质不完整");
                foreach(bool hasWall in new[]{false,true})
                {
                    var spec=new TabletopPointerPreview{Kind="metropolis",Track=(int)track,Wall=hasWall};
                    var ghost=OperationPieceAssets.PointerPreview(spec,seat,target,Quaternion.identity,null,true);
                    var meshes=ghost.GetComponentsInChildren<MeshFilter>();
                    Require(meshes.Length==(hasWall?2:1)&&meshes.Any(f=>f.sharedMesh==mesh),"大都会建造预览缺件");
                    Destroy(ghost);
                }
            }
            Project(noWall,metros);Table.ResetPan();SetTop(false);Table.Frame(BoardCamera,false,normalZoom);
            yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame(label+"-全盘.png");
            FocusDetail(target,false);
            yield return new WaitForEndOfFrame();SaveOceanFrame(label+"-近景.png");
            var building=board.CommittedPieces.Cast<Transform>().Single(t=>t.name.StartsWith("Metropolis "+track+" "+city.LocationId+" ["));
            var entrance=building.forward;
            Require(building.GetComponentsInChildren<MeshFilter>().Single().sharedMesh==mesh,"大都会网格不符");
            var bounds=building.GetComponentInChildren<Renderer>().bounds;
            RecordHeight(label,bounds.size.y);
            Require(bounds.size.y>=cityHeight*1.5f,"大都会未形成足够的高度差");
            FaceStreet();yield return new WaitForEndOfFrame();SaveOceanFrame(label+"-沿街.png");
            var roof=BoardCamera.WorldToScreenPoint(bounds.center+Vector3.up*bounds.extents.y*.72f);
            Require(board.BuildingAt(new Vector2(roof.x,Screen.height-roof.y))==city.LocationId,"高处拾取错位");
            FocusDetail(target,true);
            yield return new WaitForEndOfFrame();SaveOceanFrame(label+"-俯视.png");
            Project(withWall,metros);FocusDetail(target,false);
            yield return null;yield return new WaitForEndOfFrame();SaveOceanFrame(label+"-城墙.png");
            building=board.CommittedPieces.Cast<Transform>().Single(t=>t.name.StartsWith("Metropolis "+track+" "+city.LocationId+" ["));
            Require(Vector3.Angle(entrance,building.forward)<.05f,"城门与大都会入口错向");
            Require(building.GetComponentsInChildren<MeshFilter>().Length==2,"城墙与大都会组合缺件");
        }
        board.Refresh(view);Table.ResetPan();SetTop(false);Table.Frame(BoardCamera,false,normalZoom);
        File.WriteAllText(Path.Combine(output,"实机高度.csv"),heights.ToString());
    }
}

