using Catan.Core;
using Xunit;
using M3=Catan.Core.M3;
using M5=Catan.Core.M5;

namespace Catan.Rules.Tests;

// 来源：catan-seafarers-2025-en.pdf，第2页，建造船与移动船。
public sealed class PirateShipBlockingTests
{
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)]
    [InlineData(true,false)] [InlineData(true,true)]
    public void AllSixEdgesRejectPaidAndFreeShipsAndReopenWhenThePirateLeaves(bool combined,bool free)
    {
        var board=M3Harness.New().GetPlayerView("P1").Board;
        var sea=board.Tiles.First(t=>t.Resource=="sea"&&board.Edges.Count(e=>e.Vertices.All(t.Vertices.Contains))==6);
        var edges=board.Edges.Where(e=>e.Vertices.All(sea.Vertices.Contains)).ToArray();
        foreach(var target in edges)
        {
            var existing=edges.Where(e=>e.Id!=target.Id).Select(e=>e.Id).ToArray();
            var blocked=Create(combined,sea.Id,existing,free?"free":"build");
            AssertBlocked(blocked,target.Id);
            var clear=Create(combined,"frame",existing,free?"free":"build");
            // 组合版免费船的落点通过 Preview 提供，常规 LegalShipEdgeIds 仅用于付费／开局船。
            if(!combined||!free)Assert.Contains(target.Id,clear.Legal());Assert.True(clear.Preview(target.Id,null).Success);
            Assert.True(clear.Execute(target.Id,null).Success);clear.Invariants();
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PirateBlocksBothMovingAwayAndMovingOntoItsHexWithoutChangingShipsOrSupply(bool combined)
    {
        var board=M3Harness.New().GetPlayerView("P1").Board;
        var pair=(from sea in board.Tiles where sea.Resource=="sea"
                  from source in board.Edges where source.Vertices.All(sea.Vertices.Contains)
                  from destination in board.Edges where destination.Vertices.Intersect(sea.Vertices).Count()==1
                  let root=source.Vertices.Intersect(destination.Vertices).FirstOrDefault()
                  where root!=null&&board.Tiles.Any(t=>t.Resource!="sea"&&t.Vertices.Contains(root))
                  select(sea,source,destination,root)).First();
        var away=Create(combined,pair.sea.Id,[pair.source.Id],"move",pair.root);
        Assert.DoesNotContain(pair.source.Id,away.Movable());AssertBlocked(away,pair.destination.Id,pair.source.Id);
        var into=Create(combined,pair.sea.Id,[pair.destination.Id],"move",pair.root);
        Assert.Contains(pair.destination.Id,into.Movable());AssertBlocked(into,pair.source.Id,pair.destination.Id);
        var clear=Create(combined,"frame",[pair.destination.Id],"move",pair.root);
        Assert.True(clear.Execute(pair.source.Id,pair.destination.Id).Success);clear.Invariants();
    }
    static void AssertBlocked(Probe probe,string target,string? source=null)
    {
        string before=probe.Save();var preview=probe.Preview(target,source);
        Assert.False(preview.Success);Assert.Equal("PirateBlocksShip",preview.Error);Assert.Equal(before,probe.Save());
        var result=probe.Execute(target,source);Assert.False(result.Success);Assert.Equal("PirateBlocksShip",result.Error);
        Assert.Equal(before,probe.Save());if(source==null)Assert.DoesNotContain(target,probe.Legal());probe.Invariants();
    }
    sealed record Probe(Func<string> Save,Func<string,string?,(bool Success,string Error)> Preview,Func<string,string?,(bool Success,string Error)> Execute,Func<string[]> Legal,Func<string[]> Movable,Action Invariants);
    static Probe Create(bool combined,string pirate,string[] ships,string mode,string? root=null)
    {
        if(combined)
        {
            var game=M5Harness.Fixture(s=>
            {
                s.FirstBarbarianAttack=true;s.RobberTileId=s.Tiles.First(t=>t.Resource=="desert").Id;s.PirateTileId=pirate;
                s.Ships=ships.Select(id=>new M5.ShipPlacement{PlayerId="P1",LocationId=id,BuiltTurn=1}).ToArray();
                if(root!=null)s.Settlements=[M5Harness.Piece(root)];
                M5Harness.Player(s).Resources=M5Harness.Bag(wood:2,wool:2);
                if(mode=="free"){s.Phase=M5.GamePhase.PendingChoice;s.DecisionReturnPhase=M5.GamePhase.Action;s.PendingDecision=new(){Kind="ProgressRoads",PlayerId="P1",Amount=2,ReturnPhase=M5.GamePhase.Action};}
            });
            return Wrap(game,mode);
        }
        var sailing=M3Harness.Fixture(s=>
        {
            s.PirateTileId=pirate;s.Ships=ships.Select(id=>M3Harness.Ship(id)).ToArray();
            if(root!=null)s.Settlements=[M3Harness.Piece(root)];
            M3Harness.Player(s).Resources=M3Harness.Bag(wood:2,wool:2);
            if(mode=="free"){s.Phase=M3.GamePhase.RoadBuilding;s.PendingDecision=new(){Kind="RoadBuilding",PlayerId="P1",RemainingRoads=2,ReturnPhase=M3.GamePhase.Action};}
        });
        return Wrap(sailing,mode);
    }
    static Probe Wrap(M3.SeafarersGameSession game,string mode)
    {
        M3.Command Command(string target,string? source){var c=M3Harness.Cmd(mode=="free"?M3.CommandKind.PlaceFreeShip:mode=="move"?M3.CommandKind.MoveShip:M3.CommandKind.BuildShip,target:target);c.SourceId=source!;return c;}
        return new(game.Save,(t,s)=>{var r=game.Preview(Command(t,s));return(r.Success,r.ErrorCode);},(t,s)=>{var r=game.Execute(Command(t,s));return(r.Success,r.ErrorCode);},()=>game.GetPlayerView("P1").LegalShipEdgeIds,()=>game.GetPlayerView("P1").MovableShipEdgeIds,()=>M3Harness.Invariants(game));
    }
    static Probe Wrap(M5.CombinedGameSession game,string mode)
    {
        M5.Command Command(string target,string? source){var c=M5Harness.Cmd(mode=="free"?M5.CommandKind.PlaceFreeShip:mode=="move"?M5.CommandKind.MoveShip:M5.CommandKind.BuildShip,target:target);c.SourceId=source!;return c;}
        return new(game.Save,(t,s)=>{var r=game.Preview(Command(t,s));return(r.Success,r.ErrorCode);},(t,s)=>{var r=game.Execute(Command(t,s));return(r.Success,r.ErrorCode);},()=>game.GetPlayerView("P1").LegalShipEdgeIds,()=>game.GetPlayerView("P1").MovableShipEdgeIds,()=>M5Harness.Invariants(game));
    }
}
