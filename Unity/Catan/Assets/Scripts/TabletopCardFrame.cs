using UnityEngine;

// 只保留选中卡牌的交互描边；普通卡面不加装饰边框。
public static class TabletopCardFrame
{
    static readonly Color[] Bases={new Color(.34f,.15f,.065f),new Color(.025f,.24f,.30f),new Color(.24f,.29f,.35f),new Color(.055f,.36f,.25f)};
    static readonly Color[] Metals={new Color(.85f,.57f,.28f),new Color(.87f,.74f,.43f),new Color(.82f,.87f,.93f),new Color(.95f,.79f,.40f)};
    public static Color BaseColor=>Bases[StrategyHudTheme.Selected];
    public static Color MetalColor=>Metals[StrategyHudTheme.Selected];
    public static void Draw(Rect r,bool selected=false)
    {
        // 普通卡面不叠加装饰边框，仅在选中时显示交互描边。
        if(!selected||Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;
        GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,StrategyHudTheme.Accent,1.5f,Mathf.Min(r.width,r.height)*.095f);
    }
}
