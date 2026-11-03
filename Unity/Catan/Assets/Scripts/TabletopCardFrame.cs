using UnityEngine;

// 卡面共用的窄边框，四个主题即时切换；不遮住原画角标，不创建每帧贴图。
public static class TabletopCardFrame
{
    static readonly Color[] Bases={new Color(.34f,.15f,.065f),new Color(.025f,.24f,.30f),new Color(.24f,.29f,.35f),new Color(.055f,.36f,.25f)};
    static readonly Color[] Metals={new Color(.85f,.57f,.28f),new Color(.87f,.74f,.43f),new Color(.82f,.87f,.93f),new Color(.95f,.79f,.40f)};
    public static Color BaseColor=>Bases[StrategyHudTheme.Selected];
    public static Color MetalColor=>Metals[StrategyHudTheme.Selected];
    public static void Draw(Rect r,bool selected=false)
    {
        if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;
        float width=Mathf.Min(r.width,r.height),rim=width*.021f,radius=width*.095f;
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,BaseColor,rim,radius);
        float inset=rim*.28f;
        var inner=new Rect(r.x+inset,r.y+inset,r.width-2*inset,r.height-2*inset);
        GUI.DrawTexture(inner,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,selected?StrategyHudTheme.Accent:MetalColor,Mathf.Max(.5f,rim*.25f),Mathf.Max(0,radius-inset));
        if(selected)
            GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,StrategyHudTheme.Accent,Mathf.Max(1,rim*.55f),radius);
    }
}
