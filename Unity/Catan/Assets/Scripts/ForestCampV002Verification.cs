using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using Catan.Core.M3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

// 显式美术验收入口：使用公开地图定义，不访问权威对局或隐藏状态。
public sealed class ForestCampV002Verification:MonoBehaviour
{
    string output;Camera camera;GameObject prefab;StringBuilder report=new StringBuilder();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--forest-v002-verify")>=0){var o=new GameObject("森林v002验收");DontDestroyOnLoad(o);o.AddComponent<ForestCampV002Verification>();}}
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--forest-v002-verify");output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);
        yield return null;var old=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("森林v002真实运行");SceneManager.SetActiveScene(scene);yield return SceneManager.UnloadSceneAsync(old);
        prefab=Resources.Load<GameObject>("Ultra/ForestCamp/v002/ForestCamp");if(prefab==null)throw new Exception("缺少森林v002预制体");
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.62f);
        var sun=new GameObject("日光").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.005f;sun.shadowNormalBias=.01f;
        camera=new GameObject("验收镜头").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.nearClipPlane=.0001f;camera.farClipPlane=3;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.62f,.56f);
        var single=Instantiate(prefab);single.GetComponent<LODGroup>().ForceLOD(0);
        yield return Capture("unity-hero",new Vector3(0,.145f,-.12f),new Vector3(0,.004f,0),.0455f);
        yield return Capture("unity-reverse",new Vector3(0,.13f,.12f),new Vector3(0,.004f,0),.0455f);
        yield return Capture("unity-top",new Vector3(0,.15f,0),Vector3.zero,.0455f);
        yield return Capture("unity-camp",new Vector3(.035f,.06f,-.075f),new Vector3(.013f,.004f,-.009f),.022f);
        var jadeRoot=new GameObject("现有悬浮玉牌验收");var jade=jadeRoot.AddComponent<UltraNumberTokens>();
        jade.Initialize(new Dictionary<string,Vector3>{{"forest-sample",Vector3.zero}},camera);
        jade.Refresh(new[]{new Catan.Core.Tile{Id="forest-sample",Resource="wood",Number=6}});
        yield return Capture("unity-default-jade",new Vector3(0,.15f,-.09373f),Vector3.zero,.055f);
        yield return Capture("unity-reverse-jade",new Vector3(0,.15f,.09373f),Vector3.zero,.055f);
        Destroy(jadeRoot);
        for(int i=1;i<3;i++){single.GetComponent<LODGroup>().ForceLOD(i);yield return Capture("unity-lod"+i,new Vector3(0,.145f,-.12f),new Vector3(0,.004f,0),.0455f);}
        Destroy(single);yield return null;
        // 从当前9种航海家剧本、3/4人版本中枚举最大实际地图。
        var max=SeafarersScenarios.Ids.SelectMany(id=>new[]{3,4}.Select(n=>SeafarersScenarios.Create(id,n))).OrderByDescending(s=>s.Tiles.Length).First();
        var positions=max.Vertices.ToDictionary(v=>v.Id,v=>new Vector3(v.X*Mathf.Sqrt(3)*.02f,0,-v.Y*.02f));
        var centers=max.Tiles.ToDictionary(t=>t.Id,t=>t.Vertices.Select(v=>positions[v]).Aggregate(Vector3.zero,(a,b)=>a+b)/6);
        var board=new GameObject("最大地图森林压力样板");
        foreach(var tile in max.Tiles){var obj=Instantiate(prefab,board.transform);obj.name=tile.Id;obj.transform.position=centers[tile.Id];obj.transform.rotation=Quaternion.Euler(0,30,0);}
        var center=centers.Values.Aggregate(Vector3.zero,(a,b)=>a+b)/centers.Count;float span=centers.Values.Max(p=>Mathf.Max(Mathf.Abs(p.x-center.x),Mathf.Abs(p.z-center.z)))+.055f;
        report.AppendLine("版本："+Application.version+"；Unity："+Application.unityVersion+"；GPU："+SystemInfo.graphicsDeviceName);
        report.AppendLine("最大测试地图："+max.Id+"，"+max.PlayerCount+"人，"+max.Tiles.Length+"块；全部公开位置替换为森林进行保守压力测试，非正常地形分布。");
        yield return Capture("unity-max-map",center+new Vector3(0,span*1.6f,-span),center,span*1.08f);
        // 以GPU读回完成为边界计时，明确包含同步读回开销，不等同实际交互帧率。
        var times=new List<double>();var rt=new RenderTexture(800,800,24);var tex=new Texture2D(800,800,TextureFormat.RGB24,false);
        for(int i=0;i<35;i++)
        {
            yield return null;var watch=System.Diagnostics.Stopwatch.StartNew();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,800,800),0,0);tex.Apply();watch.Stop();
            camera.targetTexture=null;RenderTexture.active=null;if(i>=5)times.Add(watch.Elapsed.TotalMilliseconds);
        }
        rt.Release();Destroy(rt);Destroy(tex);times.Sort();report.AppendLine("800×800最大图30次同步渲染+读回：中位数="+times[15].ToString("F2")+"ms，P95="+times[28].ToString("F2")+"ms；包含GPU同步及读回，不作为正式帧率达标结论。");
        Destroy(board);yield return null;
        var adjacent=new GameObject("相邻地块及节点");
        foreach(var pos in new[]{Vector3.zero,new Vector3(.05925f,0,.034208f),new Vector3(0,0,.068416f)}){var o=Instantiate(prefab,adjacent.transform);o.transform.localPosition=pos;o.GetComponent<LODGroup>().ForceLOD(0);}
        // 实际运行资源的村庄和道路，落在共享顶点及边中心。
        PlayerComponentAssets.Building("village","P1","共享交点村庄",new Vector3(.01975f,EntityBoardAssets.BoardSurfaceHeight,.034208f),adjacent.transform);
        var road=GameObject.CreatePrimitive(PrimitiveType.Cube);road.name="道路净空量规_宽4毫米";road.transform.SetParent(adjacent.transform);road.transform.position=new Vector3(0,.001f,.034208f);road.transform.localScale=new Vector3(.034f,.002f,.004f);road.GetComponent<Renderer>().material=M3BoardRenderer.Material(new Color(.7f,.15f,.08f));
        yield return Capture("unity-adjacent",new Vector3(.04f,.16f,-.11f),new Vector3(.02f,.004f,.027f),.075f);
        yield return Capture("unity-node",new Vector3(.04f,.07f,-.02f),new Vector3(.01975f,.003f,.034208f),.028f);
        report.AppendLine("邻接：三块、真实村庄资源、4毫米道路量规；装饰0 Collider。尚未完成真实鼠标/触控拾取、玉牌遮挡与Android真机。");
        File.WriteAllText(Path.Combine(output,"运行验证.txt"),report.ToString());Debug.Log("FOREST_V002_PLAYER_OK");Application.Quit(0);
    }
    IEnumerator Capture(string name,Vector3 pos,Vector3 target,float size)
    {
        camera.transform.position=pos;camera.transform.LookAt(target,name=="unity-top"?Vector3.forward:Vector3.up);camera.orthographicSize=size;
        yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();
        var rt=new RenderTexture(1200,1200,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(1200,1200,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,1200),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(tex);
    }
}
