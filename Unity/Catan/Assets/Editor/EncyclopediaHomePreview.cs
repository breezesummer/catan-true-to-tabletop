using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EncyclopediaHomePreview
{
    const string Key="Catan.EncyclopediaHomePreview";
    static int stage;
    static double next;
    static EncyclopediaHomePreview(){EditorApplication.update+=Tick;}

    [MenuItem("Catan/美术/百科详情实机打样")]
    public static void RunDetails(){
        SessionState.SetBool(Key+".details",true);
        StartPreview();
    }
    [MenuItem("Catan/美术/百科首页实机打样")]
    public static void Run(){SessionState.SetBool(Key+".details",false);StartPreview();}
    static void StartPreview(){
        stage=0;next=0;
        var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../artifacts/游戏百科原型/首页-实机打样.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        SessionState.SetString(Key,output);
        EditorSceneManager.OpenScene("Assets/M6.unity");
        B45DockEditorVerification.SetSize(1920,1080);
        EditorApplication.isPlaying=true;
    }
    static void Tick(){
        var output=SessionState.GetString(Key,"");
        if(output==""||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        try{
            var app=UnityEngine.Object.FindFirstObjectByType<M6App>();
            if(app==null)return;
            if(stage==0)app.StartGame("combined","through-the-desert",4,"P1",true,threeKingdoms:true,harbormaster:true,fishing:true);
            else if(stage==1){
                if(!app.Ready)throw new InvalidOperationException("棋盘尚未完成初始化");
                app.OpenTableMenu();
                typeof(M6App).GetMethod("ActivateTablePauseEntry",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,new object[]{3});
            }else if(SessionState.GetBool(Key+".details",false)){
                if(!DetailStep(app,stage-2,Path.GetDirectoryName(output)))return;
                stage++;next=EditorApplication.timeSinceStartup+.45;
                return;
            }else if(stage==2)ScreenCapture.CaptureScreenshot(output);
            else if(stage==3 || stage==4){
                // 从首页点击卡牌图鉴，走与玩家相同的 GameView 鼠标事件队列。
                B45DockEditorVerification.Send(new Event{type=stage==3?EventType.MouseDown:EventType.MouseUp,
                    button=0,mousePosition=new Vector2(1322,873)});
            }else{
                const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
                if(!File.Exists(output)||(int)typeof(M6App).GetField("bookSection",flags).GetValue(app)!=3)
                    throw new InvalidOperationException("卡牌图鉴入口未打开");
                var back=typeof(M6App).GetMethod("HandleTableEscape",flags);
                back.Invoke(app,null);
                if((int)typeof(M6App).GetField("bookSection",flags).GetValue(app)!=-1)
                    throw new InvalidOperationException("图鉴返回首页失败");
                back.Invoke(app,null);
                if(!app.TableMenuOpen||(bool)typeof(M6App).GetField("encyclopediaOpen",flags).GetValue(app))
                    throw new InvalidOperationException("百科返回菜单失败");
                SessionState.EraseString(Key);stage=0;
                Debug.Log("百科首页截图、卡牌图鉴点击和两级返回已通过");
                if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="-executeMethod"))EditorApplication.Exit(0);
                return;
            }
            stage++;next=EditorApplication.timeSinceStartup+2;
        }catch(Exception ex){SessionState.EraseString(Key);Debug.LogException(ex);if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="-executeMethod"))EditorApplication.Exit(1);}
    }
    static void ValidateExampleArt(){
        var ids=new System.Collections.Generic.HashSet<string>();
        foreach(var general in Catan.Core.ThreeKingdoms.GeneralCatalog.Definitions)
            foreach(var skill in general.Skills)ids.Add(skill.Id);
        foreach(var card in Catan.Core.DevelopmentCardText.Names)ids.Add(card);
        foreach(var card in new[]{"骑士","道路建设","丰收","垄断","胜利点"})ids.Add(card);
        foreach(var id in ids){
            var asset=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Encyclopedia/专属局面/"+id+".png");
            if(asset==null)throw new InvalidOperationException("缺少专属局面图："+id);
        }
        Debug.Log("已核对专属局面纹理："+ids.Count);
    }
    // 坐标与百科画布一致；鼠标按下、抬起和截图各占一步。
    static readonly (int x,int y,string expected)[] DetailClicks={
        (1340,228,"航海家"),(1485,55,""),(546,384,"穿越沙漠"),(1485,55,""),
        (838,755,""),(817,270,"张辽 · 登锋陷阵"),(1485,55,""),(1485,55,""),
        (1144,753,""),(817,329,"道路建设 · 基础发展卡"),(1485,55,""),(1485,55,""),
        (1340,468,"海港与捕鱼"),(1250,422,"港口计分"),(1485,55,"海港与捕鱼"),(1485,55,""),
        (532,755,""),(817,329,"驶向新海岸"),(1485,55,""),(1485,55,""),
        (1432,755,""),(817,270,"道路"),(1485,55,""),(1485,55,""),
        (838,755,""),(817,329,"吕布 · 傲睨万物"),(1040,278,"吕布 · 傲睨万物"),(1485,55,""),(1485,55,""),
        (1144,753,""),(817,388,"丰收 · 基础发展卡"),(1485,55,""),(1485,55,"")
    };
    static bool DetailStep(M6App app,int step,string folder){
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        int index=step/3;
        if(index==DetailClicks.Length){
            ValidateExampleArt();
            if((int)typeof(M6App).GetField("bookSection",flags).GetValue(app)!=-1)
                throw new InvalidOperationException("详情未返回首页");
            SessionState.EraseString(Key);stage=0;
            Debug.Log("五个栏目详情与关联词条已从首页点击进入并返回");
            if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="-executeMethod"))EditorApplication.Exit(0);
            return false;
        }
        var click=DetailClicks[index];
        if(step%3<2){
            float scale=Mathf.Min(1920f/1672f,1080f/941f);
            var point=new Vector2((1920-1672*scale)/2+click.x*scale,(1080-941*scale)/2+click.y*scale);
            B45DockEditorVerification.Send(new Event{type=step%3==0?EventType.MouseDown:EventType.MouseUp,button=0,mousePosition=point});
        }else{
            string entry=(string)typeof(M6App).GetField("bookEntry",flags).GetValue(app);
            if(entry!=click.expected)throw new InvalidOperationException("详情点击失败，步骤 "+index+" 实际词条 "+entry);
            if(entry.Length>0){
                var name=entry.Split(' ')[0];
                if(name=="吕布"&&click.y==278){
                    var choices=(System.Collections.Generic.Dictionary<string,int>)typeof(M6App).GetField("bookSkillChoices",flags).GetValue(app);
                    if(choices["tk-g02"]!=1)throw new InvalidOperationException("利驭技能页未切换");
                    name+="利驭";
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+"-详情实机.png"));
            }
        }
        return true;
    }

}
