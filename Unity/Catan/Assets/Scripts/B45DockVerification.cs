#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 显式 --b45-dock-verify：运行真实 OperationDock 与屏幕事件，夹具不修改权威局面。
public sealed class B45DockVerification : MonoBehaviour
{
    public static Action<int,int> ConfigureViewport;
    public static Action<Event> SendPointer;
    [Serializable] sealed class Result
    {
        public string 状态,说明;
        public int 断言数,截图数,鼠标点击,说明次数;
        public string[] 截图;
    }
    readonly Result result=new Result();
    readonly List<string> images=new List<string>();
    readonly HashSet<string> selected=new HashSet<string>();
    M6App app;
    OperationDock dock;
    OperationSnapshot snapshot;
    Font font;
    string output,stage,authority;
    Exception failure;
    Vector2 safeInsets;
    bool rendering;
    int inspections;
    void Require(bool value,string message){result.断言数++;if(!value)throw new InvalidOperationException(stage+"："+message);}
    IEnumerator Start()
    {
        var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--b45-dock-verify");if(flag<0)yield break;
        output=Path.GetFullPath(args[flag+1]);Directory.CreateDirectory(output);
        app=GetComponent<M6App>();font=MobileTabletop.LoadFont();
        var work=Flatten(Verify());
        while(true)
        {
            object next;
            try {if(!work.MoveNext())break;next=work.Current;}
            catch(Exception ex){failure=ex;break;}
            yield return next;
        }
        result.状态=failure==null?"通过":"失败";
        result.说明="真实Unity GameView中的OperationDock隔离夹具，截图来自渲染后的游戏画面，鼠标事件通过与GameView相同的QueueGameViewInputEvent原生队列注入；权威局面未改动。手机安全区与长按仅桌面模拟，未代替Android真机或用户试玩。"+(failure==null?"":"\n"+failure);
        result.截图=images.ToArray();result.截图数=images.Count;result.说明次数=inspections;
        File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(result,true));
        if(failure==null)Debug.Log("CATAN_B45_DOCK_OK: "+result.断言数+" 项，"+images.Count+" 张截图");
        else Debug.LogError("CATAN_B45_DOCK_FAILED: "+failure);
    }
    static IEnumerator Flatten(IEnumerator root)
    {
        var stack=new Stack<IEnumerator>();stack.Push(root);
        while(stack.Count>0){var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}if(top.Current is IEnumerator child)stack.Push(child);else yield return top.Current;}
    }
    IEnumerator Verify()
    {
        stage="启动隔离桌面";Require(ConfigureViewport!=null&&SendPointer!=null,"缺少编辑器图形驱动，请用B45DockEditorVerification.Run");
        app.StartGame("cities-knights","heading-for-new-shores",4,"P1",true);yield return new WaitForSecondsRealtime(.15f);yield return new WaitForSecondsRealtime(.15f);
        var city=GetComponent<M4App>();authority=app.ExportEnvelope();var table=city.VerificationBoard.TableScene;
        var source=city.VerificationOperationSnapshot;
        snapshot=new OperationSnapshot{Scope="b45-dock-fixture",Revision="0",Seat="P1",CanInteract=true,GoodCount=8,Goods=new int[8],Bank=new int[8],ResourceHandLimit=7,SpecialHandLimit=4};
        snapshot.Builds.AddRange(source.Builds);snapshot.Notify=message=>Debug.Log("B45_DOCK_NOTICE: "+message);
        snapshot.Cards.AddRange(new[]{new OperationHandCard{Id="stable:17",Name="发明家",Detail="仅用于输入夹具；不生成或支付任何权威牌。",Progress=true,ArtIndex=7},new OperationHandCard{Id="stable:31",Name="主教",Detail="稳定实例身份输入检查。",Progress=true,ArtIndex=20}});
        dock=new OperationDock();dock.BindTable(table);dock.InspectGood=(good,rect)=>inspections++;dock.InspectCard=(id,rect)=>inspections++;
        dock.DirectSelection=new OperationHandSelection{Active=true,CanSelectGood=(good,ordinal)=>good>=0&&ordinal<snapshot.Goods[good],IsGoodSelected=(good,ordinal)=>selected.Contains(good+":"+ordinal),SelectGood=(good,ordinal)=>Toggle(good+":"+ordinal),CanSelectCard=id=>snapshot.Cards.Any(c=>c.Id==id),IsCardSelected=id=>selected.Contains(id),SelectCard=Toggle,SelectedGoods=()=>selected.Count(k=>!k.StartsWith("stable:")),SelectedCards=()=>selected.Count(k=>k.StartsWith("stable:"))};
        app.enabled=false;city.enabled=false;rendering=true;
        foreach(var size in new[]{new Vector2Int(1440,900),new Vector2Int(1920,1080),new Vector2Int(2560,1080),new Vector2Int(2400,1080)})
        {
            stage="分辨率 "+size.x+"×"+size.y;ConfigureViewport(size.x,size.y);safeInsets=size.x==2400?new Vector2(80,28):Vector2.zero;
#if UNITY_EDITOR
            MobileTabletop.VerificationSafeArea=new Rect(safeInsets.x,safeInsets.y,size.x-safeInsets.x*2,size.y-safeInsets.y*2);
#endif
            for(int i=0;i<6;i++)yield return new WaitForSecondsRealtime(.15f);
            Require(Screen.width==size.x&&Screen.height==size.y,"GameView实际尺寸不匹配："+Screen.width+"×"+Screen.height);
            foreach(int count in new[]{0,1,5,70})
            {
                snapshot.Goods[0]=count;snapshot.Revision=size+"-"+count;selected.Clear();yield return new WaitForSecondsRealtime(.15f);yield return new WaitForSecondsRealtime(.15f);
                var rectangles=dock.VerificationHandRects;
                Require(rectangles.Length==count,"显示牌数不匹配");
                if(count>0)
                {
                    Require(Math.Abs(rectangles[0].x-342)<.01f,"手牌没有固定左起点");
                    for(int i=1;i<count;i++)Require(rectangles[i].x-rectangles[i-1].x<=108.01f&&rectangles[i].x-rectangles[i-1].x>=25.99f,"卡边有间隔或露出不足");
                }
                yield return Capture(size.x+"x"+size.y+"-"+count+"张"+(safeInsets.x>0?"-模拟安全区":""));
            }
        }
        stage="真实鼠标事件";ConfigureViewport(1440,900);safeInsets=Vector2.zero;
#if UNITY_EDITOR
        MobileTabletop.VerificationSafeArea=null;
#endif
        for(int i=0;i<5;i++)yield return new WaitForSecondsRealtime(.15f);
        snapshot.Goods[0]=5;snapshot.Revision="input";yield return new WaitForSecondsRealtime(.15f);yield return new WaitForSecondsRealtime(.15f);
        var point=new Vector2(370,800);
        Send(EventType.MouseDown,point,0);yield return new WaitForSecondsRealtime(.15f);Require(selected.Count==0,"按下即选择");
        Send(EventType.MouseUp,point,0);yield return new WaitForSecondsRealtime(.15f);Require(selected.SetEquals(new[]{"0:0"}),"松手没有选择原牌");result.鼠标点击++;
        Send(EventType.MouseDown,point,1);yield return new WaitForSecondsRealtime(.15f);Require(inspections==1&&selected.SetEquals(new[]{"0:0"}),"右键说明改变选择");
        Send(EventType.MouseDown,point,0);yield return new WaitForSecondsRealtime(.15f);Send(EventType.MouseDrag,point+new Vector2(90,0),0);yield return new WaitForSecondsRealtime(.15f);Send(EventType.MouseUp,point+new Vector2(90,0),0);yield return new WaitForSecondsRealtime(.15f);
        Require(selected.SetEquals(new[]{"0:0"}),"拖动误算点击");
        yield return Capture("鼠标选中与右键说明");
        if(MobileTabletop.Enabled)
        {
            stage="长按模拟";Send(EventType.MouseDown,point,0);yield return new WaitForSecondsRealtime(.65f);Send(EventType.MouseUp,point,0);yield return new WaitForSecondsRealtime(.15f);
            Require(inspections==2&&selected.SetEquals(new[]{"0:0"}),"长按松手又触发选择");
        }
        stage="页签与排序保留身份";
        yield return Click(new Vector2(1220,798));yield return Click(new Vector2(370,800));
        Require(selected.Contains("stable:17"),"发展牌未按稳定ID选择");
        yield return Click(new Vector2(1220,846));Require(selected.Contains("stable:17"),"整理替换了选中身份");
        yield return Click(new Vector2(1220,747));Require(selected.Contains("0:0")&&selected.Contains("stable:17"),"切标签丢失跨类草稿");
        stage="滚动末张";snapshot.Goods[0]=70;snapshot.Revision="scroll";selected.Clear();yield return new WaitForSecondsRealtime(.15f);yield return new WaitForSecondsRealtime(.15f);
        SendPointer(new Event{type=EventType.ScrollWheel,mousePosition=ScreenPoint(point),delta=new Vector2(0,100)});yield return new WaitForSecondsRealtime(.15f);yield return new WaitForSecondsRealtime(.15f);
        Require(dock.ScrollOffset(false)>0,"滚轮没有横向滚动");
        yield return Click(new Vector2(1134,800));Require(selected.Contains("0:69"),"滚动后末张不可点击");yield return Capture("超量滚动至末张");
        Require(!dock.HasOverlay,"手牌组件生成模态窗口");Require(app.ExportEnvelope()==authority,"显示与输入改变了权威存档");
    }
    void Toggle(string key){if(!selected.Remove(key))selected.Add(key);}
    MobileViewport Viewport=>MobileTabletop.Viewport;
    Vector2 ScreenPoint(Vector2 logical)=>new Vector2(Viewport.ToScreenX(logical.x),Viewport.ToScreenY(logical.y));
    void Send(EventType type,Vector2 point,int button)=>SendPointer(new Event{type=type,button=button,mousePosition=ScreenPoint(point)});
    IEnumerator Click(Vector2 point){Send(EventType.MouseDown,point,0);yield return new WaitForSecondsRealtime(.15f);Send(EventType.MouseUp,point,0);yield return new WaitForSecondsRealtime(.15f);result.鼠标点击++;}
    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();
        Require(image!=null&&image.width==Screen.width&&image.height==Screen.height,"屏幕截图大小错误");
        var pixels=image.GetPixels32();Require(pixels.Count(p=>p.r>20||p.g>20||p.b>20)>pixels.Length/4,"截图黑屏");
        string file=name+".png";File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());Destroy(image);images.Add(file);
    }
    void OnGUI()
    {
        if(!rendering)return;
        try
        {
            GUI.depth=-400;var v=Viewport;GUI.matrix=Matrix4x4.TRS(new Vector3(v.Left,v.Top,0),Quaternion.identity,new Vector3(v.Width/1440,v.Height/900,1));
            GUI.Label(new Rect(342,624,800,42),"B4.5 组件隔离验收 · "+stage,new GUIStyle(GUI.skin.label){font=font,fontSize=18});
            if(Event.current.rawType!=EventType.Layout&&Event.current.rawType!=EventType.Repaint)Debug.Log("B45_RUNTIME_POINTER: "+Event.current.type+" raw="+Event.current.rawType+" at="+Event.current.mousePosition+" enabled="+GUI.enabled+" first="+(dock.VerificationHandRects.Length>0?dock.VerificationHandRects[0].ToString():"none"));
            dock.Draw(snapshot,font);GUI.matrix=Matrix4x4.identity;
        }
        catch(Exception ex){failure=ex;rendering=false;Debug.LogError("CATAN_B45_DOCK_FAILED: "+ex);}
    }
}

#endif
