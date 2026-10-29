using System;
using System.Linq;
using Catan.Core.TradeModules;
using UnityEngine;
using Catan.Core.M3;
using CommandKind=Catan.Core.M3.CommandKind;

public sealed partial class M3App
{
    readonly FishingDock fishingDock=new FishingDock();
    PlayerView fishingView;
    void DrawFishing()
    {
        if(fishingView!=view)
        {
            fishingView=view;
            var choices=view.LegalFishingActions.Where(c=>c.Kind==CommandKind.SpendFish||c.Kind==CommandKind.PassOldBoot||c.Kind==CommandKind.ResolveFishReplacement).Select(c=>new FishingDock.Option
            {
                Kind=c.Kind==CommandKind.PassOldBoot?"boot":c.Kind==CommandKind.ResolveFishReplacement?"replace":"spend",Action=c.FishAction,Ship=c.FishBuildShip,Pirate=c.FishRemovePirate,Target=c.TargetId,Player=c.OtherPlayerId,
                Label=c.Kind==CommandKind.PassOldBoot?"转交旧靴":c.FishAction==FishAction.BuildRoad?(c.FishBuildShip?"建造船":"修建道路"):c.FishAction==FishAction.RemoveRobber?(c.FishRemovePirate?"移出海盗":"移出强盗"):c.FishAction==FishAction.TakeResource?new[]{"木材","砖块","羊毛","小麦","矿石"}[(int)c.Resource]:"捕鱼行动",
                Submit=tokens=> { var command=new Command {Id=Guid.NewGuid().ToString("N"),PlayerId=c.PlayerId,Kind=c.Kind,TargetId=c.TargetId,OtherPlayerId=c.OtherPlayerId,Resource=c.Resource,FishAction=c.FishAction,FishTokenIds=tokens,FishBuildShip=c.FishBuildShip,FishRemovePirate=c.FishRemovePirate,ExpectedEventSequence=c.ExpectedEventSequence,DecisionId=c.DecisionId,};Submit(command); }
            }).ToArray();
            fishingDock.Bind(view.TradeModules?.Fishing,view.PlayerId,choices);
        }
        if(tabletop.HistoryOpen||ai.TablePresentationBlocked)return;
        Func<string,Vector2> position=id=>MobileTabletop.Logical(board.ScreenPoint(id));
        fishingDock.DrawMap(font,position);
        if(dock.HasOverlay||mapChoice.Active||tabletop.ContextVisible) { fishingDock.Reset(); return; }
        fishingDock.Draw(font,ai.IsHumanTurn,false,view.Board.RobberTileId,position,view.Board.PirateTileId);
    }
}
