using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyOceanViews()
    {
        int index=0;
        foreach(string rules in new[]{"base","seafarers","cities-knights","combined","combined"})
        {
            mode=rules;
            bool threeKingdoms=index==4;
            string label=threeKingdoms?"三国双扩展":rules;
            app.StartGame(mode,index==3?"new-world":"heading-for-new-shores",4,"P1",true,
                threeKingdoms:threeKingdoms,harbormaster:true,fishing:true);
            yield return null; yield return null;
            for(int step=0;!Snapshot.CanTrade&&step<4000;step++)
            {
                if(app.IsHumanTurn) Advance();
                else { app.SetPaused(false); app.StepAi(); app.SetPaused(true); }
                yield return null;
            }
            Require(Snapshot.CanTrade,"海面验收未推进到有手牌的正常回合");
            var table=Table; var camera=BoardCamera;
            for(int frame=0;!table.Ocean.MaskReady&&frame<600;frame++)yield return null;
            Require(table.Ocean.MaskReady,"海岸遮罩未完成");
            int revision=table.Ocean.MaskRevision;
            var ocean=table.Ocean.GetComponentsInChildren<Renderer>().First();
            Require(ocean.bounds.size.x>10&&ocean.bounds.size.z>10,"连续水面网格轴向不正确");
            Require(ocean.sharedMaterial.shader.isSupported,"当前设备不支持海面着色器");
            Require(ocean.sharedMaterial.GetTexture("_CoastMask")!=null,"海岸遮罩未绑定");
            Require(table.Ocean.GetComponentsInChildren<Collider>().Length==0,"海水不得阻挡棋盘拾取");
            Require(!table.transform.parent.GetComponentsInChildren<OceanTileSurface>().Any(t=>t.GetComponentsInChildren<Renderer>().Any(r=>r.enabled)),"装饰海板未融入连续海面");
            string saved=Authority;
            checks+=TabletopMapVerificationV010.Check(table);
            table.Objects.End(); table.Objects.Begin("verification"); table.Objects.End();
            yield return new WaitForSeconds(2);
            CheckProjection(false); CheckProjection(true); SetTop(false);
            var a=Position(Vertices[0].Id);var b=Position(Vertices[1].Id);
            var c=Position(Vertices[Vertices.Length/2].Id);
            table.Frame(camera,false,1);
            float shape=Vector2.Distance(table.Project(a),table.Project(b))/Vector2.Distance(table.Project(a),table.Project(c));
            foreach(float zoom in new[]{1f,1.5f,.65f})
            {
                table.Frame(camera,false,zoom);
                float next=Vector2.Distance(table.Project(a),table.Project(b))/Vector2.Distance(table.Project(a),table.Project(c));
                Require(Mathf.Abs(shape-next)<.0001f,"缩放改变棋盘形状");
                yield return new WaitForEndOfFrame();
                SaveOceanFrame(label+"-"+(zoom==1?"默认":zoom>1?"远景":"近景")+".png");
            }
            table.Frame(camera,false,1);
            var markers=table.transform.parent.GetComponentInChildren<TabletopReferenceMarkers>();
            foreach(var triangle in markers.PublicPortFootprints)
                foreach(var point in triangle)Require(TabletopScene.BoardFitArea.Contains(table.Project(point)),"默认取景裁掉港口");
            if(markers.PublicPortPositions.Length>0)
            {
                table.Frame(camera,false,.65f);
                var port=camera.WorldToScreenPoint(markers.PublicPortPositions[0]);
                var target=camera.ViewportToScreenPoint(new Vector3(.46f,.55f,0));
                table.PanFromScreen(port,target);table.EndPan();
                yield return new WaitForEndOfFrame();SaveOceanFrame(label+"-港口.png");
                table.ResetPan();
            }
            table.Frame(camera,false,1);
            yield return new WaitForEndOfFrame();
            var first=ReadOceanPixels(table);
            yield return new WaitForSeconds(.75f);
            yield return new WaitForEndOfFrame();
            var second=ReadOceanPixels(table);
            int changed=0;
            for(int pixel=0;pixel<first.Length;pixel++)if(!first[pixel].Equals(second[pixel]))changed++;
            Require(changed>100,"海浪与反光没有随时间变化");
            Require(table.Ocean.MaskRevision==revision,"静止棋盘重复重建海岸遮罩");
            Require(saved==Authority,"海面、缩放或平移改变了权威存档");
            Debug.Log("CATAN_OCEAN_VIEW_OK: "+label+" 动画变化像素="+changed+" 遮罩修订="+revision);
            configurations++;index++;
        }
    }

    void SaveOceanFrame(string name)
    {
        RenderTexture.active=null;
        var frame=ScreenCapture.CaptureScreenshotAsTexture();
        try{File.WriteAllBytes(Path.Combine(output,name),frame.EncodeToPNG());cameraImages++;}
        finally{Destroy(frame);}
    }

    Color32[] ReadOceanPixels(TabletopScene table)
    {
        var target=new RenderTexture(320,200,24);
        var prior=RenderTexture.active;
        var pixels=new Texture2D(320,200,TextureFormat.RGB24,false);
        try
        {
            table.RenderComposite(target);RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,320,200),0,0);pixels.Apply();return pixels.GetPixels32();
        }
        finally{RenderTexture.active=prior;target.Release();Destroy(target);Destroy(pixels);}
    }
}
