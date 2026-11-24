using System;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

public sealed partial class M6App
{
    bool AllianceDecision=>generalSelection.Contract?.DecisionKind?.StartsWith("alliance-b6-",StringComparison.Ordinal)==true;
    static Rect AllianceArea=>new Rect(CandidateArea.x,CandidateArea.yMax-222,CandidateArea.width,222);

    void DrawAllianceDecision()
    {
        var r=AllianceArea;
        var decision=ThreeKingdomsProjection.SkillDecision;
        var actions=generalSelection.Contract.Actions;
        bool terms=decision.Kind=="alliance-b6-terms";
        bool confirm=decision.Kind=="alliance-b6-confirm";
        var cards=actions.Where(a=>a.Command.Option=="alliance-b6-card").ToArray();
        var buttons=actions.Where(a=>a.Command.Option!="alliance-b6-card").ToArray();
        var target=buttons.FirstOrDefault()?.SelectionPlayerIds?.FirstOrDefault();
        string title=decision.Title;
        if(decision.Kind=="alliance-b6-accept"&&target!=null)title=PlayerDisplayName(target)+"请求结盟";
        TabletopResourceChoiceUI.DrawFrame(r,title,0,0);
        if(terms||confirm)
            GeneralFrameUI.Text(new Rect(r.x+12,r.y+45,r.width-24,30),DisplayPlayerText(decision.Description),18);
        bool ready=GeneralCanSubmit&&!generalSelection.IsSubmitting;
        // 逐张添加权威允许的条件。按钮留在固定的一行，选满八种牌也能立即提出或撤回。
        for(int i=0;i<cards.Length;i++)
        {
            var c=cards[i].Command;
            var face=new Rect(r.center.x+(i-(cards.Length-1)*.5f)*91-32,r.y+78,64,89);
            EntityCardAssets.DrawCardFace(face,EntityCardAssets.ResourceOrCommodityFront(c.CardType));
            if(ThreeKingdomsDirectInput.Read(face,ready,false)==1){SubmitGeneralSkill(c);return;}
        }
        float width=Mathf.Min(206,(r.width-40)/Math.Max(1,buttons.Length)-10);
        for(int i=0;i<buttons.Length;i++)
        {
            var action=buttons[i];
            var area=new Rect(r.center.x+(i-(buttons.Length-1)*.5f)*(width+10)-width/2,terms?r.yMax-45:r.center.y-8,width,37);
            if(GeneralFrameUI.Choice(area,action.Label,ready)){SubmitGeneralSkill(action.Command);return;}
        }
    }
    internal static bool SharesAllianceAssetsB6(ThreeKingdomsView view,string seat,string owner) =>
        seat==owner||view?.PackageVersion==GeneralCatalog.B6PackageVersion&&view.AllianceMembers?.Contains(owner)==true;
    internal static string AllianceVictoryTextB6(ThreeKingdomsView view,string winner)
    {
        var winners=view?.AllianceWinnerPlayerIds;
        return winners!=null&&winners.Length>1?string.Join("、",winners.Select(PlayerLabel))+" 共同胜利":PlayerLabel(winner)+" 获胜";
    }
    bool HasAllianceB6 => ThreeKingdomsProjection?.PackageVersion==GeneralCatalog.B6PackageVersion&&
        (ThreeKingdomsProjection.AllianceMembers?.Length>1||ThreeKingdomsProjection.Players.Any(p=>p.ActiveGeneralId=="tk-g50"));
    internal string AlliancePlayerCaptionB6(string player)
    {
        var view=ThreeKingdomsProjection;
        if(view?.AllianceWinnerPlayerIds?.Contains(player)==true)return view.AllianceWinnerPlayerIds.Length>1?"共同获胜":"获胜";
        if(!HasAllianceB6)return null;
        if(view.AllianceMembers?.Contains(player)!=true)return "其他阵营";
        return view.AllianceMembers.Length>1?"同盟":"独立阵营";
    }
    internal string AlliancePlayerHintB6(string player)
    {
        var view=ThreeKingdomsProjection;
        if(view?.AllianceWinnerPlayerIds?.Contains(player)==true)return AllianceVictoryTextB6(view,player);
        if(view?.AllianceMembers?.Contains(player)!=true)return "不同阵营之间不能交易";
        if(view.AllianceMembers.Length==1)return "独立阵营；不同阵营之间不能交易";
        return "本方："+string.Join("、",view.AllianceMembers.Select(PlayerLabel))+"\n共享港口、科技与骑士；成员共同胜利";
    }
    void DrawAllianceVictoryB6()
    {
        var view=ThreeKingdomsProjection;if(!Finished||view?.AllianceWinnerPlayerIds?.Length<1||view?.AllianceWinnerPlayerIds==null)return;
        var style=new GUIStyle(title){fontSize=40,alignment=TextAnchor.MiddleCenter,wordWrap=true};style.normal.textColor=PromptColor;
        string text=AllianceVictoryTextB6(view,view.AllianceWinnerPlayerIds[0]);var area=new Rect(190,345,1060,145);
        var shadow=new GUIStyle(style);shadow.normal.textColor=new Color(.10f,.025f,.01f,.55f);
        GUI.Label(new Rect(area.x+1,area.y+2,area.width,area.height),text,shadow);GUI.Label(area,text,style);
    }
}
