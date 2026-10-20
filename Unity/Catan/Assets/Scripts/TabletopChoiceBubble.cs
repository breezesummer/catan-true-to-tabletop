using System;
using UnityEngine;

// 地图上的局部确认只保留意图，实际执行仍走权威命令入口。
public sealed class TabletopChoiceBubble
{
    string title;
    string[] labels;
    Action<int> select;
    Rect area;
    public OperationWindowState Windows;
    public bool Active => select != null&&Windows?.Suppressed!=true;
    public void Clear() { select=null; }
    internal void Choose(int index)
    {if(select==null||index<0||index>=labels.Length)return;var callback=select;Clear();callback(index);}
    public void Open(Vector2 screen,string heading,string[] choices,Action<int> callback)
    {
        var point=MobileTabletop.Logical(screen);
        float height=70+choices.Length*46;
        area=new Rect(Mathf.Clamp(point.x-160,24,1096),Mathf.Clamp(point.y-height*.5f,24,876-height),320,height);
        title=heading;labels=choices;select=callback;
        Windows?.Describe(heading);
    }
    public void Draw()
    {
        if(!Active)return;
        ModernOperationUI.Styles();
        var heading=new GUIStyle(ModernOperationUI.Title){fontSize=18};
        ModernOperationUI.WindowFrame(area,new Rect(area.x+16,area.y+8,area.width-112,34),title,heading:heading);
        if(ModernOperationUI.Button(new Rect(area.xMax-82,area.y+8,30,28),"—")){if(Windows==null)Clear();else Windows.Hide(title);}
        if(ModernOperationUI.Button(new Rect(area.xMax-42,area.y+8,30,28),"×")){if(Windows==null)Clear();else Windows.Close();}
        for(int i=0;i<labels.Length;i++)if(ModernOperationUI.Button(new Rect(area.x+18,area.y+51+i*46,area.width-36,38),labels[i]))
        {Choose(i);GUIUtility.ExitGUI();}
    }
}
