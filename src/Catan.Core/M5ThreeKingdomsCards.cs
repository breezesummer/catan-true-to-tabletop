using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M5
{
    public sealed partial class GameState
    {
        [DataMember(Order=310, EmitDefaultValue=false)] public Command ThreeKingdomsDeferredCommand { get; set; }
        [DataMember(Order=311, EmitDefaultValue=false)] public string[] ThreeKingdomsDiceRecipients { get; set; }
        [DataMember(Order=312, EmitDefaultValue=false)] public string[] ThreeKingdomsProgressUse { get; set; }
        [DataMember(Order=313, EmitDefaultValue=false)] public B2HandChange[] ThreeKingdomsExchange { get; set; }
    }
    public sealed partial class CombinedGameSession
    {
        private static bool B3(GameState s) => s.ThreeKingdoms?.RulesVersion == GeneralCatalog.B3RulesVersion || B4(s);
        private static bool B2(GameState s) => s.ThreeKingdoms?.SkillsEnabled == true;
        private B2Host CardHost(GameState s) => new B2Host(this, s);
        private static bool B2Protected(GameState s, string player) => B2(s) && (B2SkillEngine.IsProtected(s.ThreeKingdoms, player) || B2SkillEngine.IsB4HandLossForbidden(s.ThreeKingdoms,player));
        private static bool B2DrawBlocked(GameState s, string player) => B2(s) && B2SkillEngine.IsDrawBlocked(s.ThreeKingdoms, player);
        private static bool B2Unlimited(GameState s, string player) => B2(s) && s.ThreeKingdoms.Generals.Any(g => g.Region == GeneralRegion.Active && g.OwnerPlayerId == player && g.GeneralId == "tk-g33");
        private static int B2Escrow(GameState s, int card) => B2(s) ? B2SkillEngine.EscrowCards(s.ThreeKingdoms)[card] : 0;
        private static bool B2Busy(GameState s) => B2(s) && (B2SkillEngine.HasPending(s.ThreeKingdoms) || B3DiceEngine.HasPending(s.ThreeKingdoms) || B2SkillEngine.HasB5Comparison(s.ThreeKingdoms));
        private static string ThreeKingdomsVersionFor(ThreeKingdomsState tk) => tk == null ? RulesVersion : tk.RulesVersion == GeneralCatalog.B6RulesVersion ? ThreeKingdomsB6RulesVersion : tk.RulesVersion == GeneralCatalog.B5DirectRulesVersion ? ThreeKingdomsB5DirectRulesVersion : tk.RulesVersion == GeneralCatalog.B5RulesVersion ? ThreeKingdomsB5RulesVersion : tk.RulesVersion == GeneralCatalog.B45RulesVersion ? ThreeKingdomsB45RulesVersion : tk.RulesVersion == GeneralCatalog.B4RulesVersion ? ThreeKingdomsB4RulesVersion : tk.RulesVersion == GeneralCatalog.B3RulesVersion ? ThreeKingdomsB3RulesVersion : tk.SkillsEnabled ? ThreeKingdomsB2RulesVersion : ThreeKingdomsRulesVersion;

        private sealed partial class B2Host : IThreeKingdomsCardHost
        {
            private readonly CombinedGameSession session;
            private readonly GameState game;
            public B2Host(CombinedGameSession session, GameState game) { this.session = session; this.game = game; }
            public ThreeKingdomsState State => game.ThreeKingdoms;
            public int FinalDiceTotal => B5FinalDiceTotal(game);
            public int[] GetHand(string playerId) { var p = game.Players.Single(x => x.Id == playerId); return Cards(p.Resources, p.Commodities); }
            public int[] GetBank() => Cards(game.Bank, game.CommodityBank);
            private static int[] Cards(ResourceBag resources, CommodityBag commodities) => Enumerable.Range(0, 8).Select(i => i < 5 ? resources[(Resource)i] : commodities[(Commodity)(i - 5)]).ToArray();
            private static void Change(ResourceBag resources, CommodityBag commodities, int[] cards, int sign)
            {
                Require(cards != null && cards.Length == 8 && cards.All(n => n >= 0), "InvalidCards", "牌种数量无效。");
                for (int i = 0; i < 8; i++) { if (i < 5) resources[(Resource)i] += cards[i] * sign; else commodities[(Commodity)(i - 5)] += cards[i] * sign; }
                Require(ValidBag(resources) && ValidBag(commodities), "InsufficientCards", "牌的实际供应不足。");
            }
            public void RemoveHand(string playerId, int[] cards) { var p = game.Players.Single(x => x.Id == playerId); Change(p.Resources, p.Commodities, cards, -1); TrackHand(playerId,cards,-1); }
            public void AddHand(string playerId, int[] cards) { var p = game.Players.Single(x => x.Id == playerId); Change(p.Resources, p.Commodities, cards, 1); TrackHand(playerId,cards,1); }
            private void TrackHand(string player,int[] cards,int sign) { if(game.B4SkillHandDelta!=null) for(int i=0;i<8;i++)game.B4SkillHandDelta[player][i]+=cards[i]*sign; }
            public void RemoveBank(int[] cards) => Change(game.Bank, game.CommodityBank, cards, -1);
            public void AddBank(int[] cards) => Change(game.Bank, game.CommodityBank, cards, 1);
            public int DrawRandom(int exclusiveMaximum) => Draw(game.Random, exclusiveMaximum);
            private static B2ProgressCard Card(ProgressCardKind card) => new B2ProgressCard { Id = card.ToString(), Color = (int)ProgressCatalog.Track(card), PublicPoint = ProgressCatalog.IsVictoryPoint(card) };
            public B2ProgressCard[] GetProgressCards(string playerId) => game.Players.Single(x => x.Id == playerId).ProgressCards.Select(Card).ToArray();
            public int GetProgressDeckCount(int color) => game.ProgressDecks.Single(d => (int)d.Track == color).Cards.Length;
            public void RemoveProgress(string playerId, string cardId)
            {
                var p = game.Players.Single(x => x.Id == playerId);
                int index = Array.FindIndex(p.ProgressCards, c => c.ToString() == cardId);
                Require(index >= 0, "NoProgressCard", "没有指定发展牌。");
                p.ProgressCards = p.ProgressCards.Where((c, i) => i != index).ToArray();
            }
            public B2ProgressCard DrawProgress(string playerId, int color)
            {
                var deck = game.ProgressDecks.Single(d => (int)d.Track == color);
                if (deck.Cards.Length == 0) return null;
                var card = deck.Cards[0]; deck.Cards = deck.Cards.Skip(1).ToArray();
                var p = game.Players.Single(x => x.Id == playerId);
                if (ProgressCatalog.IsVictoryPoint(card)) { p.ProgressVictoryCards = Append(p.ProgressVictoryCards, card); QueueB4Progress(game,p.Id,card,null); }
                else p.ProgressCards = Append(p.ProgressCards, card);
                return Card(card);
            }
            public void ReturnProgress(B2ProgressCard card)
            {
                var value = (ProgressCardKind)Enum.Parse(typeof(ProgressCardKind), card.Id);
                var deck = game.ProgressDecks.Single(d => d.Track == ProgressCatalog.Track(value)); deck.Cards = Append(deck.Cards, value);
            }
            public B2HarvestTile[] GetHarvestTiles(string playerId)
            {
                var tiles = new List<B2HarvestTile>();
                foreach (var tile in game.Tiles.Where(t => t.Number.HasValue && (t.Id != game.RobberTileId || B2SkillEngine.B5DoubleHarvest(game.ThreeKingdoms,playerId,t.Id))))
                {
                    bool city = game.Cities.Any(b => b.PlayerId == playerId && tile.Vertices.Contains(b.LocationId));
                    if (!city && !game.Settlements.Any(b => b.PlayerId == playerId && B5Working(game,"Settlement",b.LocationId) && tile.Vertices.Contains(b.LocationId))) continue;
                    var resource = Resources.FirstOrDefault(r => ResourceName(r) == tile.Resource);
                    int[] choices;
                    if (tile.Resource == "gold") choices = Enumerable.Range(0, 5).ToArray();
                    else if (ResourceName(resource) != tile.Resource) continue;
                    else
                    {
                        var values = new List<int> { (int)resource };
                        if (city && resource == Resource.Wool) values.Add(5);
                        if (city && resource == Resource.Ore) values.Add(6);
                        if (city && resource == Resource.Wood) values.Add(7);
                        choices = values.ToArray();
                    }
                    tiles.Add(new B2HarvestTile { TileId = tile.Id, Number = tile.Number.Value, AllowedCardTypes = choices });
                }
                return tiles.ToArray();
            }
        }

        // 先按原版同类短缺规则计算，再把所有席位的实体库存一起预留。
        private void BeginB2Harvest(GameState s)
        {
            if (!B2(s) || s.ThreeKingdoms.Skills.HarvestTurnId == s.ThreeKingdoms.Clock.TurnInstanceId) return;
            var host = CardHost(s);
            int total = host.FinalDiceTotal;
            var allocations = TurnOrder(s).Select(p => new B2HarvestAllocation {
                PlayerId = p.Id, AqueductEligible = total != 7 && B6Technology(s,p.Id,(int)ImprovementTrack.Science) >= 3
            }).ToArray();
            if (total != 7)
            {
                for (int type = 0; type < 8; type++)
                {
                    int card = type;
                    Resource resource = card < 5 ? (Resource)card : card == 5 ? Resource.Wool : card == 6 ? Resource.Ore : Resource.Wood;
                    var claims = allocations.Where(a => !B2DrawBlocked(s, a.PlayerId) && B2SkillEngine.CanGainB6(s.ThreeKingdoms,a.PlayerId,card)).Select(a => new {
                        Allocation = a, Amount = s.Tiles.Where(t => t.Number == total && (t.Id != s.RobberTileId || B2SkillEngine.B5DoubleHarvest(s.ThreeKingdoms,a.PlayerId,t.Id)) && t.Resource == ResourceName(resource)).Sum(t => B5HarvestAmount(s,a.PlayerId,t,card))
                    }).Where(x => x.Amount > 0).ToArray();
                    int supply = host.GetBank()[card];
                    if (supply >= claims.Sum(x => x.Amount)) foreach (var claim in claims) claim.Allocation.Cards[card] = claim.Amount;
                    else if (claims.Length == 1) claims[0].Allocation.Cards[card] = supply;
                }
                foreach (var a in allocations.Where(a => !B2DrawBlocked(s, a.PlayerId))) a.GoldCount = s.Tiles.Where(t => t.Resource == "gold" && t.Number == total && (t.Id != s.RobberTileId || B2SkillEngine.B5DoubleHarvest(s.ThreeKingdoms,a.PlayerId,t.Id))).Sum(t => (B2SkillEngine.B5DoubleHarvest(s.ThreeKingdoms,a.PlayerId,t.Id)?2:1)*(s.Settlements.Count(b => b.PlayerId == a.PlayerId && B5Working(s,"Settlement",b.LocationId) && t.Vertices.Contains(b.LocationId)) + 2 * s.Cities.Count(b => b.PlayerId == a.PlayerId && t.Vertices.Contains(b.LocationId))));
            }
            B2SkillEngine.BeginHarvest(host, allocations, false);
        }

        private void CheckB2Capacity(GameState s)
        {
            if (!B2(s) || B2Busy(s)) return;
            foreach (var p in TurnOrder(s))
            {
                bool deferred = s.ThreeKingdoms.Skills.DeferredCapacityPlayers.Contains(p.Id);
                if (p.ProgressCards.Length <= B5ProgressLimit(s,p.Id) || B2Unlimited(s, p.Id))
                {
                    s.ThreeKingdoms.Skills.DeferredCapacityPlayers = s.ThreeKingdoms.Skills.DeferredCapacityPlayers.Where(id => id != p.Id).ToArray();
                    continue;
                }
                if (B2Protected(s, p.Id))
                {
                    if (!deferred) s.ThreeKingdoms.Skills.DeferredCapacityPlayers = Append(s.ThreeKingdoms.Skills.DeferredCapacityPlayers, p.Id);
                    continue;
                }
                if ((p.Id != s.ActivePlayerId || deferred) && !(s.PendingDecision?.Kind == "ProgressDiscard" && s.PendingDecision.PlayerId == p.Id) && !s.DecisionQueue.Any(d => d.Kind == "ProgressDiscard" && d.PlayerId == p.Id))
                    QueueDecision(s, new PendingDecision { Kind = "ProgressDiscard", PlayerId = p.Id });
            }
            if (s.PendingDecision == null && s.DecisionQueue.Length > 0) ContinueDecisions(s);
        }
    }
}
