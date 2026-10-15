using System;
using System.Linq;
using Catan.Core;
using Catan.Core.M3;
using UnityEngine;
using CommandKind=Catan.Core.M3.CommandKind;
using GamePhase=Catan.Core.M3.GamePhase;

public sealed partial class M3App
{
    void DrawMobileSailingDecision()
    {
        if(view.Phase==GamePhase.SetupPort)
        {
            TableNote("新世界 · 开局港口",()=>
            {
                var port=view.NextSetupPort;
                ModernOperationUI.Info("当前港口："+(port.Resource.HasValue?ResourceNames[(int)port.Resource.Value]+" 2:1":"通用 3:1")+"。点击合法沿海空边，港口之间需保持间隔。");
                if(ModernOperationUI.LayoutButton("拿起当前开局港口",true,true))Pickup(CommandKind.SetupPort);
            });return;
        }
        if(view.Phase==GamePhase.GoldChoice)
        {
            var claim=view.GoldClaims.FirstOrDefault(c=>c.PlayerId==seat);if(claim==null)return;
            TableNote("金矿资源 · 已选 "+gold.Total+" / "+Math.Min(claim.Amount,view.Bank.Total),()=>
            {
                for(int i=0;i<5;i++)
                {
                    var good=(Resource)i;GUILayout.BeginHorizontal();
                    GUILayout.Label(ResourceNames[i]+"  "+gold[good],ModernOperationUI.Text,GUILayout.Width(340),GUILayout.Height(86));
                    if(ModernOperationUI.LayoutButton("−",gold[good]>0))gold[good]--;
                    if(ModernOperationUI.LayoutButton("+",gold[good]<view.Bank[good]&&gold.Total<claim.Amount))gold[good]++;
                    GUILayout.EndHorizontal();
                }
                if(ModernOperationUI.LayoutButton("确认领取金矿资源",gold.Total==Math.Min(claim.Amount,view.Bank.Total),true))
                {var command=NewCommand(CommandKind.ChooseGoldResources);command.Resources=gold.Copy();Submit(command);gold=new ResourceBag();}
            });return;
        }
        TableNote("安置持有港口",()=>
        {
            if(view.OwnHeldPorts.Length==0)return;
            specialSelection%=view.OwnHeldPorts.Length;var port=view.OwnHeldPorts[specialSelection];
            if(ModernOperationUI.LayoutButton("选择："+(port.Resource.HasValue?ResourceNames[(int)port.Resource.Value]+" 2:1":"通用 3:1")))specialSelection=(specialSelection+1)%view.OwnHeldPorts.Length;
            ModernOperationUI.Info("选择后，点击连接己方建筑的合法海岸边放置。");
            if(ModernOperationUI.LayoutButton("拿起此港口",true,true)){portSource=port.Id;Pickup(CommandKind.PlacePort);}
        });
    }
    void DrawMobileSailingActions()
    {
        TableNote("航海与剧本行动",()=>
        {
            var own=view.Players.First(p=>p.Id==seat);
            if(ModernOperationUI.LayoutButton("移动开放航线的船")){tableNotes=false;Pickup(CommandKind.MoveShip);}
            ModernOperationUI.Info("每回合至多移动 1 船；本回合新造的船不能移动。");
            if(view.OwnHeldPorts.Length>0&&ModernOperationUI.LayoutButton("安置持有港口")){portSource=view.OwnHeldPorts[0].Id;tableNotes=false;Pickup(CommandKind.PlacePort);}
            var fortress=view.Board.Fortresses.FirstOrDefault(f=>f.PlayerId==seat&&f.Strength>0);
            if(fortress!=null)
            {
                if(ModernOperationUI.LayoutButton("攻击己方海盗堡垒 · "+fortress.Strength))Submit(NewCommand(CommandKind.AttackFortress,fortress.VertexId));
                if(view.LegalInvasionRootVertexIds.Length>0&&ModernOperationUI.LayoutButton("选择西行进攻航线起点")){tableNotes=false;Pickup(CommandKind.ChooseInvasionRoute);}
            }
            if(view.Wonders.Length>0)
            {
                specialSelection%=view.Wonders.Length;var wonder=view.Wonders[specialSelection];
                if(ModernOperationUI.LayoutButton("奇观："+WonderName(wonder.Id)))specialSelection=(specialSelection+1)%view.Wonders.Length;
                ModernOperationUI.Info("每层："+BagText(wonder.Cost)+"；条件："+WonderRequirement(wonder.Requirement));
                if(ModernOperationUI.LayoutButton("认领此奇观"))Submit(NewCommand(CommandKind.ClaimWonder,wonder.Id));
                if(ModernOperationUI.LayoutButton("建造奇观一层"))Submit(NewCommand(CommandKind.BuildWonder,wonder.Id));
            }
            ModernOperationUI.Info("额外分 "+own.BonusVictoryPoints+" · 布匹 "+own.Cloth+" · 奇观层数 "+own.WonderLevel);
            if(ModernOperationUI.LayoutButton("宣告胜利"))Submit(NewCommand(CommandKind.DeclareVictory));
            if(ModernOperationUI.LayoutButton("关闭行动面板"))tableNotes=false;
        });
    }
}
