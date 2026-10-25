using System;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

// 仅根据本席授权用途和手牌数量展示；规则核心仍裁定整批费用是否合法。
public static class ThreeKingdomsPaymentUI
{
    static readonly string[] Names={"木材","砖块","羊毛","谷物","矿石","布匹","钱币","纸张"};
    public static bool CanDeclare(ThreeKingdomsView view,string seat) => GeneralCatalog.IsAutomaticPackage(view?.PackageVersion)&&view.Players.Any(p=>p.PlayerId==seat&&p.ActiveGeneralId=="tk-g07"&&!p.FaceDown);
    public static int? TradeUse(ThreeKingdomsView view,int real,int? selected)
    {
        var allowed=view?.EffectiveCardTypes;
        if(allowed==null||real<0||real>=allowed.Length||allowed[real].Length<2)return null;
        int current=selected.HasValue&&allowed[real].Contains(selected.Value)?selected.Value:real;
        ModernOperationUI.Info("实际交出"+Names[real]+"，选择本次交换用途：");
        var area=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.Height(118),GUILayout.ExpandWidth(true));
        float cell=area.width/8;
        for(int i=0;i<8;i++)
        {
            var face=new Rect(area.x+i*cell+(cell-46)/2,area.y,46,69);
            bool old=GUI.enabled;GUI.enabled=old&&allowed[real].Contains(i);
            var color=GUI.color;if(!GUI.enabled)GUI.color=new Color(color.r,color.g,color.b,.3f);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(i));GUI.color=color;
            if(TabletopCardInput.Delta(face)>0)current=i;GUI.enabled=old;
            ModernOperationUI.Label(new Rect(area.x+i*cell,area.y+70,cell,44),(current==i?"✓ ":"")+Names[i],ModernOperationUI.Center);
        }
        return current==real?(int?)null:current;
    }
    public static int[] Payment(ThreeKingdomsView view,string seat,int[] selected,int[] hand,int total)
    {
        if(total<=0||view?.EffectiveCardTypes==null||!CanDeclare(view,seat)&&!view.EffectiveCardTypes.Any(types=>types.Length>1))return null;
        bool choose=GUILayout.Toggle(selected!=null,"自行挑选实际支付牌（可按武将能力视为其他牌种）",GUI.skin.button,GUILayout.Height(35));
        if(!choose)return null;
        if(selected==null)selected=new int[8];
        ModernOperationUI.ResourceCards("实际支付 · 共需 "+total+" 张",selected,hand,total);
        string usage=string.Join("；",Enumerable.Range(0,8).Where(i=>selected[i]>0).Select(i=>Names[i]+"可用于"+string.Join("／",view.EffectiveCardTypes[i].Select(t=>Names[t]))));
        if(usage.Length>0)ModernOperationUI.Info(usage);
        return selected;
    }
    public static void Declaration(ThreeKingdomsView view,string seat,int[] selected,ref int[] real,ref int[] uses)
    {
        if(!CanDeclare(view,seat)||selected==null){real=null;uses=null;return;}
        bool declare=GUILayout.Toggle(real!=null,"蛊惑：逐张声明本次支付或交易的牌种",GUI.skin.button,GUILayout.Height(35));
        if(!declare){real=null;uses=null;return;}
        var cards=B4CardInteraction.ExpandCards(selected);
        uses=B4CardInteraction.PreserveDeclarations(real,uses,cards);real=cards;
        if(cards.Length==0){ModernOperationUI.Info("请先选择实际交出的手牌。",true);return;}
        ModernOperationUI.Info("左侧是仅自己可见的真实牌；点右侧卡面声明用途。确认用途后，其他玩家将依次选择是否质疑。");
        for(int card=0;card<cards.Length;card++)
        {
            var area=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.Height(102),GUILayout.ExpandWidth(true));
            float cell=area.width/9,width=Mathf.Min(42,cell-4),height=width*1.5f;
            var actual=new Rect(area.x+(cell-width)/2,area.y,width,height);
            EntityCardAssets.DrawCardFace(actual,EntityCardAssets.ResourceOrCommodityFront(cards[card]));
            ModernOperationUI.Label(new Rect(area.x,area.y+height+2,cell,35),"真实\n"+Names[cards[card]],ModernOperationUI.Center);
            for(int type=0;type<8;type++)
            {
                var face=new Rect(area.x+(type+1)*cell+(cell-width)/2,area.y,width,height);
                EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(type));
                if(TabletopCardInput.Delta(face)>0)uses[card]=type;
                ModernOperationUI.Label(new Rect(area.x+(type+1)*cell,area.y+height+2,cell,35),(uses[card]==type?"✓ ":"")+Names[type],ModernOperationUI.Center);
            }
        }
    }
}
