using UnityEngine;

// 在内容区测量完整文本，先缩字号再绘制；Clip仅作为最后一道边界保护。
public static class TabletopTextFit
{
    static readonly GUIContent Measure=new GUIContent();
    static readonly RectOffset Zero=new RectOffset();
    public static void Fit(GUIStyle style,string text,Rect area,int minimum=8,bool wrap=false)
    {
        style.padding=Zero;style.margin=Zero;style.wordWrap=wrap;style.clipping=TextClipping.Clip;
        Measure.text=text;
        while(style.fontSize>minimum&&(style.CalcHeight(Measure,area.width)>area.height||!wrap&&style.CalcSize(Measure).x>area.width))style.fontSize--;
    }
    public static void Label(Rect area,string text,GUIStyle style,int minimum=8,bool wrap=false)
    {Fit(style,text,area,minimum,wrap);GUI.Label(area,text,style);}
}
