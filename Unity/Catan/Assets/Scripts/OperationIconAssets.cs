using System;
using UnityEngine;

// 所有资源缩略图统一使用完整卡面；照片只负责显示，不提供规则数据。
public static class OperationIconAssets
{
    static readonly string[] Names = { "木", "砖", "羊", "麦", "矿", "布", "币", "纸" };
    static GUIStyle fallbackStyle;
    public static Texture2D GoodTexture(int good)
    {
        if(good<0||good>=8)throw new ArgumentOutOfRangeException(nameof(good));
        return EntityCardAssets.ResourceOrCommodityFront(good);
    }
    public static Rect GoodUv(int good)
    {
        if(good<0||good>=8)throw new ArgumentOutOfRangeException(nameof(good));
        return new Rect(0,0,1,1);
    }
    public static void DrawGood(Rect rect,int good,bool missing=false,Color? tint=null)
    {
        var texture=GoodTexture(good);var original=GUI.color;
        GUI.color=original*(missing?new Color(.55f,.55f,.55f,.8f):Color.white);
        if(texture!=null)EntityCardAssets.DrawCardFace(rect,texture,tint:tint);
        else
        {
            if(fallbackStyle==null)fallbackStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter};
            GUI.Label(rect,Names[good],fallbackStyle);
        }
        GUI.color=original;
        if(missing)
        {
            // 缺料标记放在卡面旁，避免包围或裁切卡牌。
            var label=new GUIStyle(GUI.skin.label){alignment=TextAnchor.LowerRight,fontSize=15,fontStyle=FontStyle.Bold};
            label.normal.textColor=ModernOperationUI.Error;
            GUI.Label(new Rect(rect.xMax-5,rect.y,13,rect.height),"−",label);
        }
    }
}
