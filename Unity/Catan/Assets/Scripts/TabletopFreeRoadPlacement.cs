using System;
using System.Linq;
using UnityEngine;

// 四类规则和联机视图共用地图交互；落子仍通过原来的 Submit 校验与同步。
public sealed partial class M2App
{
    void UpdateFreeRoadPlacement(Vector2 mouse,bool click)
        =>TabletopRoadPlacement.Update(mouse,click,view.Board.Edges,view.Board.Tiles,view.LegalEdgeIds,Array.Empty<string>(),
            board.ScreenPoint,(id,legal)=>board.Preview(id,true,legal),(id,legal)=>board.Preview(id,true,legal),board.ClearPreview,(id,ship)=>Submit(NewCommand(Catan.Core.M2.CommandKind.PlaceFreeRoad,id)));
}
public sealed partial class M3App
{
    void UpdateFreeRoadPlacement(Vector2 mouse,bool click)
        =>TabletopRoadPlacement.Update(mouse,click,view.Board.Edges,view.Board.Tiles,view.LegalEdgeIds,view.LegalShipEdgeIds,
            board.ScreenPoint,(id,legal)=>board.Preview(id,true,legal),board.PreviewShip,board.ClearPreview,
            (id,ship)=>Submit(NewCommand(ship?Catan.Core.M3.CommandKind.PlaceFreeShip:Catan.Core.M3.CommandKind.PlaceFreeRoad,id)));
}
public sealed partial class M4App
{
    bool FreeRoadPlacementActive=>view!=null&&(view.Phase==Catan.Core.M4.GamePhase.RoadBuilding||view.PendingDecision?.Kind=="ProgressRoads");
    string[] FreeRoadIds=>view.PendingDecision?.Kind=="ProgressRoads"?view.LegalActions.Where(c=>c.Kind==Catan.Core.M4.CommandKind.ResolveProgressChoice&&!c.Decline&&c.TargetId!=null).Select(c=>c.TargetId).Distinct().ToArray():view.LegalEdgeIds;
    void UpdateFreeRoadPlacement(Vector2 mouse,bool click)
        =>TabletopRoadPlacement.Update(mouse,click,view.Board.Edges,view.Board.Tiles,FreeRoadIds,Array.Empty<string>(),
            board.ScreenPoint,(id,legal)=>board.Preview(id,true,legal),(id,legal)=>board.Preview(id,true,legal),board.ClearPreview,
            (id,ship)=>Submit(New(view.PendingDecision?.Kind=="ProgressRoads"?Catan.Core.M4.CommandKind.ResolveProgressChoice:Catan.Core.M4.CommandKind.PlaceFreeRoad,id)));
}
public sealed partial class M5App
{
    bool FreeRoadPlacementActive=>view!=null&&(view.Phase==Catan.Core.M5.GamePhase.RoadBuilding||view.PendingDecision?.Kind=="ProgressRoads");
    string[] FreeRoadIds=>view.PendingDecision?.Kind=="ProgressRoads"?view.LegalActions.Where(c=>c.Kind==Catan.Core.M5.CommandKind.ResolveProgressChoice&&!c.Decline&&!c.BuildShip&&c.TargetId!=null).Select(c=>c.TargetId).Distinct().ToArray():view.LegalEdgeIds;
    string[] FreeShipIds=>view.PendingDecision?.Kind=="ProgressRoads"?view.LegalActions.Where(c=>c.Kind==Catan.Core.M5.CommandKind.ResolveProgressChoice&&!c.Decline&&c.BuildShip&&c.TargetId!=null).Select(c=>c.TargetId).Distinct().ToArray():view.LegalShipEdgeIds;
    void UpdateFreeRoadPlacement(Vector2 mouse,bool click)
        =>TabletopRoadPlacement.Update(mouse,click,view.Board.Edges,view.Board.Tiles,FreeRoadIds,FreeShipIds,
            board.ScreenPoint,(id,legal)=>board.Preview(id,true,legal),board.PreviewShip,board.ClearPreview,
            (id,ship)=>{
                var command=New(view.PendingDecision?.Kind=="ProgressRoads"?Catan.Core.M5.CommandKind.ResolveProgressChoice:ship?Catan.Core.M5.CommandKind.PlaceFreeShip:Catan.Core.M5.CommandKind.PlaceFreeRoad,id);
                command.BuildShip=ship;Submit(command);
            });
}
