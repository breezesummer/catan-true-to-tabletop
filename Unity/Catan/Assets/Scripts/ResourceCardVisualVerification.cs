using System;
using System.Collections;
using System.IO;
using UnityEngine;

// 显式命令行启用的卡面检查，不进入正常游戏流程。
public sealed class ResourceCardVisualVerification : MonoBehaviour
{
    string output;
    int priorTheme;
    bool captureRequested;
    Texture2D captured;
    readonly Texture2D[] cards=new Texture2D[5];
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Activate()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--resource-card-verify");
        if(flag<0||flag+1>=args.Length)return;
        var probe=new GameObject("资源牌视觉验收").AddComponent<ResourceCardVisualVerification>();
        probe.output=args[flag+1];DontDestroyOnLoad(probe.gameObject);
    }
    IEnumerator Start()
    {
        priorTheme=StrategyHudTheme.Selected;Directory.CreateDirectory(output);
        // 等待启动画面退场；首帧截图可能仍是黑色启动缓冲。
        yield return new WaitForSecondsRealtime(8);
        for(int i=0;i<5;i++)
        {
            cards[i]=EntityCardAssets.ResourceFront(i);
            if(cards[i]==null||cards[i].width!=338||cards[i].height!=552||cards[i].isReadable)
            {Debug.LogError("CATAN_RESOURCE_CARDS_FAILED: 卡面未正确导入 "+i);Application.Quit(1);yield break;}
            if(EntityCardAssets.ResourceOrCommodityFront(i)!=cards[i])
            {Debug.LogError("CATAN_RESOURCE_CARDS_FAILED: 卡面入口不一致");Application.Quit(1);yield break;}
        }
        for(int theme=0;theme<4;theme++)
        {
            StrategyHudTheme.Select(theme);
            captureRequested=true;
            for(int frame=0;captured==null&&frame<180;frame++)yield return null;
            var shot=captured;captured=null;
            if(shot==null){Debug.LogError("CATAN_RESOURCE_CARDS_FAILED: 未取得GUI绘制帧");StrategyHudTheme.Select(priorTheme);Application.Quit(1);yield break;}
            Color sample=shot.GetPixel(shot.width/2,shot.height/2);
            if(sample.maxColorComponent<.03f)
            {Destroy(shot);Debug.LogError("CATAN_RESOURCE_CARDS_FAILED: 未取得卡面图像");StrategyHudTheme.Select(priorTheme);Application.Quit(1);yield break;}
            File.WriteAllBytes(Path.Combine(output,"theme-"+theme+".png"),shot.EncodeToPNG());Destroy(shot);
        }
        StrategyHudTheme.Select(priorTheme);
        File.WriteAllText(Path.Combine(output,"result.json"),"{\"status\":\"passed\",\"resourceCards\":5,\"themes\":4,\"screenshots\":4,\"readableTextures\":0}");
        Debug.Log("CATAN_RESOURCE_CARDS_OK: 五种卡面、四个主题及独立运行截图完成");Application.Quit(0);
    }
    void OnDestroy(){StrategyHudTheme.Select(priorTheme);}
    void OnGUI()
    {
        if(!captureRequested||Event.current.type!=EventType.Repaint)return;
        // 后台窗口的屏幕缓冲可能为黑；将同一套实际GUI绘制到独立RT中复核。
        var prior=RenderTexture.active;var matrix=GUI.matrix;var color=GUI.color;
        var target=new RenderTexture(1440,900,0,RenderTextureFormat.ARGB32);target.Create();Graphics.SetRenderTarget(target);
        GUI.depth=-10000;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;
        try { DrawGallery();
            captured=new Texture2D(1440,900,TextureFormat.RGB24,false);
            captured.ReadPixels(new Rect(0,0,1440,900),0,0);captured.Apply();captureRequested=false;
        }
        finally {Graphics.SetRenderTarget(prior);GUI.matrix=matrix;GUI.color=color;target.Release();Destroy(target);}
    }
    void DrawGallery()
    {
        GUI.DrawTexture(new Rect(0,0,1440,900),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.075f,.075f,.075f),0,0);
        GUI.Label(new Rect(40,20,1300,50),StrategyHudTheme.Names[StrategyHudTheme.Selected],new GUIStyle(GUI.skin.label){fontSize=30,normal={textColor=Color.white}});
        for(int i=0;i<5;i++)
        {
            EntityCardAssets.DrawCardFace(new Rect(40+i*280,90,240,392),cards[i]);
            EntityCardAssets.DrawCardFace(new Rect(75+i*280,540,100,164),cards[i]);
            EntityCardAssets.DrawCardFace(new Rect(190+i*280,540,40,66),cards[i]);
        }
        var selected=new Rect(75,540,100,164);TabletopCardFrame.Draw(selected,true);
    }
}
