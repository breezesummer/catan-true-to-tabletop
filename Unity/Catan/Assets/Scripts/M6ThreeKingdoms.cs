using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

// 所有候选仅来自本席人类投影。桌面选中只是草稿，确认才进入共同权威入口。
public sealed partial class M6App
{
    string GeneralAssetSeatB6=>presentation is M4App cities?cities.M6View?.PlayerId??HumanSeat:presentation is M5App combined?combined.M6View?.PlayerId??HumanSeat:HumanSeat;
    static readonly Color SkillMuted=new Color(.73f,.75f,.65f);
    static readonly Rect GeneralArea=new Rect(22,714,148,171);
    static readonly Rect SkillsArea=new Rect(180,714,146,171);
    static readonly Rect SkillBar=new Rect(342,650,932,58);
    static readonly Rect CandidateArea=new Rect(342,474,932,168);
    static readonly Rect LibraryArea=new Rect(22,416,304,288);
    static readonly string[] ThreeKingdomsCardNames={"木材","砖","羊毛","谷物","矿石","布匹","钱币","纸张"};
    readonly HashSet<string> selectedGenerals=new HashSet<string>();
    readonly B45SelectionContext generalSelection=new B45SelectionContext();
    readonly Dictionary<string,string> selectedProgressFaces=new Dictionary<string,string>();
    readonly OperationHandSelection generalHand=new OperationHandSelection();
    string threeKingdomsDecision,inspectedGeneral,inspectionText,inspectionScope;
    Rect inspectionAnchor;
    bool generalLibraryOpen,generalSubmitting,retireDraft;
    ThreeKingdomsView selectionView;
    Vector2 generalScroll,candidateScroll,skillScroll,inspectionScroll;
    int threeKingdomsRetirementPhase;
    double generalClickBlockedUntil;
    string observedSkillEvent,flashingSkill,latestSkillResult;
    double skillFlashUntil;
    bool observedSkillsInitialized;
    ThreeKingdomsView ThreeKingdomsProjection=>presentation is M4App cities?cities.M6View?.ThreeKingdoms:
        presentation is M5App combined?combined.M6View?.ThreeKingdoms:null;
    bool ThreeKingdomsAdvanceAvailable=>presentation is M4App cities?
        cities.M6View?.LegalActions.Any(c=>c.Kind==Catan.Core.M4.CommandKind.AdvanceThreeKingdomsPhase)==true:
        presentation is M5App combined&&combined.M6View?.LegalActions.Any(c=>c.Kind==Catan.Core.M5.CommandKind.AdvanceThreeKingdomsPhase)==true;
    internal bool ThreeKingdomsSkillSelectionActive=>generalSelection.Active||retireDraft||ThreeKingdomsProjection?.PendingDecision?.PlayerId==GeneralAssetSeatB6;
    bool NativeGeneralPaymentActive=>presentation is M4App a?a.ThreeKingdomsPaymentActive:presentation is M5App b&&b.ThreeKingdomsPaymentActive;
    internal bool ThreeKingdomsSelectionActive=>ThreeKingdomsSkillSelectionActive||NativeGeneralPaymentActive;
    internal bool ThreeKingdomsPanelBlocksTable=>false;
    bool GeneralCanInteract=>!TableDiceRolling&&!TablePresentationBlocked&&(!NetworkGame||Network.CanInteract);
    bool GeneralCanSubmit=>GeneralCanInteract&&Time.unscaledTimeAsDouble>=generalClickBlockedUntil;
    internal string ThreeKingdomsPhaseCaption=>ThreeKingdomsProjection==null?null:"第 "+ThreeKingdomsProjection.Clock.Round+" 轮 · "+ThreeKingdomsPhaseName(ThreeKingdomsProjection.Clock.Phase)+(GeneralAssetSeatB6!=HumanSeat?" · 代操 "+GeneralAssetSeatB6:"");
    bool CandidateVisible=>ThreeKingdomsProjection?.PendingDecision?.PlayerId==GeneralAssetSeatB6||generalSelection.Active&&(generalSelection.Options.Length>1||generalSelection.PrivateOptions.Length>0||generalSelection.RequiresBankCards||generalSelection.DiceIndices.Length>0);
    bool BottomGeneralCandidatesVisible=>CandidateVisible&&generalSelection.DiceIndices.Length==0&&!(generalSelection.Contract?.DirectBatch==true&&(generalSelection.Contract.DecisionKind=="b4-reveal"||generalSelection.Contract.DecisionKind=="b4-subset"));
    bool NativeGeneralPaymentContains(Vector2 p)=>presentation is M4App a?a.ThreeKingdomsPaymentContains(p):presentation is M5App b&&b.ThreeKingdomsPaymentContains(p);
    internal bool ThreeKingdomsBlocksInput(Vector2 p)=>ThreeKingdomsProjection!=null&&(GeneralArea.Contains(p)||SkillsArea.Contains(p)||SkillBar.Contains(p)||BottomGeneralCandidatesVisible&&CandidateArea.Contains(p)||SpecialGeneralCandidatesContain(p)||NativeGeneralPaymentContains(p)||generalLibraryOpen&&LibraryArea.Contains(p)||inspectionText!=null&&InspectionRect.Contains(p));
    Rect InspectionRect=>new Rect(Mathf.Clamp(inspectionAnchor.x,18,1022),Mathf.Clamp(inspectionAnchor.y-276,212,414),400,268);
    void ResetThreeKingdomsPanel()
    {
        ThreeKingdomsDirectInput.Reset();
        if(presentation is M4App paymentCities)paymentCities.CancelThreeKingdomsPayment();else if(presentation is M5App paymentCombined)paymentCombined.CancelThreeKingdomsPayment();
        selectedGenerals.Clear();generalSelection.Reset();selectedProgressFaces.Clear();selectionView=null;threeKingdomsDecision=null;
        generalLibraryOpen=false;inspectionText=null;generalSubmitting=false;generalClickBlockedUntil=0;retireDraft=false;generalScroll=candidateScroll=skillScroll=Vector2.zero;
        observedSkillEvent=flashingSkill=latestSkillResult=null;skillFlashUntil=0;observedSkillsInitialized=false;
    }
    bool TryCloseThreeKingdomsPanel()
    {
        if(inspectionText!=null){inspectionText=null;return true;}
        if(generalLibraryOpen){generalLibraryOpen=false;return true;}
        if(NativeGeneralPaymentActive)return presentation is M4App paymentCities?paymentCities.ReselectOrCancelThreeKingdomsPayment():presentation is M5App paymentCombined&&paymentCombined.ReselectOrCancelThreeKingdomsPayment();
        if(retireDraft){retireDraft=false;return true;}
        if(generalSelection.Active&&!generalSelection.IsSubmitting)
        {
            if(generalSelection.CanCancel){generalSelection.Reset();selectedProgressFaces.Clear();return true;}
            if(generalSelection.SelectedCounts.Sum()+generalSelection.SelectedBankCounts.Sum()+generalSelection.SelectedPrivateCounts.Sum()+generalSelection.SelectedPlayers.Length+generalSelection.SelectedTiles.Length+generalSelection.SelectedProgress.Length>0)
            {generalSelection.Reselect();selectedProgressFaces.Clear();return true;}
        }
        if(selectedGenerals.Count>0){selectedGenerals.Clear();return true;}
        return false;
    }
    internal void VerificationOpenThreeKingdomsPanel(){generalLibraryOpen=true;SyncThreeKingdomsSelection();}
    internal void VerificationChooseGeneralSkill(int index)
    {
        var actions=ThreeKingdomsProjection?.SkillActions;
        if(actions==null||index<0||index>=actions.Length)throw new InvalidOperationException("当前没有属于人类席位的技能选项。");
        SubmitGeneralSkill(actions[index].Command);
    }
    internal void RestartLocalGame()
    {
        if(NetworkGame||!Ready)return;
        var names=localPlayerNames.ToArray();
        StartGame(RuleSet,ScenarioId,PlayerCount,HumanSeat,Paused,targetVictoryPoints:TargetVictoryPoints,aiSpeedLevel:AiSpeedLevel,threeKingdoms:ThreeKingdoms);
        localPlayerNames=names;Notice="已保留玩法、三国开关、席位、姓名、胜利目标与 AI 节奏，重新生成地图和行动顺序。";
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
    int[] ThreeKingdomsHand()
    {
        if(presentation is M4App a){var r=a.M6View.OwnResources;var c=a.M6View.OwnCommodities;return new[]{r.Wood,r.Brick,r.Wool,r.Wheat,r.Ore,c.Cloth,c.Coin,c.Paper};}
        if(presentation is M5App b){var r=b.M6View.OwnResources;var c=b.M6View.OwnCommodities;return new[]{r.Wood,r.Brick,r.Wool,r.Wheat,r.Ore,c.Cloth,c.Coin,c.Paper};}
        return new int[8];
    }
    internal void SyncThreeKingdomsSelection()
    {
        var view=ThreeKingdomsProjection;
        if(view==null){if(selectionView!=null)ResetThreeKingdomsPanel();return;}
        if(selectionView==view)return;
        selectionView=view;
        var latest=view.SkillHistory?.LastOrDefault(h=>h.PlayerId==GeneralAssetSeatB6);
        string eventKey=latest==null?null:latest.EventId+":"+latest.Complete;
        if(eventKey!=observedSkillEvent)
        {
            if(observedSkillsInitialized&&latest!=null){flashingSkill=latest.SkillId;latestSkillResult=latest.Result;skillFlashUntil=Time.unscaledTimeAsDouble+1.6;}
            observedSkillEvent=eventKey;
        }
        observedSkillsInitialized=true;
        var pending=view.PendingDecision?.PlayerId==GeneralAssetSeatB6?view.PendingDecision:null;
        string token=pending?.DecisionId;
        if(token!=threeKingdomsDecision){threeKingdomsDecision=token;selectedGenerals.Clear();generalSubmitting=false;candidateScroll=Vector2.zero;}
        generalSelection.Refresh(view,ThreeKingdomsHand());
        generalSelection.RevalidateProgressCopies(presentation is M4App pc?pc.M6View.OwnProgressCards.Select(c=>c.ToString()).ToArray():presentation is M5App pp?pp.M6View.OwnProgressCards.Select(c=>c.ToString()).ToArray():Array.Empty<string>());
        if(!generalSelection.Active)selectedProgressFaces.Clear();
        if(view.SkillDecision?.PlayerId==GeneralAssetSeatB6&&!generalSelection.Active)
            generalSelection.Begin(view,GeneralAssetSeatB6,null,ThreeKingdomsHand());
        if(!view.CanRetire)retireDraft=false;
        string scope=GeneralAssetSeatB6+"|"+view.WindowId+"|"+view.SkillDecision?.DecisionId+"|"+token;
        if(scope!=inspectionScope){inspectionText=null;inspectionScope=scope;ThreeKingdomsDirectInput.Reset();}
    }
    internal OperationHandSelection ThreeKingdomsHandSelection
    {
        get
        {
            generalHand.Active=ThreeKingdomsSelectionActive;
            generalHand.CanSelectGood=(t,n)=>GeneralCanInteract&&generalSelection.CanSelectHand(t,n);
            generalHand.IsGoodSelected=generalSelection.IsHandSelected;
            generalHand.SelectGood=(t,n)=>generalSelection.ToggleHand(t,n);
            generalHand.CanSelectCard=id=>GeneralCanInteract&&generalSelection.CanSelectProgress(ProgressKind(id),ProgressOrdinal(id));
            generalHand.IsCardSelected=id=>generalSelection.IsProgressSelected(ProgressKind(id),ProgressOrdinal(id));
            generalHand.SelectCard=id=>generalSelection.ToggleProgress(ProgressKind(id),ProgressOrdinal(id));
            generalHand.SelectedGoods=()=>generalSelection.SelectedCounts.Sum();generalHand.SelectedCards=()=>generalSelection.SelectedProgress.Length;
            generalHand.Reason=generalSelection.Active?"按操作条选牌；其他牌可右击查看。":"请先完成当前选择。";return generalHand;
        }
    }
    static int ProgressOrdinal(string id)=>int.TryParse((id??"0:0").Split(':').Last(),out var n)?n:0;
    static string ProgressKind(string displayId)=>int.TryParse((displayId??"").Split(':')[0],out var kind)?((Catan.Core.M4.ProgressCardKind)kind).ToString():null;
    internal void InspectThreeKingdomsGood(int type,Rect anchor){if(type>=0&&type<8)Inspect(ThreeKingdomsCardNames[type]+"\n"+(generalSelection.Active?generalSelection.Description:"真实手牌。"),anchor);}
    internal void InspectThreeKingdomsCard(string id,Rect anchor){var kind=ProgressKind(id);if(kind!=null)Inspect(Catan.Core.DevelopmentCardText.Name(kind)+"\n"+(generalSelection.Active?generalSelection.Description:"可用时点击牌面进入用途选择。"),anchor);}
    void Inspect(string text,Rect anchor){inspectionText=text;inspectionAnchor=anchor;inspectionScroll=Vector2.zero;}
    void BeginGeneralSkill(string skill)
    {
        if(!GeneralCanInteract)return;
        if(presentation is M4App a)a.CancelForGeneralSelection();else if(presentation is M5App b)b.CancelForGeneralSelection();
        retireDraft=false;generalSelection.Begin(ThreeKingdomsProjection,GeneralAssetSeatB6,skill,ThreeKingdomsHand());selectedProgressFaces.Clear();candidateScroll=Vector2.zero;
    }
    void DrawThreeKingdoms()
    {
        SyncThreeKingdomsSelection();var view=ThreeKingdomsProjection;if(view==null)return;
        PastoralMenuUI.Styles(font);
        DrawGeneralDock(view);DrawGeneralSkills(view);if(!NativeGeneralPaymentActive)DrawGeneralOperationBar(view);
        if(view.PendingDecision?.PlayerId==GeneralAssetSeatB6)DrawGeneralCandidates(view.PendingDecision);
        else if(CandidateVisible)DrawSkillCandidates();
        DrawGeneralBoardTargets();
        if(generalLibraryOpen)DrawGeneralLibrary(view);
        if(inspectionText!=null)DrawGeneralInspection();
    }
    void DrawGeneralDock(ThreeKingdomsView view)
    {
        PastoralMenuUI.Panel(GeneralArea);var own=view.Players.FirstOrDefault(p=>p.PlayerId==GeneralAssetSeatB6);
        var general=string.IsNullOrEmpty(own?.ActiveGeneralId)?null:GeneralCatalog.Get(own.ActiveGeneralId);
        PastoralMenuUI.Label(new Rect(30,722,132,29),general?.Name??"武将占位",23,null,true,TextAnchor.MiddleCenter);
        PastoralMenuUI.Label(new Rect(30,754,132,22),general==null?"等待统一登场":general.Faction+" · 寿命 "+own.RemainingLife+" 轮",14,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
        PastoralMenuUI.Label(new Rect(30,778,132,22),own?.FaceDown==true?"背面朝上":"正面朝上",13,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
        if(PastoralMenuUI.Button(new Rect(30,809,132,30),"暗置 / 武将库"))generalLibraryOpen=!generalLibraryOpen;
        if(PastoralMenuUI.Button(new Rect(30,847,132,30),"提前隐退",view.CanRetire&&GeneralCanInteract&&!generalSelection.Active))retireDraft=!retireDraft;
    }
    void DrawGeneralSkills(ThreeKingdomsView view)
    {
        var own=view.Players.FirstOrDefault(p=>p.PlayerId==GeneralAssetSeatB6);
        var skills=B45SkillCatalog.AvailableEntries(view,GeneralAssetSeatB6);
        CopperTabletopSkin.Panel(SkillsArea,ModernOperationUI.Background);
        skillScroll=GUI.BeginScrollView(SkillsArea,skillScroll,new Rect(0,0,128,Mathf.Max(171,skills.Length*55)));
        for(int i=0;i<skills.Length;i++)
        {
            var skill=skills[i];var info=skill;bool active=info?.HasActiveEntry==true;
            bool enabled=active&&GeneralCanInteract&&view.SkillDecision==null&&view.PendingDecision==null&&view.SkillActions.Any(a=>a.Command.SkillId==skill.SkillId||skill.SkillId==B45SkillCatalog.RepairSkillId&&a.Command.Option==B45SkillCatalog.RepairSkillId);
            var runtime=view.SkillRuntimeStatus?.FirstOrDefault(s=>s.SkillId==skill.SkillId);
            var area=new Rect(3,i*55+3,125,51);bool selected=generalSelection.SkillId==skill.SkillId;
            bool flashing=flashingSkill==skill.SkillId&&Time.unscaledTimeAsDouble<skillFlashUntil;
            CopperTabletopSkin.Panel(area,ModernOperationUI.Surface,selected||flashing,false);
            PastoralMenuUI.Label(new Rect(area.x+4,area.y+1,117,25),skill.Name,17,enabled?PastoralMenuUI.Ivory:SkillMuted,true,TextAnchor.MiddleCenter);
            PastoralMenuUI.Label(new Rect(area.x+4,area.y+25,117,14),runtime?.Summary??info?.Limit??"依当前规则",10,SkillMuted,false,TextAnchor.MiddleCenter);
            PastoralMenuUI.Label(new Rect(area.x+4,area.y+39,117,12),(active?"主动":"被动")+(active&&!enabled?" · 暂不可用":""),9,SkillMuted,false,TextAnchor.MiddleCenter);
            int input=ThreeKingdomsDirectInput.Read(area,enabled);
            if(input==1)BeginGeneralSkill(skill.SkillId);
            if(input==2)Inspect((info?.Description??skill.Description)+"\n当前："+(runtime?.Summary??"")+"\n"+(enabled?"可选择牌与目标后确认。":runtime?.UnavailableReason??(active?"当前时机、费用或剩余次数不满足；可用时亮起。":"被动效果只由真实事件触发。")),new Rect(180,714+i*55,146,48));
        }
        GUI.EndScrollView();
    }
    void DrawGeneralOperationBar(ThreeKingdomsView view)
    {
        CopperTabletopSkin.Panel(SkillBar,ModernOperationUI.Background);
        var pending=view.PendingDecision?.PlayerId==GeneralAssetSeatB6?view.PendingDecision:null;
        if(pending!=null)
        {
            string caption=pending.Kind==ThreeKingdomsDecisionKind.SelectOpening?"私密选将":pending.Kind==ThreeKingdomsDecisionKind.SelectReplacement?"补充武将":"选择登场";
            PastoralMenuUI.Label(new Rect(354,655,622,22),caption+" · 已选 "+selectedGenerals.Count+" / "+pending.RequiredCount+" 张",17,PastoralMenuUI.Ivory,true);
            PastoralMenuUI.Label(new Rect(354,680,622,22),"候选只向你显示；全员提交后统一公开。",13,PastoralMenuUI.Muted);
            if(PastoralMenuUI.Button(new Rect(984,661,112,36),"重选",!generalSubmitting))selectedGenerals.Clear();
            if(PastoralMenuUI.Button(new Rect(1108,661,154,36),generalSubmitting?"等待确认":"确认选择",!generalSubmitting&&GeneralCanSubmit&&selectedGenerals.Count==pending.RequiredCount,true))
            {generalSubmitting=true;SubmitThreeKingdoms(pending.Kind==ThreeKingdomsDecisionKind.SelectOpening||pending.Kind==ThreeKingdomsDecisionKind.SelectReplacement?"ChooseGenerals":"DeployGeneral",selectedGenerals.OrderBy(x=>x,StringComparer.Ordinal).ToArray());}
            return;
        }
        if(retireDraft)
        {
            PastoralMenuUI.Label(new Rect(354,655,614,46),"提前隐退：当前将进入隐退库，随后补将。确认后不可撤销。",16,PastoralMenuUI.Ivory);
            if(PastoralMenuUI.Button(new Rect(984,661,112,36),"取消"))retireDraft=false;
            if(PastoralMenuUI.Button(new Rect(1108,661,154,36),"确认隐退",GeneralCanSubmit&&view.CanRetire,true)){retireDraft=false;SubmitThreeKingdoms("RetireGeneral");}return;
        }
        if(generalSelection.Active)
        {
            string name=generalSelection.Contract.IsResponse?view.SkillDecision?.Title??"事件响应":B45SkillCatalog.Get(generalSelection.SkillId)?.Name??"技能选择";
            string requirement=generalSelection.Contract.IsResponse?view.SkillDecision?.Description:B45SkillCatalog.Get(generalSelection.SkillId)?.Cost;
            if(generalSelection.SkillId=="tk-g08-s2"&&!generalSelection.Contract.IsResponse)requirement="点目标头像；确认进入私密选择即占用本次次数";
            string target=string.Join("、",generalSelection.SelectedPlayers.Select((p,i)=>(i+1)+"."+PlayerDisplayName(p)));
            PastoralMenuUI.Label(new Rect(354,653,612,25),name+" · "+requirement,15,PastoralMenuUI.Ivory,true);
            PastoralMenuUI.Label(new Rect(354,679,612,23),generalSelection.Summary+(target.Length>0?" · "+target:""),13,SkillMuted);
            bool skip=generalSelection.SkipCommand!=null;
            if(PastoralMenuUI.Button(new Rect(974,661,88,36),generalSelection.CanCancel?"取消":"重选",!generalSelection.IsSubmitting))
            {if(generalSelection.CanCancel)generalSelection.Reset();else generalSelection.Reselect();selectedProgressFaces.Clear();}
            if(skip&&PastoralMenuUI.Button(new Rect(1068,661,88,36),generalSelection.SkipCommand?.Option=="keep"?"保留骰子":generalSelection.SkipCommand?.Option=="luoshen-stop"?"停止重掷":"放弃本次",GeneralCanSubmit&&!generalSelection.IsSubmitting)){var c=generalSelection.SkipCommand;SubmitGeneralSkill(c);}
            if(PastoralMenuUI.Button(new Rect(skip?1162:1074,661,skip?100:188,36),generalSelection.IsSubmitting?"等待确认":"确认",GeneralCanSubmit&&generalSelection.CanConfirm,true))
            {var c=generalSelection.BeginSubmit();if(c!=null)SubmitGeneralSkill(c);}
            return;
        }
        string state=ThreeKingdomsPhaseCaption;
        if(view.SkillDecision!=null)state+=" · 等待 "+PlayerDisplayName(view.SkillDecision.PlayerId);
        else if(view.WaitingSelectionPlayerIds?.Length>0)state+=" · 等待 "+string.Join("、",view.WaitingSelectionPlayerIds.Select(PlayerDisplayName));
        bool ownFreePlay=view.Clock.Phase==ThreeKingdomsPhase.Play&&view.Clock.ActivePlayerId==GeneralAssetSeatB6;
        PastoralMenuUI.Label(ownFreePlay?new Rect(500,661,762,36):new Rect(354,661,700,36),state,16,PastoralMenuUI.Ivory);
        if(!GeneralCatalog.UsesB4Package(view.PackageVersion)&&ThreeKingdomsAdvanceAvailable&&PastoralMenuUI.Button(new Rect(1080,661,182,36),"推进当前阶段",IsHumanTurn&&GeneralCanSubmit))SubmitThreeKingdoms("AdvanceThreeKingdomsPhase");
        else if(view.Clock.Phase==ThreeKingdomsPhase.Play&&view.Clock.ActivePlayerId==GeneralAssetSeatB6&&PastoralMenuUI.Button(new Rect(354,661,132,36),"结束出牌",GeneralCanSubmit&&IsHumanTurn,true))EndThreeKingdomsPlay();
    }
    void EndThreeKingdomsPlay()
    {if(presentation is M4App a)a.EndThreeKingdomsTurn();else if(presentation is M5App b)b.EndThreeKingdomsTurn();}
    void DrawGeneralCandidates(ThreeKingdomsDecision pending)
    {
        PastoralMenuUI.Panel(CandidateArea);
        var ids=pending.CandidateIds;float width=Mathf.Min(210,900f/Mathf.Max(1,ids.Length));
        for(int i=0;i<ids.Length;i++)
        {
            var general=GeneralCatalog.Get(ids[i]);bool selected=selectedGenerals.Contains(ids[i]);var r=new Rect(352+i*width,486,width-8,143);
            CopperTabletopSkin.Panel(r,ModernOperationUI.Surface,selected);
            PastoralMenuUI.Label(new Rect(r.x+7,r.y+8,r.width-14,30),(selected?"✓ ":"")+general.Name,23,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            PastoralMenuUI.Label(new Rect(r.x+7,r.y+45,r.width-14,26),general.Faction+" · "+general.Title,13,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
            PastoralMenuUI.Label(new Rect(r.x+7,r.y+78,r.width-14,54),string.Join(" / ",general.Skills.Select(s=>s.Name)),16,PastoralMenuUI.Ivory,false,TextAnchor.MiddleCenter);
            int input=ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSubmitting);
            if(input==1){if(!selectedGenerals.Remove(ids[i])){if(pending.RequiredCount==1)selectedGenerals.Clear();if(selectedGenerals.Count<pending.RequiredCount)selectedGenerals.Add(ids[i]);}inspectedGeneral=ids[i];}
            if(input==2)Inspect(general.Name+"\n"+string.Join("\n",general.Skills.Select(s=>s.Name+"："+s.OriginalText)),r);
        }
    }
    static bool IsLocalCandidate(B2SkillAction a)=>B45SelectionContract.Players(a).Length==0&&B45SelectionContract.Board(a).Length==0&&B45SelectionContract.Cost(a).Sum()==0&&B45SelectionContract.Progress(a).Length==0;
    void DrawSkillCandidates()
    {
        if(DrawSpecialGeneralCandidates())return;
        if(generalSelection.RequiresBankCards){DrawGeneralBankCandidates();return;}
        var options=generalSelection.Options;
        // 牌、头像、棋盘目标不生成文字组合列表。这里只容纳分支、银行类型及私密授权候选。
        var local=options.Where(a=>IsLocalCandidate(a)||options.Length>1).ToArray();
        var privateCards=generalSelection.PrivateOptions.GroupBy(a=>a.Command.CardType).Select(g=>g.First()).ToArray();
        if(local.Length==0&&privateCards.Length==0)return;
        PastoralMenuUI.Panel(CandidateArea);
        PastoralMenuUI.Label(new Rect(354,479,900,22),privateCards.Length>0?"获授权查看的目标手牌 · 与你的可支付手牌分开":"当前用途 / 候选 · 选中后在操作条确认",14,PastoralMenuUI.Ink,true);
        int count=local.Length+privateCards.Length;float contentWidth=Mathf.Max(900,count*142);
        candidateScroll=GUI.BeginScrollView(new Rect(350,504,916,132),candidateScroll,new Rect(0,0,contentWidth,110));
        for(int i=0;i<count;i++)
        {
            bool privateCard=i>=local.Length;var action=privateCard?privateCards[i-local.Length]:local[i];var r=new Rect(i*142,2,134,103);
            bool selected=privateCard?generalSelection.SelectedPrivateCounts[action.Command.CardType]>0:generalSelection.IsOptionSelected(action);
            CopperTabletopSkin.Panel(r,ModernOperationUI.Surface,selected);
            int type=privateCard?action.Command.CardType:action.ResultCardType??(action.Command.Option=="choose-card"||action.Command.Option=="b4-guess"||action.Command.Option=="claim"?action.Command.CardType:-1);
            if(type>=0&&type<8)EntityCardAssets.DrawCardFace(new Rect(r.x+8,r.y+5,40,60),EntityCardAssets.ResourceOrCommodityFront(type));
            else if(Enum.TryParse(action.VirtualCardId,out Catan.Core.M4.ProgressCardKind kind))EntityCardAssets.DrawCardFace(new Rect(r.x+8,r.y+5,40,60),EntityCardAssets.ProgressFront((int)kind));
            string caption=privateCard?ThreeKingdomsCardNames[action.Command.CardType]:OptionCaption(action);
            PastoralMenuUI.Label(new Rect(r.x+6,r.y+65,r.width-12,36),(selected?"✓ ":"")+caption,12,PastoralMenuUI.Ivory,false,TextAnchor.MiddleCenter);
            if(type<0&&action.VirtualCardId==null)PastoralMenuUI.Label(new Rect(r.x+6,r.y+6,r.width-12,56),caption,14,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            int input=ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting);
            if(input==1){if(privateCard)generalSelection.TogglePrivateCard(action.Command.CardType);else generalSelection.ChooseOption(action);}
            if(input==2)Inspect(DisplayPlayerText(action.Label),new Rect(352+i*142-candidateScroll.x,504,134,103));
        }
        GUI.EndScrollView();
    }
    void DrawGeneralBankCandidates()
    {
        PastoralMenuUI.Panel(CandidateArea);
        PastoralMenuUI.Label(new Rect(354,479,900,23),"银行取牌 · 已选 "+generalSelection.SelectedBankCounts.Sum()+" / "+generalSelection.RequiredBankCount+" 张；与本人手牌分开",15,PastoralMenuUI.Muted,true);
        var bank=generalSelection.BankOptions.GroupBy(a=>a.Command.CardType).Select(g=>g.First()).ToArray();
        for(int i=0;i<bank.Length;i++)
        {
            var action=bank[i];int type=action.Command.CardType;float x=358+i*112;var face=new Rect(x+20,506,50,75);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(type));
            int input=ThreeKingdomsDirectInput.Read(face,GeneralCanInteract&&!generalSelection.IsSubmitting);
            if(input==1)generalSelection.ToggleBankCard(type);
            if(input==2)Inspect(ThreeKingdomsCardNames[type]+" · 银行库存 "+action.AvailableCount,face);
            int amount=generalSelection.SelectedBankCounts[type];
            PastoralMenuUI.Label(new Rect(x,583,100,23),ThreeKingdomsCardNames[type]+" × "+amount,14,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            if(PastoralMenuUI.Button(new Rect(x+24,608,48,25),"−",amount>0&&GeneralCanInteract&&!generalSelection.IsSubmitting))generalSelection.ToggleBankCard(type,-1);
        }
    }
    string OptionCaption(B2SkillAction a)
    {
        switch(a.Command.Option)
        {
            case "gift":return "赠予手牌 · 点牌和头像";
            case "exchange-hand":return "弃换 1–4 张手牌";
            case "exchange-progress":return "同色换发展牌";
            case "response-guose":return "两张砖 · 点目标头像";
            case "response-liuli":return "一张麦 · 点目标地块";
        }
        // 文字仅用于显示；选择条件全部取自结构化合同。
        return DisplayPlayerText(a.Label);
    }
    internal void DrawThreeKingdomsAvatar(string player,Rect area)
    {
        var view=ThreeKingdomsProjection;if(view==null)return;
        bool selectable=generalSelection.CanSelectPlayer(player)&&GeneralCanInteract;int ordinal=Array.IndexOf(generalSelection.SelectedPlayers,player);
        if(selectable||ordinal>=0){Outline(area,ordinal>=0?new Color(1,.81f,.28f):new Color(.48f,.89f,.62f),3);PastoralMenuUI.Label(new Rect(area.xMax-14,area.y-8,25,25),ordinal>=0?(ordinal+1).ToString():"+",16,PastoralMenuUI.Ivory,true);}
        if(generalSelection.Active)
        {
            int input=ThreeKingdomsDirectInput.Read(area,selectable);
            if(input==1)generalSelection.TogglePlayer(player);
            if(input==2){var p=view.Players.FirstOrDefault(x=>x.PlayerId==player);Inspect(PlayerDisplayName(player)+" · "+GeneralName(p?.ActiveGeneralId),area);}
        }
        if(view.SkillDecision?.PlayerId==player||view.WaitingSelectionPlayerIds?.Contains(player)==true)
            PastoralMenuUI.Label(new Rect(area.x-30,area.yMax+24,110,18),"等待选择",11,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
    }
    static void Outline(Rect r,Color c,float width)
    {TabletopUI.Fill(new Rect(r.x,r.y,r.width,width),c);TabletopUI.Fill(new Rect(r.x,r.yMax-width,r.width,width),c);TabletopUI.Fill(new Rect(r.x,r.y,width,r.height),c);TabletopUI.Fill(new Rect(r.xMax-width,r.y,width,r.height),c);}
    void DrawGeneralBoardTargets()
    {
        if(!generalSelection.Active)return;
        Func<string,Vector2> screenPoint=null;
        if(presentation is M4App a)screenPoint=a.VerificationBoard.ScreenPoint;else if(presentation is M5App b)screenPoint=b.VerificationBoard.ScreenPoint;
        if(screenPoint==null)return;
        var targets=generalSelection.CandidateActions.SelectMany(B45SelectionContract.Board).Distinct().OrderBy(id=>id,StringComparer.Ordinal)
            .Select(id=>new {Id=id,Point=MobileTabletop.Logical(screenPoint(id))}).ToArray();
        foreach(var target in targets)
        {
            var id=target.Id;var point=target.Point;if(point.y<211||point.y>644||point.x<332||point.x>1274)continue;
            if(BottomGeneralCandidatesVisible&&CandidateArea.Contains(point))continue;
            var nearby=targets.Where(t=>(t.Point-point).sqrMagnitude<1600).ToArray();int local=Array.FindIndex(nearby,t=>t.Id==id);
            // 确有重叠时在对象附近展开带编号的小标记，不把一次点击交给任意“最近对象”。
            float size=nearby.Length>1?28:40;
            if(nearby.Length>1){point=nearby.Aggregate(Vector2.zero,(sum,t)=>sum+t.Point)/nearby.Length;point.x+=(local-(nearby.Length-1)/2f)*32;}
            var area=new Rect(point.x-size/2,point.y-size/2,size,size);int number=Array.IndexOf(generalSelection.SelectedTiles,id);
            bool legal=GeneralCanInteract&&generalSelection.CanSelectTile(id);
            Outline(area,number>=0?new Color(1,.81f,.28f):new Color(.48f,.89f,.62f),2);
            if(number>=0||nearby.Length>1)PastoralMenuUI.Label(area,number>=0?"✓"+(number+1):(local+1).ToString(),15,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            int input=ThreeKingdomsDirectInput.Read(area,legal);
            if(input==1)generalSelection.ToggleTile(id);
            if(input==2)Inspect("棋盘对象 "+id+"\n"+generalSelection.Description,area);
        }
    }
    void DrawGeneralLibrary(ThreeKingdomsView view)
    {
        PastoralMenuUI.Panel(LibraryArea);
        PastoralMenuUI.Label(new Rect(32,423,245,26),"武将状态 · 公共库 "+view.PoolCount+" / 隐退 "+view.RetiredCount,14,null,true);
        if(PastoralMenuUI.Button(new Rect(281,422,36,26),"×"))generalLibraryOpen=false;
        var own=view.Players.FirstOrDefault(p=>p.PlayerId==GeneralAssetSeatB6);
        string text="你的暗置："+string.Join("、",own?.HiddenGeneralIds.Select(GeneralName)??Array.Empty<string>())+"\n\n";
        text+=string.Join("\n",view.Players.Select(p=>PlayerDisplayName(p.PlayerId)+"："+GeneralName(p.ActiveGeneralId)+" · "+p.RemainingLife+"轮"));
        if(view.PublicPoolGeneralIds!=null)text+="\n\n公共库："+string.Join("、",view.PublicPoolGeneralIds.Select(GeneralName));
        if(view.RetiredGeneralIds!=null)text+="\n\n隐退库："+string.Join("、",view.RetiredGeneralIds.Select(GeneralName));
        text+="\n\n"+string.Join("\n",view.SkillStatus??Array.Empty<string>());
        var statuses=view.BoardAssets?.Where(a=>a.Damaged||a.Kind=="Knight"&&a.Level!=a.EffectiveLevel).Select(a=>PlayerDisplayName(a.PlayerId)+"："+ThreeKingdomsBoardPresentation.Describe(a))??Array.Empty<string>();
        text+="\n\n"+string.Join("\n",statuses);
        if(view.ProgressHandLimit.HasValue)text+="\n你的发展牌上限："+view.ProgressHandLimit.Value+" 张";
        foreach(var hand in view.MilitaryHands??Array.Empty<B5RevealedHand>())
            text+="\n"+(hand.Public?"持续明牌 · ":"本次私密查看 · ")+PlayerDisplayName(hand.PlayerId)+"："+string.Join("、",Enumerable.Range(0,8).Where(i=>hand.Cards[i]>0).Select(i=>ThreeKingdomsCardNames[i]+" × "+hand.Cards[i]));
        text+="\n\n技能历史：\n"+string.Join("\n",(view.SkillHistory??Array.Empty<B4SkillHistoryView>()).Reverse().Take(30).Select(h=>PlayerDisplayName(h.PlayerId)+" · "+(B45SkillCatalog.Get(h.SkillId)?.Name??h.SkillId)+"："+(h.Complete?"":"处理中 · ")+h.Result));
        float height=Mathf.Max(153,new GUIStyle(PastoralMenuUI.Text){fontSize=14}.CalcHeight(new GUIContent(text),258)+8);
        generalScroll=GUI.BeginScrollView(new Rect(32,456,284,153),generalScroll,new Rect(0,0,258,height));
        PastoralMenuUI.Label(new Rect(0,0,258,height),text,14,PastoralMenuUI.Muted);GUI.EndScrollView();
        DrawScheduledRetirement();
    }
    void DrawScheduledRetirement()
    {
        var phases=presentation is M4App a?a.M6View.LegalActions.Where(c=>c.RetireAtPhase.HasValue).Select(c=>c.RetireAtPhase.Value).Distinct().ToArray():
            presentation is M5App b?b.M6View.LegalActions.Where(c=>c.RetireAtPhase.HasValue).Select(c=>c.RetireAtPhase.Value).Distinct().ToArray():Array.Empty<ThreeKingdomsPhase>();
        if(phases.Length>0)
        {threeKingdomsRetirementPhase%=phases.Length;if(PastoralMenuUI.Button(new Rect(32,617,166,33),"下次"+ThreeKingdomsPhaseName(phases[threeKingdomsRetirementPhase])))threeKingdomsRetirementPhase=(threeKingdomsRetirementPhase+1)%phases.Length;
        if(PastoralMenuUI.Button(new Rect(206,617,110,33),"预约隐退",GeneralCanSubmit))SubmitRetirementIntent(phases[threeKingdomsRetirementPhase],false);}
        bool cancel=presentation is M4App c?c.M6View.LegalActions.Any(x=>x.CancelRetirementIntent):presentation is M5App d&&d.M6View.LegalActions.Any(x=>x.CancelRetirementIntent);
        if(PastoralMenuUI.Button(new Rect(32,659,284,33),"取消隐退预约",cancel&&GeneralCanSubmit))SubmitRetirementIntent(null,true);
    }
    void DrawGeneralInspection()
    {
        var r=InspectionRect;PastoralMenuUI.Panel(r);
        if(PastoralMenuUI.Button(new Rect(r.xMax-37,r.y+5,30,27),"×")){inspectionText=null;return;}
        var style=new GUIStyle(GUI.skin.label){font=font,fontSize=14,wordWrap=true};style.normal.textColor=PastoralMenuUI.Ink;
        float h=style.CalcHeight(new GUIContent(inspectionText),r.width-42);
        inspectionScroll=GUI.BeginScrollView(new Rect(r.x+12,r.y+34,r.width-24,r.height-44),inspectionScroll,new Rect(0,0,r.width-42,Mathf.Max(r.height-44,h)));
        GUI.Label(new Rect(0,0,r.width-42,Mathf.Max(r.height-44,h)),inspectionText,style);GUI.EndScrollView();
    }
    static B2SkillCommand CopyGeneralSkill(B2SkillCommand source)=>B45SelectionContract.Copy(source);
    void SubmitRetirementIntent(ThreeKingdomsPhase? phase,bool cancel)
    {
        if(!GeneralCanSubmit)return;string id=Guid.NewGuid().ToString("N");
        if(presentation is M4App a)a.Submit(new Catan.Core.M4.Command{Id=id,PlayerId=GeneralAssetSeatB6,Kind=Catan.Core.M4.CommandKind.RetireGeneral,RetireAtPhase=phase,CancelRetirementIntent=cancel,WindowId=ThreeKingdomsProjection.WindowId});
        else if(presentation is M5App b)b.Submit(new Catan.Core.M5.Command{Id=id,PlayerId=GeneralAssetSeatB6,Kind=Catan.Core.M5.CommandKind.RetireGeneral,RetireAtPhase=phase,CancelRetirementIntent=cancel,WindowId=ThreeKingdomsProjection.WindowId});
    }
    void SubmitGeneralSkill(B2SkillCommand source)
    {
        if(source==null||!GeneralCanSubmit){generalSelection.AcknowledgeFailure();return;}
        var skill=CopyGeneralSkill(source);string id=Guid.NewGuid().ToString("N");bool accepted=false;
        if(presentation is M4App a){var result=a.Submit(new Catan.Core.M4.Command{Id=id,PlayerId=GeneralAssetSeatB6,Kind=Catan.Core.M4.CommandKind.UseGeneralSkill,SkillCommand=skill,DecisionId=skill.DecisionId,WindowId=skill.WindowId??ThreeKingdomsProjection.WindowId});accepted=result?.Success==true;}
        else if(presentation is M5App b){var result=b.Submit(new Catan.Core.M5.Command{Id=id,PlayerId=GeneralAssetSeatB6,Kind=Catan.Core.M5.CommandKind.UseGeneralSkill,SkillCommand=skill,DecisionId=skill.DecisionId,WindowId=skill.WindowId??ThreeKingdomsProjection.WindowId});accepted=result?.Success==true;}
        if(accepted||NetworkGame&&Network.CommandPending)generalClickBlockedUntil=Time.unscaledTimeAsDouble+.35;
        if(accepted&&!NetworkGame){generalSelection.Reset();selectionView=null;selectedProgressFaces.Clear();}
        else if(!NetworkGame||!Network.CommandPending)generalSelection.AcknowledgeFailure();
    }
    void SubmitThreeKingdoms(string kind,string[] ids=null)
    {
        var view=ThreeKingdomsProjection;if(view==null||!GeneralCanSubmit){generalSubmitting=false;return;}
        string decision=view.PendingDecision?.PlayerId==GeneralAssetSeatB6?view.PendingDecision.DecisionId:null;bool accepted=false;
        if(presentation is M4App a)accepted=a.Submit(new Catan.Core.M4.Command{Id=Guid.NewGuid().ToString("N"),PlayerId=GeneralAssetSeatB6,Kind=(Catan.Core.M4.CommandKind)Enum.Parse(typeof(Catan.Core.M4.CommandKind),kind),GeneralIds=ids??Array.Empty<string>(),DecisionId=decision,WindowId=view.WindowId})?.Success==true;
        else if(presentation is M5App b)accepted=b.Submit(new Catan.Core.M5.Command{Id=Guid.NewGuid().ToString("N"),PlayerId=GeneralAssetSeatB6,Kind=(Catan.Core.M5.CommandKind)Enum.Parse(typeof(Catan.Core.M5.CommandKind),kind),GeneralIds=ids??Array.Empty<string>(),DecisionId=decision,WindowId=view.WindowId})?.Success==true;
        if(!NetworkGame||!Network.CommandPending)generalSubmitting=false;
        if(accepted){selectionView=null;generalClickBlockedUntil=Time.unscaledTimeAsDouble+.35;}
    }
}
