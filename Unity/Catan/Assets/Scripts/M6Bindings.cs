using System;
using System.IO;
using System.Linq;
using Catan.AI;

// Strongly typed bridges keep private AI views local to the policy call.
public sealed partial class M6App
{
    public void Connect(M2App app,M2LocalGameHost host)
    {
        var policy=new BaseGameAi();
        Bind(app,()=>Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M2.GamePhase.Finished,player=>
        {
            var command=policy.Decide(host.View(player));
            if(command==null)return "NoDecision";
            if(command.PlayerId!=player)return "WrongSeat";
            command.Id=Guid.NewGuid().ToString("N");
            LastCommandKind=command.Kind.ToString();
            var result=host.Submit(command);
            return result.Success?null:result.ErrorCode;
        },host.ExportSave,app.M6Refresh,app.M6Close,app.M6Visible);
    }
    public void Connect(M3App app,M3LocalGameHost host)
    {
        var policy=new SeafarersAi();
        Bind(app,()=>Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M3.GamePhase.Finished,player=>
        {
            var command=policy.Decide(host.View(player));
            if(command==null)return "NoDecision";
            if(command.PlayerId!=player)return "WrongSeat";
            command.Id=Guid.NewGuid().ToString("N");
            LastCommandKind=command.Kind.ToString();
            var result=host.Submit(command);
            return result.Success?null:result.ErrorCode;
        },host.ExportSave,app.M6Refresh,app.M6Close,app.M6Visible);
    }
    public void Connect(M4App app,M4LocalGameHost host)
    {
        var policy=new CitiesKnightsAi();
        Bind(app,()=>M4App.Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M4.GamePhase.Finished,player=>
        {
            var command=policy.Decide(host.View(player));
            if(command==null)return "NoDecision";
            if(command.PlayerId!=player)return "WrongSeat";
            command.Id=Guid.NewGuid().ToString("N");
            LastCommandKind=command.Kind.ToString();
            var result=host.Submit(command);
            return result.Success?null:result.ErrorCode;
        },host.ExportSave,app.M6Refresh,app.M6Close,app.M6Visible);
    }
    public void Connect(M5App app,M5LocalGameHost host)
    {
        var policy=new CombinedAi();
        Bind(app,()=>M5App.Actor(app.M6View),()=>app.M6View.Phase==Catan.Core.M5.GamePhase.Finished,player=>
        {
            var command=policy.Decide(host.View(player));
            if(command==null)return "NoDecision";
            if(command.PlayerId!=player)return "WrongSeat";
            command.Id=Guid.NewGuid().ToString("N");
            LastCommandKind=command.Kind.ToString();
            var result=host.Submit(command);
            return result.Success?null:result.ErrorCode;
        },host.ExportSave,app.M6Refresh,app.M6Close,app.M6Visible);
    }
    static string Actor(Catan.Core.M2.PlayerView view)
    {
        if(view.TradeOffer!=null)return view.TradeOffer.OtherPlayerId;
        if(view.Discards.Length>0)return view.Discards[0].PlayerId;
        return view.PendingDecision!=null&&!string.IsNullOrEmpty(view.PendingDecision.PlayerId)?view.PendingDecision.PlayerId:view.ActivePlayerId;
    }
    static string Actor(Catan.Core.M3.PlayerView view)
    {
        if(view.TradeOffer!=null)return view.TradeOffer.OtherPlayerId;
        if(view.Discards.Length>0)return view.Discards[0].PlayerId;
        if(view.Phase==Catan.Core.M3.GamePhase.GoldChoice&&view.GoldClaims.Length>0)return view.GoldClaims[0].PlayerId;
        return view.PendingDecision!=null&&!string.IsNullOrEmpty(view.PendingDecision.PlayerId)?view.PendingDecision.PlayerId:view.ActivePlayerId;
    }
    static void ValidateAuthority(SavedGame data)
    {
        int players;string scenario=data.ScenarioId;
        switch(data.RuleSet)
        {
            case "base":
                var baseHost=new M2LocalGameHost(data.PlayerCount);baseHost.ImportSave(data.Authority);
                players=baseHost.View(data.HumanSeat).Players.Length;break;
            case "seafarers":
                var seaHost=new M3LocalGameHost(data.PlayerCount);seaHost.ImportSave(data.Authority);
                var seaView=seaHost.View(data.HumanSeat);players=seaView.Players.Length;scenario=seaView.ScenarioId;break;
            case "cities-knights":
                var cityHost=new M4LocalGameHost(data.PlayerCount);cityHost.ImportSave(data.Authority);
                players=cityHost.View(data.HumanSeat).Players.Length;break;
            default:
                var combinedHost=new M5LocalGameHost(data.PlayerCount);combinedHost.ImportSave(data.Authority);
                var combinedView=combinedHost.View(data.HumanSeat);players=combinedView.Players.Length;scenario=combinedView.ScenarioId;break;
        }
        if(players!=data.PlayerCount||scenario!=data.ScenarioId)throw new InvalidDataException("M6 configuration does not match authority");
    }
}
