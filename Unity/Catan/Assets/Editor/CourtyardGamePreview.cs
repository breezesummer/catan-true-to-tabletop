using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Catan.Core.M2;

// 仅显式编辑器菜单启用：新建临时对局并提交合法开局命令，不读取或覆盖用户存档。
[InitializeOnLoad]
public static class CourtyardGamePreview
{
    const string Key="Catan.CourtyardPreview";
    static int stage;
    static double next;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static CourtyardGamePreview(){EditorApplication.update+=Tick;}
    [MenuItem("Catan/美术/查看四色庭院实机效果")]
    public static void Run()
    {
        if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;EditorApplication.delayCall+=Run;return;}
        CourtyardImport.Run();
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));string output=Path.Combine(repo,"docs/verification/courtyard-four-colours");Directory.CreateDirectory(output);
        SessionState.SetString(Key,output);stage=0;next=0;EditorSceneManager.OpenScene("Assets/M6.unity",OpenSceneMode.Single);
        // 复用既有 GameView 分辨率配置，截图仍由实际游戏窗口产生。
        typeof(B45DockEditorVerification).GetMethod("SetSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1440,900});
        EditorApplication.isPlaying=true;
    }
    static object Get(object owner,string name)=>owner.GetType().GetProperty(name,Flags).GetValue(owner);
    static void Tick()
    {
        string output=SessionState.GetString(Key,"");if(output.Length==0||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        try
        {
            var app=UnityEngine.Object.FindAnyObjectByType<M6App>();if(app==null)return;
            if(stage==0){app.StartGame("base","heading-for-new-shores",4,"P1",true);stage=1;next=EditorApplication.timeSinceStartup+2;return;}
            var basic=app.GetComponent<M2App>();if(basic==null)return;var host=(M2LocalGameHost)Get(basic,"VerificationHost");if(host==null)return;
            if(stage==1)
            {
                host.NewGame(4,20261029);
                for(int i=0;i<20;i++)
                {
                    var v=host.View("P1");if(v.Phase!=GamePhase.SetupSettlement&&v.Phase!=GamePhase.SetupRoad)break;
                    v=host.View(v.ActivePlayerId);bool village=v.Phase==GamePhase.SetupSettlement;
                    string target=(village?v.LegalVertexIds:v.LegalEdgeIds).First();
                    var result=host.Submit(new Command{Id=Guid.NewGuid().ToString("N"),PlayerId=v.ActivePlayerId,Kind=village?CommandKind.SetupSettlement:CommandKind.SetupRoad,TargetId=target});
                    if(!result.Success)throw new Exception("开局命令失败："+result.ErrorCode);
                }
                typeof(M2App).GetMethod("M6Refresh",Flags).Invoke(basic,null);stage=2;next=EditorApplication.timeSinceStartup+4;return;
            }
            var board=(BoardRenderer)Get(basic,"VerificationBoard");
            if(stage==2)
            {
                var red=host.View("P1").Board.Settlements.First(p=>p.PlayerId=="P1");var instance=board.CommittedPieces.Cast<Transform>().First(t=>t.name.StartsWith(red.LocationId+" "));
                if(instance.GetComponentsInChildren<MeshFilter>().Length!=45||instance.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("红色庭院没有通过正式入口接入");
                foreach(string id in new[]{"P1","P2","P3","P4"})
                {
                    string key=PlayerComponentAssets.ColourKey(id);
                    if(PlayerComponentAssets.BuildingResource("village",id)!="ThreeKingdoms/v003/courtyard-"+key)throw new Exception("村庄配色资源错误："+id);
                    var piece=board.CommittedPieces.Cast<Transform>().First(t=>t.name.Contains("[courtyard-"+key+"]"));
                    if(piece.GetComponentsInChildren<MeshFilter>().Length!=45||piece.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("庭院网格/碰撞错误："+id);
                    if(!piece.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).SequenceEqual(instance.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh)))throw new Exception("四色未共享网格");
                    var colourMaterials=piece.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m.name.StartsWith("Village_Seat_Courtyard_")).Distinct().ToArray();
                    if(colourMaterials.Length!=(key=="red"?1:3)||colourMaterials.Any(m=>m.name!="Village_Seat_Courtyard_Red"&&!m.name.StartsWith("Village_Seat_Courtyard_"+key)))throw new Exception("玩家材质错配："+id);
                    if(OperationPieceAssets.Texture("settlement",id)==null)throw new Exception("玩家缩略图缺失："+id);
                    Capture(board.BoardCamera,piece.position,Path.Combine(output,"庭院-"+key+".png"));
                }
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>())if(renderer.sharedMaterials.Any(m=>m==null||m.shader.name!="Universal Render Pipeline/Lit"))throw new Exception("材质未适配URP");
                ScreenCapture.CaptureScreenshot(Path.Combine(output,"游戏默认视角.png"));
                Capture(board.BoardCamera,board.Position(red.LocationId),Path.Combine(output,"红色庭院游戏近景.png"));
                File.WriteAllText(Path.Combine(output,"运行验证.txt"),"Unity="+Application.unityVersion+"\n图形设备="+SystemInfo.graphicsDeviceName+"\n正式村庄入口="+PlayerComponentAssets.BuildingResource("village","P1")+"\n红色村庄="+host.View("P1").Board.Settlements.Count(p=>p.PlayerId=="P1")+"\n网格=45；材质URP/Lit；无模型碰撞；四色正式入口均已接入；四色共享45个网格，各自玩家材质与缩略图存在。\n真实Unity临时对局，合法完成开局，AI暂停；没有写入用户存档。近景使用同一游戏模型、棋盘和光照的URP相机。\n");
                board.ChangeZoom(-.14f);var screen=board.BoardCamera.WorldToScreenPoint(board.Position(red.LocationId));board.TableScene.PanFromScreen(new Vector2(screen.x,screen.y),new Vector2(Screen.width*.52f,Screen.height*.55f));board.TableScene.EndPan();stage=3;next=EditorApplication.timeSinceStartup+3;return;
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"游戏拉近视角.png"));
            File.WriteAllText(Path.Combine(output,"完成.txt"),"通过");SessionState.EraseString(Key);Debug.Log("CATAN_COURTYARD_GAME_OK");
        }
        catch(Exception ex){SessionState.EraseString(Key);Debug.LogException(ex);File.WriteAllText(Path.Combine(output,"失败.txt"),ex.ToString());}
    }
    static void Capture(Camera camera,Vector3 center,string path)
    {
        var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize,near=camera.nearClipPlane;var rect=camera.rect;
        camera.rect=new Rect(0,0,1,1);camera.nearClipPlane=.0001f;camera.orthographicSize=.019f;camera.transform.position=center+new Vector3(.030f,.026f,-.040f);camera.transform.LookAt(center+Vector3.up*.004f);
        var target=new RenderTexture(1600,1200,24,RenderTextureFormat.ARGB32);target.Create();var prior=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;
            var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally{RenderTexture.active=prior;target.Release();UnityEngine.Object.DestroyImmediate(target);camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;camera.nearClipPlane=near;camera.rect=rect;}
    }
}
