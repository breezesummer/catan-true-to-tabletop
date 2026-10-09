using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Catan.Core.M3
{
    public sealed partial class SeafarersGameSession
    {
        private void InitializeSeafarers(GameState s)
        {
            if (scenario.Id == "new-world")
            {
                s.UnplacedPorts = s.Ports.Select(p => new Port { Id = p.Id, Resource = p.Resource, Vertices = Array.Empty<string>() }).ToArray();
                Shuffle(s.UnplacedPorts, s.Random); s.Ports = Array.Empty<Port>(); s.Phase = GamePhase.SetupPort;
                s.PendingDecision = new PendingDecision { Kind = "SetupPort", PlayerId = s.ActivePlayerId };
            }
            s.HiddenResources = scenario.HiddenResources.ToArray(); Shuffle(s.HiddenResources, s.Random);
            s.HiddenNumbers = scenario.HiddenNumbers.ToArray(); Shuffle(s.HiddenNumbers, s.Random);
            s.BonusEdgeIds = scenario.BonusEdgeIds.ToArray(); s.GiftPorts = Json.Copy(scenario.GiftPorts);
            s.GiftCards = scenario.GiftCardEdgeIds.Select((e, i) => new GiftCard { EdgeId = e, Kind = s.DevelopmentDeck[i] }).ToArray();
            s.DevelopmentDeck = s.DevelopmentDeck.Skip(s.GiftCards.Length).ToArray();
            s.Villages = scenario.Villages.Select(v => new VillageState { VertexId = v.VertexId, Number = v.Number }).ToArray();
            s.ClothSupply = s.Villages.Length == 0 ? 0 : 50 - 5 * s.Villages.Length;
            s.Fortresses = scenario.Fortresses.Select(f => new FortressState { PlayerId = f.PlayerId, VertexId = f.VertexId, BeachheadVertexId = f.BeachheadVertexId, StartingVertexId = f.StartingVertexId, InvasionStartingVertexId = f.StartingVertexId }).ToArray();
            foreach (var f in scenario.Fortresses)
            {
                var p = s.Players.Single(x => x.Id == f.PlayerId);
                PlaceSettlement(s, p, f.StartingVertexId); p.Pieces.Settlements--; // A physical settlement is reserved in its fortress.
                PlaceShip(s, p, f.StartingShipEdgeId); s.Ships.Last().IsInvasionRoute = true; RecordRegion(s, p, f.StartingVertexId, true);
            }
        }

        private static bool IsLand(Tile t) { return t.Resource != "sea" && t.Resource != "fog"; }
        private IEnumerable<Tile> EdgeTiles(GameState s, string edge)
        {
            var e = EdgeAt(edge); return s.Tiles.Where(t => e.Vertices.All(t.Vertices.Contains));
        }
        private bool SeaEdge(GameState s, string edge)
        {
            var tiles = EdgeTiles(s, edge).ToArray();
            return tiles.Length == 1 || tiles.Any(t => !IsLand(t));
        }
        private bool PirateBlocks(GameState s, string edge)
        {
            return s.PirateTileId != null && EdgeTiles(s, edge).Any(t => t.Id == s.PirateTileId);
        }
        private bool ApplySeafarers(GameState s, PlayerState p, Command c)
        {
            switch (c.Kind)
            {
                case CommandKind.ChooseInvasionRoute:
                    ActionPhase(s); Require(LegalInvasionRoots(s, p.Id).Contains(c.TargetId), "InvalidInvasionRoot", "西行航线离开主岛前可改选己方沿海建筑作为起点。");
                    SelectInvasionRoot(s, p.Id, c.TargetId); return true;
                case CommandKind.SetupPort:
                    Require(s.Phase == GamePhase.SetupPort && s.UnplacedPorts.Length > 0, "WrongPhase", "当前不需要放置初始港口。");
                    Require(LegalPortEdges(s, p.Id).Contains(c.TargetId), "InvalidPortEdge", "请选择间隔至少一条边的海岸港位。");
                    var setupPort = Json.Copy(s.UnplacedPorts[0]); setupPort.EdgeId = c.TargetId; setupPort.Vertices = EdgeAt(c.TargetId).Vertices.ToArray();
                    s.Ports = Append(s.Ports, setupPort); s.UnplacedPorts = s.UnplacedPorts.Skip(1).ToArray(); s.SetupPortIndex++;
                    s.ActivePlayerId = s.Players[(s.StartingPlayerIndex + (s.UnplacedPorts.Length == 0 ? 0 : s.SetupPortIndex)) % s.Players.Length].Id;
                    if (s.UnplacedPorts.Length == 0) { s.Phase = GamePhase.SetupSettlement; s.PendingDecision = null; }
                    else s.PendingDecision.PlayerId = s.ActivePlayerId;
                    return true;
                case CommandKind.SetupShip:
                    Require(s.Phase == GamePhase.SetupRoad && s.PendingDecision != null, "WrongPhase", "请先摆放开局定居点。");
                    ValidateShip(s, p, c.TargetId, s.PendingDecision.AnchorVertexId); PlaceShip(s, p, c.TargetId); AdvanceSetup(s); return true;
                case CommandKind.BuildShip:
                    ActionPhase(s); ValidateShip(s, p, c.TargetId, null); Pay(s, p, new ResourceBag { Wood = 1, Wool = 1 }); PlaceShip(s, p, c.TargetId); return true;
                case CommandKind.PlaceFreeShip:
                    Require(s.Phase == GamePhase.RoadBuilding && s.PendingDecision != null, "WrongPhase", "当前没有免费船待放置。");
                    ValidateShip(s, p, c.TargetId, null); PlaceShip(s, p, c.TargetId); s.PendingDecision.RemainingRoads--;
                    if (s.PendingDecision.RemainingRoads == 0) FinishDecision(s); return true;
                case CommandKind.MoveShip:
                    ActionPhase(s); ValidateMoveSource(s, p, c.SourceId);
                    Require(c.SourceId != c.TargetId, "SameEdge", "船必须移动到另一条边。");
                    var ship = s.Ships.Single(x => x.LocationId == c.SourceId);
                    s.Ships = s.Ships.Where(x => x.LocationId != c.SourceId).ToArray(); p.ShipsRemaining++;
                    ValidateShip(s, p, c.TargetId, null); PlaceShip(s, p, c.TargetId);
                    s.Ships.Last().IsWarship = ship.IsWarship; s.ShipMovedThisTurn = true; return true;
                case CommandKind.MovePirate:
                    Require(s.Phase == GamePhase.RobberMove && s.PendingDecision != null && scenario.InitialPirateTileId != null && scenario.Id != "pirate-islands", "WrongPhase", "当前不能移动海盗。");
                    Require(scenario.Id != "cloth-for-catan" || s.Villages.Any(v => v.TradingPlayerIds.Contains(p.Id)), "NoClothRoute", "建立布匹贸易航线后才能移动海盗。");
                    Require(c.TargetId != s.PirateTileId && (c.TargetId == "frame" || s.Tiles.Any(t => t.Id == c.TargetId && t.Resource == "sea")), "InvalidPirateTile", "海盗须移动到另一海格或边框。");
                    s.PirateTileId = c.TargetId; s.PendingDecision.Token = "pirate";
                    s.PendingDecision.EligibleVictimIds = c.TargetId == "frame" ? Array.Empty<string>() : s.Ships.Where(x => x.PlayerId != p.Id && EdgeTiles(s, x.LocationId).Any(t => t.Id == c.TargetId)).Select(x => x.PlayerId).Distinct().OrderBy(x => x).ToArray();
                    if (s.PendingDecision.EligibleVictimIds.Length == 0) FinishDecision(s); else { s.PendingDecision.Kind = "Steal"; s.Phase = GamePhase.RobberSteal; } return true;
                case CommandKind.StealCloth:
                    Require(s.Phase == GamePhase.RobberSteal && s.PendingDecision != null && s.PendingDecision.Token == "pirate" && scenario.Id == "cloth-for-catan" && s.PendingDecision.EligibleVictimIds.Contains(c.OtherPlayerId), "InvalidVictim", "只能向海盗相邻船主偷取一块布。");
                    var victim = s.Players.Single(x => x.Id == c.OtherPlayerId); Require(victim.Cloth > 0, "NoCloth", "该玩家没有布匹。"); victim.Cloth--; p.Cloth++; FinishDecision(s); return true;
                case CommandKind.ClaimWonder:
                    ActionPhase(s); var wonder = scenario.Wonders.FirstOrDefault(w => w.Id == c.TargetId);
                    Require(wonder != null && p.WonderId == null && !s.Players.Any(x => x.WonderId == c.TargetId), "UnavailableWonder", "每人只能选择一座尚未被认领的奇迹。");
                    Require(WonderRequirement(s, p, wonder), "WonderRequirement", "尚未满足这座奇迹的认领条件。"); p.WonderId = wonder.Id; return true;
                case CommandKind.BuildWonder:
                    ActionPhase(s); Require(p.WonderId != null && p.WonderId == c.TargetId && p.WonderLevel < 4, "InvalidWonder", "请选择自己的未完成奇迹。");
                    Pay(s, p, scenario.Wonders.Single(w => w.Id == p.WonderId).Cost); p.WonderLevel++; return true;
                case CommandKind.AttackFortress:
                    ActionPhase(s); Require(scenario.Id == "pirate-islands" && CanAttackFortress(s, p.Id), "FortressNotReached", "必须先将航线连接至自己的海盗要塞。"); EndTurn(s, p); return true;
                default: return false;
            }
        }

        private void ValidateShip(GameState s, PlayerState p, string edge, string anchor)
        {
            var location = scenario.Edges.FirstOrDefault(e => e.Id == edge); Require(location != null, "UnknownEdge", "请选择有效边。");
            Require(p.ShipsRemaining > 0, "NoShips", "船库存不足。");
            Require(!s.Roads.Any(r => r.LocationId == edge) && !s.Ships.Any(r => r.LocationId == edge), "OccupiedEdge", "该边已有道路或船。");
            Require(SeaEdge(s, edge), "ShipOnLand", "船只能放在海边。"); Require(!PirateBlocks(s, edge), "PirateBlocksShip", "海盗封锁此边。");
            if (anchor != null) Require(location.Vertices.Contains(anchor), "ShipNotAtAnchor", "开局船须连接刚建的定居点。");
            else Require(location.Vertices.Any(v => Buildings(s).Any(b => b.PlayerId == p.Id && b.LocationId == v) ||
                (!Buildings(s).Any(b => b.PlayerId != p.Id && b.LocationId == v) && s.Ships.Any(r => r.PlayerId == p.Id && EdgeAt(r.LocationId).Vertices.Contains(v)))), "DisconnectedShip", "船须连接己方建筑或船，路船转换须经过己方建筑。");
            if (scenario.Id == "pirate-islands") ValidatePirateRoute(s, p, edge);
        }
        private void PlaceShip(GameState s, PlayerState p, string edge)
        {
            var invasion = scenario.Id == "pirate-islands" && IsInvasionExtension(s, p.Id, edge);
            s.Ships = Append(s.Ships, new ShipPlacement { PlayerId = p.Id, LocationId = edge, BuiltTurn = s.Turn, IsInvasionRoute = invasion }); p.ShipsRemaining--;
        }
        private void ValidateMoveSource(GameState s, PlayerState p, string edge)
        {
            var ship = s.Ships.FirstOrDefault(x => x.LocationId == edge && x.PlayerId == p.Id);
            Require(ship != null, "NotOwnShip", "请选择自己的船。"); Require(!s.ShipMovedThisTurn, "ShipAlreadyMoved", "每回合只能移动一艘船。");
            Require(ship.BuiltTurn < s.Turn, "NewShip", "本回合新造的船不能移动。"); Require(!PirateBlocks(s, edge), "PirateBlocksShip", "海盗旁的船不能移动。");
            Require(EdgeAt(edge).Vertices.Any(v => !Buildings(s).Any(b => b.PlayerId == p.Id && b.LocationId == v) && !s.Ships.Any(x => x.PlayerId == p.Id && x.LocationId != edge && EdgeAt(x.LocationId).Vertices.Contains(v)) && !s.Villages.Any(x => x.VertexId == v)), "ClosedRoute", "只能移动开放航线末端的船。");
        }
        private bool HasLegalRoute(GameState s, PlayerState p)
        {
            if (HasLegalRoad(s, p)) return true;
            foreach (var edge in scenario.Edges) { try { ValidateShip(s, p, edge.Id, null); return true; } catch (RuleException) { } } return false;
        }
        private string[] LegalShips(string player)
        {
            if (state.ActivePlayerId != player || state.TradeOffer != null) return Array.Empty<string>();
            var kind = state.Phase == GamePhase.SetupRoad ? CommandKind.SetupShip : state.Phase == GamePhase.RoadBuilding ? CommandKind.PlaceFreeShip : CommandKind.BuildShip;
            if (state.Phase != GamePhase.SetupRoad && state.Phase != GamePhase.RoadBuilding && state.Phase != GamePhase.Action) return Array.Empty<string>();
            return scenario.Edges.Where(e => IsLegal(new Command { PlayerId = player, Kind = kind, TargetId = e.Id })).Select(e => e.Id).ToArray();
        }
        private string[] MovableShips(string player)
        {
            if (state.ActivePlayerId != player || state.Phase != GamePhase.Action || state.TradeOffer != null) return Array.Empty<string>();
            var p = state.Players.Single(x => x.Id == player);
            return state.Ships.Where(x => x.PlayerId == player).Where(x => { try { ValidateMoveSource(state, p, x.LocationId); return true; } catch (RuleException) { return false; } }).Select(x => x.LocationId).ToArray();
        }
        private void ValidateSeafarersSettlement(GameState s, PlayerState p, string vertex, bool setup)
        {
            var tiles = s.Tiles.Where(t => t.Vertices.Contains(vertex) && IsLand(t)).ToArray();
            var ownBeachhead = scenario.Id == "pirate-islands" && scenario.Fortresses.Any(f => f.PlayerId == p.Id && f.BeachheadVertexId == vertex);
            Require(tiles.Length > 0 && (ownBeachhead || tiles.Any(t => !scenario.ForbiddenSettlementTileIds.Contains(t.Id))), "SeaSettlement", "此处不能建定居点。");
            if (scenario.Id == "forgotten-tribe") Require(tiles.Any(t => t.Number.HasValue), "UnnumberedIsland", "只能在有号码的陆地上建定居点。");
            Require(!s.Fortresses.Any(f => f.Strength > 0 && f.VertexId == vertex), "FortressOccupied", "该点是海盗要塞。");
            if (setup) Require(tiles.Any(t => scenario.StartingTileIds.Contains(t.Id)) && !scenario.BannedSetupVertexIds.Contains(vertex), "InvalidStartingRegion", "不能在此放置开局定居点。");
            if (scenario.Id == "pirate-islands" && !tiles.Any(t => scenario.StartingTileIds.Contains(t.Id)))
                Require(scenario.Fortresses.Any(f => f.PlayerId == p.Id && f.BeachheadVertexId == vertex), "NotBeachhead", "海盗群岛只可在自己的滩头建立新定居点。");
        }
        private void RecordRegion(GameState s, PlayerState p, string vertex, bool setup)
        {
            foreach (var region in scenario.Regions.Where(r => s.Tiles.Any(t => r.TileIds.Contains(t.Id) && t.Vertices.Contains(vertex) && IsLand(t))))
            {
                if (setup && !p.HomeRegions.Contains(region.Id)) p.HomeRegions = Append(p.HomeRegions, region.Id);
                if (!setup && !p.HomeRegions.Contains(region.Id) && !p.SettledRegions.Contains(region.Id)) p.BonusVictoryPoints += scenario.IslandBonusPoints;
                if (!p.SettledRegions.Contains(region.Id)) p.SettledRegions = Append(p.SettledRegions, region.Id);
            }
        }
        private void AdvanceSetup(GameState s)
        {
            s.PendingDecision = null; s.SetupPlacementIndex++;
            if (s.SetupPlacementIndex == s.Players.Length * scenario.SetupRounds) { s.ActivePlayerId = s.Players[s.StartingPlayerIndex].Id; s.Turn = 1; s.Phase = GamePhase.ProductionAwaitRoll; }
            else
            {
                int round = s.SetupPlacementIndex / s.Players.Length, index = s.SetupPlacementIndex % s.Players.Length;
                int offset = round % 2 == 0 ? index : s.Players.Length - index - 1;
                s.ActivePlayerId = s.Players[(s.StartingPlayerIndex + offset) % s.Players.Length].Id; s.Phase = GamePhase.SetupSettlement;
            }
        }
        private void GrantStartingResources(GameState s, PlayerState p, string vertex)
        {
            foreach (var tile in s.Tiles.Where(t => t.Vertices.Contains(vertex)))
            {
                if (tile.Resource == "gold") AddGold(s, p.Id, 1);
                else if (Resources.Any(r => ResourceName(r) == tile.Resource))
                { var resource = ParseResource(tile.Resource); if (s.Bank[resource] > 0) Transfer(s.Bank, p.Resources, resource, 1); }
            }
        }
        private static void AddGold(GameState s, string player, int amount)
        {
            if (amount <= 0) return;
            var claim = s.GoldClaims.FirstOrDefault(g => g.PlayerId == player);
            if (claim != null) claim.Amount += amount; else s.GoldClaims = Append(s.GoldClaims, new GoldClaim { PlayerId = player, Amount = amount });
        }
        private void BeginGold(GameState s)
        {
            if (s.GoldClaims.Length == 0 || s.Phase == GamePhase.GoldChoice || s.Phase == GamePhase.Finished) return;
            s.GoldReturnPhase = s.Phase; s.GoldReturnDecision = s.PendingDecision;
            s.Phase = GamePhase.GoldChoice; s.PendingDecision = new PendingDecision { Kind = "Gold", PlayerId = s.GoldClaims[0].PlayerId, ReturnPhase = s.GoldReturnPhase };
        }
        private void ChooseGold(GameState s, PlayerState p, Command c)
        {
            Require(s.Phase == GamePhase.GoldChoice && s.GoldClaims.Length > 0 && s.GoldClaims[0].PlayerId == p.Id, "NotGoldRecipient", "请由待决席位选择资源。");
            Require(ValidBag(c.Resources) && c.Resources.Total == Math.Min(s.GoldClaims[0].Amount, s.Bank.Total) && Covers(s.Bank, c.Resources), "InvalidGoldChoice", "请选择规定数量且银行有库存的资源。");
            TransferBag(s.Bank, p.Resources, c.Resources); s.GoldClaims = s.GoldClaims.Skip(1).ToArray();
            if (s.GoldClaims.Length > 0) s.PendingDecision.PlayerId = s.GoldClaims[0].PlayerId;
            else
            {
                s.Phase = s.GoldReturnPhase; s.PendingDecision = s.GoldReturnDecision; s.GoldReturnDecision = null;
                if (s.ProductionAfterGold) { s.ProductionAfterGold = false; ResolveProduction(s); }
            }
        }
        private void ResolveRoll(GameState s)
        {
            if (scenario.Id == "pirate-islands" && s.PirateTileId != null)
            {
                int strength = Math.Min(s.LastDice1, s.LastDice2); s.LastPirateStrength = strength;
                int index = Array.IndexOf(scenario.PiratePath, s.PirateTileId);
                s.PirateTileId = scenario.PiratePath[(index + strength) % scenario.PiratePath.Length];
                var tile = s.Tiles.Single(t => t.Id == s.PirateTileId);
                foreach (var player in s.Players.Where(p => Buildings(s).Any(b => b.PlayerId == p.Id && tile.Vertices.Contains(b.LocationId))))
                {
                    var fleet = s.Ships.Count(x => x.PlayerId == player.Id && x.IsWarship);
                    if (fleet > strength) AddGold(s, player.Id, 1);
                    else if (fleet < strength) RandomDiscard(s, player, 1 + s.Cities.Count(x => x.PlayerId == player.Id));
                }
                if (s.GoldClaims.Length > 0) { s.ProductionAfterGold = true; s.Phase = GamePhase.Action; return; }
            }
            ResolveProduction(s);
        }
        private void ResolveProduction(GameState s)
        {
            if (s.LastDice1 + s.LastDice2 == 7)
            {
                s.Discards = s.Players.Where(x => x.Resources.Total > 7).Select(x => new DiscardRequirement { PlayerId = x.Id, Amount = x.Resources.Total / 2 }).ToArray();
                s.PendingDecision = new PendingDecision { Kind = "Robber", PlayerId = s.ActivePlayerId, ReturnPhase = GamePhase.Action };
                if (s.Discards.Length > 0) s.Phase = GamePhase.Discard; else BeginRobberOrPirate(s);
            }
            else
            {
                var total = s.LastDice1 + s.LastDice2; Produce(s, total); s.Phase = GamePhase.Action;
                var start = Array.FindIndex(s.Players, p => p.Id == s.ActivePlayerId);
                for (var i = 0; i < s.Players.Length; i++)
                {
                    var p = s.Players[(start + i) % s.Players.Length];
                    AddGold(s, p.Id, s.Tiles.Where(t => t.Resource == "gold" && t.Number == total && t.Id != s.RobberTileId).Sum(t => s.Settlements.Count(b => b.PlayerId == p.Id && t.Vertices.Contains(b.LocationId)) + 2 * s.Cities.Count(b => b.PlayerId == p.Id && t.Vertices.Contains(b.LocationId))));
                }
                foreach (var village in s.Villages.Where(v => v.Number == total && v.Cloth > 0))
                    foreach (var id in village.TradingPlayerIds.OrderBy(id => (Array.FindIndex(s.Players, p => p.Id == id) - start + s.Players.Length) % s.Players.Length))
                    {
                        if (village.Cloth > 0) { village.Cloth--; s.Players.Single(p => p.Id == id).Cloth++; }
                        else if (s.ClothSupply > 0) { s.ClothSupply--; s.Players.Single(p => p.Id == id).Cloth++; }
                    }
            }
        }
        private void BeginRobberOrPirate(GameState s)
        {
            if (scenario.Id == "pirate-islands")
            {
                s.PendingDecision.Token = "any"; s.PendingDecision.Kind = "Steal"; s.PendingDecision.EligibleVictimIds = s.Players.Where(p => p.Id != s.ActivePlayerId).Select(p => p.Id).ToArray(); s.Phase = GamePhase.RobberSteal;
            }
            else s.Phase = GamePhase.RobberMove;
        }
        private static void RandomDiscard(GameState s, PlayerState p, int amount)
        {
            for (var i = 0; i < amount && p.Resources.Total > 0; i++)
            {
                var index = Draw(s.Random, p.Resources.Total);
                foreach (var r in Resources) { if (index < p.Resources[r]) { Transfer(p.Resources, s.Bank, r, 1); break; } index -= p.Resources[r]; }
            }
        }

        private void ResolveSeafarersAfterAction(GameState s, Command c)
        {
            bool route = c.Kind == CommandKind.SetupRoad || c.Kind == CommandKind.SetupShip || c.Kind == CommandKind.BuildRoad || c.Kind == CommandKind.BuildShip || c.Kind == CommandKind.PlaceFreeRoad || c.Kind == CommandKind.PlaceFreeShip || c.Kind == CommandKind.MoveShip;
            if (route)
            {
                Discover(s, c.PlayerId, c.TargetId);
                if (s.Ships.Any(x => x.LocationId == c.TargetId && x.PlayerId == c.PlayerId)) CollectGifts(s, c.PlayerId, c.TargetId);
            }
            if (route || c.Kind == CommandKind.BuildSettlement) ConnectVillages(s);
            if (s.Phase == GamePhase.RoadBuilding && !HasLegalRoute(s, s.Players.Single(p => p.Id == s.ActivePlayerId))) FinishDecision(s);
            BeginGold(s); RequirePortPlacement(s);
        }
        private void Discover(GameState s, string player, string edge)
        {
            if (scenario.Id != "fog-islands") return;
            var e = EdgeAt(edge); var p = s.Players.Single(x => x.Id == player);
            foreach (var tile in s.Tiles.Where(t => t.Resource == "fog" && t.Vertices.Any(e.Vertices.Contains)).OrderBy(t => t.Id))
            {
                Require(s.HiddenResources.Length > 0, "EmptyExplorationDeck", "探索地块已耗尽。");
                tile.Resource = s.HiddenResources[0]; s.HiddenResources = s.HiddenResources.Skip(1).ToArray();
                if (tile.Resource != "sea")
                {
                    var n = Draw(s.Random, s.HiddenNumbers.Length); tile.Number = s.HiddenNumbers[n]; s.HiddenNumbers = s.HiddenNumbers.Where((v, i) => i != n).ToArray();
                    if (tile.Resource == "gold") AddGold(s, player, 1);
                    else if (s.Bank[ParseResource(tile.Resource)] > 0) Transfer(s.Bank, p.Resources, ParseResource(tile.Resource), 1);
                }
            }
        }
        private void CollectGifts(GameState s, string player, string edge)
        {
            var p = s.Players.Single(x => x.Id == player);
            if (s.BonusEdgeIds.Contains(edge)) { p.BonusVictoryPoints++; s.BonusEdgeIds = s.BonusEdgeIds.Where(x => x != edge).ToArray(); }
            var gift = s.GiftCards.FirstOrDefault(x => x.EdgeId == edge);
            if (gift != null) { p.DevelopmentCards = Append(p.DevelopmentCards, new DevelopmentCard { Kind = gift.Kind, BoughtTurn = Math.Max(1, s.Turn) }); s.GiftCards = s.GiftCards.Where(x => x.EdgeId != edge).ToArray(); }
            var port = s.GiftPorts.FirstOrDefault(x => x.EdgeId == edge);
            if (port != null) { p.HeldPorts = Append(p.HeldPorts, Json.Copy(port)); s.GiftPorts = s.GiftPorts.Where(x => x.EdgeId != edge).ToArray(); }
        }
        private HashSet<string> ReachByShips(GameState s, string player, IEnumerable<string> starts)
        {
            var seen = new HashSet<string>(starts); var queue = new Queue<string>(seen);
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (Buildings(s).Any(b => b.PlayerId != player && b.LocationId == v)) continue;
                foreach (var ship in s.Ships.Where(x => x.PlayerId == player && EdgeAt(x.LocationId).Vertices.Contains(v)))
                    foreach (var end in EdgeAt(ship.LocationId).Vertices) if (seen.Add(end)) queue.Enqueue(end);
            }
            return seen;
        }
        private void ConnectVillages(GameState s)
        {
            foreach (var p in s.Players)
            {
                var reach = ReachByShips(s, p.Id, Buildings(s).Where(b => b.PlayerId == p.Id).Select(b => b.LocationId));
                foreach (var village in s.Villages.Where(v => !v.TradingPlayerIds.Contains(p.Id) && reach.Contains(v.VertexId)))
                { village.TradingPlayerIds = Append(village.TradingPlayerIds, p.Id); if (village.Cloth > 0) { village.Cloth--; p.Cloth++; } }
            }
        }
        private string[] LegalPortEdges(GameState s, string player)
        {
            bool setup = s.Phase == GamePhase.SetupPort && s.ActivePlayerId == player;
            if (!setup && !s.Players.Any(p => p.Id == player && p.HeldPorts.Length > 0)) return Array.Empty<string>();
            return scenario.Edges.Where(e => SeaEdge(s, e.Id) && EdgeTiles(s, e.Id).Any(IsLand) && (setup || Buildings(s).Any(b => b.PlayerId == player && e.Vertices.Contains(b.LocationId))) && !s.Ports.Any(p => p.Vertices.Any(e.Vertices.Contains))).Select(e => e.Id).ToArray();
        }
        private void RequirePortPlacement(GameState s)
        {
            if (s.Phase != GamePhase.Action && s.Phase != GamePhase.ProductionAwaitRoll && s.Phase != GamePhase.RoadBuilding) return;
            if (LegalPortEdges(s, s.ActivePlayerId).Length == 0) return;
            s.PortReturnDecision = s.PendingDecision;
            s.PendingDecision = new PendingDecision { Kind = "Port", PlayerId = s.ActivePlayerId, ReturnPhase = s.Phase }; s.Phase = GamePhase.PortPlacement;
        }
        private void PlacePort(GameState s, PlayerState p, Command c)
        {
            Require(s.Phase == GamePhase.PortPlacement && s.PendingDecision != null && s.PendingDecision.PlayerId == p.Id, "NoPortPending", "当前没有待放置的港口。");
            var port = p.HeldPorts.FirstOrDefault(x => x.Id == c.SourceId);
            Require(port != null && LegalPortEdges(s, p.Id).Contains(c.TargetId), "InvalidPortEdge", "港口须紧邻己方沿海建筑，并与其他港口间隔一条边。");
            var placed = Json.Copy(port); placed.EdgeId = c.TargetId; placed.Vertices = EdgeAt(c.TargetId).Vertices.ToArray(); s.Ports = Append(s.Ports, placed); p.HeldPorts = p.HeldPorts.Where(x => x.Id != c.SourceId).ToArray();
            s.Phase = s.PendingDecision.ReturnPhase; s.PendingDecision = s.PortReturnDecision; s.PortReturnDecision = null;
        }
        private bool WonderRequirement(GameState s, PlayerState p, WonderDefinition w)
        {
            switch (w.Requirement)
            {
                case "two-cities": return s.Cities.Count(b => b.PlayerId == p.Id) >= 2;
                case "city-six-vp": return s.Cities.Any(b => b.PlayerId == p.Id) && VictoryPoints(s, p.Id, true) >= 6;
                case "port-city-route-five": return s.Cities.Any(b => b.PlayerId == p.Id && s.Ports.Any(port => port.Vertices.Contains(b.LocationId))) && LongestRoad(s, p.Id) >= 5;
                case "marker-building": return Buildings(s).Any(b => b.PlayerId == p.Id && w.VertexIds.Contains(b.LocationId));
                default: throw new InvalidDataException("Unknown wonder requirement: " + w.Requirement);
            }
        }
        private bool HasVictory(GameState s, string player)
        {
            var p = s.Players.Single(x => x.Id == player);
            if (scenario.Id == "wonders-of-catan") return p.WonderLevel == 4 || VictoryPoints(s, player, true) >= 10 && p.WonderLevel > s.Players.Where(x => x.Id != player).Max(x => x.WonderLevel);
            return VictoryPoints(s, player, true) >= scenario.TargetVictoryPoints && (scenario.Id != "pirate-islands" || s.Fortresses.Single(f => f.PlayerId == player).Strength == 0);
        }
        private static void FinishGame(GameState s, string[] winners)
        {
            s.WinnerPlayerIds = winners; s.WinnerPlayerId = winners[0]; s.Phase = GamePhase.Finished; s.PendingDecision = null; s.GoldClaims = Array.Empty<GoldClaim>(); s.GoldReturnDecision = null; s.Discards = Array.Empty<DiscardRequirement>(); s.TradeOffer = null;
        }
        private void EndTurn(GameState s, PlayerState p)
        {
            if (scenario.Id == "pirate-islands" && CanAttackFortress(s, p.Id))
            {
                AttackFortress(s, p);
                RecalculateAwards(s); if (HasVictory(s, p.Id)) { FinishGame(s, new[] { p.Id }); return; }
            }
            if (s.Villages.Count(v => v.Cloth == 0) >= 5)
            {
                var max = s.Players.Max(x => VictoryPoints(s, x.Id, true)); var contenders = s.Players.Where(x => VictoryPoints(s, x.Id, true) == max).ToArray();
                var cloth = contenders.Max(x => x.Cloth); FinishGame(s, contenders.Where(x => x.Cloth == cloth).Select(x => x.Id).ToArray()); return;
            }
            s.ActivePlayerId = s.Players[(Array.FindIndex(s.Players, x => x.Id == p.Id) + 1) % s.Players.Length].Id; s.Turn++; s.DevelopmentCardPlayedThisTurn = false; s.ShipMovedThisTurn = false; s.Phase = GamePhase.ProductionAwaitRoll;
        }
    }
}
