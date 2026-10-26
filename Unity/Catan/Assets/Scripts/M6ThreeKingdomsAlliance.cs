using System;
using System.Linq;
using Catan.Core.ThreeKingdoms;
using UnityEngine;

public sealed partial class M6App
{
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
