using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 显式命令行美术验收；普通对局不创建任何验证对象。
public sealed class ThreeKingdomsVillageVerification : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(Environment.GetCommandLineArgs().Contains("--verify-three-kingdoms-villages"))
            new GameObject("四色村庄验收").AddComponent<ThreeKingdomsVillageVerification>();
    }
    IEnumerator Start()
    {
        yield return null;yield return null;
        try {Verify();Debug.Log("CATAN_THREE_KINGDOMS_PLAYER_OK");Application.Quit(0);}
        catch(Exception e){Debug.LogException(e);Application.Quit(1);}
    }
    void Verify()
    {
        var args=Environment.GetCommandLineArgs();var output=args[Array.IndexOf(args,"--verify-three-kingdoms-villages")+1];Directory.CreateDirectory(output);
        var root=new GameObject("实际资源入口对照");var camera=Camera.main;
        // 独立验证层保留正式灯光与URP，避免主页叠加到样板上。
        const int layer=28;camera.cullingMask=1<<layer;camera.orthographic=true;camera.orthographicSize=.043f;
        camera.rect=new Rect(0,0,1,1);camera.nearClipPlane=.0001f;camera.farClipPlane=2;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.07f,.12f);
        int count=0;var lines=new System.Collections.Generic.List<string>();
        for(int seat=0;seat<4;seat++)
        {
            string id="P"+(seat+1);float x=(seat-1.5f)*.024f;
            var board=PhotoSampleAssets.Piece(false,"棋盘"+id,new Vector3(x,0,0),Quaternion.identity,BoardRenderer.SeatColors[seat],root.transform);
            var supply=UnityEngine.Object.Instantiate(PlayerComponentAssets.Model(PlayerComponentAssets.BuildingResource("village",id)),new Vector3(x,0,.027f),Quaternion.Euler(0,180,0),root.transform);
            var legal=OperationPieceAssets.PointerPreview(TabletopPointerPreview.ForCommand("BuildSettlement"),id,new Vector3(x,0,-.027f),Quaternion.identity,root.transform,true);
            var illegal=OperationPieceAssets.PointerPreview(TabletopPointerPreview.ForCommand("BuildSettlement"),id,new Vector3(x,0,-.054f),Quaternion.identity,root.transform,false);
            var expected=board.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
            foreach(var obj in new[]{board,supply,legal,illegal})
            {
                if(obj==null || obj.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("村庄入口缺失或混入碰撞");
                if(!expected.SequenceEqual(obj.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh)))throw new Exception("入口几何不一致");
                foreach(var t in obj.GetComponentsInChildren<Transform>())t.gameObject.layer=layer;
                foreach(var r in obj.GetComponentsInChildren<Renderer>())
                    for(int m=0;m<r.sharedMaterials.Length;m++)if(r.sharedMaterials[m].name.StartsWith("Village_Seat"))
                    {
                        if(r.sharedMaterials[m].color!=BoardRenderer.SeatColors[seat])throw new Exception("玩家色错误");
                        var block=new MaterialPropertyBlock();r.GetPropertyBlock(block,m);if(!block.isEmpty)throw new Exception("归属色被预览覆盖");
                    }
                count++;
            }
            if(OperationPieceAssets.Texture("settlement",id)==null)throw new Exception("缩略图缺失");
            lines.Add(id+"：棋盘/供应资源/合法预览/非法预览网格相同，归属材质保留，缩略图存在");
        }
        Capture(camera,output,"Unity入口对照",new Vector3(.06f,.10f,-.12f),new Vector3(0,.004f,-.012f),.065f);
        Capture(camera,output,"Unity顶视",new Vector3(0,.15f,-.012f),new Vector3(0,0,-.012f),.065f);
        Capture(camera,output,"Unity反向",new Vector3(-.06f,.10f,.12f),new Vector3(0,.004f,-.012f),.065f);
        // 对照后只保留一个村庄并按正式道路长度放置三条相接道路。
        foreach(Transform t in root.transform)t.gameObject.SetActive(false);
        var sample=PlayerComponentAssets.Building("village","P1","拥挤节点村庄",Vector3.zero,root.transform);
        foreach(var t in sample.GetComponentsInChildren<Transform>())t.gameObject.layer=layer;
        for(int i=0;i<3;i++)
        {
            float angle=i*120*Mathf.Deg2Rad;var pos=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*.02f;
            var road=PhotoSampleAssets.Piece(true,"相邻道路",pos,Quaternion.Euler(0,i*120,0),BoardRenderer.SeatColors[0],root.transform);
            foreach(var t in road.GetComponentsInChildren<Transform>())t.gameObject.layer=layer;
        }
        Capture(camera,output,"Unity拥挤节点",new Vector3(.04f,.055f,-.06f),new Vector3(0,.005f,0),.033f);
        lines.Add("Unity版本="+Application.unityVersion+"；验证实例="+count+"；静态资源验收，不代表触控或真机性能通过。");
        File.WriteAllLines(Path.Combine(output,"运行入口验收.txt"),lines);
    }
    static void Capture(Camera camera,string directory,string name,Vector3 position,Vector3 target,float size)
    {
        camera.transform.position=position;camera.transform.LookAt(target);camera.orthographicSize=size;
        var rt=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32);rt.Create();
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};RenderPipeline.SubmitRenderRequest(camera,request);
        var prior=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());
        RenderTexture.active=prior;Destroy(image);rt.Release();Destroy(rt);
    }
}
