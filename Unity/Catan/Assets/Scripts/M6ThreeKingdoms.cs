using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

// 只读取人类所属席位的投影。候选、暗置、未公开拟登场不从权威存档取值。
public sealed partial class M6App
{
    const string ThreeKingdomsLabel="三国争霸 B1 开发骨架 · 武将技能未启用";
    static readonly Rect ThreeKingdomsBar=new Rect(402,103,760,70);
    static readonly Rect ThreeKingdomsPanel=new Rect(235,185,970,520);
    readonly HashSet<string> selectedGenerals=new HashSet<string>();
    string threeKingdomsDecision;
    bool threeKingdomsPanelOpen;
    ThreeKingdomsView ThreeKingdomsProjection=>presentation is M4App cities?cities.M6View?.ThreeKingdoms:
        presentation is M5App combined?combined.M6View?.ThreeKingdoms:null;
    bool ThreeKingdomsAdvanceAvailable=>presentation is M4App cities?
        cities.M6View?.LegalActions.Any(c=>c.Kind==Catan.Core.M4.CommandKind.AdvanceThreeKingdomsPhase)==true:
        presentation is M5App combined&&combined.M6View?.LegalActions.Any(c=>c.Kind==Catan.Core.M5.CommandKind.AdvanceThreeKingdomsPhase)==true;
    void ResetThreeKingdomsPanel(){selectedGenerals.Clear();threeKingdomsDecision=null;threeKingdomsPanelOpen=false;}
    internal bool ThreeKingdomsBlocksInput(Vector2 point)=>ThreeKingdomsProjection!=null&&
        (threeKingdomsPanelOpen||ThreeKingdomsBar.Contains(point));
    internal bool ThreeKingdomsPanelBlocksTable=>ThreeKingdomsProjection!=null&&threeKingdomsPanelOpen;
    bool TryCloseThreeKingdomsPanel(){if(!ThreeKingdomsPanelBlocksTable)return false;threeKingdomsPanelOpen=false;return true;}
    internal void VerificationOpenThreeKingdomsPanel()=>threeKingdomsPanelOpen=true;
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
        var pending=view.PendingDecision;
        bool ownChoice=pending!=null&&pending.PlayerId==HumanSeat;
        if(ownChoice&&threeKingdomsDecision!=pending.DecisionId)
        {
            threeKingdomsDecision=pending.DecisionId;selectedGenerals.Clear();threeKingdomsPanelOpen=true;
        }
        else if(!ownChoice&&threeKingdomsDecision!=null)
        {threeKingdomsDecision=null;selectedGenerals.Clear();threeKingdomsPanelOpen=false;}
        PastoralMenuUI.Styles(font);
        PastoralMenuUI.Round(ThreeKingdomsBar,new Color(.12f,.18f,.14f,.96f),6);
        PastoralMenuUI.Label(new Rect(415,110,738,22),ThreeKingdomsLabel,14,PastoralMenuUI.Ivory,true);
        string state="第 "+view.Clock.Round+" 轮 · "+ThreeKingdomsPhaseName(view.Clock.Phase);
        if(pending!=null)state+=" · "+(ownChoice?"请完成私密选择":PlayerDisplayName(pending.PlayerId)+" 正在选将");
        PastoralMenuUI.Label(new Rect(415,139,398,24),state,14,PastoralMenuUI.Ivory);
        if(PastoralMenuUI.Button(new Rect(830,131,150,33),ownChoice?"继续私密选将":"武将与寿命"))threeKingdomsPanelOpen=!threeKingdomsPanelOpen;
        if(PastoralMenuUI.Button(new Rect(991,131,159,33),"推进当前阶段",ThreeKingdomsAdvanceAvailable&&IsHumanTurn))SubmitThreeKingdoms("AdvanceThreeKingdomsPhase");
        if(!threeKingdomsPanelOpen)return;
        PastoralMenuUI.Fill(new Rect(0,175,1440,725),new Color(.035f,.08f,.045f,.50f));
        PastoralMenuUI.Panel(ThreeKingdomsPanel);
        PastoralMenuUI.Label(new Rect(265,211,860,32),ownChoice?ThreeKingdomsDecisionCaption(pending):"武将与寿命",25,null,true);
        if(PastoralMenuUI.Button(new Rect(1127,210,48,34),"收起"))threeKingdomsPanelOpen=false;
        PastoralMenuUI.Label(new Rect(265,252,910,28),ThreeKingdomsLabel+" · 仅本地 · 每轮末当前登场将寿命减 1",14,PastoralMenuUI.Muted);
        if(ownChoice)DrawGeneralChoices(view,pending);
        else DrawGeneralLibrary(view);
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
            if(PastoralMenuUI.Card(area,general.Name,general.Faction+" · "+general.Title+"\n\n"+string.Join(" / ",general.Skills.Select(s=>s.Name))+"\n技能未启用",chosen))
            {
                if(chosen)selectedGenerals.Remove(ids[i]);
                else if(selectedGenerals.Count<decision.RequiredCount)selectedGenerals.Add(ids[i]);
                else if(decision.RequiredCount==1){selectedGenerals.Clear();selectedGenerals.Add(ids[i]);}
            }
        }
        PastoralMenuUI.Label(new Rect(265,555,910,42),"已选 "+selectedGenerals.Count+" / "+decision.RequiredCount+" 张。候选、拟登场与暗置仅你可见；收起面板不会取消待决。",15,PastoralMenuUI.Muted);
        bool ready=selectedGenerals.Count==decision.RequiredCount&&IsHumanTurn;
        if(PastoralMenuUI.Button(new Rect(835,625,340,46),"确认选择",ready,true))
        {
            bool select=decision.Kind==ThreeKingdomsDecisionKind.SelectOpening||decision.Kind==ThreeKingdomsDecisionKind.SelectReplacement;
            SubmitThreeKingdoms(select?"ChooseGenerals":"DeployGeneral",selectedGenerals.OrderBy(x=>x,StringComparer.Ordinal).ToArray());
        }
    }
    void DrawGeneralLibrary(ThreeKingdomsView view)
    {
        int row=0;
        foreach(var player in view.Players)
        {
            float y=296+row++*61;
            PastoralMenuUI.RoomRow(new Rect(265,y,910,52));
            string publicState=string.IsNullOrEmpty(player.ActiveGeneralId)?player.SelectionComplete?"选择完成，等待统一公开":"尚未完成选将":
                GeneralName(player.ActiveGeneralId)+" · 剩余 "+player.RemainingLife+" 轮"+(player.FaceDown?" · 背面":"");
            PastoralMenuUI.Label(new Rect(280,y+12,570,30),PlayerDisplayName(player.PlayerId)+"  ·  "+publicState,17,null,true);
            string hidden=player.PlayerId==HumanSeat?"你的暗置："+string.Join("、",player.HiddenGeneralIds.Select(GeneralName)):"暗置 "+player.HiddenCount+" 张";
            PastoralMenuUI.Label(new Rect(848,y+13,315,30),hidden,14,PastoralMenuUI.Muted);
        }
        string pending=view.PendingDecision==null?"暗置将不会老化。主动隐退后须完成补将与登场，合法窗口内可继续隐退。":
            "等待 "+PlayerDisplayName(view.PendingDecision.PlayerId)+" 完成武将选择。";
        PastoralMenuUI.Label(new Rect(265,555,910,48),pending,15,PastoralMenuUI.Muted);
        if(PastoralMenuUI.Button(new Rect(265,625,350,46),"主动隐退当前将",view.CanRetire&&!TableDiceRolling,true))SubmitThreeKingdoms("RetireGeneral");
        if(PastoralMenuUI.Button(new Rect(835,625,340,46),"完成"+ThreeKingdomsPhaseName(view.Clock.Phase),ThreeKingdomsAdvanceAvailable&&IsHumanTurn,true))SubmitThreeKingdoms("AdvanceThreeKingdomsPhase");
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
