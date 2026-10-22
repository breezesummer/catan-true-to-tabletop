using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

// 仅由显式命令行启动的资产展示验收，不接触规则状态。
public sealed class ForestCampVerification:MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--forest-camp-verify")>=0)
        {var root=new GameObject("森林样板验收");DontDestroyOnLoad(root);root.AddComponent<ForestCampVerification>();}
    }
    IEnumerator Start()
    {
        yield return null;
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--forest-camp-verify");
        string output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);
        var old=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("森林资产验收");SceneManager.SetActiveScene(scene);
        yield return SceneManager.UnloadSceneAsync(old);
        var prefab=Resources.Load<GameObject>("Ultra/ForestCamp/v001/ForestCamp");
        if(prefab==null){Debug.LogError("缺少森林模型");Application.Quit(2);yield break;}
        Instantiate(prefab);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.59f,.65f);
        var sun=new GameObject("日光").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;
        sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.03f;sun.shadowNormalBias=.02f;
        var camera=new GameObject("验收镜头").AddComponent<Camera>();camera.orthographic=true;
        camera.orthographicSize=.0505f;camera.nearClipPlane=.0001f;camera.farClipPlane=2;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.68f,.65f,.59f);
        var positions=new[]{new Vector3(.055f,.105f,-.10f),new Vector3(-.07f,.08f,.10f),new Vector3(0,.15f,0),new Vector3(.047f,.052f,-.067f)};
        var names=new[]{"unity-hero","unity-reverse","unity-top","unity-camp"};
        for(int i=0;i<positions.Length;i++)
        {
            camera.transform.position=positions[i];var target=i==3?new Vector3(.015f,.004f,-.012f):new Vector3(0,.004f,0);
            camera.transform.LookAt(target,i==2?Vector3.forward:Vector3.up);camera.orthographicSize=i==3?.023f:.0505f;
            yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();
            var rt=new RenderTexture(1200,1200,24);camera.targetTexture=rt;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;
            var texture=new Texture2D(1200,1200,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,1200),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(output,names[i]+".png"),texture.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Destroy(rt);Destroy(texture);
        }
        File.WriteAllText(Path.Combine(output,"运行验证.txt"),"版本："+Application.version+"\n图形设备："+SystemInfo.graphicsDeviceName+"\n真实Unity Player渲染：四视角\n未执行：Android真机、完整棋盘集成与性能验收。");
        Debug.Log("FOREST_CAMP_PLAYER_OK");Application.Quit(0);
    }
}
