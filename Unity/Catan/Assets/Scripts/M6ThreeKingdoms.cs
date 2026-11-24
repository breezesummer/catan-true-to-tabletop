using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

// 所有候选仅来自本席人类投影。完成最后一次点选后走同一权威命令入口。
public sealed partial class M6App
{
    string GeneralAssetSeatB6=>presentation is M4App cities?cities.M6View?.PlayerId??HumanSeat:presentation is M5App combined?combined.M6View?.PlayerId??HumanSeat:HumanSeat;
    static readonly Rect GeneralArea=new Rect(28,OperationDock.HandRack.yMax-205,164,205);
    static readonly Rect SkillsArea=new Rect(218,714,62,171);
    bool FishHandSelected=>presentation is M4App fishCities?fishCities.FishHandSelected:presentation is M5App fishCombined&&fishCombined.FishHandSelected;
    float SkillBarOffset=>FishHandSelected?-132:0;
    Rect SkillBar=>new Rect(342,650+SkillBarOffset,932,58);
    static readonly Rect CandidateArea=new Rect(342,474,932,168);
    static readonly Rect GeneralCandidateArea=new Rect(342,322,932,320);
    static readonly Rect LibraryArea=OperationDock.HandViewport;
    static readonly Rect GeneralDetailArea=new Rect(500,260,420,390);
    string detailGeneralId;
    internal bool GeneralHandVisible=>generalLibraryOpen&&!FishHandSelected&&!ThreeKingdomsSelectionActive;
    internal void CloseGeneralLibrary(){generalLibraryOpen=false;detailGeneralId=null;}
    internal void ShowGeneralLibrary(){generalLibraryOpen=true;detailGeneralId=null;}
    static readonly string[] ThreeKingdomsCardNames={"木材","砖","羊毛","谷物","矿石","布匹","钱币","纸张"};
    readonly HashSet<string> selectedGenerals=new HashSet<string>();
    readonly B45SelectionContext generalSelection=new B45SelectionContext();
    readonly Dictionary<string,string> selectedProgressFaces=new Dictionary<string,string>();
    readonly OperationHandSelection generalHand=new OperationHandSelection();
    string threeKingdomsDecision,inspectedGeneral,inspectionText,inspectionScope;
    Rect inspectionAnchor;
    float inspectionHeight=100;
    bool generalLibraryOpen,generalSubmitting,retireDraft;
    ThreeKingdomsView selectionView;
    Vector2 generalScroll,candidateScroll,skillScroll,inspectionScroll;

