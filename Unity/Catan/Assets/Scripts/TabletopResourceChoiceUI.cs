using System;
using Catan.Core;
using UnityEngine;

// 金矿、引水渠和捕鱼共用银行牌面。取单张直接落定，多张可调整后再领取。
public static class TabletopResourceChoiceUI
{
    public static readonly Rect Area=new Rect(352,462,738,242);
    static readonly string[] Names={"木材","砖块","羊毛","小麦","矿石"};

    public static void Draw(int amount,ResourceBag selection,ResourceBag bank,Action confirm,string caption="金矿 · 选择资源",OperationWindowState windows=null,Action cancel=null)
    {
        if(windows?.Suppressed==true)return;
        windows?.Describe(caption);
        int required=Math.Min(amount,bank.Total);
        string title=required>1?caption+"   "+selection.Total+" / "+required:caption;
        ModernOperationUI.WindowFrame(Area,new Rect(Area.x+16,Area.y+6,Area.width-104,32),title);
        if(ModernOperationUI.Button(new Rect(Area.xMax-80,Area.y+8,32,28),"—"))
        {if(windows!=null)windows.Hide(caption);else cancel?.Invoke();return;}
        if(ModernOperationUI.Button(new Rect(Area.xMax-42,Area.y+8,32,28),"×"))
        {if(windows!=null)windows.Close();else cancel?.Invoke();return;}
        for(int i=0;i<Names.Length;i++)
        {
            var resource=(Resource)i;
            int count=selection[resource];
            float x=Area.x+62+i*135,y=Area.y+43;
            var face=new Rect(x,y-(count>0?6:0),72,106);
            var previous=GUI.color;
            if(bank[resource]==0)GUI.color=new Color(.47f,.47f,.47f,1);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(i));
            GUI.color=previous;
            ModernOperationUI.Label(new Rect(x-24,y+109,120,24),count>0?Names[i]+" × "+count:Names[i],ModernOperationUI.Center);
            if(required==1)
            {
                if(bank[resource]>0&&TabletopButtonFeedback.Button(face))
                {
                    foreach(Resource r in Enum.GetValues(typeof(Resource)))selection[r]=0;
                    selection[resource]=1;
                    confirm();
                    return;
                }
            }
            else
            {
                int delta=TabletopCardInput.Delta(face);
                if(delta!=0)OperationResourceChoice.Change(i,delta,amount,selection,bank);
            }
        }
        if(required!=1&&ModernOperationUI.Button(new Rect(Area.center.x-98,Area.yMax-48,196,36),"领取 "+required+" 张资源",selection.Total==required,true))confirm();
    }
}
