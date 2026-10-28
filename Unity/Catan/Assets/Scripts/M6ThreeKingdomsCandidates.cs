using System;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

public sealed partial class M6App
{
    static readonly Rect DiceChoicesArea=new Rect(206,180,426,150);
    static readonly Rect[] SkillDiceAreas={new Rect(40,201,45,45),new Rect(110,202,45,45),new Rect(76,161,45,45)};
    string specialChoiceToken;
    int fixedChoiceRed=-1,fixedChoiceEvent=-1;
    Vector2 specialScroll;
    internal bool SpecialGeneralCandidatesContain(Vector2 p)=>generalSelection.Active&&generalSelection.DiceIndices.Length>0&&
        (DiceChoicesArea.Contains(p)||SkillDiceAreas.Any(r=>r.Contains(p)));
    bool DrawSpecialGeneralCandidates()
    {
        if(!generalSelection.Active)return false;
        var contract=generalSelection.Contract;var options=generalSelection.Options;
        string token=contract.WindowId+"|"+contract.DecisionId;
        if(token!=specialChoiceToken){specialChoiceToken=token;fixedChoiceRed=fixedChoiceEvent=-1;specialScroll=Vector2.zero;}
        if(contract.DirectBatch&&(contract.DecisionKind=="b4-reveal"||contract.DecisionKind=="b4-subset"))return true;
        if(generalSelection.DiceIndices.Length>0){DrawGeneralDiceCandidates();return true;}
        if(contract.DecisionKind=="b4-pile"){DrawGeneralPileCandidates();return true;}
        if(generalSelection.RequiresBankCards)return false;
        if(options.Length==0)return false;
        // 不涉及桌面物体的类型、颜色、猜测数值与旧包单选银行，仍以结构化字段定位。
        if(options.All(a=>new[]{"card","b4-daoshu","color","b4-declare","b4-true","b4-false","response-challenge","response-trust","claim","luoying","done"}.Contains(a.Command.Option)))
        {DrawGeneralTypeCandidates(options);return true;}
        return false;
    }
    void DrawGeneralTypeCandidates(B2SkillAction[] actions)
    {
        PastoralMenuUI.Panel(CandidateArea);
        string title=generalSelection.Contract.DecisionKind=="shelie"?"涉猎公开牌 · 最多取三种，已选内容保留在权威待决":
            actions[0].Command.Option=="b4-daoshu"?"声明要猜的真实牌种 · 再点目标头像":
            actions[0].Command.Option=="color"?"选择发展牌颜色":actions[0].Command.Option=="b4-declare"?"声明小麦张数 · 尚未公开你的手牌子集":"当前响应候选 · 选中后确认";
        PastoralMenuUI.Label(new Rect(354,480,900,22),title,14,PastoralMenuUI.Ink,true);
        specialScroll=GUI.BeginScrollView(new Rect(350,505,916,130),specialScroll,new Rect(0,0,Mathf.Max(900,actions.Length*113),110));
        for(int i=0;i<actions.Length;i++)
        {
            var action=actions[i];var c=action.Command;var r=new Rect(i*113+3,2,106,106);bool selected=generalSelection.IsOptionSelected(action);
            CopperTabletopSkin.Panel(r,ModernOperationUI.Surface,selected);
            bool typed=c.Option=="card"||c.Option=="b4-daoshu"||c.Option=="claim"||c.Option=="luoying";
            int type=c.Option=="luoying"?0:c.CardType;
            string caption;
            if(typed&&type>=0&&type<8)
            {
                EntityCardAssets.DrawCardFace(new Rect(r.x+30,r.y+5,44,66),EntityCardAssets.ResourceOrCommodityFront(type));
                caption=ThreeKingdomsCardNames[type]+(c.Option=="luoying"?" × "+c.Counts[0]:"");
            }
            else if(c.Option=="color")
            {
                var color=c.CardType==0?new Color(.72f,.52f,.12f):c.CardType==1?new Color(.15f,.38f,.65f):new Color(.2f,.52f,.28f);
                PastoralMenuUI.Round(new Rect(r.x+26,r.y+7,54,61),color,4);caption=new[]{"黄色贸易","蓝色政治","绿色科学"}[Mathf.Clamp(c.CardType,0,2)];
            }
            else
            {
                caption=c.Option=="b4-declare"?c.CardType+" 张小麦":c.Option=="b4-true"?"声明为真":c.Option=="b4-false"?"声明为假":c.Option=="response-challenge"?"质疑":c.Option=="response-trust"?"不质疑":c.Option=="done"?"完成选择":DisplayPlayerText(action.Label);
                PastoralMenuUI.Label(new Rect(r.x+6,r.y+8,r.width-12,58),caption,16,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            }
            PastoralMenuUI.Label(new Rect(r.x+3,r.y+75,r.width-6,26),(selected?"✓ ":"")+caption,13,PastoralMenuUI.Ivory,false,TextAnchor.MiddleCenter);
            int input=ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting);
            if(input==1)generalSelection.ChooseOption(action);
            if(input==2)Inspect(DisplayPlayerText(action.Label),new Rect(354+i*113-specialScroll.x,505,106,106));
        }
        GUI.EndScrollView();
    }
    void DrawGeneralPileCandidates()
    {
        PastoralMenuUI.Panel(CandidateArea);
        PastoralMenuUI.Label(new Rect(354,480,900,22),"下书 · 只显示已授权亮出堆；未亮出堆保留牌背",14,PastoralMenuUI.Ink,true);
        var actions=generalSelection.Options;var revealed=ThreeKingdomsProjection.RevealedSkillCards??new int[8];
        for(int i=0;i<actions.Length;i++)
        {
            var action=actions[i];bool shown=action.Command.Option=="b4-revealed";bool selected=generalSelection.IsOptionSelected(action);
            var r=new Rect(354+i*450,509,438,118);CopperTabletopSkin.Panel(r,ModernOperationUI.Surface,selected);
            if(shown)
            {
                var types=Enumerable.Range(0,Math.Min(8,revealed.Length)).Where(t=>revealed[t]>0).ToArray();
                for(int n=0;n<types.Length;n++)
                {
                    float x=r.x+8+n*50;EntityCardAssets.DrawCardFace(new Rect(x,r.y+7,42,63),EntityCardAssets.ResourceOrCommodityFront(types[n]));
                    PastoralMenuUI.Label(new Rect(x,r.y+70,42,20),"×"+revealed[types[n]],12,PastoralMenuUI.Ivory,false,TextAnchor.MiddleCenter);
                }
                if(types.Length==0)PastoralMenuUI.Label(new Rect(r.x+12,r.y+20,r.width-24,50),"亮出堆为空",18,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
            }
            else EntityCardAssets.DrawCardFace(new Rect(r.x+188,r.y+6,48,72),EntityCardAssets.Texture(EntityCardAssets.Good(0).Back));
            PastoralMenuUI.Label(new Rect(r.x+6,r.y+91,r.width-12,23),(selected?"✓ ":"")+(shown?"亮出堆 · "+revealed.Sum()+" 张":"未亮出堆 · 内容保密"),14,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            int input=ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting);
            if(input==1)generalSelection.ChooseOption(action);
            if(input==2)Inspect(shown?"这些牌由对方正式亮出。":"这些牌尚未公开；选择后按规则取得，不预览真身。",r);
        }
    }
    void DrawGeneralDiceCandidates()
    {
        foreach(int index in generalSelection.DiceIndices)
        {
            if(index<0||index>=SkillDiceAreas.Length)continue;var r=SkillDiceAreas[index];bool selected=generalSelection.SelectedDie==index;
            Outline(r,selected?new Color(1,.81f,.28f):new Color(.48f,.89f,.62f),3);
            PastoralMenuUI.Label(new Rect(r.x-6,r.yMax+2,r.width+12,20),index==0?"红骰":index==1?"白骰":"事件骰",12,PastoralMenuUI.Ivory,false,TextAnchor.MiddleCenter);
            int input=ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting);
            if(input==1){generalSelection.SelectDie(index);specialScroll=Vector2.zero;}
            if(input==2)Inspect(generalSelection.Description,r);
        }
        PastoralMenuUI.Panel(DiceChoicesArea);
        if(generalSelection.SelectedDie<0)
        {PastoralMenuUI.Label(new Rect(218,193,402,118),"先点选描边的红骰或白骰，再选择费用与对应合法地块。",16,PastoralMenuUI.Ink,false,TextAnchor.MiddleCenter);return;}
        var actions=generalSelection.Options;
        if(actions.Any(a=>a.Command.Option=="fixed")){DrawFixedDiceCandidates(actions.Where(a=>a.Command.Option=="fixed").ToArray());return;}
        bool modify=actions.Any(a=>a.Command.Option=="modify");
        PastoralMenuUI.Label(new Rect(218,186,402,26),modify?"鬼才 · 选真实费用及高亮地块决定点数":"观星 · 选择这颗骰子的合法候选",14,PastoralMenuUI.Ink,true);
        if(modify)
        {
            string values=string.Join(" / ",generalSelection.CandidateActions.Where(a=>a.SelectionDiceValue.HasValue).Select(a=>a.SelectionDiceValue.Value).Distinct().OrderBy(x=>x));
            PastoralMenuUI.Label(new Rect(218,219,400,91),"可改为："+values+"\n点击本人手牌与棋盘高亮地块。选择不会扣牌或重掷；操作条确认后生效。",15,PastoralMenuUI.Ink);return;
        }
        for(int i=0;i<actions.Length;i++)
        {
            var action=actions[i];float width=Mathf.Min(110,390f/Mathf.Max(1,actions.Length));var r=new Rect(219+i*(width+4),220,width,90);
            bool selected=generalSelection.IsOptionSelected(action);CopperTabletopSkin.Panel(r,ModernOperationUI.Surface,selected);
            string face=generalSelection.SelectedDie==2?DiceEventName(action.SelectionDiceValue??0):(action.SelectionDiceValue??0).ToString();
            PastoralMenuUI.Label(new Rect(r.x+4,r.y+5,r.width-8,45),face,23,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            PastoralMenuUI.Label(new Rect(r.x+4,r.y+53,r.width-8,29),(selected?"✓ ":"")+"候选 "+(action.Command.CardType+1),13,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
            if(ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting)==1)generalSelection.ChooseOption(action);
        }
    }
    void DrawFixedDiceCandidates(B2SkillAction[] actions)
    {
        PastoralMenuUI.Label(new Rect(218,186,402,26),"直谏 · 红骰与白骰合计7；选择事件结果",14,PastoralMenuUI.Ink,true);
        var reds=actions.Select(a=>a.Command.Counts[0]).Distinct().OrderBy(x=>x).ToArray();
        var events=actions.Select(a=>a.Command.CardType).Distinct().OrderBy(x=>x).ToArray();
        for(int i=0;i<reds.Length;i++)
        {
            var r=new Rect(219+i*65,218,59,36);bool selected=fixedChoiceRed==reds[i];
            CopperTabletopSkin.Panel(r,new Color(.45f,.13f,.11f),selected);
            PastoralMenuUI.Label(r,reds[i]+" + "+(7-reds[i]),14,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            if(ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting)==1)fixedChoiceRed=reds[i];
        }
        for(int i=0;i<events.Length;i++)
        {
            var r=new Rect(219+i*65,264,59,40);bool selected=fixedChoiceEvent==events[i];
            CopperTabletopSkin.Panel(r,ModernOperationUI.Surface,selected);
            PastoralMenuUI.Label(r,DiceEventName(events[i]),13,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            if(ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting)==1)fixedChoiceEvent=events[i];
        }
        if(fixedChoiceRed>0&&fixedChoiceEvent>0)
        {var chosen=actions.FirstOrDefault(a=>a.Command.Counts[0]==fixedChoiceRed&&a.Command.CardType==fixedChoiceEvent);if(chosen!=null)generalSelection.ChooseOption(chosen);}
    }
    static string DiceEventName(int face)=>face<=3?"黑色 "+face:face==4?"贸易":face==5?"政治":"科学";
}