    string observedSkillEvent,flashingSkill,latestSkillResult;
    double skillFlashUntil;
    bool observedSkillsInitialized;
    internal ThreeKingdomsView ThreeKingdomsProjection=>presentation is M4App cities?cities.M6View?.ThreeKingdoms:
        presentation is M5App combined?combined.M6View?.ThreeKingdoms:null;
    bool ThreeKingdomsAdvanceAvailable=>presentation is M4App cities?
        cities.M6View?.LegalActions.Any(c=>c.Kind==Catan.Core.M4.CommandKind.AdvanceThreeKingdomsPhase)==true:
        presentation is M5App combined&&combined.M6View?.LegalActions.Any(c=>c.Kind==Catan.Core.M5.CommandKind.AdvanceThreeKingdomsPhase)==true;
    internal bool ThreeKingdomsSkillSelectionActive=>generalSelection.Active||retireDraft||ThreeKingdomsProjection?.PendingDecision?.PlayerId==GeneralAssetSeatB6;
    bool NativeGeneralPaymentActive=>presentation is M4App a?a.ThreeKingdomsPaymentActive:presentation is M5App b&&b.ThreeKingdomsPaymentActive;
    internal bool ThreeKingdomsSelectionActive=>ThreeKingdomsSkillSelectionActive||NativeGeneralPaymentActive;
    internal string GeneralActionCaption=>TurningFixedDice?"确认":generalSelection.Active?B45SkillCatalog.Get(generalSelection.SkillId)?.Name??"选择":NativeGeneralPaymentActive?"支付":retireDraft?"隐退":"选将";
    internal bool CanFinishGeneralAction=>GeneralCanSubmit&&!AllianceDecision&&generalSelection.CanConfirm;
    internal void FinishGeneralAction()
    {
        if(!GeneralCanSubmit)return;
        if(generalSelection.OpenActivationChoices()){FocusSkillHand();return;}
        var command=generalSelection.BeginSubmit();
        if(command!=null)SubmitGeneralSkill(command);
    }
    internal bool ThreeKingdomsPanelBlocksTable=>false;
    bool GeneralTableCovered=>presentation is M4App cities&&cities.GeneralModalOpen||presentation is M5App combined&&combined.GeneralModalOpen;
    bool GeneralCanInteract=>!TableDiceRolling&&!TablePresentationBlocked&&!GeneralTableCovered&&inspectionText==null&&detailGeneralId==null&&(!NetworkGame||Network.CanInteract);
    string longdanWindow;
    internal bool LongdanSelected=>longdanWindow!=null&&longdanWindow==ThreeKingdomsProjection?.WindowId;
    bool LongdanAvailable(ThreeKingdomsView view)=>IsHumanTurn&&view.SkillDecision==null&&view.PendingDecision==null&&
        !generalSelection.Active&&GeneralAssetSeatB6==HumanSeat&&view.Players.Any(p=>p.PlayerId==HumanSeat&&p.ActiveGeneralId=="tk-g42"&&!p.FaceDown)&&
        view.EffectiveCardTypes?.Any(types=>types.Length>1)==true;
    bool GeneralCanSubmit=>GeneralCanInteract;
    internal string ThreeKingdomsPhaseCaption=>ThreeKingdomsProjection==null?null:"第 "+ThreeKingdomsProjection.Clock.Round+" 轮 "+ThreeKingdomsPhaseName(ThreeKingdomsProjection.Clock.Phase)+(GeneralAssetSeatB6!=HumanSeat?" 代操 "+GeneralAssetSeatB6:"");
    bool GoldResourceChoice=>generalSelection.Contract?.DecisionKind=="gold";
    bool CandidateVisible=>ThreeKingdomsProjection?.PendingDecision?.PlayerId==GeneralAssetSeatB6||generalSelection.Active&&generalSelection.Contract.DecisionKind!="b5m-luanji-defend"&&(generalSelection.Luoying||AllianceDecision||GoldResourceChoice||generalSelection.Raid||generalSelection.ShowOptionChoices||generalSelection.PrivateOptions.Length>0||generalSelection.RequiresBankCards||generalSelection.DiceIndices.Length>0);
    bool BottomGeneralCandidatesVisible=>CandidateVisible&&generalSelection.DiceIndices.Length==0&&!(generalSelection.Contract?.DirectBatch==true&&(generalSelection.Contract.DecisionKind=="b4-reveal"||generalSelection.Contract.DecisionKind=="b4-subset"));
    bool NativeGeneralPaymentContains(Vector2 p)=>presentation is M4App a?a.ThreeKingdomsPaymentContains(p):presentation is M5App b&&b.ThreeKingdomsPaymentContains(p);
    Rect SkillCandidateBounds=>generalSelection.Luoying?LuoyingArea:AllianceDecision?AllianceArea:
        GoldResourceChoice||generalSelection.RequiresBankCards?TabletopResourceChoiceUI.CompactArea:CandidateArea;
    internal bool ThreeKingdomsBlocksInput(Vector2 p)=>ThreeKingdomsProjection!=null&&(GeneralArea.Contains(p)||SkillsArea.Contains(p)||ThreeKingdomsSkillSelectionActive&&SkillBar.Contains(p)||BottomGeneralCandidatesVisible&&((ThreeKingdomsProjection?.PendingDecision?.PlayerId==GeneralAssetSeatB6)?GeneralCandidateArea:SkillCandidateBounds).Contains(p)||SpecialGeneralCandidatesContain(p)||NativeGeneralPaymentContains(p)||GeneralHandVisible&&LibraryArea.Contains(p)||detailGeneralId!=null&&GeneralDetailArea.Contains(p)||inspectionText!=null&&InspectionRect.Contains(p));
    Rect InspectionRect
    {
        get
        {
            return new Rect(Mathf.Clamp(inspectionAnchor.x,18,1022),Mathf.Clamp(inspectionAnchor.y-inspectionHeight-12,212,610),400,inspectionHeight);
        }
    }
    void ResetThreeKingdomsPanel()
    {
        longdanWindow=null;ThreeKingdomsDirectInput.Reset();
        if(presentation is M4App paymentCities)paymentCities.CancelThreeKingdomsPayment();else if(presentation is M5App paymentCombined)paymentCombined.CancelThreeKingdomsPayment();
        selectedGenerals.Clear();generalSelection.Reset();selectedProgressFaces.Clear();selectionView=null;threeKingdomsDecision=null;
        generalLibraryOpen=false;detailGeneralId=null;inspectionText=null;generalSubmitting=false;retireDraft=false;generalScroll=candidateScroll=skillScroll=Vector2.zero;
        observedSkillEvent=flashingSkill=latestSkillResult=null;skillFlashUntil=0;observedSkillsInitialized=false;
        ResetGeneralSkins();
    }
    bool TryCloseThreeKingdomsPanel()
    {
        if(detailGeneralId!=null){detailGeneralId=null;return true;}
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
        StartGame(RuleSet,ScenarioId,PlayerCount,HumanSeat,Paused,targetVictoryPoints:customVictoryTarget?TargetVictoryPoints:(int?)null,aiSpeedLevel:AiSpeedLevel,threeKingdoms:ThreeKingdoms,harbormaster:Harbormaster,fishing:Fishing);
        localPlayerNames=names;Notice="已保留玩法、三国开关、席位、姓名、胜利目标与 AI 节奏，重新生成地图和行动顺序。";
    }
    static string GeneralDescription(GeneralDefinition general)=>general.Name+" · "+general.Faction+"\n"+general.Title+"\n\n"+string.Join("\n\n",general.Skills.Select(skill=>skill.Name+"\n"+skill.OriginalText));
    static string GeneralName(string id)=>string.IsNullOrEmpty(id)?"尚未公开":GeneralCatalog.Get(id).Name;
    static string ThreeKingdomsPhaseName(ThreeKingdomsPhase phase)
    {
        switch(phase)
        {
            case ThreeKingdomsPhase.OpeningSelection:return "开局选择武将";
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
        {
            // 桌面上未提交的交易、设施或地图选择不能盖住新到达的结盟回应。
            if(view.SkillDecision.Kind?.StartsWith("alliance-b6-",StringComparison.Ordinal)==true)
            {
                if(presentation is M4App cities)cities.CancelForGeneralSelection();
                else if(presentation is M5App combined)combined.CancelForGeneralSelection();
            }
            generalSelection.Begin(view,GeneralAssetSeatB6,null,ThreeKingdomsHand());
            FocusSkillHand();
        }
        if(!view.CanRetire)retireDraft=false;
        string scope=GeneralAssetSeatB6+"|"+view.WindowId+"|"+view.SkillDecision?.DecisionId+"|"+token;
        if(scope!=inspectionScope){inspectionText=null;inspectionScope=scope;ThreeKingdomsDirectInput.Reset();}
    }
    internal OperationHandSelection ThreeKingdomsHandSelection
    {
        get
        {
            generalHand.Active=ThreeKingdomsSelectionActive;
            generalHand.HighlightTargets=generalSelection.Active&&!generalSelection.IsSubmitting;
            generalHand.CanSelectGood=(t,n)=>GeneralCanInteract&&generalSelection.CanSelectHand(t,n);
            generalHand.IsGoodSelected=generalSelection.IsHandSelected;
            generalHand.SelectGood=(t,n)=>{
                if(!generalSelection.ToggleHand(t,n)||!generalSelection.IsHandSelected(t,n))return;
                if(AllianceDecision)FinishGeneralAction();else FinishGeneralPick();
            };
            generalHand.CanSelectCard=id=>GeneralCanInteract&&generalSelection.CanSelectProgress(ProgressKind(id),ProgressOrdinal(id));
            generalHand.IsCardSelected=id=>generalSelection.IsProgressSelected(ProgressKind(id),ProgressOrdinal(id));
            generalHand.SelectCard=id=>{string kind=ProgressKind(id);int ordinal=ProgressOrdinal(id);if(generalSelection.ToggleProgress(kind,ordinal)&&generalSelection.IsProgressSelected(kind,ordinal))FinishGeneralPick();};
            generalHand.SelectedGoods=()=>generalSelection.SelectedCounts.Sum();generalHand.SelectedCards=()=>generalSelection.SelectedProgress.Length;
            generalHand.Reason=null;return generalHand;
        }
    }
    static int ProgressOrdinal(string id)=>int.TryParse((id??"0:0").Split(':').Last(),out var n)?n:0;
    static string ProgressKind(string displayId)=>int.TryParse((displayId??"").Split(':')[0],out var kind)?((Catan.Core.M4.ProgressCardKind)kind).ToString():null;
    internal void InspectThreeKingdomsGood(int type,Rect anchor){if(type>=0&&type<8)Inspect(ThreeKingdomsCardNames[type]+"\n"+(generalSelection.Active?generalSelection.Description:"真实手牌。"),anchor);}
    internal void InspectThreeKingdomsCard(string id,Rect anchor){var kind=ProgressKind(id);if(kind!=null)Inspect(Catan.Core.DevelopmentCardText.Name(kind)+"\n"+(generalSelection.Active?generalSelection.Description:"可用时点击牌面进入用途选择。"),anchor);}
    void Inspect(string text,Rect anchor)
    {
        inspectionText=text;inspectionAnchor=anchor;inspectionScroll=Vector2.zero;
        var style=new GUIStyle{font=font,fontSize=14,wordWrap=true};
        inspectionHeight=Mathf.Clamp(style.CalcHeight(new GUIContent(text??""),358)+48,100,268);
    }
    void BeginGeneralSkill(string skill)
    {
        if(!GeneralCanInteract)return;
        if(generalSelection.SkillId==skill&&generalSelection.CanCancel){generalSelection.Reset();return;}
        if(presentation is M4App a)a.CancelForGeneralSelection();else if(presentation is M5App b)b.CancelForGeneralSelection();
        retireDraft=false;generalSelection.Begin(ThreeKingdomsProjection,GeneralAssetSeatB6,skill,ThreeKingdomsHand());selectedProgressFaces.Clear();candidateScroll=Vector2.zero;
        if(generalSelection.Options.Length==1)FinishGeneralPick();
        FocusSkillHand();
    }
    bool CancelGeneralPointerPick()
    {
        if(!generalSelection.Active||generalSelection.IsSubmitting||GeneralTableCovered||detailGeneralId!=null||inspectionText!=null)return false;
        if(generalSelection.CanCancel)generalSelection.Reset();else generalSelection.Reselect();
        inspectionText=null;selectedProgressFaces.Clear();ThreeKingdomsDirectInput.Reset();
        return true;
    }
    void FocusSkillHand()
    {
        if(!generalSelection.Active)return;
        var actions=generalSelection.CandidateActions;
        bool goods=actions.Any(a=>B45SelectionContract.Cost(a).Any(n=>n>0))||
            generalSelection.Contract.DecisionKind=="b4-subset"||generalSelection.Contract.DecisionKind=="b4-reveal";
        bool cards=actions.Any(a=>B45SelectionContract.Progress(a).Length>0);
        if(!goods&&!cards)return;
        CloseGeneralLibrary();
        if(presentation is M4App cities)cities.FocusGeneralHand(cards&&!goods);
        else if(presentation is M5App combined)combined.FocusGeneralHand(cards&&!goods);
    }
    void FinishGeneralPick(bool playerPicked=false)
    {
        if(!GeneralCanSubmit)return;
        var command=generalSelection.FinishDirectPick(playerPicked);
        if(command!=null)SubmitGeneralSkill(command);
    }
    void ChooseGeneralOption(B2SkillAction action)
    {
        if(!generalSelection.ChooseOption(action))return;
        FinishGeneralPick();
        FocusSkillHand();
    }
    void PickGeneralPlayer(string player)
    {
        if(generalSelection.TogglePlayer(player)&&generalSelection.SelectedPlayers.Contains(player))FinishGeneralPick(true);
    }
    void PickGeneralBoardTarget(string id)
    {
        if(generalSelection.ToggleTile(id)&&generalSelection.SelectedTiles.Contains(id))FinishGeneralPick();
    }
    void DrawThreeKingdoms()
    {
        SyncThreeKingdomsSelection();var view=ThreeKingdomsProjection;if(view==null)return;
        PastoralMenuUI.Styles(font);
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&(Event.current.type==EventType.Repaint||!GeneralTableCovered&&detailGeneralId==null&&inspectionText==null);
        DrawGeneralDock(view);DrawGeneralSkills(view);
        if(!NativeGeneralPaymentActive&&ThreeKingdomsSkillSelectionActive)DrawGeneralOperationBar(view);
        if(view.PendingDecision?.PlayerId==GeneralAssetSeatB6)DrawGeneralCandidates(view.PendingDecision);
        else if(CandidateVisible)DrawSkillCandidates();
        DrawGeneralBoardTargets();
        if(GeneralHandVisible)DrawGeneralLibrary(view);
        GUI.enabled=enabled&&!GeneralTableCovered;
        if(detailGeneralId!=null)DrawGeneralDetail();
        if(inspectionText!=null)DrawGeneralInspection();
        GUI.enabled=enabled;
    }
    void DrawGeneralDock(ThreeKingdomsView view,bool interactive=true)
    {
        var own=view.Players.FirstOrDefault(p=>p.PlayerId==GeneralAssetSeatB6);
        var general=string.IsNullOrEmpty(own?.ActiveGeneralId)?null:GeneralCatalog.Get(own.ActiveGeneralId);
        bool dynamicPortrait=own?.FaceDown==false && GeneralSkinArt.Get(general?.Id)?.useStatic==false;
        // 动态框沿用选将预览的 4:5 比例，底边对齐手牌底栏。
        var portrait=dynamicPortrait?GeneralArea:new Rect(28,714,138,171);
        GeneralFrameUI.Portrait(portrait,general?.Id,general?.Name,general?.Faction,
            Array.IndexOf(HistoryPlayers,GeneralAssetSeatB6),Ready&&!Finished&&GeneralAssetSeatB6==HumanSeat&&view.Clock.ActivePlayerId==HumanSeat&&
            view.Clock.Phase>=ThreeKingdomsPhase.Preparation&&view.Clock.Phase<=ThreeKingdomsPhase.End,
            own?.FaceDown==true,general==null?-1:own.RemainingLife);
        if(!interactive)return;
        int portraitInput=ThreeKingdomsDirectInput.Read(portrait,true);
        if(portraitInput==1&&general!=null)detailGeneralId=general.Id;
        if(portraitInput==2&&general!=null)detailGeneralId=general.Id;

    }
    void DrawGeneralSkills(ThreeKingdomsView view)
    {
        var skills=B45SkillCatalog.AvailableEntries(view,GeneralAssetSeatB6);

        float skillStep=Mathf.Min(49,SkillsArea.height/(skills.Length+1));
        GUI.BeginGroup(SkillsArea);
        for(int i=0;i<skills.Length;i++)
        {
            var skill=skills[i];
            bool responding=generalSelection.Contract?.IsResponse==true&&generalSelection.SkillId==skill.SkillId;
            bool longdan=skill.SkillId=="tk-g42-s1"&&LongdanAvailable(view);
            bool active=skill.HasActiveEntry||responding||longdan;
            bool available=active&&!generalSelection.IsSubmitting&&view.PendingDecision==null&&
                (longdan||responding||view.SkillDecision==null&&view.SkillActions.Any(a=>a.Command.SkillId==skill.SkillId||skill.SkillId==B45SkillCatalog.RepairSkillId&&a.Command.Option==B45SkillCatalog.RepairSkillId));
            var area=new Rect(1,i*skillStep+2,SkillsArea.width-2,skillStep-4);bool selected=generalSelection.SkillId==skill.SkillId||longdan&&LongdanSelected;
            bool flashing=flashingSkill==skill.SkillId&&Time.unscaledTimeAsDouble<skillFlashUntil;
            GeneralFrameUI.Skill(area,skill.Name,active,available,selected,flashing);
            int input=ThreeKingdomsDirectInput.Read(area,available&&GeneralCanInteract);
            if(input==1)
            {
                if(longdan)
                {
                    longdanWindow=LongdanSelected?null:view.WindowId;
                    if(LongdanSelected)
                    {
                        if(presentation is M4App cities)cities.FocusGeneralHand(false);
                        else if(presentation is M5App combined)combined.FocusGeneralHand(false);
                        ShowTablePrompt(NativeGeneralPaymentActive?"龙胆：点手牌支付":"龙胆：选择要支付的行动");
                    }
                }
                else if(!responding)BeginGeneralSkill(skill.SkillId);
                else if(generalSelection.Raid||AllianceDecision)return;
                else if(generalSelection.CanConfirm&&generalSelection.Options.Length==1)FinishGeneralAction();
                else generalSelection.Reselect();
            }
            if(input==2)Inspect(skill.Name+"\n"+skill.Description,new Rect(SkillsArea.x+area.x,SkillsArea.y+area.y,area.width,area.height));
        }
        var retireArea=new Rect(1,skills.Length*skillStep+2,SkillsArea.width-2,skillStep-4);
        bool canRetire=view.CanRetire&&view.Clock.ActivePlayerId==GeneralAssetSeatB6&&GeneralCanInteract&&!generalSelection.Active;
        GeneralFrameUI.Skill(retireArea,"隐退",true,view.CanRetire&&view.Clock.ActivePlayerId==GeneralAssetSeatB6&&!generalSelection.Active,retireDraft,false);
        int retireInput=ThreeKingdomsDirectInput.Read(retireArea,canRetire);
        if(retireInput==1)retireDraft=!retireDraft;
        if(retireInput==2)Inspect("隐退\n令当前武将隐退，已有暗置武将登场；系统每三轮补发。",SkillsArea);
        GUI.EndGroup();
    }
    string GeneralSkipName=>generalSelection.AwaitingActivation?"取消":generalSelection.SkipCommand?.Option=="keep"?"保留骰子":generalSelection.SkipCommand?.Option=="luoshen-stop"?"停止重掷":"跳过";
    void DrawGeneralOperationBar(ThreeKingdomsView view)
    {
        var matrix=GUI.matrix;
        GUI.matrix=matrix*Matrix4x4.Translate(new Vector3(0,SkillBarOffset,0));
        try { DrawGeneralOperationBarContent(view); }
        finally { GUI.matrix=matrix; }
    }
    void DrawGeneralOperationBarContent(ThreeKingdomsView view)
    {

        var pending=view.PendingDecision?.PlayerId==GeneralAssetSeatB6?view.PendingDecision:null;
        if(pending!=null)
        {
            if(pending.Kind==ThreeKingdomsDecisionKind.SelectOpening)
                DrawHandInstruction("请选择两名武将加入武将库"+selectedGenerals.Count+"/"+pending.RequiredCount);
            else if(pending.Kind==ThreeKingdomsDecisionKind.SelectReplacement)
                PastoralMenuUI.Label(new Rect(354,655,622,22),"补充武将 · 已选 "+selectedGenerals.Count+" / "+pending.RequiredCount+" 张",17,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            else
                DrawHandInstruction("请选择登场武将");

            if(GeneralFrameUI.Choice(new Rect(984,661,112,36),"重选",!generalSubmitting))selectedGenerals.Clear();
            if(GeneralFrameUI.Choice(new Rect(1108,661,154,36),generalSubmitting?"等待确认":"确认选择",!generalSubmitting&&GeneralCanSubmit&&selectedGenerals.Count==pending.RequiredCount,true))
            {generalSubmitting=true;SubmitThreeKingdoms(pending.Kind==ThreeKingdomsDecisionKind.SelectOpening||pending.Kind==ThreeKingdomsDecisionKind.SelectReplacement?"ChooseGenerals":"DeployGeneral",selectedGenerals.OrderBy(x=>x,StringComparer.Ordinal).ToArray());}
            return;
        }
        if(retireDraft)
        {
            PastoralMenuUI.Label(new Rect(354,655,614,46),"令当前武将隐退",16,PastoralMenuUI.Ivory,false,TextAnchor.MiddleCenter);
            if(GeneralFrameUI.Choice(new Rect(984,661,112,36),"取消"))retireDraft=false;
            if(GeneralFrameUI.Choice(new Rect(1108,661,154,36),"确认隐退",GeneralCanSubmit&&view.CanRetire,true)){retireDraft=false;SubmitThreeKingdoms("RetireGeneral");}return;
        }
        if(generalSelection.Active)
        {
            if(GoldResourceChoice||generalSelection.Luoying||AllianceDecision)return;
            string instruction=TurningFixedDice?"请旋转红骰和事件骰":CurrentSkillInstruction(view);
            if(instruction.Length>0)DrawHandInstruction(instruction);
            if(generalSelection.Contract.DecisionKind=="b5m-luanji-defend")
            {
                var actions=generalSelection.Contract.Actions;
                var selected=generalSelection.SelectedCounts;
                var resist=actions.Where(a=>a.Command.Option=="b5m-resist")
                    .OrderByDescending(a=>selected[a.Command.CardType]>0)
                    .ThenByDescending(a=>a.Command.CardType==4).FirstOrDefault();
                var participate=actions.FirstOrDefault(a=>a.Command.Option=="b5m-participate");
                bool ready=GeneralCanSubmit&&!generalSelection.IsSubmitting;
                if(GeneralFrameUI.Choice(SkillConfirmRect,"确认",ready&&resist!=null,true))
                    SubmitGeneralSkill(resist.Command);
                else if(GeneralFrameUI.Choice(SkillCancelRect,"取消",ready&&participate!=null))
                    SubmitGeneralSkill(participate.Command);
                return;
            }
            bool skip=generalSelection.SkipCommand!=null;
            bool cancel=generalSelection.CanCancel;
            bool confirm=generalSelection.CanConfirm;
            if(confirm||generalSelection.Raid||generalSelection.SkillId=="tk-g22-s1")
            {
                if(GeneralFrameUI.Choice(skip||cancel?SkillConfirmRect:SkillCancelRect, "确认", GeneralCanSubmit&&confirm,true))
                {FinishGeneralAction();return;}
            }
            if(skip||cancel)
            {
                if(GeneralFrameUI.Choice(SkillCancelRect,skip&&!generalSelection.Raid?GeneralSkipName:"取消",GeneralCanSubmit&&!generalSelection.IsSubmitting))
                {
                    if(skip)SubmitGeneralSkill(generalSelection.SkipCommand);
                    else CancelGeneralPointerPick();
                }
            }
            return;
        }
    }
    internal static readonly Rect SkillConfirmRect=new Rect(TabletopHandLayout.RestRight-192,673,90,29);
    internal static readonly Rect SkillCancelRect=new Rect(TabletopHandLayout.RestRight-94,673,94,29);
    GUIStyle handInstructionStyle;
    internal void DrawHandInstruction(string text)
    {
        if(handInstructionStyle==null)
        {
            Styles();
            handInstructionStyle=new GUIStyle(title){fontSize=30,alignment=TextAnchor.MiddleCenter,wordWrap=false};
            handInstructionStyle.normal.textColor=PromptColor;
        }
        TabletopTextFit.Label(new Rect(TabletopHandLayout.RestLeft,640,TabletopHandLayout.RestRight-TabletopHandLayout.RestLeft,35),text,handInstructionStyle,18,true);
    }
    int instructionRevision=-1;
    string skillInstruction;
    string CurrentSkillInstruction(ThreeKingdomsView view)
    {
        if(instructionRevision!=generalSelection.Revision)
        {
            instructionRevision=generalSelection.Revision;
            skillInstruction=ResolveSkillInstruction(view);
        }
        return skillInstruction;
    }
    string ResolveSkillInstruction(ThreeKingdomsView view)
    {
        var s=generalSelection;
        if(s.AwaitingActivation&&s.Contract.DecisionKind=="dice-observe")return "是否发动观星";
        if(s.AwaitingActivation)return "是否发动「"+GeneralActionCaption+"」？";
        if(s.SkillId=="tk-g22-s1")return "请选择至多四张资源牌或一张发展牌";
        if(s.Raid)return s.RaidCount>0?"请选择被突袭的玩家":"";
        if(s.RequiresBankCards||s.DiceIndices.Length>0||s.Contract.DecisionKind=="shelie"||
           s.Contract.DecisionKind=="b4-pile"||s.Contract.DecisionKind=="b4-declare")return "";
        if(s.SkillId=="tk-g05-s2"&&!s.Contract.IsResponse)
            return s.SelectedCounts.Sum()<2?"请选择两张砖":"请选择一名其他玩家作为「乐不思蜀」的目标";
        if(s.Contract.DecisionKind=="response-poxi-select")return "请从双方手牌中选择四张牌，每种各一张";
        var decision=view.SkillDecision;
        if(decision?.PlayerId==GeneralAssetSeatB6)
        {
            if(decision.Kind=="optional")return "是否发动「"+GeneralActionCaption+"」？";
            switch(decision.Kind)
            {
                case "civil-fencheng-discard":return "请至少弃置 "+s.MinimumHandCount+" 张牌";
                case "civil-hand-select":return "请选择弃置 "+s.MinimumHandCount+" 张牌";
                case "civil-fencheng":case "civil-pojun":
                case "civil-guess":case "b4-guess":case "b4-give":case "response-guhuo":
                    return DisplayPlayerText(decision.Description??decision.Title);
                case "civil-burn":return "请选择一座己方城市焚毁";
                case "civil-two-roads":return "请选择两条不同的道路";
                case "civil-luanwu":return "请选择支付两张砖或承受道路损失";
                case "civil-subset":case "b4-subset":return "请选择至少一张手牌暗置";
                case "b4-reveal":return "请选择要展示的手牌，可以不选";
                case "response-jieying-target":return "请选择一名其他玩家作为「劫营」的目标";
                case "response-qice-use":return "请选择要获得的发展牌";
                case "response-zhiyu-discard":return "请选择一张手牌弃置";
                case "b5m-luanji-defend":return "是否支付一张矿石抵御本次入侵？";
                case "b5m-zaiqi":return s.AwaitingEffectChoice?"再起：请选择本次要执行的效果":"再起：请选择一座己方村庄升级为城市";
                case "b5m-huoshou-move":return "祸首：请点击强盗或海盗的新地块，再选择要偷取的玩家";
                case "dice-fixed":if(s.SkillId=="tk-g20-s1")return "请选择黑七骰面";break;
                case "map-zhimeng-target":return "请选择一名其他玩家，与其均分双方手牌";
                case "seat-jinglue-card":return "请选择一张与上次取得的牌种不同的手牌";
                case "seat-fuqi-give":return "请选择一张要交出的手牌";
                case "seat-fuqi-request":return "请选择要取得的一至两张资源牌或商品牌";
                default:return DisplayPlayerText(decision.Title??"");
            }
        }
        return ActiveSkillInstruction(s.SkillId,s);
    }
    static string ActiveSkillInstruction(string skill,B45SelectionContext selection)
    {
        switch(skill)
        {
            case "tk-g03-s2":return "请选择一名其他玩家猜测牌种";
            case "tk-g05-s1":return selection.SelectedCounts.Sum()==0?"请选择一张小麦":"请选择强盗或海盗的新位置";
            case "tk-g08-s2":return "请选择一名其他玩家查看其手牌";
            case "tk-g11-s3":return "请选择两名不同的其他玩家参与「间书」";
            case "tk-g15-s1":return selection.SelectedCounts.Sum()==0?"请选择要赠予的手牌":"请选择一名其他玩家接收所选手牌";
            case "tk-g16-s1":return "请选择一处己方建筑或设施拆除";
            case "tk-g29-s1":return "请选择两名不同的玩家翻面";
            case "tk-g31-s1":return "请选择一名玩家作为「焚城」的起始玩家";
            case "tk-g35-s1":return "请选择至少一张矿石，换取等量随机资源牌";
            case "tk-g39-s1":return "请选择一名其他玩家进行猜测";
            case "tk-g45-s1":return "请选择一名其他玩家交付手牌";
            case "tk-g46-s1":return "请选择一名领土接壤的玩家";
            case "tk-g47-s1":return selection.SelectedTiles.Length==0?"请选择第一块待交换的地块":"请选择可与其交换的第二块地块";
            case "tk-g48-s1":return "请选择两名不同的玩家交换座次";
            case "tk-g48-s2":return "请选择一名其他玩家查看其手牌";
            case "tk-g49-s2":return "请选择一名其他玩家，消耗一枚田标记取得其一张手牌";
            case "tk-g51-s1":return "烈弓：请点击要摧毁的建筑，或选择摧毁两条道路";
            case "tk-g54-s1":return selection.SelectedCounts.Sum()==0?"请选择一张手牌作为修复费用":"请选择一处受损建筑或设施进行修复";
        }
        var actions=selection.CandidateActions;
        if(actions.Any(a=>B45SelectionContract.Cost(a).Sum()>selection.SelectedCounts.Sum()))return "请选择发动技能所需的手牌";
        if(actions.Any(a=>B45SelectionContract.Players(a).Length>selection.SelectedPlayers.Length))return "请选择目标玩家";
        if(actions.Any(a=>B45SelectionContract.Progress(a).Length>selection.SelectedProgress.Length))return "请选择发动技能所需的发展牌";
        if(actions.Any(a=>B45SelectionContract.Board(a).Length>selection.SelectedTiles.Length))return "请选择技能作用的棋盘目标";
        return "";
    }
    void EndThreeKingdomsPlay()
    {if(presentation is M4App a)a.EndThreeKingdomsTurn();else if(presentation is M5App b)b.EndThreeKingdomsTurn();}
    void DrawGeneralCandidates(ThreeKingdomsDecision pending)
    {
        var ids=pending.CandidateIds;float width=Mathf.Min(210,900f/Mathf.Max(1,ids.Length));
        for(int i=0;i<ids.Length;i++)
        {
            var general=GeneralCatalog.Get(ids[i]);bool selected=selectedGenerals.Contains(ids[i]);var r=new Rect(352+i*width,322,width-8,318);
            DrawGeneralCard(r,general,selected);
            int input=ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSubmitting);
            if(input==1){if(!selectedGenerals.Remove(ids[i])){if(pending.RequiredCount==1)selectedGenerals.Clear();if(selectedGenerals.Count<pending.RequiredCount)selectedGenerals.Add(ids[i]);}inspectedGeneral=ids[i];}
            if(input==2)Inspect(general.Name+"\n"+string.Join("\n",general.Skills.Select(s=>s.Name+"："+s.OriginalText)),r);
        }
    }
    static bool IsLocalCandidate(B2SkillAction a)=>B45SelectionContract.Players(a).Length==0&&B45SelectionContract.Board(a).Length==0&&B45SelectionContract.Cost(a).Sum()==0&&B45SelectionContract.Progress(a).Length==0;
    void DrawSkillCandidates()
    {
        if(generalSelection.Luoying){DrawLuoying();return;}
        if(AllianceDecision){DrawAllianceDecision();return;}
        if(generalSelection.Raid){DrawRaidHarvest();return;}
        if(GoldResourceChoice){DrawGeneralBankCandidates();return;}
        if(DrawSpecialGeneralCandidates())return;
        if(generalSelection.RequiresBankCards){DrawGeneralBankCandidates();return;}
        var options=generalSelection.Options;
        // 牌、头像、棋盘目标不生成文字组合列表。这里只容纳分支、银行类型及私密授权候选。
        var local=options.Where(a=>a.Command.Option!="civil-kurou"&&(IsLocalCandidate(a)||a.VirtualCardId!=null||a.CardSelectionKind=="declare"||a.Command.Option=="seat-jinglue-take"||generalSelection.AwaitingEffectChoice)).ToArray();
        if(generalSelection.Contract.DecisionKind=="b5m-zaiqi"&&!generalSelection.AwaitingEffectChoice)local=Array.Empty<B2SkillAction>();
        var privateCards=generalSelection.PrivateOptions.GroupBy(a=>a.Command.CardType).Select(g=>g.First()).ToArray();
        if(local.Length==0&&privateCards.Length==0)return;
        if(privateCards.Length==0&&local.Length<=6&&local.All(a=>a.ResultCardType==null&&a.RealCardType==null&&a.VirtualCardId==null&&a.Command.Option!="choose-card"&&a.Command.Option!="b4-guess"&&a.Command.Option!="claim"))
        {
            int columns=Mathf.Min(3,local.Length);
            float width=Mathf.Min(240,880f/columns);
            for(int i=0;i<local.Length;i++)
            {
                var action=local[i];
                var area=new Rect(CandidateArea.center.x-columns*(width+16)/2+(i%columns)*(width+16),CandidateArea.yMax-68-(local.Length>3?66:0)+(i/columns)*66,width,58);
                if(GeneralFrameUI.Choice(area,OptionCaption(action),GeneralCanInteract&&!generalSelection.IsSubmitting,generalSelection.IsOptionSelected(action)))
                    ChooseGeneralOption(action);
            }
            return;
        }
        DrawCandidateRow(privateCards.Length>0?"对方手牌":GeneralActionCaption,local.Concat(privateCards).ToArray(),local.Length,true);
    }
    internal static Rect RaidCardRect(int index,int count)=>new Rect(CandidateArea.center.x-count*54+index*108+8,510,84,91);
    void DrawRaidHarvest()
    {
        var actions=generalSelection.Contract.Actions.Where(a=>a.Command.Option=="raid").ToArray();
        var harvest=ThreeKingdomsProjection.RaidHarvestCards??Enumerable.Range(0,8).Select(t=>actions.Max(a=>a.Command.Counts[t])).ToArray();
        var kinds=Enumerable.Range(0,8).Where(t=>harvest[t]>0).ToArray();
        var panel=new Rect(0,470,Mathf.Max(320,kinds.Length*108+32),154);
        panel.x=CandidateArea.center.x-panel.width/2;
        TabletopResourceChoiceUI.DrawFrame(panel,"突袭：请选择不收获的资源",0,0);
        for(int i=0;i<kinds.Length;i++){
            int type=kinds[i],count=generalSelection.RaidForgone(type);
            var r=RaidCardRect(i,kinds.Length);
            var face=new Rect(r.center.x-29,r.y+(count>0?0:5),58,86);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(type),count>0?1:0);
            if(harvest[type]>1)PastoralMenuUI.Label(new Rect(r.x,r.yMax,r.width,20),count+"/"+harvest[type],13,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            if(ThreeKingdomsDirectInput.Read(r,GeneralCanInteract&&!generalSelection.IsSubmitting,false)==1)
                generalSelection.CycleRaidCard(type);
        }
    }
    void DrawGeneralBankCandidates()
    {
        bool gold=GoldResourceChoice;
        var area=TabletopResourceChoiceUI.CompactArea;
        TabletopResourceChoiceUI.DrawFrame(area,gold?"金矿：请选择资源":"请选择资源",gold?0:generalSelection.SelectedBankCounts.Sum(),gold?1:generalSelection.RequiredBankCount);
        var bank=(gold?generalSelection.Contract.Actions.Where(a=>a.Command.Option=="card"):generalSelection.BankOptions).GroupBy(a=>a.Command.CardType).Select(g=>g.First()).ToArray();
        for(int i=0;i<bank.Length;i++)
        {
            var action=bank[i];int type=action.Command.CardType;float step=Mathf.Min(120,880f/Mathf.Max(1,bank.Length));
            float center=area.center.x+(i-(bank.Length-1)*.5f)*step;
            var face=new Rect(center-32,area.y+40,64,94);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(type));
            int input=ThreeKingdomsDirectInput.Read(face,GeneralCanInteract&&!generalSelection.IsSubmitting);
            if(gold){if(input==1){SubmitGeneralSkill(action.Command);return;}continue;}
            if(input==1&&generalSelection.ToggleBankCard(type))FinishGeneralPick();
            if(input==2&&GeneralCanInteract&&!generalSelection.IsSubmitting)generalSelection.ToggleBankCard(type,-1);
            int amount=generalSelection.SelectedBankCounts[type];
            if(amount>0)PastoralMenuUI.Label(new Rect(face.xMax,face.center.y-15,32,30),"×"+amount,16,PastoralMenuUI.Ivory,true,TextAnchor.MiddleLeft);
        }
    }
    string OptionCaption(B2SkillAction a)
    {
        switch(a.Command.Option)
        {
            case "fixed":if(a.Command.SkillId=="tk-g20-s1")return "发动祸首";break;
            case "b5m-zaiqi-city":return "有人城市倒塌 → 升级一座城市";
            case "b5m-zaiqi-point":return "有人得分 → 失去1分";
            case "b5m-zaiqi-card":return "有人失牌 → 摸一张资源或商品";
            case "b5m-zaiqi-double":return "己方地块被掠夺 → 该地块双倍产出";
            case "gift":return "赠予手牌 · 点牌和头像";
            case "exchange-hand":return "弃换 1–4 张手牌";
            case "exchange-progress":return "同色换发展牌";
            case "response-guose":return "两张砖 · 点目标头像";
            case "response-liuli":return "一张麦 · 点目标地块";
        }
        // 文字仅用于显示；选择条件全部取自结构化合同。
        return DisplayPlayerText(a.Label);
    }
    internal bool GeneralPlayerTarget(string player)=>generalSelection.Active&&generalSelection.CanSelectPlayer(player)&&GeneralCanInteract;
    internal void DrawThreeKingdomsAvatar(string player,Rect area)
    {
        var view=ThreeKingdomsProjection;if(view==null)return;
        bool selectable=generalSelection.CanSelectPlayer(player)&&GeneralCanInteract;int ordinal=Array.IndexOf(generalSelection.SelectedPlayers,player);
        if(selectable||ordinal>=0)GeneralFrameUI.DrawTargetGlow(area);
        if(ordinal>=0)PastoralMenuUI.Label(new Rect(area.xMax-23,area.y+1,22,22),(ordinal+1).ToString(),15,PastoralMenuUI.Ivory,true);
        if(generalSelection.Active&&GeneralCanInteract)
        {
            int input=ThreeKingdomsDirectInput.Read(area,selectable);
            if(input==1)PickGeneralPlayer(player);
            if(input==2){var p=view.Players.FirstOrDefault(x=>x.PlayerId==player);Inspect(PlayerDisplayName(player)+" · "+GeneralName(p?.ActiveGeneralId),area);}
        }
        if(view.SkillDecision?.PlayerId==player||view.WaitingSelectionPlayerIds?.Contains(player)==true)
            PastoralMenuUI.Label(new Rect(area.x,area.yMax-16,area.width,16),"等待选择",11,PastoralMenuUI.Muted,false,TextAnchor.MiddleCenter);
    }
    static void Outline(Rect r,Color c,float width)
    {TabletopUI.Fill(new Rect(r.x,r.y,r.width,width),c);TabletopUI.Fill(new Rect(r.x,r.yMax-width,r.width,width),c);TabletopUI.Fill(new Rect(r.x,r.y,width,r.height),c);TabletopUI.Fill(new Rect(r.xMax-width,r.y,width,r.height),c);}
    internal string VerificationMarkerId;
    internal Rect? VerificationMarkerArea;
    void DrawKurouIncome(Rect area,string id)
    {
        if(generalSelection.SkillId!="tk-g16-s1")return;
        var count=generalSelection.CandidateActions.FirstOrDefault(a=>a.SelectionBoardIds?.Contains(id)==true)?.RefundCardCount;
        if(!count.HasValue)return;
        string kind=id.Split(':')[0];
        var label=kind=="Improvement"?new Rect(area.center.x-17,area.yMax-16,34,16):area;
        if(kind!="Improvement")label=new Rect(area.center.x-18,area.y-9,36,20);
        var shadow=label;shadow.position+=Vector2.one;
        PastoralMenuUI.Label(shadow,"+"+count.Value,13,new Color(.17f,.095f,.035f),true,TextAnchor.MiddleCenter);
        PastoralMenuUI.Label(label,"+"+count.Value,13,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
    }
    internal bool DrawGeneralFacilityTarget(Rect area,string player,int track)
    {
        string id="Improvement:"+player+":"+track;
        if(!generalSelection.Active||!generalSelection.CandidateActions.Any(a=>B45SelectionContract.Board(a).Contains(id)))return false;
        Outline(area,generalSelection.SelectedTiles.Contains(id)?new Color(1,.81f,.28f):new Color(.48f,.89f,.62f),2);
        DrawKurouIncome(area,id);
        if(ThreeKingdomsDirectInput.Read(area,GeneralCanInteract&&generalSelection.CanSelectTile(id))==1)PickGeneralBoardTarget(id);
        return true;
    }
    int pieceTargetRevision=-1;
    string[] pieceTargets=Array.Empty<string>();
    internal bool GeneralTargetSelected(string id)=>generalSelection.SelectedTiles.Contains(id);
    internal string[] GeneralPieceTargets()
    {
        if(!generalSelection.Active)return Array.Empty<string>();
        if(generalSelection.Revision!=pieceTargetRevision){
            pieceTargetRevision=generalSelection.Revision;
            pieceTargets=generalSelection.CandidateActions.SelectMany(B45SelectionContract.Board).Distinct()
                .Where(id=>generalSelection.CanSelectTile(id)).ToArray();
        }
        return pieceTargets;
    }
    void DrawGeneralBoardTargets()
    {
        if(!generalSelection.Active)return;
        Func<string,Vector2> screenPoint=null;
        if(presentation is M4App a)screenPoint=a.VerificationBoard.ScreenPoint;else if(presentation is M5App b)screenPoint=b.VerificationBoard.ScreenPoint;
        if(screenPoint==null)return;
        var targets=generalSelection.CandidateActions.SelectMany(B45SelectionContract.Board).Distinct().OrderBy(id=>id,StringComparer.Ordinal)
            .Where(id=>!id.StartsWith("Improvement:",StringComparison.Ordinal))
            .Select(id=>new {Id=id,Point=MobileTabletop.Logical(screenPoint(B45SelectionContract.BoardLocationId(id)))}).ToArray();
        foreach(var target in targets)
        {
            var id=target.Id;var point=target.Point;
            if(!TabletopScene.BoardArea.Contains(point)||ThreeKingdomsBlocksInput(point))continue;
            if(presentation is M4App cities&&cities.GeneralBoardCovered(point)||presentation is M5App combined&&combined.GeneralBoardCovered(point))continue;
            if(!generalSelection.CanSelectTile(id))continue;
            var nearby=targets.Where(t=>(t.Point-point).sqrMagnitude<1600).ToArray();int local=Array.FindIndex(nearby,t=>t.Id==id);
            // 确有重叠时在对象附近展开带编号的小标记，不把一次点击交给任意“最近对象”。
            bool income=generalSelection.SkillId=="tk-g16-s1";
            float size=income?44:nearby.Length>1?28:40;
            if(nearby.Length>1){point=nearby.Aggregate(Vector2.zero,(sum,t)=>sum+t.Point)/nearby.Length;point.x+=(local-(nearby.Length-1)/2f)*(income?46:32);}
            var area=new Rect(point.x-size/2,point.y-size/2,size,size);int number=Array.IndexOf(generalSelection.SelectedTiles,id);
            bool legal=GeneralCanInteract;
            bool contour=presentation is M4App m4&&m4.VerificationBoard.HasOutlinePiece(id)||presentation is M5App m5&&m5.VerificationBoard.HasOutlinePiece(id);
            var location=B45SelectionContract.BoardLocationId(id);
            bool tile=!contour&&(presentation is M4App c4&&c4.VerificationBoard.DrawTileSelection(location,number>=0)||presentation is M5App c5&&c5.VerificationBoard.DrawTileSelection(location,number>=0));
            if(!tile&&!contour)ProgressTableUI.Mark(point,number>=0,null);
            if(!income&&!tile&&(number>=0||nearby.Length>1))PastoralMenuUI.Label(area,number>=0?"✓"+(number+1):(local+1).ToString(),15,PastoralMenuUI.Ivory,true,TextAnchor.MiddleCenter);
            DrawKurouIncome(area,id);
            if(id==VerificationMarkerId)VerificationMarkerArea=area;
            int input=ThreeKingdomsDirectInput.Read(area,legal);
            if(input==1)PickGeneralBoardTarget(id);
            if(input==2)Inspect("棋盘对象 "+id+"\n"+generalSelection.Description,area);
        }
    }
    static void DrawGeneralCard(Rect r,GeneralDefinition general,bool selected=false)
    {
        CopperTabletopSkin.Panel(r,ModernOperationUI.Surface,selected);
        float portraitHeight=r.height*.42f;
        GeneralFrameUI.Portrait(new Rect(r.center.x-portraitHeight*.4f,r.y+12,portraitHeight*.8f,portraitHeight),general.Id,general.Name,general.Faction,-1,false,false,-1);
        var description=string.Join("\n",general.Skills.Select(skill=>skill.Name+"："+skill.OriginalText));
        PastoralMenuUI.Label(new Rect(r.x+10,r.y+portraitHeight+20,r.width-20,r.height-portraitHeight-32),description,14,PastoralMenuUI.Ivory,false,TextAnchor.MiddleCenter);
        if(selected)PastoralMenuUI.Label(new Rect(r.xMax-30,r.y+8,22,24),"✓",20,null,true,TextAnchor.MiddleCenter);
    }
    string[] LibraryIds(ThreeKingdomsView view)
    {
        var own=view.Players.FirstOrDefault(p=>p.PlayerId==GeneralAssetSeatB6);
        return (own?.HiddenGeneralIds??Array.Empty<string>()).Concat(string.IsNullOrEmpty(own?.ActiveGeneralId)?Array.Empty<string>():new[]{own.ActiveGeneralId}).Distinct().ToArray();
    }
    internal int GeneralLibraryCount => LibraryIds(ThreeKingdomsProjection).Length;
    void DrawGeneralLibrary(ThreeKingdomsView view)
    {
        var ids=LibraryIds(view);
        for(int i=0;i<ids.Length;i++)
        {
            var general=GeneralCatalog.Get(ids[i]);var card=new Rect(LibraryArea.x+i*120,714,108,151);
            GeneralFrameUI.Portrait(card,general.Id,general.Name,general.Faction,-1,false,false,-1);
            if(ThreeKingdomsDirectInput.Read(card,true)==1)detailGeneralId=general.Id;
        }
    }
    internal bool DrawPlayerGeneralCard(string player,Rect area,bool inspectable,Rect? header=null)
    {
        var visible=ThreeKingdomsProjection?.Players.FirstOrDefault(p=>p.PlayerId==player);
        if(string.IsNullOrEmpty(visible?.ActiveGeneralId))return false;
        var general=GeneralCatalog.Get(visible.ActiveGeneralId);
        var avatar=header??new Rect(area.x+2,area.y-2,34,40);
        GeneralFrameUI.Avatar(avatar,general.Id,visible.FaceDown);
        var caption=header.HasValue?area:new Rect(area.x+38,area.y+2,area.width-40,area.height-4);
        PastoralMenuUI.Label(caption,general.Name,13,StrategyHudTheme.HandPainted?StrategyHudTheme.Text:(Color?)null,false,TextAnchor.MiddleCenter);
        if(inspectable && ThreeKingdomsDirectInput.Read(area,GUI.enabled)==1)detailGeneralId=general.Id;
        return true;
    }
    void DrawGeneralDetail()
    {
        DrawGeneralCard(GeneralDetailArea,GeneralCatalog.Get(detailGeneralId));
        var skin=GeneralSkinArt.Get(detailGeneralId);
        if(skin!=null&&GeneralFrameUI.Choice(new Rect(GeneralDetailArea.x+10,GeneralDetailArea.y+10,94,32),
            skin.useStatic?"切换动皮":"切换静皮"))
            GeneralSkinArt.SetStatic(skin,!skin.useStatic);
        if(GeneralFrameUI.Choice(new Rect(GeneralDetailArea.xMax-42,GeneralDetailArea.y+8,32,28),"×"))detailGeneralId=null;
    }
    void DrawGeneralInspection()
    {
        var r=InspectionRect;TabletopUI.Fill(r,new Color(.035f,.04f,.035f,.64f));
        var close=new Rect(r.xMax-37,r.y+5,30,27);
        ModernOperationUI.DrawWindowSymbol(close,"×",Color.white);
        if(ThreeKingdomsDirectInput.Read(close,true,false)==1){inspectionText=null;return;}
        var style=new GUIStyle(GUI.skin.label){font=font,fontSize=14,wordWrap=true};style.normal.textColor=PastoralMenuUI.Ivory;
        float h=style.CalcHeight(new GUIContent(inspectionText),r.width-42);
        inspectionScroll=GUI.BeginScrollView(new Rect(r.x+12,r.y+34,r.width-24,r.height-44),inspectionScroll,new Rect(0,0,r.width-42,Mathf.Max(r.height-44,h)));
        GUI.Label(new Rect(0,0,r.width-42,Mathf.Max(r.height-44,h)),inspectionText,style);GUI.EndScrollView();
    }
    static B2SkillCommand CopyGeneralSkill(B2SkillCommand source)=>B45SelectionContract.Copy(source);
    void SubmitGeneralSkill(B2SkillCommand source)
    {
        if(source==null||!GeneralCanSubmit){generalSelection.AcknowledgeFailure();return;}
        var skill=CopyGeneralSkill(source);string id=Guid.NewGuid().ToString("N");bool accepted=false;
        if(presentation is M4App a){var result=a.Submit(new Catan.Core.M4.Command{Id=id,PlayerId=GeneralAssetSeatB6,Kind=Catan.Core.M4.CommandKind.UseGeneralSkill,SkillCommand=skill,DecisionId=skill.DecisionId,WindowId=skill.WindowId??ThreeKingdomsProjection.WindowId});accepted=result?.Success==true;}
        else if(presentation is M5App b){var result=b.Submit(new Catan.Core.M5.Command{Id=id,PlayerId=GeneralAssetSeatB6,Kind=Catan.Core.M5.CommandKind.UseGeneralSkill,SkillCommand=skill,DecisionId=skill.DecisionId,WindowId=skill.WindowId??ThreeKingdomsProjection.WindowId});accepted=result?.Success==true;}
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
        if(accepted)selectionView=null;
    }
}
