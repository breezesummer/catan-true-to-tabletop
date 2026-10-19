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
        area=new Rect(Mathf.Clamp(point.x-150,24,1116),Mathf.Clamp(point.y-110,170,650),300,82+choices.Length*40);
        title=heading;labels=choices;select=callback;
        Windows?.Describe(heading);
    }
    public void Draw()
    {
        if(!Active)return;
        ModernOperationUI.Panel(area);
        ModernOperationUI.Label(new Rect(area.x+12,area.y+8,192,28),title,ModernOperationUI.Text);
        if(ModernOperationUI.Button(new Rect(area.xMax-82,area.y+8,30,28),"—")){if(Windows==null)Clear();else Windows.Hide(title);}
        if(ModernOperationUI.Button(new Rect(area.xMax-42,area.y+8,30,28),"×")){if(Windows==null)Clear();else Windows.Close();}
        for(int i=0;i<labels.Length;i++)if(ModernOperationUI.Button(new Rect(area.x+12,area.y+40+i*40,276,34),labels[i]))
        {Choose(i);GUIUtility.ExitGUI();}
    }
}
