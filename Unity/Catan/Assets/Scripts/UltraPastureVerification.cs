using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Catan.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 显式启动的 U2 工程验证。合成局面只作用于独立展示根，不写入规则状态。
public sealed class UltraPastureVerification : MonoBehaviour
{
    [Serializable] sealed class Report
    {
        public string version=M6App.BuildVersion,result,error,device;
        public int legalLayouts,rulePackages,stressTiles,pickChecks;
        public bool orderIndependent,upgradeSameArea,ownershipAndRemoval,restore,publicOnly,noColliders;
        public List<Measurement> performance=new List<Measurement>();
    }
    [Serializable] sealed class Measurement
    {
        public string name;public int frames,renderers,renderedFrames;public bool gcAvailable;public double p50,p95,p99;public long maxGc,draws,triangles,allocated;
    }
    int renderedFrames;Camera measuredCamera;
    M6App app;string output,fixtures;Report report=new Report();
    static readonly PiecePlacement[] Empty=new PiecePlacement[0];
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {if(Array.IndexOf(Environment.GetCommandLineArgs(),"--ultra-pasture-verify")>=0)new GameObject("U2 显式验证").AddComponent<UltraPastureVerification>();}
    static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:"";}
    void Start()
    {
        output=Path.GetFullPath(Arg("--ultra-pasture-verify"));fixtures=Path.GetFullPath(Arg("--ultra-theme-fixtures"));Directory.CreateDirectory(output);
        Application.runInBackground=true;report.device=SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType;
        RenderPipelineManager.endCameraRendering+=OnRendered;
        StartCoroutine(Guard(Run()));
    }
    IEnumerator Guard(IEnumerator work)
    {
        var stack=new Stack<IEnumerator>();stack.Push(work);
        while(stack.Count>0)
        {
            object current=null;bool next=false;
            try {next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}
            catch(Exception ex){report.error=ex.ToString();Finish(false);yield break;}
            if(!next){stack.Pop();continue;}if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
        }
        Finish(true);
    }
    void OnRendered(ScriptableRenderContext context,Camera camera){if(camera==measuredCamera)renderedFrames++;}
    void OnDestroy(){RenderPipelineManager.endCameraRendering-=OnRendered;}
    void Finish(bool ok)
    {
        report.result=ok?"通过":"失败";File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
        Debug.Log(ok?"CATAN_U2_OK":"CATAN_U2_FAILED "+report.error);Application.Quit(ok?0:1);
    }
    static void Require(bool test,string message){if(!test)throw new InvalidOperationException(message);}
    IEnumerator Frames(int n=5){for(int i=0;i<n;i++)yield return null;}
    IEnumerator Switch(string id){app.RequestBoardTheme(id);yield return Frames();Require(BoardThemeController.Current.Id==id,"主题未完成切换");}
    IEnumerator Run()
    {
        yield return Frames();app=FindFirstObjectByType<M6App>();Require(app!=null,"缺少游戏入口");
        app.ReadSave(Path.Combine(fixtures,"crowded.json"));yield return Frames();yield return Switch("ultra");
        string authority=app.ExportEnvelope();
        yield return SampleLayouts();Require(authority==app.ExportEnvelope(),"样板测试改变权威局面");
        foreach(string fixture in new[]{"ordinary","maximum","crowded","fog"})
        {
            app.ReadSave(Path.Combine(fixtures,fixture+".json"));yield return Frames();
            var tiles=FindObjectsByType<UltraPastureTile>(FindObjectsSortMode.None);
            Require(tiles.Length>0,"实际局面缺少草原样板："+fixture);
            string layout=string.Join(";",tiles.OrderBy(t=>t.TileId).Select(t=>t.LayoutSignature));
            string saved=app.ExportEnvelope();yield return Switch("tabletop");
            Require(FindObjectsByType<UltraPastureTile>(FindObjectsSortMode.None).Length==0,"原主题残留草原");
            yield return Switch("ultra");Require(saved==app.ExportEnvelope(),"切换改变权威状态");
            Require(layout==string.Join(";",FindObjectsByType<UltraPastureTile>(FindObjectsSortMode.None).OrderBy(t=>t.TileId).Select(t=>t.LayoutSignature)),"切换后牧场未还原");
            app.ReadSave(Path.Combine(fixtures,fixture+".json"));yield return Frames();
            Require(layout==string.Join(";",FindObjectsByType<UltraPastureTile>(FindObjectsSortMode.None).OrderBy(t=>t.TileId).Select(t=>t.LayoutSignature)),"存档恢复后牧场不同");
            Capture(FindFirstObjectByType<TabletopScene>().TableCamera,fixture+"-board.png");
            if(fixture=="ordinary")
            {
                var basic=app.GetComponent<M2App>();var basicBoard=basic.VerificationBoard;var view=basic.M6View;
                foreach(var pasture in FindObjectsByType<UltraPastureTile>(FindObjectsSortMode.None))
                {
                    foreach(string id in pasture.VertexIds)
                    {Require(basic.VerificationPick(Catan.Core.M2.CommandKind.BuildSettlement,basicBoard.ScreenPoint(id))==id,"草原角点拾取发生偏移");report.pickChecks++;}
                    string signature=pasture.LayoutSignature;basicBoard.Preview(pasture.VertexIds[0],false,true);basicBoard.ClearPreview();
                    Require(signature==pasture.LayoutSignature,"预览触发开发区");
                }
                foreach(var edge in view.Board.Edges)
                {Require(basic.VerificationPick(Catan.Core.M2.CommandKind.BuildRoad,basicBoard.ScreenPoint(edge.Id))==edge.Id,"道路拾取发生偏移");report.pickChecks++;}
            }
        }
        report.restore=true;
        foreach(string rules in new[]{"base","seafarers","cities-knights","combined"})
        {
            app.StartGame(rules,"heading-for-new-shores",4,"P1",true);yield return Frames();
            Require(FindObjectsByType<UltraPastureTile>(FindObjectsSortMode.None).Length>0,"规则包未接入："+rules);report.rulePackages++;
        }
        // 迷雾模型和玉牌只由公开地形决定，未知资源没有草原组件。
        var fogRoot=new GameObject("未知地形验证");
        Require(EntityBoardAssets.Terrain("fog",Vector3.zero,fogRoot.transform)==null,"未知地形取得草原资产");
        var jade=fogRoot.AddComponent<UltraNumberTokens>();jade.Initialize(new Dictionary<string,Vector3>{{"T0",Vector3.zero}});
        jade.Refresh(new[]{new Tile{Id="T0",Resource="fog",Number=6}});
        Require(fogRoot.transform.childCount==0,"迷雾显示隐藏点数");Destroy(fogRoot);report.publicOnly=true;
        app.ReadSave(Path.Combine(fixtures,"maximum.json"));yield return Frames();
        yield return Switch("tabletop");yield return Measure("最大49格真实桌游");
        yield return Switch("ultra");yield return Measure("最大49格草原样板混合占位");
        var board=FindFirstObjectByType<M3BoardRenderer>();var publicTiles=app.GetComponent<M3App>().M6View.Board.Tiles;
        var stress=new GameObject("U2 合成49格密度样板");
        foreach(var tile in publicTiles)
        {
            var instance=Instantiate(BoardThemeAssets.Model(EntityBoardAssets.TerrainResource("wool")),board.Position(tile.Id),Quaternion.identity,stress.transform);
            instance.name="合成样板 "+tile.Id;report.stressTiles++;
            var positions=tile.Vertices.ToDictionary(id=>id,id=>board.Position(id));var pasture=instance.GetComponent<UltraPastureTile>();pasture.Bind(tile,positions);
            pasture.Refresh(Empty,new[]{0,2,4}.Select(i=>new PiecePlacement{LocationId=pasture.VertexIds[i],PlayerId="P"+(i/2+1)}).ToArray());
            board.transform.Find(tile.Id)?.gameObject.SetActive(false);
        }
        yield return Frames();Capture(board.TableScene.TableCamera,"maximum-repeated-pasture.png");
        yield return Measure("最大49格全部开发草原合成压力");Destroy(stress);
    }
    IEnumerator SampleLayouts()
    {
        var sample=Instantiate(BoardThemeAssets.Model(EntityBoardAssets.TerrainResource("wool")));sample.name="草原独立样板";
        var tile=sample.GetComponent<UltraPastureTile>();var positions=new Dictionary<string,Vector3>();var ids=new string[6];
        for(int i=0;i<6;i++){ids[i]="V"+i;float angle=(30+i*60)*Mathf.Deg2Rad;positions.Add(ids[i],new Vector3(Mathf.Cos(angle)*.04f,.008f,Mathf.Sin(angle)*.04f));}
        var data=new Tile{Id="T0",Resource="wool",Number=6,Vertices=ids};tile.Bind(data,positions);
        int[] masks=Enumerable.Range(0,64).Where(m=>(m&(((m<<1)|(m>>5))&63))==0).ToArray();
        foreach(int mask in masks)
        {
            var buildings=Enumerable.Range(0,6).Where(i=>(mask&(1<<i))!=0).Select(i=>new PiecePlacement{LocationId=ids[i],PlayerId="P"+(i%3+1)}).ToArray();
            tile.Refresh(buildings,Empty);string expected=tile.LayoutSignature;
            Require(tile.DevelopedCount==buildings.Length&&Mathf.Abs(tile.DevelopedFraction-buildings.Length/3f)<.0001f,"三分之一开发面积不符");
            tile.Refresh(Empty,Empty);foreach(var p in buildings.Reverse()){var current=buildings.Reverse().TakeWhile(x=>x!=p).Concat(new[]{p}).ToArray();tile.Refresh(current,Empty);}
            Require(tile.LayoutSignature==expected,"建造顺序改变布局");
            tile.Refresh(Empty,buildings);Require(tile.DevelopedCount==buildings.Length,"城市升级扩大面积");
            tile.Refresh(buildings,Empty);Require(tile.LayoutSignature==expected,"城市降级改变布局");report.legalLayouts++;
        }
        tile.Refresh(new[]{new PiecePlacement{LocationId=ids[0],PlayerId="P4"}},Empty);
        Require(tile.LayoutSignature.Contains("P4"),"归属未更新");tile.Refresh(Empty,Empty);Require(tile.DevelopedCount==0,"移除后未恢复原始态");
        report.orderIndependent=report.upgradeSameArea=report.ownershipAndRemoval=true;
        Require(sample.GetComponentsInChildren<Collider>(true).Length==0,"模型拦截点击");report.noColliders=true;
        var cameraObject=new GameObject("U2 四视图相机");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.052f;
        camera.nearClipPlane=.001f;camera.farClipPlane=2;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.17f,.16f);camera.cullingMask=1<<28;
        cameraObject.AddComponent<UniversalAdditionalCameraData>();
        var poses=new[]{new Vector3(0,.16f,0),new Vector3(.13f,.035f,-.14f),new Vector3(.10f,.12f,-.14f),new Vector3(.10f,.12f,-.14f)};
        for(int i=0;i<4;i++)
        {
            if(i==3)
            {
                tile.Refresh(new[]{new PiecePlacement{LocationId=ids[0],PlayerId="P1"},new PiecePlacement{LocationId=ids[2],PlayerId="P2"},new PiecePlacement{LocationId=ids[4],PlayerId="P1"}},Empty);
                foreach(int vertex in new[]{0,2,4})PlayerComponentAssets.Building("village",vertex==2?"P2":"P1","角点样板",positions[ids[vertex]],sample.transform);
            }
            SetLayer(sample.transform,28);camera.transform.position=poses[i];camera.transform.LookAt(new Vector3(0,.008f,0));
            foreach(var group in sample.GetComponentsInChildren<LODGroup>())group.ForceLOD(0);
            yield return Frames(2);Capture(camera,"pasture-view-"+i+".png");
        }
        tile.Refresh(new[]{new PiecePlacement{LocationId=ids[0],PlayerId="P1"}},Empty);SetLayer(sample.transform,28);
        foreach(Transform child in sample.transform)if(child.name.StartsWith("角点样板",StringComparison.Ordinal))child.gameObject.SetActive(false);
        yield return Frames(2);Capture(camera,"pasture-one-third.png");
        var jade=sample.AddComponent<UltraNumberTokens>();jade.Initialize(new Dictionary<string,Vector3>{{"T0",Vector3.zero}},camera);jade.Refresh(new[]{data});SetLayer(sample.transform,28);
        yield return Frames(2);
        foreach(var text in sample.GetComponentsInChildren<TextMesh>())Require(text.GetComponent<Renderer>().bounds.size.magnitude<.025f,"玉牌字体超出薄牌边框");
        Capture(camera,"pasture-jade.png");
        Destroy(cameraObject);Destroy(sample);yield return Frames();
    }
    static void SetLayer(Transform root,int layer){root.gameObject.layer=layer;foreach(Transform child in root)SetLayer(child,layer);}
    IEnumerator Measure(string label)
    {
        measuredCamera=FindFirstObjectByType<TabletopScene>().TableCamera;
        Require(measuredCamera!=null&&measuredCamera.enabled,"实际棋盘相机被关闭");
        // Windows 隐藏窗口可能不提交常规呈现。显式离屏渲染同一棋盘；这里只比较离屏工作负载，不报告游戏FPS。
        var target=new RenderTexture(1440,900,24);target.antiAliasing=4;target.Create();
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
        var values=new double[360];long maxGc=-1,draws=-1,triangles=-1;bool gcAvailable=false;int start=0;
        using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
        using(var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
        using(var tris=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1))
        {
            try
            {
                for(int i=0;i<60;i++){RenderPipeline.SubmitRenderRequest(measuredCamera,request);yield return null;}
                start=renderedFrames;gcAvailable=gc.Valid;
                for(int i=0;i<values.Length;i++)
                {
                    long begin=System.Diagnostics.Stopwatch.GetTimestamp();
                    RenderPipeline.SubmitRenderRequest(measuredCamera,request);yield return null;
                    values[i]=(System.Diagnostics.Stopwatch.GetTimestamp()-begin)*1000d/System.Diagnostics.Stopwatch.Frequency;
                    if(gc.Valid)maxGc=Math.Max(maxGc,gc.LastValue);if(draw.Valid)draws=Math.Max(draws,draw.LastValue);if(tris.Valid)triangles=Math.Max(triangles,tris.LastValue);
                }
                Require(renderedFrames-start>=360,"采样未实际渲染");Require(draws>0&&triangles>0,"没有有效渲染计数，拒绝性能结论");
            }
            finally {target.Release();Destroy(target);}
        }
        File.WriteAllLines(Path.Combine(output,"frames-"+report.performance.Count+".csv"),new[]{"offscreen_frame_wall_ms"}.Concat(values.Select(v=>v.ToString("F5",System.Globalization.CultureInfo.InvariantCulture))));
        Array.Sort(values);report.performance.Add(new Measurement{name=label+"（1440×900棋盘离屏，含帧调度）",frames=values.Length,renderedFrames=renderedFrames-start,gcAvailable=gcAvailable,p50=values[180],p95=values[342],p99=values[356],maxGc=maxGc,draws=draws,triangles=triangles,
            allocated=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),renderers=FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length});
    }

    void Capture(Camera camera,string name)
    {
        var previous=RenderTexture.active;var target=new RenderTexture(1440,900,24);var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
        try{target.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());}
        finally{RenderTexture.active=previous;target.Release();Destroy(target);Destroy(image);}
    }
}
