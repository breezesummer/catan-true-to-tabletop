using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Catan.Core.M2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 显式菜单/命令行启用的美术验收，只操作临时本地对局，不连接服务器或写用户存档。
[InitializeOnLoad]
public static class FishingPhotoEditorVerification
{
    const string Key="Catan.FishingPhotoReview";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator work;
    static double next,deadline;
    static string output;
    static M6App app;
    static readonly List<string> checks=new List<string>();
    static FishingPhotoEditorVerification(){EditorApplication.update+=Tick;}

    [MenuItem("Catan/美术/验收捕鱼实体原照")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先退出当前运行中的对局");
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        output=Path.Combine(root,"docs/verification/fishing-photo-v002");Directory.CreateDirectory(output);
        SessionState.SetString(Key,output);
        EditorSceneManager.OpenScene("Assets/M6.unity",OpenSceneMode.Single);
        typeof(B45DockEditorVerification).GetMethod("SetSize",Flags).Invoke(null,new object[]{1920,1200});
        EditorApplication.isPlaying=true;
    }

    static object Get(object owner,string field)=>owner.GetType().GetField(field,Flags).GetValue(owner);
    static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Flags).SetValue(owner,value);
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static void Tick()
    {
        output=SessionState.GetString(Key,"");
        if(output.Length==0||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(work==null)
            {
                app=UnityEngine.Object.FindFirstObjectByType<M6App>();if(app==null)return;
                checks.Clear();work=Review();deadline=EditorApplication.timeSinceStartup+180;
            }
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("实体原照验收超时");
            if(EditorApplication.timeSinceStartup<next)return;
            if(!work.MoveNext()){Finish(null);return;}
            if(work.Current is double wait)next=EditorApplication.timeSinceStartup+wait;
        }
        catch(Exception error){Finish(error);}
    }

    static IEnumerator Review()
    {
        yield return 1.0;
        FishingF6SampleVerification.Run();FishingF6SampleVerification.VerifyAll();
        foreach(string name in new[]{"lake","old-boot","fish-1","fish-2","fish-3","ground-4","ground-5","ground-6","ground-8","ground-9","ground-10"})
        {
            string resource=FishingTabletopAssets.Resource(name);
            Require(resource==FishingTabletopAssets.PhotoRoot+name,"没有切换到原照资产："+name);
            var model=Resources.Load<GameObject>(resource);
            var materials=model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).ToArray();
            Require(materials.Length==2&&materials.Count(m=>m.mainTexture!=null)==1,"原照材质或贴图缺失："+name);
            var material=materials.Single(m=>m.mainTexture!=null);
            Require(AssetDatabase.GetAssetPath(material.mainTexture)=="Assets/Resources/"+resource+"-albedo.png","贴图引用错误："+name);
            Require(Math.Abs(material.GetFloat("_Metallic"))<.001f,"纸板错误使用金属材质："+name);
            Require(FishingTabletopAssets.Icon(name)==Resources.Load<Texture2D>(resource),"界面图标没有切换："+name);
        }
        Require(FishingTabletopAssets.Resource("fish-back")==FishingTabletopAssets.Root+"fish-back","未知背面入口错误");
        checks.Add("11件新模型、原照贴图、图标与URP材质；旧背面/奖励保留；底面、尺寸、无碰撞体通过");

        foreach(string mode in new[]{"cities-knights","base","seafarers","combined"})
        {
            app.StartGame(mode,"heading-for-new-shores",4,"P1",true,harbormaster:true,fishing:true);yield return 1.2;
            object presentation=mode=="base"?(object)app.GetComponent<M2App>():mode=="cities-knights"?app.GetComponent<M4App>():mode=="seafarers"?(object)app.GetComponent<M3App>():app.GetComponent<M5App>();
            var board=(Component)Get(presentation,"board");
            var layer=board.GetComponent<FishingTabletopAssets>();
            var grounds=layer.transform.Cast<Transform>().Where(t=>t.name.StartsWith("渔场 ")).ToArray();
            Require(grounds.Length==6,"没有显示六个渔港："+mode);
            foreach(var ground in grounds)
            {
                Require(ground.GetComponentsInChildren<Collider>().Length==0,"渔港遮挡拾取");
                Require(ground.GetComponentsInChildren<MeshFilter>().Single().sharedMesh.vertexCount<200,"薄板网格异常");
                var renderer=ground.GetComponentInChildren<Renderer>();
                Require(renderer.sharedMaterials.Any(m=>m.mainTexture!=null&&AssetDatabase.GetAssetPath(m.mainTexture).Contains("HarborFishing/v002/")),"桌面未使用原照渔港");
            }
            for(int i=0;i<grounds.Length;i++)for(int j=0;j<i;j++)Require(Vector3.Distance(grounds[i].position,grounds[j].position)>=.0449f,"渔港间距不足："+mode);
            var identities=grounds.Select(t=>t.GetInstanceID()).OrderBy(id=>id).ToArray();
            string before=(string)Get(Get(presentation,"host"),"session").GetType().GetMethod("Save").Invoke(Get(Get(presentation,"host"),"session"),null);
            for(int i=0;i<20;i++)presentation.GetType().GetMethod("M6Refresh",Flags).Invoke(presentation,null);
            Require(identities.SequenceEqual(layer.transform.Cast<Transform>().Where(t=>t.name.StartsWith("渔场 ")).Select(t=>t.GetInstanceID()).OrderBy(id=>id)),"重复刷新重建模型");
            var rotations=grounds.Select(t=>t.rotation).ToArray();
            foreach(var ground in grounds)ground.rotation=Quaternion.Euler(0,17,0)*ground.rotation;
            Set(layer,"layoutKey",null);presentation.GetType().GetMethod("M6Refresh",Flags).Invoke(presentation,null);
            for(int i=0;i<grounds.Length;i++)Require(Quaternion.Angle(grounds[i].rotation,rotations[i])<.01f,"重排没有刷新V口朝向");
            string after=(string)Get(Get(presentation,"host"),"session").GetType().GetMethod("Save").Invoke(Get(Get(presentation,"host"),"session"),null);
            Require(before==after,"美术刷新改变规则状态");
            checks.Add(mode+"：六个原照渔港、无碰撞体、间距与20次对象复用，重排朝向刷新、规则状态不变");
            board.GetType().GetMethod("SetCamera").Invoke(board,new object[]{true});yield return .8;
            var table=board.GetComponentInChildren<TabletopScene>();
            var hud=table.HudCamera;
            Require(hud!=null&&hud.isActiveAndEnabled&&table.TableCamera.GetUniversalAdditionalCameraData().cameraStack.Contains(hud),"固定操作模型相机未加入游戏相机："+mode);
            Require(table.Objects.ActiveCount>=3,"骰子模型未生成："+mode);
            checks.Add(mode+"：固定操作模型相机有效，相机叠加存在，活动桌面物件="+table.Objects.ActiveCount);
            var projection=table.Objects.GetComponentsInChildren<Renderer>().Where(r=>r.gameObject.layer==TabletopScene.HudLayer).Take(12).Select(r=>new Projection{物件=r.name,尺寸=r.bounds.size,视口=hud.WorldToViewportPoint(r.bounds.center)}).ToArray();
            string diagnostic=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../.local/fishing-photo-"+mode+"-固定相机检查.json"));
            File.WriteAllText(diagnostic,JsonUtility.ToJson(new CameraCheck{像素单位=table.UnitsPerPixel,近裁面=hud.nearClipPlane,远裁面=hud.farClipPlane,模型=projection},true));
            ScreenCapture.CaptureScreenshot(Path.Combine(output,mode+"-游戏俯视.png"));yield return .8;
            if(mode=="base")
            {
                var basic=(M2App)presentation;var host=(M2LocalGameHost)Get(basic,"host");
                host.NewGame(4,97,harbormaster:true,fishing:true);
                var game=(BaseGameSession)Get(host,"session");var policy=new Catan.AI.BaseGameAi();
                for(int step=0;step<20;step++)
                {
                    var view=game.GetPlayerView("P1");if(view.Phase!=GamePhase.SetupSettlement&&view.Phase!=GamePhase.SetupRoad)break;
                    var command=policy.Decide(game.GetAiPlayerView(view.ActivePlayerId));command.Id="原照验收开局-"+step;
                    var result=game.Execute(command);Require(result.Success,"合法开局失败："+result.ErrorCode);
                }
                var state=game.GetAuthoritativeStateForTesting();state.ActivePlayerId="P1";state.Turn=9;state.Phase=GamePhase.Action;
                var fish=state.TradeModules.Fishing;string[] ids={"F01","F12","F22"};
                foreach(var holding in fish.Holdings){fish.Supply=fish.Supply.Concat(holding.TokenIds).ToArray();holding.TokenIds=Array.Empty<string>();}
                fish.Supply=fish.Supply.Where(id=>!ids.Contains(id)&&id!="BOOT").ToArray();fish.Discard=fish.Discard.Except(ids).ToArray();
                fish.Holdings.Single(h=>h.PlayerId=="P1").TokenIds=ids;fish.OldBootPlayerId="P1";
                Set(game,"state",state);game.AssertInvariants();
                typeof(M2App).GetMethod("M6Refresh",Flags).Invoke(basic,null);yield return 2.0;
                var basicBoard=(BoardRenderer)Get(basic,"board");basicBoard.SetCamera(false);yield return 1.0;
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"基础版-游戏默认视角.png"));yield return .8;
                basicBoard.SetCamera(true);yield return .8;
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"基础版-湖泊渔港与臭靴.png"));yield return .8;
                var lake=basicBoard.transform.Find(game.GetPlayerView("P1").TradeModules.Fishing.LakeTileId);
                Require(lake!=null&&lake.GetComponentInChildren<Renderer>().sharedMaterials.Any(m=>m.mainTexture!=null&&AssetDatabase.GetAssetPath(m.mainTexture).EndsWith("v002/lake-albedo.png")),"湖泊原照没有接入");
                Capture(basicBoard.BoardCamera,lake.position,.053f,Path.Combine(output,"湖泊-游戏近景.png"),true);
                var ground=basicBoard.GetComponent<FishingTabletopAssets>().transform.Cast<Transform>().First(t=>t.name.Contains("ground-4"));
                Capture(basicBoard.BoardCamera,ground.position,.052f,Path.Combine(output,"渔港-游戏近景.png"),true);
                Capture(basicBoard.BoardCamera,lake.position,.055f,Path.Combine(output,"湖泊-游戏侧视.png"),false);
                // 同一GameView相机拉近；界面中的臭靴和鱼筹码仍来自正式图标入口。
                basicBoard.ChangeZoom(-.11f);var screen=basicBoard.BoardCamera.WorldToScreenPoint(lake.position);
                basicBoard.TableScene.PanFromScreen(new Vector2(screen.x,screen.y),new Vector2(Screen.width*.50f,Screen.height*.55f));basicBoard.TableScene.EndPan();yield return 1.0;
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"基础版-游戏拉近.png"));yield return .8;
                checks.Add("基础版合法完成开局；临时夹具同时展示1/2/3鱼与P1臭靴；湖泊实机贴图、默认/俯视/拉近/侧视截图");
            }
            app.ShowMenu();yield return .5;
        }
    }

    static void Capture(Camera camera,Vector3 center,float size,string path,bool top)
    {
        var position=camera.transform.position;var rotation=camera.transform.rotation;float originalSize=camera.orthographicSize,near=camera.nearClipPlane;var rect=camera.rect;
        var target=new RenderTexture(1600,1200,24,RenderTextureFormat.ARGB32);target.Create();var previous=RenderTexture.active;
        try
        {
            camera.rect=new Rect(0,0,1,1);camera.orthographicSize=size;camera.nearClipPlane=.0001f;
            camera.transform.position=center+(top?new Vector3(0,.16f,-.035f):new Vector3(.045f,.025f,-.065f));camera.transform.LookAt(center+Vector3.up*.004f);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
            var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=originalSize;camera.nearClipPlane=near;camera.rect=rect;}
    }

    static void Finish(Exception error)
    {
        SessionState.EraseString(Key);work=null;
        File.WriteAllText(Path.Combine(output,"Unity验证.json"),JsonUtility.ToJson(new Result{状态=error==null?"通过":"失败",Unity=Application.unityVersion,显卡=SystemInfo.graphicsDeviceName,检查=checks.ToArray(),错误=error?.ToString()},true));
        if(error!=null)Debug.LogException(error);else Debug.Log("CATAN_FISHING_PHOTO_OK");
        if(Environment.GetCommandLineArgs().Contains("--fishing-photo-verify"))EditorApplication.Exit(error==null?0:1);
        else EditorApplication.isPlaying=false;
    }
    [Serializable] sealed class Result{public string 状态,Unity,显卡,错误;public string[] 检查;}
    [Serializable] sealed class CameraCheck{public float 像素单位,近裁面,远裁面;public Projection[] 模型;}
    [Serializable] sealed class Projection{public string 物件;public Vector3 尺寸,视口;}
}
