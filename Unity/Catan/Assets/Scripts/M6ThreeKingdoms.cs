using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

// 只读取人类所属席位的投影。候选、暗置、未公开拟登场不从权威存档取值。
public sealed partial class M6App
{
    static readonly Rect ThreeKingdomsBar=new Rect(402,103,760,70);
    static readonly Rect ThreeKingdomsPanel=new Rect(235,185,970,520);
    readonly HashSet<string> selectedGenerals=new HashSet<string>();
    string threeKingdomsDecision;
    bool threeKingdomsPanelOpen;
    bool threeKingdomsSkillsTab;
    Vector2 threeKingdomsScroll;
    string inspectedGeneral;
    B2SkillCommand threeKingdomsSkillDraft;
    int threeKingdomsRetirementPhase;
    static readonly string[] ThreeKingdomsCardNames={"木材","砖","羊毛","谷物","矿石","布匹","钱币","纸张"};
    ThreeKingdomsView ThreeKingdomsProjection=>presentation is M4App cities?cities.M6View?.ThreeKingdoms:
        presentation is M5App combined?combined.M6View?.ThreeKingdoms:null;
    bool ThreeKingdomsAdvanceAvailable=>presentation is M4App cities?
        cities.M6View?.LegalActions.Any(c=>c.Kind==Catan.Core.M4.CommandKind.AdvanceThreeKingdomsPhase)==true:
        presentation is M5App combined&&combined.M6View?.LegalActions.Any(c=>c.Kind==Catan.Core.M5.CommandKind.AdvanceThreeKingdomsPhase)==true;
    void ResetThreeKingdomsPanel(){selectedGenerals.Clear();threeKingdomsDecision=null;threeKingdomsPanelOpen=false;threeKingdomsSkillsTab=false;threeKingdomsScroll=Vector2.zero;threeKingdomsSkillDraft=null;}
    internal bool ThreeKingdomsBlocksInput(Vector2 point)=>ThreeKingdomsProjection!=null&&
        (threeKingdomsPanelOpen||ThreeKingdomsBar.Contains(point));
    internal bool ThreeKingdomsPanelBlocksTable=>ThreeKingdomsProjection!=null&&threeKingdomsPanelOpen;
    bool TryCloseThreeKingdomsPanel(){if(!ThreeKingdomsPanelBlocksTable)return false;threeKingdomsPanelOpen=false;return true;}
    internal void VerificationOpenThreeKingdomsPanel(){threeKingdomsPanelOpen=true;threeKingdomsSkillsTab=ThreeKingdomsProjection?.SkillActions?.Length>0;}
    internal void VerificationChooseGeneralSkill(int index)
    {
        var actions=ThreeKingdomsProjection?.SkillActions;
        if(actions==null||index<0||index>=actions.Length||!IsHumanTurn)throw new InvalidOperationException("当前没有属于人类席位的技能选项。");
        SubmitGeneralSkill(actions[index].Command);
    }
    internal void RestartLocalGame()
    {
        if(NetworkGame||!Ready)return;
        var names=localPlayerNames.ToArray();
        StartGame(RuleSet,ScenarioId,PlayerCount,HumanSeat,Paused,targetVictoryPoints:TargetVictoryPoints,aiSpeedLevel:AiSpeedLevel,threeKingdoms:ThreeKingdoms);
        localPlayerNames=names;
        Notice="已保留玩法、三国开关、席位、姓名、胜利目标与 AI 节奏，重新生成地图和行动顺序。";
    }
    static string GeneralName(string id)=>string.IsNullOrEmpty(id)?"尚未公开":GeneralCatalog.Get(id).Name;
    static string ThreeKingdomsPhaseName(ThreeKingdomsPhase phase)
    {
        switch(phase)
        {
            case ThreeKingdomsPhase.OpeningSelection:return "私密开局选将";
            case ThreeKingdomsPhase.InitialPlacement:return "初始摆放";
            case ThreeKingdomsPhase.Preparation:return "准备阶段";
            case ThreeKingdomsPhase.Judgment:return "判定阶段";
            case ThreeKingdomsPhase.Roll:return "掷骰阶段";
            case ThreeKingdomsPhase.Draw:return "摸牌阶段";
            case ThreeKingdomsPhase.Play:return "出牌阶段";
            case ThreeKingdomsPhase.End:return "结束阶段";
            case ThreeKingdomsPhase.RoundEnd:return "轮末结算";
            default:return "本局结束";
        }
    }
    void DrawThreeKingdoms()
    {
        var view=ThreeKingdomsProjection;if(view==null)return;
        bool automatic=GeneralCatalog.IsAutomaticPackage(view.PackageVersion);
        if(threeKingdomsSkillDraft!=null&&threeKingdomsSkillDraft.WindowId!=view.WindowId)threeKingdomsSkillDraft=null;
        var pending=view.PendingDecision;
        bool ownChoice=pending!=null&&pending.PlayerId==HumanSeat;
        bool ownSkill=view.SkillDecision!=null&&view.SkillDecision.PlayerId==HumanSeat;
        string currentDecision=ownChoice?pending.DecisionId:ownSkill?view.SkillDecision.DecisionId:null;
        if(currentDecision!=null&&threeKingdomsDecision!=currentDecision)
        {
            threeKingdomsDecision=currentDecision;selectedGenerals.Clear();inspectedGeneral=null;threeKingdomsPanelOpen=true;threeKingdomsSkillsTab=ownSkill;threeKingdomsScroll=Vector2.zero;
        }
        else if(currentDecision==null&&threeKingdomsDecision!=null)
        {threeKingdomsDecision=null;selectedGenerals.Clear();threeKingdomsPanelOpen=false;}
        PastoralMenuUI.Styles(font);
        PastoralMenuUI.Round(ThreeKingdomsBar,new Color(.12f,.18f,.14f,.96f),6);
        PastoralMenuUI.Label(new Rect(415,110,738,22),view.PackageLabel,14,PastoralMenuUI.Ivory,true);
        string state="第 "+view.Clock.Round+" 轮 · "+ThreeKingdomsPhaseName(view.Clock.Phase);
        if(pending!=null)state+=" · "+(ownChoice?"请完成私密选择":PlayerDisplayName(pending.PlayerId)+" 正在选将");
        else if(view.WaitingSelectionPlayerIds?.Length>0)state+=" · 等待 "+string.Join("、",view.WaitingSelectionPlayerIds.Select(PlayerDisplayName))+" 选将";
        else if(view.SkillDecision!=null)state+=" · "+(ownSkill?"请完成技能选择":PlayerDisplayName(view.SkillDecision.PlayerId)+" 正在选择");
        PastoralMenuUI.Label(new Rect(415,139,398,24),state,14,PastoralMenuUI.Ivory);
        if(PastoralMenuUI.Button(new Rect(830,131,150,33),ownChoice?"继续私密选将":ownSkill?"继续技能选择":"武将与技能"))threeKingdomsPanelOpen=!threeKingdomsPanelOpen;
        if(!automatic)
        {if(PastoralMenuUI.Button(new Rect(991,131,159,33),"推进当前阶段",ThreeKingdomsAdvanceAvailable&&IsHumanTurn))SubmitThreeKingdoms("AdvanceThreeKingdomsPhase");}
        else PastoralMenuUI.Label(new Rect(991,136,159,28),"阶段自动结算",14,PastoralMenuUI.Muted);
        if(!threeKingdomsPanelOpen)return;
        PastoralMenuUI.Fill(new Rect(0,175,1440,725),new Color(.035f,.08f,.045f,.50f));
        PastoralMenuUI.Panel(ThreeKingdomsPanel);
        PastoralMenuUI.Label(new Rect(265,211,850,32),ownChoice?ThreeKingdomsDecisionCaption(pending):ownSkill?view.SkillDecision.Title:"武将与技能",25,null,true);
        if(PastoralMenuUI.Button(new Rect(1127,210,48,34),"收起"))threeKingdomsPanelOpen=false;
        PastoralMenuUI.Label(new Rect(265,252,910,28),view.SkillsEnabled?view.PackageLabel+" · 每轮末寿命减 1":"旧 B1 存档 · 武将技能未启用 · 每轮末当前登场将寿命减 1",14,PastoralMenuUI.Muted);
        if(ownChoice)DrawGeneralChoices(view,pending);
        else
        {
            if(PastoralMenuUI.Button(new Rect(265,288,230,34),"公开状态与技能原文",true,false,!threeKingdomsSkillsTab)){threeKingdomsSkillsTab=false;threeKingdomsScroll=Vector2.zero;}
            if(PastoralMenuUI.Button(new Rect(507,288,230,34),ownSkill?"待决选择":"可用技能",view.SkillsEnabled,false,threeKingdomsSkillsTab)){threeKingdomsSkillsTab=true;threeKingdomsScroll=Vector2.zero;}
            if(threeKingdomsSkillsTab)DrawGeneralSkillActions(view,ownSkill);
            else DrawGeneralLibrary(view);
        }
    }
    static string ThreeKingdomsDecisionCaption(ThreeKingdomsDecision decision)
    {
        switch(decision.Kind)
        {
            case ThreeKingdomsDecisionKind.SelectOpening:return "私密选将 · 必须选 2 张";
            case ThreeKingdomsDecisionKind.DeployOpening:return "私密选择登场 · 全员完成后统一公开";
            case ThreeKingdomsDecisionKind.SelectReplacement:return "补充武将 · 必须留 1 张";
            default:return "选择登场 · 从原暗置与新留牌中选 1 张";
        }
    }
    void DrawGeneralChoices(ThreeKingdomsView view,ThreeKingdomsDecision decision)
    {
        var ids=decision.CandidateIds;int count=ids.Length;
        float width=(910-12*Mathf.Max(0,count-1))/Mathf.Max(1,count);
        for(int i=0;i<count;i++)
        {
            var general=GeneralCatalog.Get(ids[i]);bool chosen=selectedGenerals.Contains(ids[i]);
            var area=new Rect(265+i*(width+12),303,width,228);
            if(PastoralMenuUI.Card(area,general.Name,general.Faction+" · "+general.Title+"\n\n"+string.Join(" / ",general.Skills.Select(s=>s.Name))+"\n"+(view.SkillsEnabled?"全部技能已启用":"旧 B1 · 技能未启用"),chosen))
            {
                inspectedGeneral=ids[i];threeKingdomsScroll=Vector2.zero;
                if(chosen)selectedGenerals.Remove(ids[i]);
                else if(selectedGenerals.Count<decision.RequiredCount)selectedGenerals.Add(ids[i]);
                else if(decision.RequiredCount==1){selectedGenerals.Clear();selectedGenerals.Add(ids[i]);}
            }
        }
        string inspected=inspectedGeneral??ids.FirstOrDefault();
        if(inspected!=null)
        {
            string text=string.Join("\n",GeneralCatalog.Get(inspected).Skills.Select(s=>s.Name+"："+s.OriginalText));
            float height=42+Mathf.CeilToInt(text.Length/54f)*20;
            threeKingdomsScroll=GUI.BeginScrollView(new Rect(265,542,910,66),threeKingdomsScroll,new Rect(0,0,884,Mathf.Max(66,height)));
            PastoralMenuUI.Label(new Rect(0,0,875,height),text,14,PastoralMenuUI.Muted);GUI.EndScrollView();
        }
        PastoralMenuUI.Label(new Rect(265,625,550,46),"已选 "+selectedGenerals.Count+" / "+decision.RequiredCount+" 张。点击卡牌可查看原文；收起不会取消待决。",15,PastoralMenuUI.Muted);
        bool ready=selectedGenerals.Count==decision.RequiredCount&&decision.PlayerId==HumanSeat;
        if(PastoralMenuUI.Button(new Rect(835,625,340,46),"确认选择",ready,true))
        {
            bool select=decision.Kind==ThreeKingdomsDecisionKind.SelectOpening||decision.Kind==ThreeKingdomsDecisionKind.SelectReplacement;
            SubmitThreeKingdoms(select?"ChooseGenerals":"DeployGeneral",selectedGenerals.OrderBy(x=>x,StringComparer.Ordinal).ToArray());
        }
    }
    void DrawGeneralLibrary(ThreeKingdomsView view)
    {
        var statuses=view.SkillStatus??Array.Empty<string>();
        if(view.BoardAssets!=null)statuses=statuses.Concat(view.BoardAssets.Where(a=>a.Damaged||a.Kind=="Knight"&&a.Level!=a.EffectiveLevel)
            .Select(a=>PlayerDisplayName(a.PlayerId)+"："+ThreeKingdomsBoardPresentation.Describe(a))).ToArray();
        if(view.ProgressHandLimit.HasValue)statuses=statuses.Concat(new[]{"你的发展牌上限："+view.ProgressHandLimit.Value+" 张。常规修复入口位于可用技能。"}).ToArray();
        foreach(var hand in view.MilitaryHands??Array.Empty<B5RevealedHand>())
            statuses=statuses.Concat(new[]{(hand.Public?"持续明牌 · ":"本次私密查看 · ")+PlayerDisplayName(hand.PlayerId)+"："+
                string.Join("、",Enumerable.Range(0,8).Where(i=>hand.Cards[i]>0).Select(i=>ThreeKingdomsCardNames[i]+" × "+hand.Cards[i]))}).ToArray();
        string revealed=ThreeKingdomsRevealedCards(view);if(revealed.Length>0)statuses=statuses.Concat(new[]{revealed}).ToArray();
        float statusHeight=statuses.Length==0?0:36+statuses.Length*24;
        float contentHeight=statusHeight+view.Players.Sum(player=>105+(string.IsNullOrEmpty(player.ActiveGeneralId)?0:GeneralCatalog.Get(player.ActiveGeneralId).Skills.Sum(skill=>60+Mathf.CeilToInt(skill.OriginalText.Length/52f)*21)));
        threeKingdomsScroll=GUI.BeginScrollView(new Rect(265,336,910,266),threeKingdomsScroll,new Rect(0,0,884,Mathf.Max(266,contentHeight)));
        float y=0;
        if(statuses.Length>0){PastoralMenuUI.Label(new Rect(15,y,854,statusHeight),string.Join("\n",statuses),15,PastoralMenuUI.Muted);y+=statusHeight;}
        foreach(var player in view.Players)
        {
            PastoralMenuUI.RoomRow(new Rect(0,y,884,52));
            string publicState=string.IsNullOrEmpty(player.ActiveGeneralId)?player.SelectionComplete?"选择完成，等待统一公开":"尚未完成选将":
                GeneralName(player.ActiveGeneralId)+" · 剩余 "+player.RemainingLife+" 轮"+(player.FaceDown?" · 背面":"");
            PastoralMenuUI.Label(new Rect(15,y+12,545,30),PlayerDisplayName(player.PlayerId)+"  ·  "+publicState,17,null,true);
            string hidden=player.PlayerId==HumanSeat?"你的暗置："+string.Join("、",player.HiddenGeneralIds.Select(GeneralName)):"暗置 "+player.HiddenCount+" 张";
            PastoralMenuUI.Label(new Rect(570,y+13,305,30),hidden,14,PastoralMenuUI.Muted);
            y+=62;
            if(!string.IsNullOrEmpty(player.ActiveGeneralId))foreach(var skill in GeneralCatalog.Get(player.ActiveGeneralId).Skills)
            {
                PastoralMenuUI.Label(new Rect(15,y,854,26),skill.Name+(view.SkillsEnabled?"":" · 旧存档未启用"),16,null,true);y+=29;
                float height=31+Mathf.CeilToInt(skill.OriginalText.Length/52f)*21;
                PastoralMenuUI.Label(new Rect(15,y,854,height),skill.OriginalText,15,PastoralMenuUI.Muted);y+=height;
            }
            y+=43;
        }
        GUI.EndScrollView();
        if(PastoralMenuUI.Button(new Rect(265,625,GeneralCatalog.IsAutomaticPackage(view.PackageVersion)?245:350,46),"主动隐退当前将",view.CanRetire&&!TableDiceRolling,true))SubmitThreeKingdoms("RetireGeneral");
        if(GeneralCatalog.IsAutomaticPackage(view.PackageVersion))DrawScheduledRetirement();
        if(!GeneralCatalog.IsAutomaticPackage(view.PackageVersion)&&PastoralMenuUI.Button(new Rect(835,625,340,46),"完成"+ThreeKingdomsPhaseName(view.Clock.Phase),ThreeKingdomsAdvanceAvailable&&IsHumanTurn,true))SubmitThreeKingdoms("AdvanceThreeKingdomsPhase");
    }
    void DrawGeneralSkillActions(ThreeKingdomsView view,bool ownSkill)
    {
        if(threeKingdomsSkillDraft!=null){DrawGeneralSkillCost();return;}
        var actions=view.SkillActions??Array.Empty<B2SkillAction>();
        string explanation=ownSkill?view.SkillDecision.Description:view.SkillDecision!=null?"等待 "+PlayerDisplayName(view.SkillDecision.PlayerId)+" 完成技能选择。":"选择要发动的技能。按钮列出当前真实可用的目标与费用。";
        if(actions.Length==0&&view.SkillDecision==null)explanation="当前没有可以主动发动的技能。自动技能将在满足条件时结算。";
        string revealed=ThreeKingdomsRevealedCards(view);if(revealed.Length>0)explanation+="\n"+revealed;
        var selected=actions.FirstOrDefault(a=>a.CardSelectionKind=="select"&&a.Command.Counts?.Any(n=>n>0)==true)?.Command.Counts;
        if(selected!=null)explanation+="\n已选："+string.Join("、",Enumerable.Range(0,8).Where(i=>selected[i]>0).Select(i=>ThreeKingdomsCardNames[i]+" × "+selected[i]));
        PastoralMenuUI.Label(new Rect(265,337,910,58),explanation,16,PastoralMenuUI.Muted);
        var cardActions=actions.Where(a=>B4CardInteraction.ShownCards(a,view.RevealedSkillCards).Any(n=>n>0)||a.ResultCardType.HasValue||a.VirtualCardId!=null||a.Command.Option=="b4-revealed"||a.Command.Option=="b4-hidden").ToArray();
        var textActions=actions.Except(cardActions).ToArray();
        float cardHeight=Mathf.Ceil(cardActions.Length/4f)*178,rows=Mathf.Ceil(textActions.Length/2f);
        threeKingdomsScroll=GUI.BeginScrollView(new Rect(265,405,910,200),threeKingdomsScroll,new Rect(0,0,884,Mathf.Max(200,cardHeight+rows*60)));
        for(int i=0;i<cardActions.Length;i++)
        {
            var action=cardActions[i];var shown=B4CardInteraction.ShownCards(action,view.RevealedSkillCards);var types=Enumerable.Range(0,8).Where(t=>shown[t]>0).ToArray();
            int type=types.FirstOrDefault();float x=(i%4)*221,y=(i/4)*178;
            bool enabled=!TableDiceRolling&&!TablePresentationBlocked;bool old=GUI.enabled;GUI.enabled=old&&enabled;
            Catan.Core.M4.ProgressCardKind virtualKind;bool virtualCard=Enum.TryParse(action.VirtualCardId,out virtualKind);
            bool hiddenPile=action.Command.Option=="b4-hidden",revealedPile=action.Command.Option=="b4-revealed";
            int faceCount=types.Length+(virtualCard?1:0)+(action.ResultCardType.HasValue?1:0)+(hiddenPile?1:0);
            bool clicked=false;float width=Mathf.Min(66,204f/Mathf.Max(1,faceCount));int drawn=0;
            if(hiddenPile)
            {
                var face=new Rect(x+75,y,66,99);
                EntityCardAssets.DrawCardFace(face,EntityCardAssets.Texture(EntityCardAssets.Good(0).Back));
                if(TabletopCardInput.Delta(face)>0)clicked=true;
            }
            if(revealedPile&&types.Length==0)clicked=PastoralMenuUI.Button(new Rect(x+20,y+25,176,50),"空亮出堆 · 0张",enabled);
            for(int card=0;card<types.Length;card++)
            {
                var face=new Rect(x+(216-faceCount*width)/2+drawn++*width,y,width,width*1.5f);
                EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(types[card]));
                if(TabletopCardInput.Delta(face)>0)clicked=true;
            }
            if(virtualCard)
            {
                var face=new Rect(x+(216-faceCount*width)/2+drawn++*width,y,width,width*1.5f);
                EntityCardAssets.DrawCardFace(face,EntityCardAssets.ProgressFront((int)virtualKind));if(TabletopCardInput.Delta(face)>0)clicked=true;
            }
            if(action.ResultCardType.HasValue)
            {
                var face=new Rect(x+(216-faceCount*width)/2+drawn*width,y,width,width*1.5f);
                EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(action.ResultCardType.Value));if(TabletopCardInput.Delta(face)>0)clicked=true;
            }
            string cardSummary=string.Join("＋",types.Select(t=>ThreeKingdomsCardNames[t]+" × "+shown[t]));
            if(revealedPile)cardSummary="亮出堆 · "+shown.Sum()+"张";
            else if(hiddenPile)cardSummary="未亮出堆 · 牌种不可见";
            if(action.ResultCardType.HasValue)cardSummary+=(cardSummary.Length>0?" → ":"卡效：")+ThreeKingdomsCardNames[action.ResultCardType.Value];
            PastoralMenuUI.Label(new Rect(x,y+99,216,24),cardSummary,14,null,true,TextAnchor.MiddleCenter);
            string caption=action.Label;foreach(var player in view.Players)caption=caption.Replace(player.PlayerId,PlayerDisplayName(player.PlayerId));
            if(action.AvailableCount.HasValue)caption+=" · 可用 "+action.AvailableCount.Value;
            if(action.EffectiveCardType.HasValue&&action.EffectiveCardType.Value!=type)caption+=" · 视为"+ThreeKingdomsCardNames[action.EffectiveCardType.Value];
            PastoralMenuUI.Label(new Rect(x+3,y+124,212,48),caption,13,PastoralMenuUI.Muted,false,TextAnchor.UpperCenter);
            if(clicked)
            {
                if(action.Command.Option=="gift"||action.Command.Option=="exchange-hand")threeKingdomsSkillDraft=CopyGeneralSkill(action.Command);
                else SubmitGeneralSkill(action.Command);
            }
            GUI.enabled=old;
        }
        for(int i=0;i<textActions.Length;i++)
        {
            string caption=textActions[i].Label;
            foreach(var player in view.Players)caption=caption.Replace(player.PlayerId,PlayerDisplayName(player.PlayerId));
            if(textActions[i].Command.Option=="tiles")caption="天妒："+string.Join(" ＋ ",textActions[i].Command.TileIds.Select(ThreeKingdomsTileName));
            if(textActions[i].Command.Option=="exchange-progress")caption="制衡：同色换抽「"+Catan.Core.DevelopmentCardText.Name(textActions[i].Command.ProgressCardId)+"」";
            if(PastoralMenuUI.Button(new Rect((i%2)*448,cardHeight+(i/2)*60,436,50),caption,!TableDiceRolling&&!TablePresentationBlocked))
            {
                var command=textActions[i].Command;
                if(command.Option=="gift"||command.Option=="exchange-hand")threeKingdomsSkillDraft=CopyGeneralSkill(command);
                else SubmitGeneralSkill(command);
            }
        }
        GUI.EndScrollView();
        PastoralMenuUI.Label(new Rect(265,625,900,48),"收起面板不会取消待决。已提交的费用与选择随存档保存；只能使用规则提供的放弃按钮取消。",14,PastoralMenuUI.Muted);
    }
    static string ThreeKingdomsRevealedCards(ThreeKingdomsView view)
    {
        var cards=view.RevealedSkillCards;
        return cards==null||cards.All(n=>n==0)?"":"公开亮牌："+string.Join("、",Enumerable.Range(0,Math.Min(8,cards.Length)).Where(i=>cards[i]>0).Select(i=>ThreeKingdomsCardNames[i]+" × "+cards[i]));
    }
    string ThreeKingdomsTileName(string id)
    {
        var tiles=presentation is M4App cities?cities.M6View.Board.Tiles:presentation is M5App combined?combined.M6View.Board.Tiles:null;
        var tile=tiles?.FirstOrDefault(t=>t.Id==id);if(tile==null)return id;
        string terrain=tile.Resource=="wood"?"森林":tile.Resource=="brick"?"丘陵":tile.Resource=="wool"?"牧场":tile.Resource=="wheat"?"麦田":tile.Resource=="ore"?"山地":tile.Resource=="gold"?"金矿":tile.Resource;
        return terrain+tile.Number+"（"+tile.Id+"）";
    }
    void DrawGeneralSkillCost()
    {
        var draft=threeKingdomsSkillDraft;bool gift=draft.Option=="gift";
        int[] hand;
        if(presentation is M4App cities){var r=cities.M6View.OwnResources;var c=cities.M6View.OwnCommodities;hand=new[]{r.Wood,r.Brick,r.Wool,r.Wheat,r.Ore,c.Cloth,c.Coin,c.Paper};}
        else if(presentation is M5App combined){var r=combined.M6View.OwnResources;var c=combined.M6View.OwnCommodities;hand=new[]{r.Wood,r.Brick,r.Wool,r.Wheat,r.Ore,c.Cloth,c.Coin,c.Paper};}
        else return;
        var limits=B4CardInteraction.PaymentLimits(ThreeKingdomsProjection.SkillActions,draft,hand);
        int total=draft.Counts.Sum(),limit=gift?hand.Sum():4;
        string title=gift?"仁德：赠给 "+PlayerDisplayName(draft.TargetPlayerIds[0]):"制衡：选择一批 1–4 张手牌";
        PastoralMenuUI.Label(new Rect(265,337,910,48),title+"。已选 "+total+" 张，确认后一次提交。",17,null,true);
        for(int i=0;i<8;i++)
        {
            float x=265+(i%4)*228,y=389+(i/4)*112;var face=new Rect(x+86,y,56,83);
            bool old=GUI.enabled;GUI.enabled=old&&limits[i]>0&&!TableDiceRolling&&!TablePresentationBlocked;
            var color=GUI.color;if(limits[i]==0)GUI.color=new Color(color.r,color.g,color.b,.35f);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(i));GUI.color=color;
            int delta=TabletopCardInput.Delta(face);if(delta!=0)B4CardInteraction.Change(draft.Counts,i,delta,limits,limit);GUI.enabled=old;
            PastoralMenuUI.Label(new Rect(x,y+84,214,25),ThreeKingdomsCardNames[i]+" "+draft.Counts[i]+" / "+limits[i]+(limits[i]==0?" · 不可选":""),14,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
        }
        bool valid=B4CardInteraction.CanSubmit(draft.Counts,limits,limit);
        if(PastoralMenuUI.Button(new Rect(265,625,260,46),"返回，不提交"))threeKingdomsSkillDraft=null;
        if(PastoralMenuUI.Button(new Rect(835,625,340,46),gift?"确认批量赠予":"确认批量弃牌",valid&&!TableDiceRolling&&!TablePresentationBlocked,true)){SubmitGeneralSkill(draft);threeKingdomsSkillDraft=null;}
    }
    static B2SkillCommand CopyGeneralSkill(B2SkillCommand source)=>new B2SkillCommand{SkillId=source.SkillId,Option=source.Option,TargetPlayerIds=source.TargetPlayerIds?.ToArray(),Counts=source.Counts?.ToArray(),TileIds=source.TileIds?.ToArray(),ProgressCardId=source.ProgressCardId,DecisionId=source.DecisionId,WindowId=source.WindowId,CardType=source.CardType,EffectiveCardType=source.EffectiveCardType,SecondaryCounts=source.SecondaryCounts?.ToArray(),ProgressCardIds=source.ProgressCardIds?.ToArray()};
    void DrawScheduledRetirement()
    {
        var phases=presentation is M4App cities?cities.M6View.LegalActions.Where(c=>c.RetireAtPhase.HasValue).Select(c=>c.RetireAtPhase.Value).ToArray():
            presentation is M5App combined?combined.M6View.LegalActions.Where(c=>c.RetireAtPhase.HasValue).Select(c=>c.RetireAtPhase.Value).ToArray():Array.Empty<ThreeKingdomsPhase>();
        if(phases.Length==0)return;
        threeKingdomsRetirementPhase%=phases.Length;
        if(PastoralMenuUI.Button(new Rect(522,625,210,46),"下次"+ThreeKingdomsPhaseName(phases[threeKingdomsRetirementPhase])))threeKingdomsRetirementPhase=(threeKingdomsRetirementPhase+1)%phases.Length;
        if(PastoralMenuUI.Button(new Rect(744,625,205,46),"预约边界隐退",!TableDiceRolling,true))SubmitRetirementIntent(phases[threeKingdomsRetirementPhase],false);
        bool cancel=presentation is M4App city?city.M6View.LegalActions.Any(c=>c.CancelRetirementIntent):presentation is M5App combo&&combo.M6View.LegalActions.Any(c=>c.CancelRetirementIntent);
        if(PastoralMenuUI.Button(new Rect(961,625,214,46),"取消隐退预约",cancel))SubmitRetirementIntent(null,true);
    }
    void SubmitRetirementIntent(ThreeKingdomsPhase? phase,bool cancel)
    {
        string id=Guid.NewGuid().ToString("N");
        if(presentation is M4App cities)cities.Submit(new Catan.Core.M4.Command{Id=id,PlayerId=HumanSeat,Kind=Catan.Core.M4.CommandKind.RetireGeneral,RetireAtPhase=phase,CancelRetirementIntent=cancel,WindowId=ThreeKingdomsProjection.WindowId});
        else if(presentation is M5App combined)combined.Submit(new Catan.Core.M5.Command{Id=id,PlayerId=HumanSeat,Kind=Catan.Core.M5.CommandKind.RetireGeneral,RetireAtPhase=phase,CancelRetirementIntent=cancel,WindowId=ThreeKingdomsProjection.WindowId});
    }
    void SubmitGeneralSkill(B2SkillCommand source)
    {
        if(source==null)return;
        var skill=CopyGeneralSkill(source);
        string id=Guid.NewGuid().ToString("N");
        if(presentation is M4App cities)cities.Submit(new Catan.Core.M4.Command{Id=id,PlayerId=HumanSeat,Kind=Catan.Core.M4.CommandKind.UseGeneralSkill,SkillCommand=skill,DecisionId=skill.DecisionId,WindowId=ThreeKingdomsProjection.WindowId});
        else if(presentation is M5App combined)combined.Submit(new Catan.Core.M5.Command{Id=id,PlayerId=HumanSeat,Kind=Catan.Core.M5.CommandKind.UseGeneralSkill,SkillCommand=skill,DecisionId=skill.DecisionId,WindowId=ThreeKingdomsProjection.WindowId});
    }
    void SubmitThreeKingdoms(string kind,string[] ids=null)
    {
        var view=ThreeKingdomsProjection;if(view==null)return;
        string decision=view.PendingDecision?.PlayerId==HumanSeat?view.PendingDecision.DecisionId:null;
        if(presentation is M4App cities)cities.Submit(new Catan.Core.M4.Command{Id=Guid.NewGuid().ToString("N"),PlayerId=HumanSeat,Kind=(Catan.Core.M4.CommandKind)Enum.Parse(typeof(Catan.Core.M4.CommandKind),kind),GeneralIds=ids??Array.Empty<string>(),DecisionId=decision,WindowId=view.WindowId});
        else if(presentation is M5App combined)combined.Submit(new Catan.Core.M5.Command{Id=Guid.NewGuid().ToString("N"),PlayerId=HumanSeat,Kind=(Catan.Core.M5.CommandKind)Enum.Parse(typeof(Catan.Core.M5.CommandKind),kind),GeneralIds=ids??Array.Empty<string>(),DecisionId=decision,WindowId=view.WindowId});
        if(kind=="AdvanceThreeKingdomsPhase")threeKingdomsPanelOpen=false;
    }
}
