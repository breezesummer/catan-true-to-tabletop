using System;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

public sealed partial class M6App
{
    static readonly Rect DiceChoicesArea=new Rect(206,180,426,150);
    static readonly Rect[] SkillDiceAreas={new Rect(40,201,45,45),new Rect(110,202,45,45),new Rect(76,161,45,45)};
    internal int DieVisualState(int index)
    {
        var actions=generalSelection.Contract?.Actions;
        if(actions!=null)
            foreach(var candidate in actions)
                if(candidate.SelectionDiceIndex==index)return generalSelection.SelectedDie==index?2:1;
        return 0;
    }
    string specialChoiceToken;
    int fixedChoiceRed=-1,fixedChoiceEvent=-1;
    internal bool TurningFixedDice=>generalSelection.Contract?.DecisionKind=="dice-fixed"&&generalSelection.SkillId=="tk-g21-s2"&&!generalSelection.AwaitingActivation&&!generalSelection.IsSubmitting;
    internal string FixedDiceDecision=>generalSelection.Contract?.DecisionId;
    internal int FixedDieValue(int index)=>index==0?fixedChoiceRed:index==1?7-fixedChoiceRed:fixedChoiceEvent<=3?0:fixedChoiceEvent-3;
    internal void SetFixedDice(int red,int symbol)
    {
        fixedChoiceRed=red;fixedChoiceEvent=symbol==0?1:symbol+3;
        var pick=generalSelection.Options.FirstOrDefault(a=>a.Command.Option=="fixed"&&a.Command.Counts[0]==red&&a.Command.CardType==fixedChoiceEvent);
        if(pick!=null)generalSelection.ChooseOption(pick);
    }
    Vector2 specialScroll;
    static Rect LuoyingArea=>new Rect(CandidateArea.center.x-136,CandidateArea.yMax-244,272,244);
    void DrawLuoying()
    {
        var r=LuoyingArea;
        TabletopResourceChoiceUI.DrawFrame(r,"落英",0,0);
        int count=generalSelection.LuoyingCount;
        if(count>0)
        {
            var face=new Rect(r.center.x-51,r.y+46,102,146);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(0));
            if(count>1)PastoralMenuUI.Label(new Rect(face.xMax+4,face.center.y-16,42,32),"×"+count,20,PastoralMenuUI.Ivory,true);
        }
        if(GeneralFrameUI.Choice(new Rect(r.center.x-104,r.yMax-43,98,34),"确定",GeneralCanSubmit&&generalSelection.CanConfirm,true))
            FinishGeneralAction();
        else if(GeneralFrameUI.Choice(new Rect(r.center.x+6,r.yMax-43,98,34),"取消",GeneralCanSubmit&&generalSelection.SkipCommand!=null))
            SubmitGeneralSkill(generalSelection.SkipCommand);
    }
    internal bool SpecialGeneralCandidatesContain(Vector2 p)=>!TurningFixedDice&&generalSelection.Active&&generalSelection.DiceIndices.Length>0&&
        (generalSelection.SelectedDie>=0&&!generalSelection.Options.Any(a=>a.Command.Option=="modify")&&DiceChoicesArea.Contains(p)||SkillDiceAreas.Any(r=>r.Contains(p)));
    bool DrawSpecialGeneralCandidates()
    {
        if(!generalSelection.Active)return false;
        var contract=generalSelection.Contract;var options=generalSelection.Options;
        string token=contract.WindowId+"|"+contract.DecisionId;
        if(token!=specialChoiceToken){specialChoiceToken=token;specialScroll=Vector2.zero;}
        if(contract.DirectBatch&&(contract.DecisionKind=="b4-reveal"||contract.DecisionKind=="b4-subset"))return true;
        if(TurningFixedDice)return true;
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
        bool shelie=generalSelection.Contract.DecisionKind=="shelie";
        if(shelie&&generalSelection.Contract.DirectBatch)actions=actions.Where(a=>a.Command.Option=="card").ToArray();
        DrawCandidateRow(shelie?"涉猎 · 选择至多三种资源":GeneralActionCaption,actions,actions.Length,false);
    }

    void DrawCandidateRow(string title,B2SkillAction[] actions,int publicCount,bool labelCardActions)
    {
        PastoralMenuUI.Panel(CandidateArea);
        PastoralMenuUI.Label(new Rect(CandidateArea.x,CandidateArea.y+3,CandidateArea.width,26),title,17,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
        var viewport=new Rect(CandidateArea.x+8,CandidateArea.y+30,CandidateArea.width-16,CandidateArea.height-32);
        float step=Mathf.Clamp(viewport.width/Math.Max(1,actions.Length),98,146);
        float rowWidth=step*actions.Length;
        float contentWidth=Mathf.Max(viewport.width,rowWidth);
        bool scrolls=rowWidth>viewport.width;
        float height=viewport.height-(scrolls?18:0);
        candidateScroll=GUI.BeginScrollView(viewport,candidateScroll,new Rect(0,0,contentWidth,height));
        for(int i=0;i<actions.Length;i++)
        {
            var action=actions[i];
            bool privateCard=i>=publicCount;
            var c=action.Command;
            int type=action.ResultCardType??action.RealCardType??-1;
            if(privateCard||c.Option=="card"||c.Option=="b4-daoshu"||c.Option=="choose-card"||c.Option=="b4-guess"||c.Option=="claim")type=c.CardType;
            Texture2D texture=null;
            if(type>=0&&type<8)texture=EntityCardAssets.ResourceOrCommodityFront(type);
            else if(c.Option=="color")texture=EntityCardAssets.ProgressBack(c.CardType);
            else if(Enum.TryParse(action.VirtualCardId,out Catan.Core.M4.ProgressCardKind kind))texture=EntityCardAssets.ProgressFront((int)kind);
            bool selected=privateCard?generalSelection.SelectedPrivateCounts[type]>0:generalSelection.IsOptionSelected(action);
            float x=(contentWidth-rowWidth)/2+i*step;
            Rect hit;
            if(texture!=null)
            {
                bool caption=!privateCard&&(labelCardActions||c.Option=="color");
                var slot=new Rect(x+3,selected?0:6,step-6,height-(caption?24:0)-9);
                hit=EntityCardAssets.FitCardRect(slot,EntityCardAssets.FaceAspect(texture),GUI.matrix);
                EntityCardAssets.DrawCardFace(hit,texture,selected?1:0);
                if(selected)PastoralMenuUI.Label(new Rect(hit.xMax-23,hit.y+1,22,22),"✓",16,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
                if(caption)PastoralMenuUI.Label(new Rect(x,height-24,step,24),OptionCaption(action),13,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            }
            else
            {
                hit=new Rect(x+4,(height-54)/2,step-8,54);
                GeneralFrameUI.Skill(hit,OptionCaption(action),true,GeneralCanInteract,selected,false);
            }
            int input=ThreeKingdomsDirectInput.Read(hit,GeneralCanInteract&&!generalSelection.IsSubmitting);
            if(input==1)
            {
                if(privateCard){if(generalSelection.TogglePrivateCard(type))FinishGeneralPick();}
                else ChooseGeneralOption(action);
                break;
            }
            if(input==2)Inspect(DisplayPlayerText(action.Label),new Rect(viewport.x+hit.x-candidateScroll.x,viewport.y+hit.y,hit.width,hit.height));
        }
        GUI.EndScrollView();
    }
    void DrawGeneralPileCandidates()
    {
        PastoralMenuUI.Panel(CandidateArea);
        PastoralMenuUI.Label(new Rect(354,480,900,22),"下书 · 选一叠牌",14,PastoralMenuUI.Ink,true);
        var actions=generalSelection.Options;var revealed=ThreeKingdomsProjection.RevealedSkillCards??new int[8];
        for(int i=0;i<actions.Length;i++)
        {
            var action=actions[i];bool shown=action.Command.Option=="b4-revealed";bool selected=generalSelection.IsOptionSelected(action);
            var r=new Rect(354+i*450,509,438,118);
            if(shown)
            {
                var types=Enumerable.Range(0,Math.Min(8,revealed.Length)).Where(t=>revealed[t]>0).ToArray();
                for(int n=0;n<types.Length;n++)
                {
                    float step=Mathf.Min(68,r.width/types.Length);
                    float x=r.center.x+(n-(types.Length-1)*.5f)*step;
                    EntityCardAssets.DrawCardFace(new Rect(x-step/2,r.y,step-2,80),EntityCardAssets.ResourceOrCommodityFront(types[n]));
                    PastoralMenuUI.Label(new Rect(x-step/2,r.y+76,step,18),"×"+revealed[types[n]],12,PastoralMenuUI.Ivory,false,TextAnchor.MiddleCenter);
                }
                if(types.Length==0)PastoralMenuUI.Label(new Rect(r.x+12,r.y+20,r.width-24,50),"亮出堆为空",18,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
            }
            else EntityCardAssets.DrawCardFace(new Rect(r.center.x-31,r.y,62,90),EntityCardAssets.Texture(EntityCardAssets.Good(0).Back));
            PastoralMenuUI.Label(new Rect(r.x+6,r.y+91,r.width-12,23),(selected?"✓ ":"")+(shown?"亮出堆 · "+revealed.Sum()+" 张":"未亮出堆"),14,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            int input=ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting);
            if(input==1)ChooseGeneralOption(action);
            if(input==2)Inspect(shown?"这些牌由对方正式亮出。":"这些牌尚未公开；选择后按规则取得，不预览真身。",r);
        }
    }
    void DrawGeneralDiceCandidates()
    {
        foreach(int index in generalSelection.DiceIndices)
        {
            if(index<0||index>=SkillDiceAreas.Length)continue;var r=SkillDiceAreas[index];bool selected=generalSelection.SelectedDie==index;
            int input=ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting);
            if(input==1){generalSelection.SelectDie(index);specialScroll=Vector2.zero;}
            if(input==2)Inspect(generalSelection.Description,r);
        }
        if(generalSelection.SelectedDie<0)return;
        var actions=generalSelection.Options;
        bool modify=actions.Any(a=>a.Command.Option=="modify");
        if(modify)return; // 鬼才直接通过手牌和棋盘地块决定结果。
        for(int i=0;i<actions.Length;i++)
        {
            var action=actions[i];float step=Mathf.Min(72,390f/Mathf.Max(1,actions.Length));
            var r=new Rect(219+i*step,207,step-4,54);
            bool selected=generalSelection.IsOptionSelected(action);
            int face=action.SelectionDiceValue??0;
            if(generalSelection.SelectedDie==2)
            {
                float side=Mathf.Min(r.width,r.height)-4;
                r=new Rect(r.center.x-side/2,r.center.y-side/2,side,side);
                DrawEventDie(r,face,selected);
            }
            else ProgressTableUI.DiceFace(r,face,generalSelection.SelectedDie==0,selected);
            if(ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting)==1)ChooseGeneralOption(action);
        }
    }
    void DrawEventDie(Rect r,int face,bool selected)
    {
        if(Event.current.type!=EventType.Repaint)return;
        var table=presentation is M4App cities?cities.VerificationBoard.TableScene:
            ((M5App)presentation).VerificationBoard.TableScene;
        int symbol=face<=3?0:face-3;
        GUI.DrawTexture(r,table.Objects.EventDieTexture(symbol),ScaleMode.StretchToFill,true,0,Color.white,0,r.width*.18f);
        ProgressTableUI.FaceGlow(r,selected);
    }
}
