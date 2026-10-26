using System;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M5
{
    public sealed partial class Command
    {
        [DataMember(Order=320,EmitDefaultValue=false)] public string ActingAsPlayerId { get; set; }
    }
    public sealed partial class PlayerView
    {
        [DataMember(EmitDefaultValue=false)] public string ViewerPlayerId { get; internal set; }
    }
    public sealed partial class CombinedGameSession
    {
        public const string ThreeKingdomsB6RulesVersion = "catan-combined-three-kingdoms-b6-v1";
        private Command AuthorizeControlB6(GameState s,Command command)
        {
            var asset=command.ActingAsPlayerId??command.PlayerId;
            Require(B2SkillEngine.CanOperateB6(s.ThreeKingdoms,command.PlayerId,asset),"ControlPermission","当前没有此席位的操作权。");
            if(command.ActingAsPlayerId==null)return command;
            var effective=Json.Copy(command); effective.PlayerId=asset; effective.ActingAsPlayerId=null; return effective;
        }
        public PlayerView GetPlayerView(string playerId)
        {
            lock(gate)
            {
                var view=GetAssetViewB6(playerId);var tk=state.ThreeKingdoms;
                if(!B2SkillEngine.B6Enabled(tk))return view;
                var grant=tk.ControlB6;
                if(grant!=null && grant.ControllerPlayerId==playerId && grant.AssetPlayerId!=playerId && !HasOwnDecisionB6(playerId))
                    view=GetAssetViewB6(grant.AssetPlayerId);
                view.ViewerPlayerId=playerId;
                var allowed=B2SkillEngine.CanOperateB6(tk,playerId,view.PlayerId);
                if(!allowed)
                {
                    view.LegalActions=Array.Empty<Command>();view.LegalVertexIds=Array.Empty<string>();view.LegalCityVertexIds=Array.Empty<string>();view.LegalEdgeIds=Array.Empty<string>();
                    if(view.ThreeKingdoms!=null){view.ThreeKingdoms.CanRetire=false;view.ThreeKingdoms.SkillActions=Array.Empty<B2SkillAction>();}
                }
                else foreach(var c in view.LegalActions)StampControlB6(view,c);
                if(view.ThreeKingdoms!=null){view.ThreeKingdoms.ControlB6=B2SkillEngine.CloneControlB6(grant);view.ThreeKingdoms.ViewerPlayerId=playerId;}
                return view;
            }
        }
        private bool HasOwnDecisionB6(string player)
        {
            var tk=state.ThreeKingdoms;
            return tk?.Skills?.PendingDecision?.PlayerId==player || tk?.Dice?.PendingDecision?.PlayerId==player
                || tk?.Automation?.PendingDecision?.PlayerId==player || ThreeKingdomsEngine.GetPendingDecision(tk,player)!=null
                || state.PendingDecision?.PlayerId==player || state.Discards.Any(d=>d.PlayerId==player) || state.TradeOffer?.CanRespond(player)==true;
        }
        // 客户端与AI只根据已经授权的投影填充代理身份；权威入口再次核验期限。
        public static Command StampControlB6(PlayerView view,Command c)
        {
            if(c==null||view?.ViewerPlayerId==null)return c;
            if(view.PlayerId!=view.ViewerPlayerId && (c.PlayerId==view.PlayerId||c.PlayerId==view.ViewerPlayerId))
            {c.PlayerId=view.ViewerPlayerId;c.ActingAsPlayerId=view.PlayerId;}
            return c;
        }
        private static void ValidateB6Trade(GameState s,Command c)
        {
            if(!B2SkillEngine.B6Enabled(s.ThreeKingdoms))return;
            if(c.Kind==CommandKind.ProposeTrade)
            {
                var give=(c.Give?.Total??0)+(c.GiveCommodities?.Total??0);var take=(c.Receive?.Total??0)+(c.ReceiveCommodities?.Total??0);
                var targets=(string.IsNullOrEmpty(c.OtherPlayerId)||c.OtherPlayerId==TradeOffer.AllPlayers)?s.Players.Where(p=>p.Id!=c.PlayerId).Select(p=>p.Id):new[]{c.OtherPlayerId};
                Require(targets.Any(p=>B2SkillEngine.CanVoluntaryExchangeB6(s.ThreeKingdoms,c.PlayerId,p,give,take)),"TradeForbidden","当前不能进行此交易。");
            }
            if(c.Kind==CommandKind.AcceptTrade && s.TradeOffer!=null)
            {
                var offer=s.TradeOffer;
                Require(B2SkillEngine.CanVoluntaryExchangeB6(s.ThreeKingdoms,offer.ProposerId,c.PlayerId,offer.Give.Total+offer.GiveCommodities.Total,offer.Receive.Total+offer.ReceiveCommodities.Total),"TradeForbidden","当前不能进行此交易。");
            }
        }
    }
}
