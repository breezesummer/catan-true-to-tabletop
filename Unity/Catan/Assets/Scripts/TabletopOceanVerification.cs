using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyPaintedHud(string[] args)
    {
        mode = "combined";
        app.ReadSave(args[Array.IndexOf(args,"--painted-hud")+1]);
        app.SetPaused(true);
        yield return null;
        yield return null;
        Require(app.ScenarioId=="through-the-desert" && app.PlayerCount==4 && app.ThreeKingdoms
            && app.Harbormaster && app.Fishing,"手绘主题验收局面不符");
        Require(HandPaintedTabletopSkin.Ready && OceanTabletopSkin.Ready,"手绘或海洋主题贴图缺失");
        for(int frame=0; !Table.Ocean.MaskReady && frame<600; frame++) yield return null;
        Require(Table.Ocean.MaskReady,"棋盘尚未加载");
        Table.Frame(BoardCamera,false,1);
        yield return new WaitForSecondsRealtime(8.2f);
        string saved = Authority;
        var water=Table.Ocean.GetComponentsInChildren<Renderer>().First(r=>r.sharedMaterial.HasProperty("_PaintedSea")).sharedMaterial;
        int maskRevision=Table.Ocean.MaskRevision;
        foreach (int theme in new[]{3,1,2,0,3})
        {
            StrategyHudTheme.Select(theme);
            yield return new WaitForSeconds(.25f);
            yield return new WaitForEndOfFrame();
            Require(StrategyHudTheme.Selected==theme && PlayerPrefs.GetInt("catan.ui.theme.v010")==new[]{0,1,3,4}[theme],"主题偏好编号错误");
            Require(Authority==saved,"切换主题改变对局");
            Require(water.GetFloat("_PaintedSea")== (theme==3?1:0),"海面没有随桌面主题切换");
            Require(Table.Ocean.MaskRevision==maskRevision,"切换海面重新生成海岸遮罩");
            if(theme==3)Require(water.GetTexture("_SeaWash")!=null && water.shader.isSupported,"手绘海纹未加载");
            if (theme==3 || theme==1) SaveOceanFrame(StrategyHudTheme.Names[theme]+"-穿越沙漠.png");
        }
        BindDockForHeadless();
        Dock.OpenTrade();
        Require(Dock.TradeOpen,"交易面板无法展开");
        yield return null;
        yield return new WaitForEndOfFrame();
        SaveOceanFrame("清新手绘-交易.png");
        app.HandleTableEscape();
        Require(!Dock.TradeOpen && Authority==saved,"关闭交易后对局发生变化");
        configurations++;
    }
    IEnumerator VerifyOceanViews()
    {
        bool kurouOnly=Environment.GetCommandLineArgs().Contains("--kurou-view-only");
        int index=kurouOnly?4:0;
        foreach(string rules in kurouOnly?new[]{"combined"}:new[]{"base","seafarers","cities-knights","combined","combined"})
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
            if(Environment.GetCommandLineArgs().Contains("--ocean-theme-verify"))
            {
                Require(OceanTabletopSkin.Ready,"海洋主题图集缺失");
                Require(ocean.sharedMaterial.GetTexture("_OceanDetail")!=null,"海洋主题细浪纹理缺失");
                var environment=table.GetComponentInChildren<OceanThemeEnvironment>();
                Require(environment!=null&&environment.ReefCount==4,"边缘礁石未加载");
                foreach(var reef in environment.GetComponentsInChildren<Renderer>())
                {
                    Require(reef.bounds.max.y>TabletopOcean.WaterHeight+.003f,"礁石未露出水面");
                    Require(reef.sharedMaterial.shader.isSupported,"礁石着色器不受支持");
                }
                Require(ocean.sharedMaterial.GetFloat("_OceanTheme")==1,"海洋主题水面参数未启用");
                var reefs=environment.GetComponentsInChildren<Renderer>();
                var reefPositions=reefs.Select(reef=>reef.transform.position).ToArray();
                table.Frame(camera,false,1.65f);yield return null;
                Require(reefs.Select((reef,i)=>reef.transform.position==reefPositions[i]).All(same=>same),"缩放移动了礁石");
                table.Frame(camera,false,1);
                BindDockForHeadless();CheckContextEntrances();
                if(Snapshot.GoodCount==8)
                {
                    Dock.OpenFacility(2);yield return null;yield return new WaitForEndOfFrame();
                    SaveOceanFrame(label+"-科学设施.png");Dock.Reset();
                }
                Dock.OpenTrade();Require(Dock.TradeOpen,"海洋主题无法打开交易");
                yield return new WaitForSeconds(2);yield return new WaitForEndOfFrame();SaveOceanFrame(label+"-交易.png");
                app.HandleTableEscape();Require(!Dock.TradeOpen,"海洋主题无法取消交易");
                Require(saved==Authority,"海洋主题取消交易改变存档");
                foreach(int theme in new[]{0,2,1})
                {
                    StrategyHudTheme.Select(theme);yield return null;yield return new WaitForEndOfFrame();
                    Require(ocean.sharedMaterial.GetFloat("_OceanTheme")==1,"主题切换未刷新海面");
                    Require(environment.GetComponentsInChildren<Renderer>().Length==4,"礁石主题可见性错误");
                    Require(saved==Authority,"主题切换改变权威存档");
                    if(index==4)SaveOceanFrame("主题切换-"+theme+".png");
                }
            }
            table.Objects.Begin("verification");
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
            if(threeKingdoms)
            {
                var feedback=VerifyKurouPresentation();
                while(feedback.MoveNext())yield return feedback.Current;
            }
            configurations++;index++;
        }
    }

    IEnumerator VerifyKurouPresentation()
    {
        var game=GetComponent<M5App>();var host=game.VerificationHost;
        var session=(Catan.Core.M5.CombinedGameSession)host.GetType().GetField("session",PrivateInstance).GetValue(host);
        var field=session.GetType().GetField("state",PrivateInstance);
        var original=session.GetAuthoritativeStateForTesting();var state=session.GetAuthoritativeStateForTesting();
        var selection=(B45SelectionContext)typeof(M6App).GetField("generalSelection",PrivateInstance).GetValue(app);
        try
        {
            var own=state.ThreeKingdoms.Generals.First(g=>g.Region==Catan.Core.ThreeKingdoms.GeneralRegion.Active&&g.OwnerPlayerId==app.HumanSeat);
            var luxun=state.ThreeKingdoms.Generals.First(g=>g.GeneralId=="tk-g23");
            (own.GeneralId,luxun.GeneralId)=(luxun.GeneralId,own.GeneralId);
            field.SetValue(session,state);game.M6Refresh();app.ShowGeneralLibrary();
            foreach(int theme in new[]{0,2,1})
            {
                StrategyHudTheme.Select(theme);yield return null;yield return new WaitForEndOfFrame();
                SaveOceanFrame("陆逊-武将框与武将库-"+StrategyHudTheme.Names[theme]+".png");
            }
            app.CloseGeneralLibrary();
            var huang=state.ThreeKingdoms.Generals.First(g=>g.GeneralId=="tk-g16");
            string previous=own.GeneralId;own.GeneralId=huang.GeneralId;huang.GeneralId=previous;
            state.Players.First(p=>p.Id==app.HumanSeat).Improvements[0]=1;
            field.SetValue(session,state);game.M6Refresh();yield return null;
            typeof(M6App).GetMethod("BeginGeneralSkill",PrivateInstance).Invoke(app,new object[]{"tk-g16-s1"});
            Require(selection.Active&&selection.Options.Length==1,"苦肉未进入统一地图选择");
            var board=selection.CandidateActions.SelectMany(Catan.Core.ThreeKingdoms.B45SelectionContract.Board).Distinct().ToArray();
            Require(board.Any(id=>id.StartsWith("Improvement:"))&&board.Any(id=>!id.StartsWith("Improvement:")),"苦肉缺少设施或地图建筑");
            string saved=Authority;
            yield return new WaitForEndOfFrame();SaveOceanFrame("黄盖-直接选择.png");
            foreach(string id in new[]{board.First(id=>!id.StartsWith("Improvement:")),board.First(id=>id.StartsWith("Improvement:"))})
            {
                Require(selection.ToggleTile(id)&&selection.CanConfirm,"苦肉直接目标不能确认");
                Require(selection.BuildCommand().ProgressCardId!=null&&saved==Authority,"选择目标提前提交了技能");
                yield return new WaitForEndOfFrame();SaveOceanFrame(id.StartsWith("Improvement:")?"黄盖-设施选中.png":"黄盖-建筑选中.png");
            }
        }
        finally
        {
            selection.Reset();field.SetValue(session,original);game.M6Refresh();
        }
    }

    void SaveOceanFrame(string name)
    {
        RenderTexture.active=null;
        var frame=ScreenCapture.CaptureScreenshotAsTexture();
        try
        {
            Require(frame!=null,"完整窗口截图不可用");
            var pixels=frame.GetPixels32();int variation=0;
            for(int i=0;i<pixels.Length;i+=257)if(!pixels[i].Equals(pixels[0]))variation++;
            Require(variation>100,"完整窗口截图为空白，不能作为视觉验收");
            File.WriteAllBytes(Path.Combine(output,name),frame.EncodeToPNG());cameraImages++;
        }
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
