using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan.Core.M5
{
    public sealed partial class CombinedGameSession
    {
        private void InitializeProgress(GameState s)
        {
            s.ProgressDecks = ((ImprovementTrack[])Enum.GetValues(typeof(ImprovementTrack))).Select(t =>
            {
                var cards = ProgressCatalog.Deck(t).ToList(); Shuffle(cards, s.Random);
                return new ProgressDeck { Track = t, Cards = cards.ToArray() };
            }).ToArray();
        }

        private void EnqueueEventProgress(GameState s, ImprovementTrack track, int redDie)
        {
            var start = Array.FindIndex(s.Players, x => x.Id == s.ActivePlayerId);
            for (var i = 0; i < s.Players.Length; i++)
            {
                var p = s.Players[(start + i) % s.Players.Length];
                var level = p.Improvements[(int)track];
                if (level > 0 && redDie <= level + 1) QueueDecision(s, new PendingDecision { Kind = "ProgressDraw", PlayerId = p.Id, Track = track });
            }
        }

        private void DrawProgressCard(GameState s, PlayerState p, ImprovementTrack track)
        {
            var deck = s.ProgressDecks.Single(d => d.Track == track);
            if (deck.Cards.Length == 0) return;
            var card = deck.Cards[0]; deck.Cards = deck.Cards.Skip(1).ToArray();
            if (ProgressCatalog.IsVictoryPoint(card)) p.ProgressVictoryCards = Append(p.ProgressVictoryCards, card);
            else p.ProgressCards = Append(p.ProgressCards, card);
            if (p.Id != s.ActivePlayerId && p.ProgressCards.Length > 4 &&
                !(s.PendingDecision != null && s.PendingDecision.Kind == "ProgressDiscard" && s.PendingDecision.PlayerId == p.Id) &&
                !s.DecisionQueue.Any(d => d.Kind == "ProgressDiscard" && d.PlayerId == p.Id))
                s.DecisionQueue = new[] { new PendingDecision { Kind = "ProgressDiscard", PlayerId = p.Id } }.Concat(s.DecisionQueue).ToArray();
        }

        private static void ReturnProgressCard(GameState s, PlayerState p, ProgressCardKind card)
        {
            var index = Array.IndexOf(p.ProgressCards, card);
            Require(index >= 0, "NoProgressCard", "你没有这张进步卡。");
            p.ProgressCards = p.ProgressCards.Where((x, i) => i != index).ToArray();
            var deck = s.ProgressDecks.Single(d => d.Track == ProgressCatalog.Track(card));
            deck.Cards = Append(deck.Cards, card);
        }

        private bool ApplyProgressCommand(GameState s, PlayerState p, Command c)
        {
            if(c.Kind==CommandKind.PlaceFreeShip||c.Kind==CommandKind.PlaceFreeRoad)
            {
                Require(s.PendingDecision!=null&&s.PendingDecision.Kind=="ProgressRoads","WrongDecision","当前没有免费道路或船。");
                var placement=Json.Copy(c);placement.Kind=CommandKind.ResolveProgressChoice;placement.BuildShip=c.Kind==CommandKind.PlaceFreeShip;
                ResolveProgressChoice(s,p,placement);return true;
            }
            if (c.Kind == CommandKind.ResolveProgressChoice)
            {
                ResolveProgressChoice(s, p, c); return true;
            }
            if (c.Kind == CommandKind.DiscardProgressCard)
            {
                var pending = s.PendingDecision;
                var forced = s.Phase == GamePhase.PendingChoice && pending != null && pending.Kind == "ProgressDiscard" && pending.PlayerId == p.Id;
                Require(forced || (s.Phase == GamePhase.Action && s.ActivePlayerId == p.Id && pending == null), "WrongPhase", "当前不能弃进步卡。");
                Require(!ProgressCatalog.IsVictoryPoint(c.ProgressCard), "PublicVictoryCard", "胜利点卡不能丢弃。");
                ReturnProgressCard(s, p, c.ProgressCard);
                if (forced && p.ProgressCards.Length <= 4) { s.PendingDecision = null; ContinueDecisions(s); }
                return true;
            }
            if (c.Kind == CommandKind.CommercialHarborOffer)
            {
                Require(s.ActivePlayerId == p.Id, "NotActivePlayer", "请由当前玩家操作。"); ActionPhase(s);
                var target = OtherProgressPlayer(s, p, c.OtherPlayerId);
                Require(s.CommercialHarborPlayers.Contains(target.Id), "HarborAlreadyUsed", "本回合未获得对该玩家的商业港口交换额度。");
                Require(Enum.IsDefined(typeof(Resource), c.Resource) && p.Resources[c.Resource] > 0, "InsufficientResources", "必须选择自己持有的资源。");
                var used = Array.IndexOf(s.CommercialHarborPlayers, target.Id);
                s.CommercialHarborPlayers = s.CommercialHarborPlayers.Where((x, i) => i != used).ToArray();
                if (target.Commodities.Total > 0)
                {
                    Transfer(p.Resources, target.Resources, c.Resource, 1);
                    QueueDecision(s, new PendingDecision { Kind = "ProgressHarbor", PlayerId = target.Id, OtherPlayerId = p.Id, Amount = 1 });
                    ContinueDecisions(s);
                }
                return true;
            }
            if (c.Kind != CommandKind.PlayProgressCard) return false;
            Require(s.ActivePlayerId == p.Id, "NotActivePlayer", "仅可在自己的回合使用进步卡。");
            Require(s.PendingDecision == null, "DecisionPending", "请先完成待决选择。");
            Require(Enum.IsDefined(typeof(ProgressCardKind), c.ProgressCard), "UnknownCard", "未知进步卡。");
            Require(!ProgressCatalog.IsVictoryPoint(c.ProgressCard), "PublicVictoryCard", "胜利点卡抽到时自动公开。");
            if (c.ProgressCard == ProgressCardKind.Alchemy)
                Require(s.Phase == GamePhase.ProductionAwaitRoll && s.AlchemyDice1 == 0, "WrongPhase", "炼金术必须在本回合掷骰之前使用一次。");
            else ActionPhase(s);
            ReturnProgressCard(s, p, c.ProgressCard);
            switch (c.ProgressCard)
            {
                case ProgressCardKind.Alchemy:
                    Require(c.ChosenDice1 >= 1 && c.ChosenDice1 <= 6 && c.ChosenDice2 >= 1 && c.ChosenDice2 <= 6, "InvalidDice", "两个生产骰各选择 1–6。");
                    s.AlchemyDice1 = c.ChosenDice1; s.AlchemyDice2 = c.ChosenDice2; break;
                case ProgressCardKind.Crane: BuildImprovement(s, p, c.Track, 1); break;
                case ProgressCardKind.Engineering: BuildWall(s, p, c.TargetId, true); break;
                case ProgressCardKind.Medicine: BuildCity(s, p, c.TargetId, 1, 2); break;
                case ProgressCardKind.Invention:
                    var tileA = s.Tiles.FirstOrDefault(t => t.Id == c.SourceId);
                    var tileB = s.Tiles.FirstOrDefault(t => t.Id == c.TargetId);
                    Require(tileA != null && tileB != null && tileA.Id != tileB.Id && SwappableNumber(tileA.Number) && SwappableNumber(tileB.Number), "InvalidNumberSwap", "选择两个有数字的不同地块，不能交换 2、6、8、12。");
                    var number = tileA.Number; tileA.Number = tileB.Number; tileB.Number = number; break;
                case ProgressCardKind.Irrigation: ProgressHarvest(s, p, Resource.Wheat); break;
                case ProgressCardKind.Mining: ProgressHarvest(s, p, Resource.Ore); break;
                case ProgressCardKind.RoadBuilding: BeginProgressRoads(s, p, 2); break;
                case ProgressCardKind.Smithing:
                    var targets = c.TargetIds ?? Array.Empty<string>();
                    Require(targets.Length <= 2 && targets.Distinct().Count() == targets.Length, "InvalidSmithing", "至多晋升两个不同骑士。");
                    foreach (var target in targets) ProgressPromote(s, p, target);
                    break;
                case ProgressCardKind.CommercialHarbor:
                    s.CommercialHarborPlayers = s.CommercialHarborPlayers.Concat(s.Players.Where(x => x.Id != p.Id).Select(x => x.Id)).ToArray(); break;
                case ProgressCardKind.GuildDues:
                    var rich = OtherProgressPlayer(s, p, c.OtherPlayerId);
                    Require(VictoryPoints(s, rich.Id, true) > VictoryPoints(s, p.Id, true), "NotMoreVictoryPoints", "行会会费只能选择胜利点比你高的玩家。");
                    if (ProgressHandSize(rich) > 0) QueueDecision(s, new PendingDecision { Kind = "ProgressGuildDues", PlayerId = p.Id, OtherPlayerId = rich.Id, Amount = Math.Min(2, ProgressHandSize(rich)) });
                    break;
                case ProgressCardKind.Merchant:
                    var merchantTile = s.Tiles.FirstOrDefault(t => t.Id == c.TargetId);
                    Require(merchantTile != null && merchantTile.Resource != "sea" && merchantTile.Resource != "gold" && Buildings(s).Any(b => b.PlayerId == p.Id && merchantTile.Vertices.Contains(b.LocationId)), "InvalidMerchantHex", "商人必须放在你建筑相邻的陆地上。");
                    s.MerchantPlayerId = p.Id; s.MerchantTileId = merchantTile.Id; break;
                case ProgressCardKind.MerchantFleet:
                    if (c.ChooseCommodity) { Require(Enum.IsDefined(typeof(Commodity), c.Commodity), "InvalidCommodity", "商品类型错误。"); s.MerchantFleetCommodities = s.MerchantFleetCommodities.Union(new[] { c.Commodity }).ToArray(); }
                    else { Require(Enum.IsDefined(typeof(Resource), c.Resource), "InvalidResource", "资源类型错误。"); s.MerchantFleetResources = s.MerchantFleetResources.Union(new[] { c.Resource }).ToArray(); }
                    break;
                case ProgressCardKind.ResourceMonopoly:
                    Require(Enum.IsDefined(typeof(Resource), c.Resource), "InvalidResource", "资源类型错误。");
                    foreach (var other in s.Players.Where(x => x.Id != p.Id)) Transfer(other.Resources, p.Resources, c.Resource, Math.Min(2, other.Resources[c.Resource]));
                    break;
                case ProgressCardKind.TradeMonopoly:
                    Require(Enum.IsDefined(typeof(Commodity), c.Commodity), "InvalidCommodity", "商品类型错误。");
                    foreach (var other in s.Players.Where(x => x.Id != p.Id)) ProgressCommodityTransfer(other.Commodities, p.Commodities, c.Commodity, Math.Min(1, other.Commodities[c.Commodity]));
                    break;
                case ProgressCardKind.Diplomacy:
                    var road = s.Roads.FirstOrDefault(r => r.LocationId == c.TargetId);
                    var ship = s.Ships.FirstOrDefault(r => r.LocationId == c.TargetId);
                    Require(road!=null&&ProgressRoadIsOpen(s,road)||ship!=null&&ShipIsOpen(s,ship),"RoadNotOpen","只能移除开放端道路或船。");
                    var owner=road!=null?road.PlayerId:ship.PlayerId;
                    Require(!RemovalDisconnectsKnight(s,owner,c.TargetId),"KnightDisconnected","不能使骑士失去道路或船的连接。");
                    if(road!=null) {s.Roads=s.Roads.Where(r=>r!=road).ToArray();s.Players.Single(x=>x.Id==owner).Pieces.Roads++;}
                    else {s.Ships=s.Ships.Where(r=>r!=ship).ToArray();s.Players.Single(x=>x.Id==owner).ShipsRemaining++;}
                    if(owner==p.Id && (road!=null?HasLegalRoad(s,p):HasLegalShip(s,p))) QueueDecision(s,new PendingDecision {Kind="ProgressRoads",PlayerId=p.Id,Amount=1,RoadsOnly=road!=null,ShipsOnly=ship!=null});
                    break;
                case ProgressCardKind.Espionage:
                    var spied = OtherProgressPlayer(s, p, c.OtherPlayerId);
                    if (spied.ProgressCards.Length > 0) QueueDecision(s, new PendingDecision { Kind = "ProgressEspionage", PlayerId = p.Id, OtherPlayerId = spied.Id });
                    break;
                case ProgressCardKind.Encouragement:
                    foreach (var knight in s.Knights.Where(k => k.PlayerId == p.Id && !k.Active)) { knight.Active = true; knight.ActivatedTurn = s.Turn; }
                    break;
                case ProgressCardKind.Intrigue:
                    var intruded = s.Knights.FirstOrDefault(k => k.LocationId == c.TargetId && k.PlayerId != p.Id);
                    Require(intruded != null && OwnRouteEdges(s,p.Id).Any(e=>e.Vertices.Contains(intruded.LocationId)), "InvalidIntrigue", "被驱逐骑士必须在你的道路相邻顶点。");
                    DisplaceForProgress(s, intruded); break;
                case ProgressCardKind.Taxation: ProgressTaxation(s, p, c.TargetId); break;
                case ProgressCardKind.Treason:
                    var betrayed = OtherProgressPlayer(s, p, c.OtherPlayerId);
                    var knightOptions = s.Knights.Where(k => k.PlayerId == betrayed.Id).Select(k => k.LocationId).ToArray();
                    if (knightOptions.Length > 0) QueueDecision(s, new PendingDecision { Kind = "ProgressTreasonRemove", PlayerId = betrayed.Id, OtherPlayerId = p.Id, Options = knightOptions });
                    break;
                case ProgressCardKind.Wedding:
                case ProgressCardKind.Sabotage:
                    var score = VictoryPoints(s, p.Id, true);
                    foreach (var other in ProgressTurnOrder(s).Where(x => x.Id != p.Id))
                    {
                        var otherScore = VictoryPoints(s, other.Id, true);
                        var wedding = c.ProgressCard == ProgressCardKind.Wedding;
                        var amount = wedding ? Math.Min(2, ProgressHandSize(other)) : ProgressHandSize(other) / 2;
                        if (amount > 0 && (wedding ? otherScore > score : otherScore >= score))
                            QueueDecision(s, new PendingDecision { Kind = wedding ? "ProgressWedding" : "ProgressSabotage", PlayerId = other.Id, OtherPlayerId = p.Id, Amount = amount });
                    }
                    break;
            }
            if (s.PendingDecision == null && s.DecisionQueue.Length > 0) ContinueDecisions(s);
            return true;
        }

        private void ResolveProgressChoice(GameState s, PlayerState p, Command c)
        {
            var d = s.PendingDecision;
            Require(s.Phase == GamePhase.PendingChoice && d != null && d.PlayerId == p.Id && d.Kind.StartsWith("Progress", StringComparison.Ordinal) && d.Kind != "ProgressDiscard", "WrongDecision", "当前没有需要你解决的进步卡选择。");
            switch (d.Kind)
            {
                case "ProgressRoads":
                    if (!c.Decline)
                    {
                        Require(!(d.RoadsOnly&&c.BuildShip)&&!(d.ShipsOnly&&!c.BuildShip),"WrongRouteType","外交移除后须重建同种棋子。");
                        if(c.BuildShip) {ValidateShip(s,p,c.TargetId,null);PlaceShip(s,p,c.TargetId);} else {ValidateRoad(s,p,c.TargetId,null);PlaceRoad(s,p,c.TargetId);} d.Amount--;
                        if(d.Amount>0&&(d.RoadsOnly?HasLegalRoad(s,p):d.ShipsOnly?HasLegalShip(s,p):HasLegalRoute(s,p)))return;
                    }
                    break;
                case "ProgressHarbor":
                    Require(Enum.IsDefined(typeof(Commodity), c.Commodity), "InvalidCommodity", "商品类型错误。");
                    ProgressCommodityTransfer(p.Commodities, s.Players.Single(x => x.Id == d.OtherPlayerId).Commodities, c.Commodity, 1); break;
                case "ProgressGuildDues":
                    var rich = s.Players.Single(x => x.Id == d.OtherPlayerId);
                    ProgressTransferSelection(rich, p, null, c, d.Amount); break;
                case "ProgressEspionage":
                    if (!c.Decline)
                    {
                        var other = s.Players.Single(x => x.Id == d.OtherPlayerId);
                        var index = Array.IndexOf(other.ProgressCards, c.SelectedProgressCard);
                        Require(index >= 0 && !ProgressCatalog.IsVictoryPoint(c.SelectedProgressCard), "NoProgressCard", "只能选择对方手中的进步卡。");
                        other.ProgressCards = other.ProgressCards.Where((x, i) => i != index).ToArray();
                        p.ProgressCards = Append(p.ProgressCards, c.SelectedProgressCard);
                    }
                    break;
                case "ProgressWedding": ProgressTransferSelection(p, s.Players.Single(x => x.Id == d.OtherPlayerId), null, c, d.Amount); break;
                case "ProgressSabotage": ProgressTransferSelection(p, null, s, c, d.Amount); break;
                case "ProgressTreasonRemove":
                    var removed = s.Knights.FirstOrDefault(k => k.PlayerId == p.Id && k.LocationId == c.TargetId);
                    Require(removed != null && d.Options.Contains(c.TargetId), "InvalidKnight", "请选择自己的一个骑士。");
                    s.Knights = s.Knights.Where(k => k != removed).ToArray();
                    var replacementPlayer = s.Players.Single(x => x.Id == d.OtherPlayerId);
                    var destinations = ProgressKnightPlaces(s, replacementPlayer.Id);
                    if (destinations.Length > 0 && Enumerable.Range(1, removed.Level).Any(level => s.Knights.Count(k => k.PlayerId == replacementPlayer.Id && k.Level == level) < 2))
                        QueueDecision(s, new PendingDecision { Kind = "ProgressTreasonPlace", PlayerId = replacementPlayer.Id, Options = destinations, KnightLevel = removed.Level, KnightActive = removed.Active });
                    break;
                case "ProgressTreasonPlace":
                    if (!c.Decline)
                    {
                        Require(d.Options.Contains(c.TargetId) && c.KnightLevel >= 1 && c.KnightLevel <= d.KnightLevel && s.Knights.Count(k => k.PlayerId == p.Id && k.Level == c.KnightLevel) < 2, "InvalidTreasonReplacement", "请选择可用等级及合法空顶点。");
                        s.Knights = Append(s.Knights, new Knight { PlayerId = p.Id, LocationId = c.TargetId, Level = c.KnightLevel, Active = d.KnightActive, ActivatedTurn = d.KnightActive ? s.Turn : -1, PromotedTurn = -1, ActedTurn = -1 });
                    }
                    break;
                default: Require(false, "WrongDecision", "未知进步卡选择。"); break;
            }
            s.PendingDecision = null; ContinueDecisions(s);
        }

        private static bool SwappableNumber(int? number) { return number.HasValue && number != 2 && number != 6 && number != 8 && number != 12; }
        private void ProgressHarvest(GameState s, PlayerState p, Resource resource)
        {
            var sites = Buildings(s).Where(b => b.PlayerId == p.Id).Select(b => b.LocationId).ToArray();
            var amount = 2 * s.Tiles.Count(t => t.Resource == ResourceName(resource) && t.Vertices.Any(sites.Contains));
            Transfer(s.Bank, p.Resources, resource, Math.Min(amount, s.Bank[resource]));
        }
        private void BeginProgressRoads(GameState s, PlayerState p, int count)
        {
            if (HasLegalRoute(s, p)) QueueDecision(s, new PendingDecision { Kind = "ProgressRoads", PlayerId = p.Id, Amount = count });
        }
        private static PlayerState OtherProgressPlayer(GameState s, PlayerState p, string id)
        {
            var other = s.Players.FirstOrDefault(x => x.Id == id && x.Id != p.Id);
            Require(other != null, "InvalidOtherPlayer", "请选择另一名玩家。"); return other;
        }
        private static int ProgressHandSize(PlayerState p) { return p.Resources.Total + p.Commodities.Total; }
        private static IEnumerable<PlayerState> ProgressTurnOrder(GameState s)
        {
            var start = Array.FindIndex(s.Players, p => p.Id == s.ActivePlayerId);
            return Enumerable.Range(0, s.Players.Length).Select(i => s.Players[(start + i) % s.Players.Length]);
        }
        private static void ProgressCommodityTransfer(CommodityBag from, CommodityBag to, Commodity type, int count)
        {
            Require(count >= 0 && from[type] >= count, "InsufficientCommodities", "商品不足。"); from[type] -= count; to[type] += count;
        }
        private static void ProgressTransferSelection(PlayerState from, PlayerState to, GameState bank, Command c, int count)
        {
            var resources = c.Resources ?? new ResourceBag(); var commodities = c.Commodities ?? new CommodityBag();
            Require(ValidBag(resources) && ((Commodity[])Enum.GetValues(typeof(Commodity))).All(t => commodities[t] >= 0 && commodities[t] <= 12) && resources.Total + commodities.Total == count, "InvalidCardSelection", "必须选择要求数量的资源或商品。");
            TransferBag(from.Resources, to == null ? bank.Bank : to.Resources, resources);
            foreach (Commodity type in Enum.GetValues(typeof(Commodity))) ProgressCommodityTransfer(from.Commodities, to == null ? bank.CommodityBank : to.Commodities, type, commodities[type]);
        }
        private void ProgressPromote(GameState s, PlayerState p, string location)
        {
            var knight = s.Knights.FirstOrDefault(k => k.PlayerId == p.Id && k.LocationId == location);
            Require(knight != null && knight.Level < 3 && knight.PromotedTurn != s.Turn, "InvalidPromotion", "骑士每回合只能晋升一次。");
            Require(knight.Level < 2 || p.Improvements[(int)ImprovementTrack.Politics] >= 3, "FortressRequired", "强力骑士晋升需要政治三级改良。");
            Require(s.Knights.Count(k => k.PlayerId == p.Id && k.Level == knight.Level + 1) < 2, "KnightSupply", "该等级骑士供应不足。");
            knight.Level++; knight.PromotedTurn = s.Turn;
        }
        private string[] ProgressKnightPlaces(GameState s, string player)
        {
            var occupied = Buildings(s).Select(b => b.LocationId).Concat(s.Knights.Select(k => k.LocationId)).ToArray();
            return OwnRouteEdges(s,player).SelectMany(e=>e.Vertices).Distinct().Where(v => !occupied.Contains(v)).ToArray();
        }
        private bool ProgressRoadIsOpen(GameState s, PiecePlacement road)
        {
            return EdgeAt(road.LocationId).Vertices.Any(vertex =>
                !Buildings(s).Any(b => b.PlayerId == road.PlayerId && b.LocationId == vertex) &&
                !s.Knights.Any(k => k.PlayerId == road.PlayerId && k.LocationId == vertex) &&
                !s.Roads.Any(r => r != road && r.PlayerId == road.PlayerId && EdgeAt(r.LocationId).Vertices.Contains(vertex)));
        }
        private void ProgressTaxation(GameState s, PlayerState p, string tileId)
        {
            var tile = s.Tiles.FirstOrDefault(t => t.Id == tileId);
            Require(s.FirstBarbarianAttack && tile != null && tile.Resource != "sea" && tile.Id != s.RobberTileId, "InvalidTaxation", "首轮蛮族攻击后才能将强盗移至另一个陆地。");
            s.RobberTileId = tile.Id;
            var victims = Buildings(s).Where(b => b.PlayerId != p.Id && tile.Vertices.Contains(b.LocationId)).Select(b => b.PlayerId).Distinct().ToArray();
            foreach (var id in victims)
            {
                var victim = s.Players.Single(x => x.Id == id); var total = ProgressHandSize(victim);
                if (total == 0) continue;
                var selected = Draw(s.Random, total); var done = false;
                foreach (var resource in Resources)
                {
                    if (selected < victim.Resources[resource]) { Transfer(victim.Resources, p.Resources, resource, 1); done = true; break; }
                    selected -= victim.Resources[resource];
                }
                if (done) continue;
                foreach (Commodity type in Enum.GetValues(typeof(Commodity)))
                {
                    if (selected < victim.Commodities[type]) { ProgressCommodityTransfer(victim.Commodities, p.Commodities, type, 1); break; }
                    selected -= victim.Commodities[type];
                }
            }
        }

        private static void ClearProgressTurn(GameState s)
        {
            s.MerchantFleetResources = Array.Empty<Resource>(); s.MerchantFleetCommodities = Array.Empty<Commodity>();
            s.CommercialHarborPlayers = Array.Empty<string>(); s.AlchemyDice1 = 0; s.AlchemyDice2 = 0;
        }
        private static int ProgressTradeRate(GameState s, PlayerState p, Resource? resource, Commodity? commodity)
        {
            if (p.Id == s.ActivePlayerId && ((resource.HasValue && s.MerchantFleetResources.Contains(resource.Value)) || (commodity.HasValue && s.MerchantFleetCommodities.Contains(commodity.Value)))) return 2;
            if (resource.HasValue && s.MerchantPlayerId == p.Id && s.Tiles.Any(t => t.Id == s.MerchantTileId && t.Resource == ResourceName(resource.Value))) return 2;
            return 4;
        }
        private static void PopulateProgressView(GameState s, PlayerState p, PlayerView view)
        {
            view.OwnProgressCards = p.ProgressCards.ToArray(); view.MerchantPlayerId = s.MerchantPlayerId; view.MerchantTileId = s.MerchantTileId;
            view.MerchantFleetResources = s.MerchantFleetResources.ToArray(); view.MerchantFleetCommodities = s.MerchantFleetCommodities.ToArray();
            view.CommercialHarborPlayers = s.CommercialHarborPlayers.ToArray(); view.AlchemyDice1 = s.AlchemyDice1; view.AlchemyDice2 = s.AlchemyDice2;
            foreach (var player in view.Players)
            {
                var source = s.Players.Single(x => x.Id == player.Id); player.ProgressCardCount = source.ProgressCards.Length; player.ProgressVictoryCards = source.ProgressVictoryCards.ToArray();
            }
            view.PrivateTargetProgressCards = Array.Empty<ProgressCardKind>();
            var d = s.PendingDecision;
            if (d != null && d.PlayerId == p.Id)
            {
                if (d.Kind == "ProgressEspionage") view.PrivateTargetProgressCards = s.Players.Single(x => x.Id == d.OtherPlayerId).ProgressCards.ToArray();
                if (d.Kind == "ProgressGuildDues")
                {
                    var target = s.Players.Single(x => x.Id == d.OtherPlayerId); view.PrivateTargetResources = target.Resources.Copy(); view.PrivateTargetCommodities = target.Commodities.Copy();
                }
            }
        }
        private static void ValidateProgress(GameState s)
        {
            if (s.ProgressDecks == null || s.ProgressDecks.Length != 3 || s.ProgressDecks.Select(d => d.Track).Distinct().Count() != 3) throw new InvalidOperationException("Invalid progress decks.");
            var cards = s.ProgressDecks.SelectMany(d => d.Cards).Concat(s.Players.SelectMany(p => p.ProgressCards)).Concat(s.Players.SelectMany(p => p.ProgressVictoryCards)).ToArray();
            if (cards.Length != 54) throw new InvalidOperationException("Progress-card conservation violated.");
            foreach (ProgressCardKind card in Enum.GetValues(typeof(ProgressCardKind))) if (cards.Count(c => c == card) != ProgressCatalog.Count(card)) throw new InvalidOperationException("Invalid progress-card supply: " + card);
            if (s.Players.Any(p => p.ProgressCards.Any(ProgressCatalog.IsVictoryPoint) || p.ProgressVictoryCards.Any(c => !ProgressCatalog.IsVictoryPoint(c))) || s.ProgressDecks.Any(d => d.Cards.Any(c => ProgressCatalog.Track(c) != d.Track))) throw new InvalidOperationException("Invalid progress-card zone.");
            if (s.MerchantPlayerId != null && (!s.Players.Any(p => p.Id == s.MerchantPlayerId) || !s.Tiles.Any(t => t.Id == s.MerchantTileId && t.Resource != "sea"))) throw new InvalidOperationException("Invalid merchant.");
            if ((s.AlchemyDice1 == 0) != (s.AlchemyDice2 == 0) || s.AlchemyDice1 < 0 || s.AlchemyDice1 > 6 || s.AlchemyDice2 < 0 || s.AlchemyDice2 > 6) throw new InvalidOperationException("Invalid alchemy dice.");
        }

        // Candidates contain only information authorized by this seat's PlayerView.
        // The shared Apply/Preview path validates every returned action.
        private IEnumerable<Command> ProgressCandidates(GameState s, PlayerState p)
        {
            Func<CommandKind, Command> command = kind => new Command { PlayerId = p.Id, Kind = kind };
            var d = s.PendingDecision;
            if (s.Phase == GamePhase.PendingChoice && d != null && d.PlayerId == p.Id)
            {
                if (d.Kind == "ProgressDiscard") foreach (var card in p.ProgressCards.Distinct()) { var c = command(CommandKind.DiscardProgressCard); c.ProgressCard = card; yield return c; }
                if (d.Kind == "ProgressRoads")
                {
                    foreach (var edge in scenario.Topology.Edges) { var c = command(CommandKind.ResolveProgressChoice); c.TargetId = edge.Id; yield return c; var ship = command(CommandKind.ResolveProgressChoice); ship.TargetId=edge.Id;ship.BuildShip=true;yield return ship; }
                    var skip = command(CommandKind.ResolveProgressChoice); skip.Decline = true; yield return skip;
                }
                if (d.Kind == "ProgressHarbor") foreach (Commodity type in Enum.GetValues(typeof(Commodity))) { var c = command(CommandKind.ResolveProgressChoice); c.Commodity = type; yield return c; }
                if (d.Kind == "ProgressTreasonRemove" || d.Kind == "ProgressTreasonPlace")
                {
                    foreach (var option in d.Options) for (var level = 1; level <= (d.Kind == "ProgressTreasonRemove" ? 1 : d.KnightLevel); level++)
                    { var c = command(CommandKind.ResolveProgressChoice); c.TargetId = option; c.KnightLevel = level; yield return c; }
                    if (d.Kind == "ProgressTreasonPlace") { var skip = command(CommandKind.ResolveProgressChoice); skip.Decline = true; yield return skip; }
                }
                if (d.Kind == "ProgressEspionage")
                {
                    foreach (var card in s.Players.Single(x => x.Id == d.OtherPlayerId).ProgressCards.Distinct()) { var c = command(CommandKind.ResolveProgressChoice); c.SelectedProgressCard = card; yield return c; }
                    var skip = command(CommandKind.ResolveProgressChoice); skip.Decline = true; yield return skip;
                }
                if (d.Kind == "ProgressWedding" || d.Kind == "ProgressSabotage" || d.Kind == "ProgressGuildDues")
                {
                    var owner = d.Kind == "ProgressGuildDues" ? s.Players.Single(x => x.Id == d.OtherPlayerId) : p;
                    var c = command(CommandKind.ResolveProgressChoice); c.Resources = new ResourceBag(); c.Commodities = new CommodityBag(); var left = d.Amount;
                    foreach (var r in Resources) { var take = Math.Min(left, owner.Resources[r]); c.Resources[r] = take; left -= take; }
                    foreach (Commodity type in Enum.GetValues(typeof(Commodity))) { var take = Math.Min(left, owner.Commodities[type]); c.Commodities[type] = take; left -= take; }
                    yield return c;
                }
                yield break;
            }
            if (s.ActivePlayerId != p.Id || (s.Phase != GamePhase.Action && s.Phase != GamePhase.ProductionAwaitRoll)) yield break;
            foreach (var card in p.ProgressCards.Distinct())
            {
                Func<Command> play = () => { var c = command(CommandKind.PlayProgressCard); c.ProgressCard = card; return c; };
                var discard = command(CommandKind.DiscardProgressCard); discard.ProgressCard = card; yield return discard;
                switch (card)
                {
                    case ProgressCardKind.Alchemy: var alchemy = play(); alchemy.ChosenDice1 = 3; alchemy.ChosenDice2 = 3; yield return alchemy; break;
                    case ProgressCardKind.Crane: foreach (ImprovementTrack track in Enum.GetValues(typeof(ImprovementTrack))) { var c = play(); c.Track = track; yield return c; } break;
                    case ProgressCardKind.Engineering: foreach (var b in s.Cities.Where(b => b.PlayerId == p.Id)) { var c = play(); c.TargetId = b.LocationId; yield return c; } break;
                    case ProgressCardKind.Medicine: foreach (var b in s.Settlements.Where(b => b.PlayerId == p.Id)) { var c = play(); c.TargetId = b.LocationId; yield return c; } break;
                    case ProgressCardKind.Invention:
                        var tiles = s.Tiles.Where(t => SwappableNumber(t.Number)).ToArray();
                        if (tiles.Length > 1) { var c = play(); c.SourceId = tiles[0].Id; c.TargetId = tiles[1].Id; yield return c; } break;
                    case ProgressCardKind.Smithing:
                        foreach (var k in s.Knights.Where(k => k.PlayerId == p.Id)) { var c = play(); c.TargetIds = new[] { k.LocationId }; yield return c; } break;
                    case ProgressCardKind.GuildDues: case ProgressCardKind.Espionage: case ProgressCardKind.Treason:
                        foreach (var other in s.Players.Where(x => x.Id != p.Id)) { var c = play(); c.OtherPlayerId = other.Id; yield return c; } break;
                    case ProgressCardKind.Merchant: case ProgressCardKind.Taxation:
                        foreach (var tile in s.Tiles) { var c = play(); c.TargetId = tile.Id; yield return c; } break;
                    case ProgressCardKind.Diplomacy: foreach (var location in s.Roads.Select(x=>x.LocationId).Concat(s.Ships.Select(x=>x.LocationId))) { var c = play(); c.TargetId = location; yield return c; } break;
                    case ProgressCardKind.Intrigue: foreach (var k in s.Knights.Where(k => k.PlayerId != p.Id)) { var c = play(); c.TargetId = k.LocationId; yield return c; } break;
                    case ProgressCardKind.ResourceMonopoly: foreach (var r in Resources) { var c = play(); c.Resource = r; yield return c; } break;
                    case ProgressCardKind.TradeMonopoly: foreach (Commodity type in Enum.GetValues(typeof(Commodity))) { var c = play(); c.Commodity = type; yield return c; } break;
                    case ProgressCardKind.MerchantFleet:
                        foreach (var r in Resources) { var c = play(); c.Resource = r; yield return c; }
                        foreach (Commodity type in Enum.GetValues(typeof(Commodity))) { var c = play(); c.ChooseCommodity = true; c.Commodity = type; yield return c; } break;
                    default: yield return play(); break;
                }
            }
            foreach (var other in s.CommercialHarborPlayers.Distinct()) foreach (var r in Resources.Where(r => p.Resources[r] > 0))
            { var c = command(CommandKind.CommercialHarborOffer); c.OtherPlayerId = other; c.Resource = r; yield return c; }
        }
    }
}
