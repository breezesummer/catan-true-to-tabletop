using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyTerrainArt()
    {
        mode="combined";
        app.StartGame(mode,"through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        yield return null;
        yield return null;
        for(int step=0;!Snapshot.CanTrade && step<4000;step++)
        {
            if(app.IsHumanTurn) Advance();
            else { app.SetPaused(false); app.StepAi(); app.SetPaused(true); }
            yield return null;
        }
        for(int frame=0;!Table.Ocean.MaskReady && frame<900;frame++) yield return null;
        Require(Table.Ocean.MaskReady && Snapshot.CanTrade,"验收对局未就绪");
        var map=Table.transform.parent.GetComponentInChildren<PaintedMap>();
        var land=Tiles.First(t=>PaintedMap.Region(t.Resource)>=0 && PaintedMap.Region(t.Resource)<8 &&
            Tiles.Any(s=>PaintedMap.Region(s.Resource)==8 && t.Vertices.Intersect(s.Vertices).Count()==2));
        var surface=map.transform.Find(land.Id).GetComponentInChildren<MeshRenderer>();
        var ink=surface.sharedMaterial;
        Require(ink.shader.isSupported && ink.mainTexture!=null && ink.GetTexture("_RoadEdge")!=null &&
            ink.GetTexture("_BeachEdge")!=null,"地块边缘素材未加载");
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForSecondsRealtime(2);
        yield return new WaitForEndOfFrame();
        SaveOceanFrame("穿越沙漠-对局.png");
        DetailFrame(surface.bounds.center,"穿越沙漠-海岸近景",detailSize:.112f,matchCameraMsaa:true);
        if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--wave-frames")>=0)
        {
            for(int frame=0;frame<3;frame++)
            {
                yield return new WaitForSecondsRealtime(.45f);
                yield return new WaitForEndOfFrame();
                DetailFrame(surface.bounds.center,"波光-"+frame,detailSize:.112f,matchCameraMsaa:true);
            }
        }
        var pasture=map.transform.Find(Tiles.First(t=>PaintedMap.Region(t.Resource)==2).Id);
        DetailFrame(pasture.position,"牧场路肩",detailSize:.064f,matchCameraMsaa:true);
        foreach(var region in new[]{1,4})
        {
            var tile=Tiles.Where(t=>PaintedMap.Region(t.Resource)==region)
                .OrderByDescending(t=>Tiles.Count(n=>n!=t && PaintedMap.Region(n.Resource)<8 && t.Vertices.Intersect(n.Vertices).Count()==2)).First();
            DetailFrame(map.transform.Find(tile.Id).position,region==1?"砖丘路肩":"矿山路肩",detailSize:.064f,matchCameraMsaa:true);
        }
        configurations++;
    }
    IEnumerator VerifyWarehousePort(string[] args)
    {
        bool brick=args.Contains("--brick-port");
        string label=brick?"砖港":"木材港";
        var resource=brick?Catan.Core.Resource.Brick:Catan.Core.Resource.Wood;
        mode="combined";
        int save = System.Array.IndexOf(args,brick?"--brick-port":"--timber-port")+1;
        if (save < args.Length && !args[save].StartsWith("-")) app.ReadSave(args[save]);
        else app.StartGame(mode,"through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
        yield return null; yield return null;
        Require(app.PlayerCount==4 && app.ThreeKingdoms && app.Fishing && app.Harbormaster && app.ScenarioId=="through-the-desert",label+"验收配置错误");
        for(int i=0;i<900&&!Table.Ocean.MaskReady;i++)yield return null;
        Require(Table.Ocean.MaskReady,"海岸遮罩未就绪");
        var markers=Table.transform.parent.GetComponentInChildren<TabletopReferenceMarkers>();
        var trade=GetComponent<M5App>().M6View.Board.Ports.First(p=>p.Resource==resource);
        var port=markers.PortObject(trade.Id);
        var surface=port.transform.Find("HarborHouse");
        var sign=port.GetComponentInChildren<HarborSign>();
        Require(sign != null && sign.name=="TradePlaque" && sign.PickVolume != null && sign.Footprint != null,label+"缺少交易牌或拾取轮廓");
        var mat=surface.GetComponent<Renderer>().sharedMaterial;
        Require(mat.name==(brick?"BrickHarbor":"LumberHarbor") && mat.GetTexture("_BaseMap") && mat.GetTexture("_BumpMap") &&
            mat.GetTexture("_MetallicGlossMap"),label+"表面贴图未绑定");
        var deck=port.transform.Find("PierDeck").GetComponent<Renderer>();
        var stock=port.transform.Find(brick?"BrickStack":"LogStack").GetComponent<Renderer>();
        var house=surface.GetComponent<Renderer>();
        Vector3 LocalCenter(Renderer part) => port.transform.InverseTransformPoint(part.transform.TransformPoint(part.localBounds.center));
        var shed=LocalCenter(house); var cargo=LocalCenter(stock);
        var shore=CoastalDecals.ShoreEnd-CoastalDecals.DockInset;
        var footprint=CoastalDecals.Footprint(port.transform).SelectMany(t=>t).Select(p=>port.transform.InverseTransformPoint(p).z).ToArray();
        Require(Mathf.Abs(footprint.Min()-CoastalDecals.PlatformEnd(resource))<.0001f,label+"平台与商船泊位边缘不一致");
        Require(deck.bounds.min.y<TabletopOcean.WaterHeight && shed.x<cargo.x,label+"栈桩或货场位置错误");
        Require(port.GetComponentsInChildren<Collider>().Length==0 &&
            port.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled && r.sharedMaterials.Contains(mat))==(brick?6:5),label+"呈现或拾取组件异常");
        var parts=new List<Renderer>{house,stock,deck};
        if(brick)
        {
            var quay=port.transform.Find("QuayWall").GetComponent<Renderer>();
            var hoist=port.transform.Find("LoadingCrane").GetComponent<Renderer>();
            var crane=LocalCenter(hoist);
            Require(footprint.Max()>=shore && footprint.Max()<shore+.0012f &&
                cargo.z<shed.z && shed.z<shore,"砖港石岸与陆缘未接合或卸货面朝向错误");
            Require(quay.bounds.min.y<TabletopOcean.WaterHeight && quay.bounds.max.y>TabletopOcean.WaterHeight &&
                house.bounds.min.y>TabletopOcean.WaterHeight,"砖港驳岸或仓底水线错误");
            Require(crane.x<cargo.x && crane.z<shed.z && hoist.bounds.min.y>port.transform.position.y+.0007f &&
                LocalCenter(deck).x<0 && LocalCenter(sign.Face).y>.006f,"砖港吊盘、挑栈或屋坡牌位置错误");
            Debug.Log($"CATAN_BRICK_QUAY_MM 仓心 {shed*1000} 货垛 {cargo*1000} 吊架 {crane*1000} 岸线 {shore*1000:F2} 石岸后沿 {footprint.Max()*1000:F2}");
            parts.Add(quay);
            parts.Add(hoist);
        }
        else
        {
            var hoist=port.transform.Find("TimberHoist").GetComponent<Renderer>();
            var crane=LocalCenter(hoist);
            float back=footprint.Max();
            Require(shed.z<shore && cargo.z<shed.z && back>=shore && back<shore+.001f,"木材港后檐没有贴岸或货场未临海");
            Require(house.bounds.min.y<TabletopOcean.WaterHeight && stock.bounds.min.y>TabletopOcean.WaterHeight,"仓脚与备货水线错误");
            Require(crane.x<cargo.x && crane.z<shed.z && hoist.bounds.min.y>port.transform.position.y+.0007f,"吊臂或悬货落位错误");
            Require(LocalCenter(sign.Face).y>.007f,"交易牌未在石仓屋坡上");
            Debug.Log($"CATAN_TIMBER_QUAY_MM 仓心 {shed*1000} 货垛 {cargo*1000} 吊架 {crane*1000} 岸线 {shore*1000:F2} 后檐 {back*1000:F2}");
            parts.Add(hoist);
        }
        foreach (var part in parts)
        {
            var mesh=part.GetComponent<MeshFilter>().sharedMesh;
            Require(!mesh.isReadable && mesh.lodCount>1 && mesh.subMeshCount==1,label+"远景网格或材质槽错误 "+part.name);
        }
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForEndOfFrame();
        var pose=sign.transform.rotation;
        parts.Add(sign.Face);
        foreach (var part in parts)
        {
            var pixel=BoardCamera.WorldToScreenPoint(port.transform.TransformPoint(LocalCenter(part)));
            Require(markers.PickPort(new Vector2(pixel.x,Screen.height-pixel.y),BoardCamera)==trade.Id,label+"部件未命中 "+part.name);
        }
        Require(Quaternion.Angle(pose,sign.transform.rotation)<.01f,"交易牌未固定在仓房");
        yield return new WaitForSecondsRealtime(2);
        yield return new WaitForEndOfFrame();
        if(args.Contains("--board-art-views")) DetailFrame(Vector3.zero,"穿越沙漠-全盘",true);
        else SaveOceanFrame("穿越沙漠-全盘.png");
        var centerOfPort=port.transform.TransformPoint(brick ? new Vector3(0,.0047f,.0028f) : new Vector3(0,.0054f,.0021f));
        var sea=-port.transform.forward;
        DetailFrame(centerOfPort,label+"-近景",detailSize:brick ? .0130f : .0146f,
            eyeOffset:(sea+port.transform.right*(brick ? .48f : .42f)+Vector3.up*.48f).normalized*.16f,matchCameraMsaa:true);
        DetailFrame(centerOfPort,label+"-俯视",detailSize:.0205f,eyeOffset:Vector3.up*.16f,
            viewUp:port.transform.forward,matchCameraMsaa:true);
        DetailFrame(centerOfPort,label+"-棋盘视距",detailSize:.057f,
            eyeOffset:(sea+Vector3.up*2.25f).normalized*.16f,matchCameraMsaa:true);
        if(args.Contains("--board-art-views"))
        {
            var center=surface.GetComponent<Renderer>().bounds.center;
            if(args.Contains("--wheat-coast"))
            {
                var focus=markers.WheatPortCenter;
                Require(focus!=Vector3.zero,"未找到砖丘麦港");
                focus.y=EntityBoardAssets.BoardSurfaceHeight;
                var heading=Quaternion.Euler(0,-60,0);
                focus+=heading*new Vector3(.013f,0,.009f);
                var eye=heading*new Vector3(0,.135f,-.096f);
                DetailFrame(focus,"麦港-修改后",detailSize:.038f,eyeOffset:eye,matchCameraMsaa:true);
            }
            DetailFrame(Vector3.zero,"3D-全盘",true);
            DetailFrame(center,"3D-近景",detailSize:.044f);
        }
        configurations++;
    }

    IEnumerator VerifyCoastalViews()
    {
        var observed=new HashSet<string>();
        foreach(string rules in new[]{"base","seafarers","combined"})
        {
            mode=rules;
            const string scenario="heading-for-new-shores";
            var modules=new Catan.Core.TradeModules.TradeModuleOptions{Harbormaster=true,Fishing=true};
            // 两个固定合法开局都有 4 点渔场与木材港同边，可逐版对比。
            string save=rules=="base"?Catan.Core.M2.BaseGameSession.Create(Resources.Load<TextAsset>("m1-scenario").text,4,1,tradeModules:modules).Save():
                rules=="seafarers"?Catan.Core.M3.SeafarersGameSession.Create(scenario,4,56,tradeModules:modules).Save():
                Catan.Core.M5.CombinedGameSession.Create(scenario,4,97,tradeModules:modules).Save();
            app.StartGame(rules,scenario,4,"P1",true,authority:save,harbormaster:true,fishing:true);
            yield return null;yield return null;
            for(int i=0;i<900&&!Table.Ocean.MaskReady;i++)yield return null;
            Require(Table.Ocean.MaskReady,"海岸遮罩未就绪");
            Table.Frame(BoardCamera,false,1);
            yield return new WaitForEndOfFrame();
            DetailFrame(Vector3.zero,rules+"-全盘",true);
            string before=Authority;
            var board=Table.transform.parent;
            var markers=board.GetComponentInChildren<TabletopReferenceMarkers>();
            var ports=markers.transform.Find("港口").Cast<Transform>().ToArray();
            foreach(var ground in board.GetComponent<FishingTabletopAssets>().transform.Cast<Transform>().Where(t=>t.name.StartsWith("渔场 ")).OrderByDescending(t=>t.name.EndsWith("[ground-4]")))
            {
                var water=ground.GetComponent<NaturalFishingGround>();
                Require(water!=null,"缺少自然渔场");
                var a=water.Shore[0];var b=water.Shore[1];var c=water.Shore[2];
                var outer=water.Outer;
                var waterMesh=ground.Find("浅水").GetComponent<MeshFilter>().sharedMesh;
                var p=waterMesh.vertices.Select(ground.TransformPoint).ToArray();
                Require((p[2]-p[5]).sqrMagnitude<1e-10f&&(p[3]-p[4]).sqrMagnitude<1e-10f,"两条浅水岸段接缝断开");
                Require(Vector3.Distance(outer[0],a)>.4f*Vector3.Distance(a,b),"渔场没有向外拓宽");
                Require(ground.Find("游鱼").GetComponent<MeshFilter>().sharedMesh.vertexCount==NaturalFishingGround.FishPerEdge*8,"两侧鱼群不完整");
                Require(ground.GetComponentsInChildren<Collider>().Length==0,"渔场挡住棋子操作");
                string kind=Vector3.Dot(b-(a+c)*.5f,outer[1]-b)>0?"凸岸":"凹岸";
                var port=ports.FirstOrDefault(t=>
                {
                    var entry=t.position+t.forward*(CoastalDecals.ShoreEnd-CoastalDecals.DockInset);
                    return new[]{(a+b)*.5f,(b+c)*.5f}.Any(mid=>(new Vector2(mid.x-entry.x,mid.z-entry.z)).magnitude<.0035f);
                });
                string label=kind+(port==null?"":"重合");
                for(int j=0;j<2;j++)Require(ground.Find("散网"+j)!=null,"缺少沿岸散网");
                if(port!=null)
                {
                    markers.AlignPortSigns(BoardCamera);
                    var sign=port.GetComponentInChildren<HarborSign>();
                    var pixel=BoardCamera.WorldToScreenPoint(sign.Face.bounds.center);
                    Require(markers.PickPort(new Vector2(pixel.x,Screen.height-pixel.y),BoardCamera)!=null,"比例牌不能命中贸易港");
                }
                if(observed.Add(label))
                {
                    DetailFrame((a+2*b+c)*.25f+(outer[1]-b)*.5f,label);
                    Debug.Log("CATAN_COAST_FRAME: "+rules+" "+label+" "+ground.name);
                }
            }
            if(rules=="seafarers")
            {
                var sample=board.GetComponentInChildren<NaturalFishingGround>();
                yield return VerifyFishMotion(sample);
            }
            Require(Authority==before,"沿岸显示改变对局状态");
            configurations++;
        }
        Require(observed.Contains("凹岸重合")&&observed.Contains("凸岸重合"),"实机地图未同时覆盖凹岸与凸岸的港口重合");
    }
    IEnumerator VerifyFishMotion(NaturalFishingGround sample)
    {
        var fish=sample.transform.Find("游鱼").gameObject;
        int oldLayer=fish.layer;fish.layer=27;
        var root=new GameObject("鱼群运动验收");
        var camera=root.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camera.orthographicSize=.046f;camera.nearClipPlane=.001f;camera.farClipPlane=1;
        camera.aspect=1;camera.cullingMask=1<<27;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.12f,.13f);
        var center=sample.Shore[1]+(sample.Outer[1]-sample.Shore[1])*.5f;
        camera.transform.SetPositionAndRotation(center+Vector3.up*.20f,Quaternion.Euler(90,0,0));
        var target=new RenderTexture(512,512,24);
        var image=new Texture2D(512,512,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            Color32[] first=null;
            for(int i=0;i<2;i++)
            {
                yield return new WaitForSeconds(.85f);
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();
                var pixels=image.GetPixels32();
                Require(pixels.Count(p=>!p.Equals(pixels[0]))<pixels.Length/3,"鱼群验收相机混入其他场景");
                if(i==0)first=pixels;
                else
                {
                    int changed=pixels.Where((p,j)=>!p.Equals(first[j])).Count();
                    Require(changed>300,"鱼群没有实际游动");
                    Debug.Log("CATAN_FISH_SWIM_OK: changed="+changed);
                }
                File.WriteAllBytes(Path.Combine(output,"鱼群运动-"+i+".png"),image.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active=previous;fish.layer=oldLayer;
            target.Release();Destroy(target);Destroy(image);Destroy(root);
        }
    }
    void DetailFrame(Vector3 center,string label,bool full=false,float detailSize=.052f,Vector3? eyeOffset=null,Vector3? viewUp=null,bool matchCameraMsaa=false)
    {
        var camera=BoardCamera;
        var pos=camera.transform.position;var rot=camera.transform.rotation;
        var rect=camera.rect;float size=camera.orthographicSize,near=camera.nearClipPlane,aspect=camera.aspect;
        int width=full?1920:1280,height=full?1200:960;
        var target=new RenderTexture(width,height,24);
        if(matchCameraMsaa && camera.allowMSAA && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
        {
            var descriptor=target.descriptor;
            descriptor.msaaSamples=pipeline.msaaSampleCount;
            target.antiAliasing=SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
        }
        var previous=RenderTexture.active;
        var frame=new Texture2D(width,height,TextureFormat.RGB24,false);
        var markers=Table.transform.parent.GetComponentInChildren<TabletopReferenceMarkers>();
        try
        {
            if(!full)
            {
                camera.rect=new Rect(0,0,1,1);camera.orthographicSize=detailSize;camera.nearClipPlane=.0001f;
                camera.aspect=(float)width/height;
                var offset=eyeOffset??new Vector3(0,.16f,-.035f);
                camera.transform.SetPositionAndRotation(center+offset,Quaternion.LookRotation(-offset,viewUp??Vector3.up));
            }
            markers.AlignPortSigns(camera);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;
            frame.ReadPixels(new Rect(0,0,width,height),0,0);frame.Apply();
            var pixels=frame.GetPixels32();
            Require(pixels.Where((p,i)=>i%257==0 && !p.Equals(pixels[0])).Take(101).Count()>100,"棋盘相机帧不可用");
            File.WriteAllBytes(Path.Combine(output,label+".png"),frame.EncodeToPNG());cameraImages++;
        }
        finally
        {
            RenderTexture.active=previous;target.Release();Destroy(target);Destroy(frame);
            camera.rect=rect;camera.aspect=aspect;camera.orthographicSize=size;camera.nearClipPlane=near;camera.transform.SetPositionAndRotation(pos,rot);
            markers.AlignPortSigns(camera);
        }
    }
}
