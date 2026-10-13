using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated art inspection. Display samples never become committed game pieces.
public sealed class PhotoSampleGallery : MonoBehaviour
{
    public int ViewIndex { get; private set; }
    public bool ShowBack { get; private set; }
    public float Yaw { get; private set; }
    public Transform Stage { get; private set; }
    public GameObject Card { get; private set; }
    public GameObject Village { get; private set; }
    public GameObject Road { get; private set; }
    Font font;
    GUIStyle text, heading, button;
    readonly string[] views = {"斜视", "俯视", "侧视", "交点近看"};
    void Start()
    {
        if(Array.Exists(Environment.GetCommandLineArgs(),arg=>arg.StartsWith("--m6-verify-")))
        {SceneManager.LoadScene("M6");return;}
        Application.targetFrameRate = 60;
        font = Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},18);
        Stage = new GameObject("Photo samples display").transform;
        PhotoSampleAssets.Forest(Vector3.zero, Stage);
        Road=PhotoSampleAssets.Piece(true,"Road sample",new Vector3(.067f,0,-.023f),Quaternion.identity,new Color(.78f,.035f,.06f),Stage);
        Village=PhotoSampleAssets.Piece(false,"Village sample",new Vector3(.066f,0,.023f),Quaternion.identity,new Color(.78f,.035f,.06f),Stage);
        Card=Instantiate(PhotoSampleAssets.Model("resource-card"),new Vector3(-.075f,0,0),Quaternion.identity,Stage);
        var back=Instantiate(PhotoSampleAssets.Model("resource-card"),new Vector3(-.137f,.0003f,0),Quaternion.Euler(0,0,180),Stage);
        back.name="Card back sample";
        PhotoSampleAssets.Piece(false,"Crowded village",new Vector3(.0343f,.008f,-.0198f),Quaternion.identity,new Color(.78f,.035f,.06f),Stage);
        PhotoSampleAssets.Piece(true,"Crowded road",new Vector3(.0198f,.008f,-.0297f),Quaternion.Euler(0,60,0),new Color(.78f,.035f,.06f),Stage);
        var table=GameObject.CreatePrimitive(PrimitiveType.Cube);table.name="Sample table";table.transform.SetParent(Stage);
        table.transform.localPosition=new Vector3(-.032f,-.002f,0);table.transform.localScale=new Vector3(.32f,.004f,.14f);
        table.GetComponent<Renderer>().sharedMaterial=BoardRenderer.Material(new Color(.055f,.13f,.15f));
        Destroy(table.GetComponent<Collider>());
        SetView(0);
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--art-verify")>=0)gameObject.AddComponent<PhotoSamplePlayerVerification>();
    }
    public void SetView(int index)
    {
        ViewIndex=index;
        var camera=Camera.main;camera.orthographic=true;camera.rect=new Rect(0,.17f,1,.69f);
        Vector3 target=new Vector3(-.028f,.003f,0),offset;
        camera.orthographicSize=.108f;
        switch(index)
        {
            case 1:offset=new Vector3(0,.4f,-.0001f);break;
            case 2:offset=new Vector3(0,.05f,-.4f);break;
            case 3:target=new Vector3(.034f,.01f,-.02f);offset=new Vector3(.04f,.10f,-.10f);camera.orthographicSize=.040f;break;
            default:offset=new Vector3(0,.32f,-.32f);break;
        }
        camera.transform.position=target+offset;camera.transform.LookAt(target);
    }
    public void Rotate(float degrees)
    {
        Yaw+=degrees;Village.transform.rotation=Quaternion.Euler(0,Yaw,0);Road.transform.rotation=Quaternion.Euler(0,Yaw,0);
    }
    public void Flip()
    {
        ShowBack=!ShowBack;Card.transform.rotation=Quaternion.Euler(0,0,ShowBack?180:0);
        Card.transform.position=new Vector3(-.075f,ShowBack?.0003f:0,0);
    }
    public void Tint(Color color)
    {
        var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);
        foreach(var obj in new[]{Village,Road})foreach(var r in obj.GetComponentsInChildren<Renderer>())r.SetPropertyBlock(block);
    }
    void OnGUI()
    {
        if(Stage==null)return;
        if(text==null)
        {
            text=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};text.normal.textColor=new Color(.91f,.94f,.9f);
            heading=new GUIStyle(text){fontSize=30,fontStyle=FontStyle.Bold};button=new GUIStyle(GUI.skin.button){font=font,fontSize=18};
        }
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
        GUI.Label(new Rect(32,20,1000,45),"实体照片 · 第一批美术样板",heading);
        GUI.Label(new Rect(34,71,1060,40),"木头资源卡正反面 / 森林板块 / 道路 / 村庄",text);
        if(GUI.Button(new Rect(1165,24,240,48),"进入游戏试玩",button))SceneManager.LoadScene("M6");
        ViewIndex=GUI.Toolbar(new Rect(34,730,620,44),ViewIndex,views,button);
        SetView(ViewIndex);
        if(GUI.Button(new Rect(680,730,165,44),"旋转棋子 45°",button))Rotate(45);
        if(GUI.Button(new Rect(865,730,150,44),"翻转资源卡",button))Flip();
        if(GUI.Button(new Rect(1035,730,110,44),"红色",button))Tint(new Color(.78f,.035f,.06f));
        if(GUI.Button(new Rect(1165,730,110,44),"蓝色",button))Tint(new Color(.20f,.51f,.78f));
        GUI.Label(new Rect(34,803,1340,67),"本版按照片制作可旋转模型，尺寸暂用估算。卡面与板块插画经 AI 清理，细节可能与原印刷不同。\n游戏内森林、道路、村庄已接入；其他组件继续使用现有样板。",text);
        GUI.matrix=Matrix4x4.identity;
    }
}
