using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 复用实际对局入口与现有验收框架；只有显式命令行参数才执行。
public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyNaturalPieces()
    {
        var shared=new Dictionary<string,Mesh[]>();
        foreach(string rules in new[]{"base","seafarers","combined","cities-knights"})
        {
            if(configurations>0){Presentation.enabled=false;yield return null;}
            mode=rules;
            var modules=new Catan.Core.TradeModules.TradeModuleOptions{Harbormaster=true,Fishing=rules!="base"};
            string topology=Resources.Load<TextAsset>("m1-scenario").text;
            string initial=rules=="base"?Catan.Core.M2.BaseGameSession.Create(topology,4,1,tradeModules:modules).Save():
                rules=="cities-knights"?Catan.Core.M4.CitiesKnightsGameSession.Create(topology,4,17,tradeModules:modules).Save():
                rules=="seafarers"?Catan.Core.M3.SeafarersGameSession.Create("heading-for-new-shores",4,56,tradeModules:modules).Save():
                Catan.Core.M5.CombinedGameSession.Create("heading-for-new-shores",4,97,tradeModules:modules).Save();
            app.StartGame(mode,"heading-for-new-shores",4,"P1",true,authority:initial,harbormaster:true,fishing:rules!="base");
            yield return null;yield return null;
            for(int step=0;!(Snapshot.CanTrade&&app.IsHumanTurn)&&step<4000;step++)
            {
                if(app.TablePresentationBlocked){yield return null;continue;}
                if(app.IsHumanTurn)Advance();
                else {app.SetPaused(false);app.StepAi();app.SetPaused(true);}
                yield return null;
            }
            Require(Snapshot.CanTrade&&app.IsHumanTurn,"棋子验收没有完成正常开局");
            for(int step=0;!Table.Ocean.MaskReady&&step<600;step++)yield return null;
            Require(Table.Ocean.MaskReady,"棋子验收地图未就绪");
            for(int frame=0;app.TablePresentationBlocked&&frame<600;frame++)yield return null;
            app.SetPaused(false);yield return new WaitForSeconds(2);
            string before=Authority;
            var committed=Table.transform.parent.Find("Committed pieces");
            Require(committed!=null,"棋盘没有已放置棋子");
            int naturalCount=0;
            foreach(Transform piece in committed)
            {
                var renderers=piece.GetComponentsInChildren<Renderer>();
                if(!renderers.Any(r=>r.sharedMaterials.Any(m=>m.name.StartsWith("Natural_"))))continue;
                naturalCount++;
                Require(piece.GetComponentsInChildren<Collider>().Length==0,"棋子新增碰撞阻挡地图输入");
                var bounds=NaturalBounds(renderers);
                Require(Mathf.Abs(bounds.min.y-.008f)<.0002f,"新棋子没有落在棋盘表面");
            }
            Require(naturalCount>=8,"开局四色棋子未接入地图");
            VerifyStreetEntrances(committed);
            var inspectVertex=Vertices[0].Id;
            Table.Frame(BoardCamera,false,1);
            var inspect=Position(inspectVertex);inspect.y=EntityBoardAssets.BoardSurfaceHeight;
            var screen=(Vector2)BoardCamera.WorldToScreenPoint(inspect);
            var hudPoint=Table.HudCamera.transform.position;
            ScrollBoard(50,false,screen);
            Require(Mathf.Abs(BoardCamera.orthographicSize-.046f)<.0001f,"滚轮无法到达建筑特写");
            ScrollBoard(3,true,screen);
            Require(Mathf.Abs(Mathf.DeltaAngle(Table.Yaw,45))<.01f && Mathf.Abs(BoardCamera.orthographicSize-.046f)<.0001f,"旋转时倍率发生变化");
            Require(Vector2.Distance(screen,BoardCamera.WorldToScreenPoint(inspect))<1,"旋转缩放时鼠标下的地图点漂移");
            Require(Table.HudCamera.transform.position==hudPoint,"旋转移动了桌面库存相机");
            int priorPitch=Table.Pitch;
            Table.SetPitch(35,false);
            Require(Vector3.Angle(BoardCamera.transform.forward,Quaternion.Euler(35,45,0)*Vector3.forward)<.1f,"自定义俯角未生效");
            Table.SetPitch(priorPitch,false);
            ScrollBoard(-3,true,screen);Table.ResetPan();
            Table.Frame(BoardCamera,false,1);yield return new WaitForSeconds(.6f);yield return new WaitForEndOfFrame();SaveOceanFrame(rules+"-全盘.png");
            Table.Frame(BoardCamera,true,1);yield return new WaitForEndOfFrame();SaveOceanFrame(rules+"-俯视.png");
            Table.Frame(BoardCamera,false,.65f);yield return new WaitForEndOfFrame();SaveOceanFrame(rules+"-近景.png");
            if(rules=="cities-knights")
            {
                var board=GetComponent<M4App>().M6View.Board;
                foreach(var sample in new[]{(board.Cities[0],"主城"),(board.Settlements[0],"村庄")})
                {
                    var vertex=Position(sample.Item1.LocationId);
                    foreach(bool top in new[]{false,true})
                    {
                        FocusDetail(vertex,top);
                        yield return new WaitForEndOfFrame();
                        SaveOceanFrame(sample.Item2+"路口-"+(top?"82度":"66度")+".png");
                    }
                }
                FocusDetail(Position(board.Cities[0].LocationId),false);
                var center=(Vector2)BoardCamera.WorldToScreenPoint(Position(board.Cities[0].LocationId));
                ScrollBoard(3,true,center);
                int savedPitch=Table.Pitch;
                Table.SetPitch(42,false);
                var roof=Position(board.Cities[0].LocationId)+Vector3.up*.016f;
                var projected=BoardCamera.WorldToScreenPoint(roof);
                var mouse=new Vector2(projected.x,Screen.height-projected.y);
                Require(GetComponent<M4App>().VerificationBoard.BuildingAt(mouse)==board.Cities[0].LocationId,"近景建筑拾取失败");
                yield return new WaitForEndOfFrame();SaveOceanFrame("旋转45度-俯角42度.png");
                var edge=board.Roads[0].LocationId;
                FocusDetail(Position(edge),false);
                var edgePixel=GetComponent<M4App>().VerificationBoard.ScreenPoint(edge);
                var picker=typeof(M4App).GetMethod("Pick",PrivateInstance);
                Require((string)picker.Invoke(GetComponent<M4App>(),new object[]{"E",edgePixel})==edge,"旋转后道路拾取失败");
                Table.SetPitch(savedPitch,false);ScrollBoard(-3,true,center);Table.ResetPan();
            }
            Table.Frame(BoardCamera,false,1);
            var first=Vertices[0];
            foreach(string seat in new[]{"P1","P2","P3","P4"})
            foreach(string kind in new[]{"village","city","road"})
            {
                string resource=kind=="road"?PlayerComponentAssets.RoadResource(seat):PlayerComponentAssets.BuildingResource(kind,seat);
                var prefab=Resources.Load<GameObject>(resource);Require(prefab!=null,"四色资源缺失");
                var meshes=prefab.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).ToArray();
                if(!shared.TryGetValue(kind,out var expected))shared[kind]=meshes;
                else Require(meshes.SequenceEqual(expected),"四色没有共享模型网格");
                var renderers=prefab.GetComponentsInChildren<Renderer>();
                Require(renderers.Length==1&&renderers[0].sharedMaterials.Length<=3,"棋子网格或材质过度拆分");
                Require(renderers.SelectMany(r=>r.sharedMaterials).All(m=>m.mainTexture!=null&&m.shader.isSupported),"棋子材质不完整");
                var own=renderers.SelectMany(r=>r.sharedMaterials).Single(m=>m.name.StartsWith("Natural_Player_"));
                Require(own.name.EndsWith(PlayerComponentAssets.ColourKey(seat)),"玩家色不匹配");
                Require(OperationPieceAssets.Texture(kind=="village"?"settlement":kind,seat)!=null,"操作栏缩略图缺失");
                foreach(bool legal in new[]{true,false})
                {
                    var type=kind=="village"?"BuildSettlement":kind=="city"?"BuildCity":"BuildRoad";
                    var ghost=OperationPieceAssets.PointerPreview(TabletopPointerPreview.ForCommand(type),seat,Position(first.Id),Quaternion.identity,null,legal);
                    Require(ghost!=null&&ghost.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).SequenceEqual(meshes),"建造预览没有使用新网格");
                    var block=new MaterialPropertyBlock();var r=ghost.GetComponentInChildren<Renderer>();
                    if(kind=="village")r.GetPropertyBlock(block,0);else r.GetPropertyBlock(block);
                    Require(block.GetColor("_BaseColor")== (legal?new Color(.3f,1f,.75f):new Color(1f,.22f,.2f)),"预览合法性颜色丢失");
                    Destroy(ghost);
                }
            }
            Require(Authority==before,"棋子显示或建造预览改变了权威状态");
            configurations++;yield return null;
        }
        // 独立资产排布只用于四色与角度验收，不冒充真实游戏局面。
        var fixture=new GameObject("四色棋子独立验收");
        for(int seat=0;seat<4;seat++)
        {
            string id="P"+(seat+1);float x=(seat-1.5f)*.044f;
            PlayerComponentAssets.Building("village",id,"村庄",new Vector3(x,0,-.035f),fixture.transform);
            PlayerComponentAssets.Building("city",id,"城市",new Vector3(x,0,0),fixture.transform);
            PlayerComponentAssets.Road(id,"道路",new Vector3(x,0,.035f),Quaternion.Euler(0,90,0),fixture.transform);
            var cityWall=PlayerComponentAssets.Building("city-with-wall",id,"带城墙主城",new Vector3(x,0,.065f),fixture.transform);
            Require(cityWall!=null&&cityWall.GetComponentsInChildren<MeshFilter>().Any(m=>shared["city"].Contains(m.sharedMesh)),"城墙使主城退回旧模型");
        }
        foreach(var t in fixture.GetComponentsInChildren<Transform>())t.gameObject.layer=28;
        var cameraObject=new GameObject("棋子资产验收相机");var cam=cameraObject.AddComponent<Camera>();cam.enabled=false;
        cameraObject.AddComponent<UniversalAdditionalCameraData>();cam.orthographic=true;cam.orthographicSize=.09f;
        cam.nearClipPlane=.0001f;cam.farClipPlane=2;cam.cullingMask=1<<28;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.24f,.29f,.28f);
        foreach(int angle in new[]{58,66,82,90})
        {
            Vector3 center=new Vector3(0,.007f,.015f);float a=angle*Mathf.Deg2Rad;
            cam.transform.SetPositionAndRotation(center+new Vector3(0,Mathf.Sin(a),-Mathf.Cos(a))*.25f,Quaternion.Euler(angle,0,0));
            var target=new RenderTexture(1600,1200,24);var prior=RenderTexture.active;
            var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(output,"四色棋子-"+angle+"度.png"),image.EncodeToPNG());cameraImages++;
            }
            finally{RenderTexture.active=prior;target.Release();Destroy(target);Destroy(image);}
        }
        Destroy(fixture);Destroy(cameraObject);
        app.WriteSave(Path.Combine(output,"主城验收对局.json"));
        Table.Frame(BoardCamera,false,.8f);yield return null;yield return new WaitForEndOfFrame();
        SaveOceanFrame("验收对局.png");
        if(Environment.GetCommandLineArgs().Contains("--camera-review"))
            FocusDetail(Position(GetComponent<M4App>().M6View.Board.Cities[0].LocationId),false);
    }
    void ScrollBoard(float steps,bool rotate,Vector2 pointer)
    {
        if(mode=="base")GetComponent<M2App>().VerificationBoard.ScrollMap(steps,rotate,pointer);
        else if(mode=="seafarers")GetComponent<M3App>().VerificationBoard.ScrollMap(steps,rotate,pointer);
        else if(mode=="cities-knights")GetComponent<M4App>().VerificationBoard.ScrollMap(steps,rotate,pointer);
        else GetComponent<M5App>().VerificationBoard.ScrollMap(steps,rotate,pointer);
    }
    void FocusDetail(Vector3 target,bool top)
    {
        Table.ResetPan();SetTop(top);Table.Frame(BoardCamera,top,1);
        target.y=EntityBoardAssets.BoardSurfaceHeight;
        var pixel=(Vector2)BoardCamera.WorldToScreenPoint(target);
        var middle=BoardCamera.pixelRect.center;
        Table.PanFromScreen(pixel,middle);Table.EndPan();
        ScrollBoard(50,false,middle);
    }
    static Bounds NaturalBounds(Renderer[] renderers)
    {var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;}

    void VerifyStreetEntrances(Transform committed)
    {
        var roads=mode=="base"?GetComponent<M2App>().M6View.Board.Roads:
            mode=="seafarers"?GetComponent<M3App>().M6View.Board.Roads:
            mode=="cities-knights"?GetComponent<M4App>().M6View.Board.Roads:GetComponent<M5App>().M6View.Board.Roads;
        var edges=Edges.ToDictionary(e=>e.Id);
        int joined=0;
        foreach(Transform building in committed)
        {
            var colour=building.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials)
                .FirstOrDefault(m=>m.name.StartsWith("Natural_Player_city_")||m.name.StartsWith("Natural_Player_village_"));
            if(colour==null)continue;
            var vertex=Vertices.OrderBy(v=>(Position(v.Id)-building.position).sqrMagnitude).First();
            var road=roads.FirstOrDefault(r=>colour.name.EndsWith(PlayerComponentAssets.ColourKey(r.PlayerId)) &&
                edges[r.LocationId].Vertices.Contains(vertex.Id));
            if(road==null)continue;
            Vector3 towardsRoad=Position(road.LocationId)-building.position;
            towardsRoad.y=0;
            Require(Vector3.Angle(building.forward,towardsRoad)<.1f,"门前朝向与实际相邻道路不符");
            var roadPiece=committed.Cast<Transform>().Single(r=>r.name==road.LocationId);
            var mesh=roadPiece.GetComponentInChildren<MeshFilter>();
            var bounds=mesh.sharedMesh.bounds;
            var direction=towardsRoad.normalized;
            var extents=bounds.extents;
            float halfLength=Mathf.Abs(Vector3.Dot(direction,mesh.transform.TransformVector(Vector3.right*extents.x)))
                +Mathf.Abs(Vector3.Dot(direction,mesh.transform.TransformVector(Vector3.up*extents.y)))
                +Mathf.Abs(Vector3.Dot(direction,mesh.transform.TransformVector(Vector3.forward*extents.z)));
            Vector3 tip=mesh.transform.TransformPoint(bounds.center)-direction*halfLength;
            tip.y=building.position.y;
            Require(Vector3.Distance(tip,building.position)<.0015f,"石径端头没有延伸到建筑所在路口");
            joined++;
        }
        Require(joined>0,"实际对局没有可核对的建筑路口");
    }
}
