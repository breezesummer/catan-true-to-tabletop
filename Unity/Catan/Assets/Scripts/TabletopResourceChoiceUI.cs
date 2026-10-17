using System;
using Catan.Core;
using UnityEngine;

// 共用居中金矿面板；商品保留在八牌布局中，但不可领取。
public static class TabletopResourceChoiceUI
{
    public static readonly Rect Area=new Rect(338,141,764,618);
    static readonly string[] Names={"木头","砖块","羊毛","小麦","矿石","布匹","钱币","纸张"};
    static ResourceBag activeSelection;
    static string errorHint="";
    public static void Draw(int amount,ResourceBag selection,ResourceBag bank,Action confirm,Action<string> notify)
    {
        ModernOperationUI.Panel(Area);
        if(!ReferenceEquals(activeSelection,selection)){activeSelection=selection;errorHint="";}
        int required=Math.Min(amount,bank.Total);
        ModernOperationUI.Label(new Rect(368,159,704,42),"金矿 · 选择资源",ModernOperationUI.Title);
        ModernOperationUI.Label(new Rect(368,205,704,32),"点击资源牌增加数量 · 已选 "+selection.Total+" / "+required+" 张",ModernOperationUI.Center);
        for(int i=0;i<8;i++)
        {
            float x=381+(i%4)*176,y=252+(i/4)*205;
            bool resource=i<5;
            int count=resource?selection[(Resource)i]:0;
            var face=new Rect(x+28,y,88,130);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(i),0,resource?Color.white:new Color(.55f,.55f,.55f,.55f));
            ModernOperationUI.Label(new Rect(x,y+131,144,27),Names[i]+" × "+count,ModernOperationUI.Center);
            ModernOperationUI.Label(new Rect(x,y+158,144,24),resource?"银行 "+bank[(Resource)i]:"金矿不可选商品",ModernOperationUI.Small);
            // 保留点击回馈，超额时明确提示，而不是静默忽略或让计数越界。
            if(GUI.Button(face,GUIContent.none,GUIStyle.none))
            {
                string error=OperationResourceChoice.Add(i,amount,selection,bank);
                errorHint=error??"";
                if(error!=null)notify?.Invoke(error);
            }
            if(resource&&ModernOperationUI.Button(new Rect(x+117,y+95,28,31),"−",count>0)){selection[(Resource)i]--;errorHint="";}
        }
        var hint=new GUIStyle(ModernOperationUI.Center);hint.normal.textColor=ModernOperationUI.Error;
        ModernOperationUI.Label(new Rect(368,658,704,30),errorHint,hint);
        if(ModernOperationUI.Button(new Rect(368,699,186,40),"清空选择")){foreach(Resource r in Enum.GetValues(typeof(Resource)))selection[r]=0;errorHint="";}
        if(ModernOperationUI.Button(new Rect(570,699,502,40),"确认领取 "+required+" 张资源",selection.Total==required,true))confirm();
    }
}
