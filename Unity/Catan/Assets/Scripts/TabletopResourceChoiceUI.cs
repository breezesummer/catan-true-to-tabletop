using System;
using Catan.Core;
using UnityEngine;

// 金矿仅显示五种资源，领取上限仍由同一规则入口验证。
public static class TabletopResourceChoiceUI
{
    public static readonly Rect Area=new Rect(258,205,924,416);
    static readonly string[] Names={"木头","砖块","羊毛","小麦","矿石","布匹","钱币","纸张"};
    static ResourceBag activeSelection;
    static string errorHint="";
    public static void Draw(int amount,ResourceBag selection,ResourceBag bank,Action confirm,Action<string> notify,string caption="金矿 · 选择资源",OperationWindowState windows=null)
    {
        if(windows?.Hidden==true)return;
        windows?.Describe(caption);
        ModernOperationUI.Panel(Area);
        if(ModernOperationUI.Button(new Rect(1090,224,65,30),"收起")){windows?.Hide(caption);return;}
        if(!ReferenceEquals(activeSelection,selection)){activeSelection=selection;errorHint="";}
        int required=Math.Min(amount,bank.Total);
        ModernOperationUI.Label(new Rect(288,223,864,42),caption,ModernOperationUI.Title);
        ModernOperationUI.Label(new Rect(288,269,864,32),TabletopCardInput.Hint+" · 已选 "+selection.Total+" / "+required+" 张",ModernOperationUI.Center);
        for(int i=0;i<5;i++)
        {
            float x=296+i*176,y=316;
            int count=selection[(Resource)i];
            var face=new Rect(x+28,y,88,130);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(i));
            ModernOperationUI.Label(new Rect(x,y+131,144,27),Names[i]+" × "+count,ModernOperationUI.Center);
            if(bank[(Resource)i]==0)ModernOperationUI.Label(new Rect(x,y+158,144,24),"库存为0",ModernOperationUI.Center);
            // 保留点击回馈，超额时明确提示，而不是静默忽略或让计数越界。
            int delta=TabletopCardInput.Delta(face);
            if(delta!=0)
            {
                string error=OperationResourceChoice.Change(i,delta,amount,selection,bank);
                errorHint=error??"";
                if(error!=null)notify?.Invoke(error);
            }
        }
        var hint=new GUIStyle(ModernOperationUI.Center);hint.normal.textColor=ModernOperationUI.Error;
        ModernOperationUI.Label(new Rect(288,511,864,30),errorHint,hint);
        if(ModernOperationUI.Button(new Rect(288,561,186,40),"清空选择")){foreach(Resource r in Enum.GetValues(typeof(Resource)))selection[r]=0;errorHint="";}
        if(ModernOperationUI.Button(new Rect(490,561,662,40),"确认领取 "+required+" 张资源",selection.Total==required,true))confirm();
    }
}
