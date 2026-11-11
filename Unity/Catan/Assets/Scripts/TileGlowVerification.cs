using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;
using Catan.Core.M5;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyTileGlow(string[] args)
    {
        mode="combined";
        app.ReadSave(args[Array.IndexOf(args,"--tile-glow")+1]);
        app.SetPaused(true);
        yield return null; yield return null;
        Require(app.PlayerCount==4 && app.ThreeKingdoms && app.Harbormaster && app.Fishing && app.ScenarioId=="through-the-desert","流光验收配置错误");
        while(!Table.Ocean.MaskReady || !UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
        string before=Authority;
        var client=GetComponent<M5App>();
        var view=client.M6View.Board;
        var glow=Hud.TileGlow;
        if(args.Contains("--danger-fluorescence"))
        {
            var dangerRun=VerifyDangerFluorescence(client);
            while(dangerRun.MoveNext())yield return dangerRun.Current;
            yield break;
        }
        var one=new[]{new Catan.Core.PiecePlacement{PlayerId="P1",LocationId="a"}};
        var two=one.Concat(new[]{new Catan.Core.PiecePlacement{PlayerId="P1",LocationId="b"}}).ToArray();
        var none=Array.Empty<Catan.Core.PiecePlacement>();
        foreach(string resource in new[]{"wood","brick","wool","wheat","ore","gold","lake","desert","sea","fog"})
        {
            var tile=new Catan.Core.Tile{Id="probe",Resource=resource,Number=resource=="desert"||resource=="lake"? (int?)null:6,Vertices=new[]{"a","b"}};
            bool productive=resource!="desert"&&resource!="sea"&&resource!="fog";
            Require(TabletopTileGlow.ProductionLevel(tile,one,none,"P1")== (productive?1:0),"单村庄产量分类错误："+resource);
            Require(TabletopTileGlow.ProductionLevel(tile,two,none,"P1")== (productive?2:0),"双村庄产量分类错误："+resource);
            Require(TabletopTileGlow.ProductionLevel(tile,none,one,"P1")== (productive?2:0),"城市产量分类错误："+resource);
            Require(TabletopTileGlow.ProductionLevel(tile,one,none,"P2")==0,"其他玩家点亮本人地块");
        }
        var probe=new TabletopTileGlow();
        var probeTiles=new[]{new Catan.Core.Tile{Id="upgrade",Resource="wood",Number=6,Vertices=new[]{"a","b"}}};
        probe.Bind(null,probeTiles,one,none,"P1",1);
        Require(probe.Level("upgrade")==1,"村庄未亮翡翠");
        probe.Bind(null,probeTiles,none,one,"P1",2);
        Require(probe.Level("upgrade")==2,"升级城市未切换常态金色");
        probe.Bind(null,probeTiles,one,none,"P1",3);
        Require(probe.Level("upgrade")==1,"城市降级未恢复翡翠");
        probe.Bind(null,probeTiles,none,none,"P1",4);
        Require(!probe.Owned.Any(),"失去建筑后残留流光");
        probe.Bind(null,probeTiles,none,one,"P2",5);
        Require(!probe.Owned.Any(),"换席位后残留流光");
        var block=new MaterialPropertyBlock();
        foreach(var tile in view.Tiles)
        {
            int level=TabletopTileGlow.ProductionLevel(tile,view.Settlements,view.Cities,app.HumanSeat);
            Require(glow.Level(tile.Id)==level,"读档常态产量错误");
            var renderer=client.VerificationBoard.transform.Find(tile.Id)?.GetComponentInChildren<MeshRenderer>();
            if(renderer==null)continue;
            renderer.GetPropertyBlock(block);
            var value=block.GetVector("_Glow");
            Require(value.x==(level>0?1:0)&&value.z==(level>1?1:0),"实际地块流光材质错误");
            Require(renderer.sharedMaterial.shader.isSupported,"地块着色器不可用");
        }
        Table.SetPitch(45,false);Table.Frame(BoardCamera,false,1);
        var surfaces=client.VerificationBoard.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("产出荧光 ")&&r.enabled).ToArray();
        Require(surfaces.Length==glow.Owned.Count(),"荧光层数与本人产出地块不符");
        var flow=surfaces.First().sharedMaterial;
        Require(flow.shader.isSupported&&surfaces.All(r=>r.sharedMaterial==flow),"荧光着色器不可用或材质未共享");
        Require(surfaces.All(r=>r.GetComponent<MeshFilter>().sharedMesh.vertexCount==4&&r.GetComponent<Collider>()==null),"荧光几何预算错误");
        yield return new WaitForSecondsRealtime(1);
        yield return new WaitForEndOfFrame();CaptureCamera("01-常态流光全盘.png");
        foreach(int level in new[]{1,2})
        {
            string id=glow.Owned.FirstOrDefault(t=>glow.Level(t)==level && t!=view.RobberTileId);
            Require(id!=null,"实局缺少对应常态产量");
            Table.Frame(BoardCamera,false,1);FocusDetail(Position(id),false);ScrollBoard(-4,false,BoardCamera.pixelRect.center);
            Color32[] firstPixels=null;
            yield return new WaitForEndOfFrame();
            for(int frame=0;frame<4;frame++)
            {
                // 固定时刻便于复核沿边位移，避免帧率差异改变截图。
                flow.SetFloat("_FlowClock",frame*.6f);
                CaptureCamera((level==1?"翡翠":"金色")+"-"+frame+".png");
                if(frame==0)firstPixels=(Color32[])cameraPixels.Clone();
                else
                {
                    int moved=0;
                    for(int pixel=0;pixel<cameraPixels.Length;pixel++)
                        if(Math.Abs(cameraPixels[pixel].g-firstPixels[pixel].g)>20 || Math.Abs(cameraPixels[pixel].r-firstPixels[pixel].r)>20)moved++;
                    Require(moved>300,"荧光光头没有沿边移动");
                }
                Require(glow.Level(id)==level,"没有产出时流光颜色改变");
            }
        }
        app.ReactionClick(M6App.MapMarksButton.center,0);
        Require(!TabletopMapMarks.Visible,"流光未随地图标记隐藏");
        app.ReactionClick(M6App.MapMarksButton.center,0);
        Require(TabletopMapMarks.Visible,"地图标记未恢复");
        Require(before==Authority,"流光改变权威状态");
        Debug.Log("CATAN_PERSISTENT_TILE_GLOW_OK tiles="+glow.Owned.Count());
        configurations++;
    }
    IEnumerator VerifyDangerFluorescence(M5App client)
    {
        var board=client.VerificationBoard;
        var map=board.GetComponent<PaintedMap>();
        var view=client.M6View.Board;
        string before=Authority;
        var originalRobber=PaintedMap.OccupiedHex(view.Tiles.ToDictionary(t=>t.Id,t=>Position(t.Id)),view.RobberTileId);
        var originalPirate=PaintedMap.OccupiedHex(view.Tiles.ToDictionary(t=>t.Id,t=>Position(t.Id)),view.PirateTileId);
        var material=board.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.StartsWith("危险荧光 ")).sharedMaterial;
        var block=new MaterialPropertyBlock();
        var land=view.Tiles.First(t=>Hud.TileGlow.Level(t.Id)>0);
        var desert=view.Tiles.First(t=>t.Resource=="desert");
        var sea=view.Tiles.Where(t=>t.Resource=="sea").Take(2).ToArray();
        foreach(var tile in new[]{land,desert,sea[0],sea[1]})
        {
            bool pirate=tile.Resource=="sea";
            int property=pirate?PaintedMap.PirateHex:PaintedMap.RobberHex;
            Vector3 p=Position(tile.Id);
            var hex=new Vector4(p.x,p.z,1,0);
            map.SetDangerHex(property,hex);
            var surface=board.transform.Find(pirate?"危险荧光 海盗危险":"危险荧光 强盗危险").GetComponent<MeshRenderer>();
            Require(surface.enabled&&surface.sharedMaterial==material&&material.shader.isSupported,"危险荧光未启用或未复用材质");
            surface.GetPropertyBlock(block);
            Require(block.GetVector("_Center")==new Vector4(p.x,p.z,0,1),"危险荧光坐标或颜色错误");
            Require(pirate?surface.transform.position.y>TabletopOcean.WaterHeight+.0012f:surface.transform.position.y>EntityBoardAssets.BoardSurfaceHeight,"危险光被地形或波峰遮挡");
            int surfaceCount=board.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name.StartsWith("危险荧光 "));
            for(int i=0;i<20;i++)map.SetDangerHex(property,hex);
            Require(board.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name.StartsWith("危险荧光 "))==surfaceCount,"重复刷新增加危险荧光层");
            Table.SetPitch(45,false);Table.Frame(BoardCamera,false,1);FocusDetail(p,false);ScrollBoard(-4,false,BoardCamera.pixelRect.center);
            yield return new WaitForEndOfFrame();
            Color32[] pixels=null;
            string name=pirate?"海盗-"+tile.Id.Replace(':','_'):tile.Resource=="desert"?"强盗-沙漠":"强盗-产出格";
            for(int frame=0;frame<3;frame++)
            {
                material.SetFloat("_FlowClock",frame*.6f);
                CaptureCamera(name+"-"+frame+".png");
                if(frame==0)pixels=(Color32[])cameraPixels.Clone();
                else Require(cameraPixels.Where((c,i)=>Math.Abs(c.r-pixels[i].r)>20||Math.Abs(c.g-pixels[i].g)>20).Count()>300,"红色荧光没有持续流动");
            }
            map.SetDangerHex(property,Vector4.zero);
            Require(!surface.enabled,"离场后残留危险荧光");
        }
        map.SetDangerHex(PaintedMap.RobberHex,originalRobber);
        map.SetDangerHex(PaintedMap.PirateHex,originalPirate);
        app.ReactionClick(M6App.MapMarksButton.center,0);
        Require(!TabletopMapMarks.Visible,"危险流光未随地图标记隐藏");
        app.ReactionClick(M6App.MapMarksButton.center,0);
        Require(TabletopMapMarks.Visible&&Authority==before,"危险光显示恢复改变了对局");
        Debug.Log("CATAN_DANGER_FLUORESCENCE_OK");
        configurations++;
    }

    IEnumerator VerifyCollectionPan(string tile)
    {
        double start=Time.unscaledTimeAsDouble;
        Vector2 pointer=BoardCamera.pixelRect.center;
        TabletopCollectionFlight tracked=null;
        TabletopCardPose launch=null;
        var intervals=new System.Collections.Generic.List<float>(160);
        while(Time.unscaledTimeAsDouble-start<6)
        {
            yield return new WaitForEndOfFrame();
            float seconds=Time.unscaledDeltaTime;
            var flight=Hud.Collections.Active(Time.unscaledTimeAsDouble).FirstOrDefault(f=>f.SourceTileId==tile && f.LaunchPoint!=null);
            if(tracked==null && flight!=null){tracked=flight;launch=flight.LaunchPoint;}
            if(tracked==null)continue;
            if(tracked.Progress(Time.unscaledTimeAsDouble)>=1)break;
            Require(tracked.LaunchPoint==launch,"拖图改变了空中飞牌的起点");
            intervals.Add(seconds*1000);
            Table.PanFromScreen(pointer,pointer+Vector2.right*(120*seconds));
        }
        Table.EndPan();
        Require(intervals.Count>=2 && tracked.Progress(Time.unscaledTimeAsDouble)>=1,"拖图期间未观察到完整的地块收牌");
        var projected=MobileTabletop.Logical(GetComponent<M5App>().VerificationBoard.ScreenPoint(tile));
        Require(Vector2.Distance(projected,new Vector2(launch.X,launch.Y))>10,"镜头没有离开飞牌起点");
        intervals.Sort();
        Debug.Log("CATAN_COLLECTION_PAN_OK frames="+intervals.Count+" p50Ms="+intervals[intervals.Count/2]+" p95Ms="+intervals[Mathf.CeilToInt(intervals.Count*.95f)-1]+" maxMs="+intervals[intervals.Count-1]);
    }

    IEnumerator VerifyDangerHex(M5App client, Command move)
    {
        var board = client.VerificationBoard;
        var view = client.M6View.Board;
        var actualPirate = view.PirateTileId;
        Require(actualPirate != null && actualPirate != "frame", "实局需要有海盗海格");
        var surface = Table.Ocean.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial)
            .First(m=>m.shader.name=="Catan/ContinuousOcean");
        var terrain = board.transform.Find(move.TargetId).GetComponentInChildren<Renderer>().sharedMaterial;
        Vector4 Hex(string id)
        {
            if (id==null || id=="frame") return Vector4.zero;
            var p=board.Position(id); return new Vector4(p.x,p.z,1,0);
        }
        Require(surface.GetVector(PaintedMap.PirateHex)==Hex(actualPirate) &&
            terrain.GetVector(PaintedMap.RobberHex)==Hex(view.RobberTileId),"读档的危险格不匹配");
        Require(view.RobberTileId!=move.TargetId && client.VerificationHost.Submit(move).Success,"强盗移动未通过规则确认");
        client.M6Refresh();
        yield return null; yield return null;
        Require(terrain.GetVector(PaintedMap.RobberHex)==Hex(move.TargetId),"强盗移动后危险格未更新");
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForEndOfFrame(); SaveOceanFrame("陆地与海洋-全盘.png");
        app.ReactionClick(M6App.MapMarksButton.center,0);
        yield return null; yield return new WaitForEndOfFrame();
        Require(!TabletopMapMarks.Visible,"未隐藏危险格");
        SaveOceanFrame("隐藏危险光.png");
        app.ReactionClick(M6App.MapMarksButton.center,0);
        foreach(var id in new[]{move.TargetId,actualPirate})
        {
            FocusDetail(Position(id),false);
            ScrollBoard(-6,false,BoardCamera.pixelRect.center);
            yield return null; yield return new WaitForEndOfFrame();
            SaveOceanFrame(id==actualPirate ? "海盗海格.png" : "强盗地块.png");
        }
        var another = view.Tiles.First(t=>t.Resource=="sea" && t.Id!=actualPirate);
        foreach(var id in new[]{another.Id,"frame",null,actualPirate})
        {
            board.RefreshSeafarers(view.Ships,id);
            Require(surface.GetVector(PaintedMap.PirateHex)==Hex(id) && terrain.GetVector(PaintedMap.PirateHex)==Hex(id),
                "海盗移格或离场后的危险光位置错误");
        }
        Debug.Log("CATAN_DANGER_HEX_OK robber="+move.TargetId+" pirate="+actualPirate);
        configurations++;
    }

    void CheckMapMarks(bool visible)
    {
        var slips = GetComponent<M5App>().VerificationBoard.GetComponentsInChildren<JadeNumberSlip>();
        foreach (var prefix in new[] { "点数 ", "湖泊点数 ", "渔港点数 " })
            Require(slips.Any(s => s.name.StartsWith(prefix,StringComparison.Ordinal)), "缺少验收玉签 "+prefix);
        var ports = slips.Where(s => s.GetComponent<HarborSign>() != null).ToArray();
        Require(ports.Length > 0 && ports.All(s => s.GetComponent<Renderer>().enabled), "港口贸易签不可见");
        Require(slips.Except(ports).All(s => s.GetComponent<Renderer>().enabled == visible), "点数玉签显示状态不一致");
        Require(TabletopMapMarks.Visible == visible && Shader.GetGlobalFloat("_CatanMapMarksHidden") == (visible ? 0 : 1),"地块光效显示状态不一致");
    }
}
