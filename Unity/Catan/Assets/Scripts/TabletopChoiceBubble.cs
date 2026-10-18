using System;
using UnityEngine;

// 地图上的局部确认只保留意图，实际执行仍走权威命令入口。
public sealed class TabletopChoiceBubble
{
    string title;
    string[] labels;
    Action<int> select;
    Rect area;
    public bool Active => select != null;
    public void Clear() { select=null; }
    internal void Choose(int index)
    {if(select==null||index<0||index>=labels.Length)return;var callback=select;Clear();callback(index);}
    public void Open(Vector2 screen,string heading,string[] choices,Action<int> callback)
    {
        var point=MobileTabletop.Logical(screen);
        area=new Rect(Mathf.Clamp(point.x-150,24,1116),Mathf.Clamp(point.y-110,170,650),300,82+choices.Length*40);
        title=heading;labels=choices;select=callback;
    }
    public void Draw()
    {
        if(!Active)return;
        ModernOperationUI.Panel(area);
        ModernOperationUI.Label(new Rect(area.x+12,area.y+8,276,28),title,ModernOperationUI.Text);
        for(int i=0;i<labels.Length;i++)if(ModernOperationUI.Button(new Rect(area.x+12,area.y+40+i*40,276,34),labels[i]))
        {Choose(i);GUIUtility.ExitGUI();}
        if(ModernOperationUI.Button(new Rect(area.x+12,area.yMax-36,276,28),"取消"))Clear();
    }
}
