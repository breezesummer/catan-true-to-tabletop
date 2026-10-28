using System;
using System.Linq;
using UnityEngine;

// 普通操作的支付草稿：实体牌仍从手牌区选，当前牌的用途只在它上方说明和选择。
public static class ThreeKingdomsPaymentBar
{
    static readonly string[] Names={"木材","砖块","羊毛","谷物","矿石","布匹","钱币","纸张"};
    static readonly Rect ConfirmArea=new Rect(342,660,824,44);
    static Rect usageArea;
    static bool visible;
    public static bool Contains(Vector2 point)=>visible&&(ConfirmArea.Contains(point)||usageArea.Contains(point));
    public static void Draw(B45PaymentContext context,Rect? focusedCard,bool canConfirm,string previewReason,Action confirm,Action cancel)
    {
        visible=context!=null&&context.Active;
        if(!visible)return;
        bool enabled=GUI.enabled&&!context.IsSubmitting;
        float anchor=focusedCard?.center.x??610;
        float top=Mathf.Min(592,(focusedCard?.y??690)-98);
        usageArea=new Rect(Mathf.Clamp(anchor-268,342,630),Mathf.Max(480,top),536,64);
        PastoralMenuUI.Panel(usageArea);
        var cards=context.Cards;
        var current=context.Current;
        int index=context.SelectedIndex;
        bool hasCard=current!=null;
        if(Button(new Rect(usageArea.x+6,usageArea.y+5,29,23),"‹",enabled&&index>0))context.FocusCard(index-1);
        if(Button(new Rect(usageArea.x+39,usageArea.y+5,29,23),"›",enabled&&index>=0&&index+1<cards.Length))context.FocusCard(index+1);
        current=context.Current;index=context.SelectedIndex;hasCard=current!=null;
        string caption=hasCard?"第 "+(index+1)+" 张 · 真实 "+Name(current.RealType)+" → "+(current.Use<0?"请选择用途":Name(current.Use)):"请点本人手牌，选择实际支付的牌";
        PastoralMenuUI.Label(new Rect(usageArea.x+76,usageArea.y+4,302,24),caption,13,PastoralMenuUI.Ink,true);
        if(context.CanDeclare&&Button(new Rect(usageArea.x+386,usageArea.y+4,143,24),context.Declaring?"✓ 蛊惑逐张声明":"启用蛊惑声明",enabled,context.Declaring))context.SetDeclaring(!context.Declaring);
        var allowed=context.AllowedUses;
        for(int type=0;type<8;type++)
        {
            var area=new Rect(usageArea.x+6+type*65.5f,usageArea.y+32,61,26);
            bool chosen=hasCard&&current.Use==type;
            if(Button(area,(chosen?"✓ ":"")+Names[type],enabled&&hasCard&&allowed.Contains(type),chosen))context.ChooseUse(type);
        }
        PastoralMenuUI.Panel(ConfirmArea);
        string progress=(context.Title??"选择支付牌")+" · 已选 "+context.Cards.Length+" / "+context.ExpectedCount+" 张";
        PastoralMenuUI.Label(new Rect(352,662,525,20),progress,14,PastoralMenuUI.Ink,true);
        string reason=!string.IsNullOrEmpty(context.Notice)?context.Notice:previewReason;
        if(string.IsNullOrEmpty(reason))reason=context.Ready?"牌与用途已就绪，确认后执行。":"每张牌都需选择本次用途。";
        PastoralMenuUI.Label(new Rect(352,681,525,19),reason,12,context.Ready?PastoralMenuUI.Muted:ModernOperationUI.Error);
        if(Button(new Rect(884,666,82,32),"重新选牌",enabled))context.ClearSelections();
        if(Button(new Rect(974,666,74,32),"取消",enabled)){cancel?.Invoke();return;}
        if(Button(new Rect(1056,666,100,32),context.IsSubmitting?"提交中":"确认支付",enabled&&canConfirm&&context.Ready,true))confirm?.Invoke();
    }
    static string Name(int type)=>type>=0&&type<Names.Length?Names[type]:"未选择";
    static bool Button(Rect area,string label,bool enabled,bool selected=false)
    {
        CopperTabletopSkin.Panel(area,ModernOperationUI.Surface,selected,false);
        PastoralMenuUI.Label(area,label,13,enabled?PastoralMenuUI.Ivory:PastoralMenuUI.Muted,true,TextAnchor.MiddleCenter);
        return ThreeKingdomsDirectInput.Read(area,enabled,false)==1;
    }
}
