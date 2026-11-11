using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyTimberPort(string[] args)
    {
        mode="combined";
        app.ReadSave(args[System.Array.IndexOf(args,"--timber-port")+1]);
        yield return null; yield return null;
        Require(app.PlayerCount==4 && app.ThreeKingdoms && app.Fishing && app.Harbormaster && app.ScenarioId=="through-the-desert","木材港验收配置错误");
        for(int i=0;i<900&&!Table.Ocean.MaskReady;i++)yield return null;
        Require(Table.Ocean.MaskReady,"海岸遮罩未就绪");
        bool previous=BoardArtPreference.Painted;
        app.SelectBoardArt(true);
        try
        {
            var markers=Table.transform.parent.GetComponentInChildren<TabletopReferenceMarkers>();
            var port=markers.transform.Find("港口").GetComponentInChildren<PaintedTimberPort>();
            Require(port!=null,"地图缺少木材港");
            var surface=port.transform.Find("木材港原画").GetComponent<MeshFilter>();
            Require(surface.sharedMesh.vertexCount>4 && surface.GetComponent<Renderer>().sharedMaterial.mainTexture!=null,"木材港缺少原画或透明轮廓");
            var probe=new GameObject("木材港六向导入检查");
            probe.SetActive(false);
            try
            {
                var art=probe.AddComponent<PaintedTimberPort>();
                var drawings=new HashSet<Texture>();
                for(int i=0;i<6;i++)
                {
                    art.Align(Quaternion.Euler(0,30+60*i,0)*Vector3.forward,.04f);
                    var renderer=probe.GetComponentInChildren<MeshRenderer>(true);
                    var texture=renderer.sharedMaterial.mainTexture;
                    Require(texture!=null && texture.width>=1500 && drawings.Add(texture) &&
                        art.Footprint().Any(),"木材港方向缺少独立高清画稿或拾取轮廓："+i);
                }
            }
            finally { Destroy(probe); }
            Table.Frame(BoardCamera,false,1);
            yield return new WaitForEndOfFrame();
            var id=port.name.Substring("港口 ".Length);
            bool hit=port.Footprint().Any(t=>{
                var pixel=BoardCamera.WorldToScreenPoint((t[0]+t[1]+t[2])/3);
                return markers.PickPort(new Vector2(pixel.x,Screen.height-pixel.y),BoardCamera)==id;
            });
            Require(hit,"木材港画稿未命中交易港");
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            if(args.Contains("--board-art-views")) DetailFrame(Vector3.zero,"穿越沙漠-全盘",true);
            else SaveOceanFrame("穿越沙漠-全盘.png");
            DetailFrame(surface.GetComponent<Renderer>().bounds.center,"木材港-近景",detailSize:.044f);
            if(args.Contains("--board-art-views"))
            {
                var center=surface.GetComponent<Renderer>().bounds.center;
                var authority=Authority;
                int portCount=markers.PlacedPortCount;
                foreach(bool painted in new[]{false,true})
                {
                    app.SelectBoardArt(painted);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Require(markers.PlacedPortCount==portCount && markers.transform.Find("港口").Cast<Transform>()
                        .Where(t=>t.gameObject.activeSelf).All(t=>painted
                        ? t.Find("木材港原画")!=null || t.Find("港口原画")!=null : t.Find("PierDeck")!=null),"港口未同步切换");
                    var pieces=GetComponent<M5App>().VerificationBoard.CommittedPieces.GetComponentsInChildren<FantasyBoardPiece>();
                    var buildings=pieces.Where(p=>p.transform.Find("幻想棋子画面")?.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name is "city" or "village" or "metro-trade" or "metro-science" or "metro-politics").ToArray();
                    Require(buildings.Length>0 && buildings.All(p=>p.transform.Find("幻想棋子画面").GetComponent<MeshRenderer>().forceRenderingOff==!painted),"建筑未同步切换");
                    Require(Authority==authority,"外观切换改变了对局");
                    if(!painted)
                    {
                        DetailFrame(Vector3.zero,"3D-全盘",true);
                        DetailFrame(center,"3D-近景",detailSize:.044f);
                    }
                }
            }
            configurations++;
        }
        finally { if(previous!=BoardArtPreference.Painted) app.SelectBoardArt(previous); }
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
