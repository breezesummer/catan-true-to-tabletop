using System.Linq;

namespace Catan.Core.ThreeKingdoms
{
    // AI 合法位置预览会频繁复制状态；逐字段深拷贝避免反复构造 JSON 序列化器与临时缓冲区。
    public static partial class B2SkillEngine
    {
        private static T[] CopyB5Array<T>(T[] values) => values == null ? null : (T[])values.Clone();

        public static B5BoardState CloneB5Board(B5BoardState state) => state == null ? null : new B5BoardState
        {
            Damages = state.Damages?.Select(d => d == null ? null : new B5Damage
            { PlayerId = d.PlayerId, Kind = d.Kind, LocationId = d.LocationId, Priority = d.Priority }).ToArray(),
            Receipts = state.Receipts?.Select(r => r == null ? null : new B5ConstructionReceipt
            { AssetId = r.AssetId, PlayerId = r.PlayerId, Level = r.Level, PaidCount = r.PaidCount }).ToArray(),
            Suppressed = state.Suppressed?.Select(s => s == null ? null : new B5SuppressedTechnology
            { PlayerId = s.PlayerId, Levels = CopyB5Array(s.Levels) }).ToArray(),
            CollapseProtectedPlayers = CopyB5Array(state.CollapseProtectedPlayers),
            ScoreAdjustments = state.ScoreAdjustments?.Select(s => s == null ? null : new B5ScoreAdjustment
            { PlayerId = s.PlayerId, Amount = s.Amount }).ToArray(),
            DeferredOperations = state.DeferredOperations?.Select(CloneB5BoardOption).ToArray(),
            ProcessedInvasions = CopyB5Array(state.ProcessedInvasions),
            Invasions = state.Invasions?.Select(i => i == null ? null : new B5InvasionRecord
            {
                EventId = i.EventId, IsVirtual = i.IsVirtual, Participants = CopyB5Array(i.Participants),
                CityCollapsed = i.CityCollapsed, PointAwarded = i.PointAwarded,
                AttackStrength = i.AttackStrength, DefenseStrength = i.DefenseStrength
            }).ToArray(),
            TokenController = state.TokenController, LastFormalTotal = state.LastFormalTotal
        };

        public static B5MilitaryState CloneB5Military(B5MilitaryState state) => state == null ? null : new B5MilitaryState
        {
            PermanentShanqiPlayers = CopyB5Array(state.PermanentShanqiPlayers),
            PermanentYaowuPlayers = CopyB5Array(state.PermanentYaowuPlayers),
            TurnHandSnapshots = state.TurnHandSnapshots?.Select(CloneB5PlayerCount).ToArray(),
            PublicHands = state.PublicHands?.Select(CloneB5PlayerCount).ToArray(),
            DelayedDeployments = state.DelayedDeployments?.Select(CloneB5PlayerCount).ToArray(),
            HuoshouEnds = state.HuoshouEnds?.Select(CloneB5PlayerCount).ToArray(),
            ScoreAdjustments = state.ScoreAdjustments?.Select(CloneB5PlayerCount).ToArray(),
            DoubleHarvest = state.DoubleHarvest?.Select(h => h == null ? null : new B5TileHarvest
            { PlayerId = h.PlayerId, TileId = h.TileId, EventId = h.EventId }).ToArray(),
            ImmediateInvasionTurnId = state.ImmediateInvasionTurnId,
            ComparisonSource = state.ComparisonSource == null ? null : CloneFrame(state.ComparisonSource),
            SuspendedFrames = state.SuspendedFrames?.Select(f => f == null ? null : CloneFrame(f)).ToArray(),
            SuspendedHarvest = state.SuspendedHarvest
        };

        private static B5PlayerCount CloneB5PlayerCount(B5PlayerCount count) => count == null ? null : new B5PlayerCount
        { PlayerId = count.PlayerId, Value = count.Value, TurnId = count.TurnId, TenureId = count.TenureId };

        public static B5CivilState CloneB5Civil(B5CivilState state) => state == null ? null : new B5CivilState
        {
            Relations = state.Relations?.Select(r => r == null ? null : new B5CivilRelation
            { Kind = r.Kind, PlayerId = r.PlayerId, TargetPlayerId = r.TargetPlayerId, TenureId = r.TenureId }).ToArray(),
            Jianshu = state.Jianshu?.Select(j => j == null ? null : new B5JianshuEffect
            { Id = j.Id, SourcePlayerId = j.SourcePlayerId, TenureId = j.TenureId, PlayerId = j.PlayerId, OtherPlayerId = j.OtherPlayerId }).ToArray(),
            Markers = state.Markers?.Select(m => m == null ? null : new B5CivilMarker
            { PlayerId = m.PlayerId, Key = m.Key, Value = m.Value, TurnId = m.TurnId }).ToArray()
        };

        public static B5CivilPayload CloneB5CivilPayload(B5CivilPayload payload) => payload == null ? null : new B5CivilPayload
        {
            OwnerPlayerId = payload.OwnerPlayerId, RootEventId = payload.RootEventId, Argument = payload.Argument,
            BoardKind = payload.BoardKind, Selection = CopyB5Array(payload.Selection), SelectedIds = CopyB5Array(payload.SelectedIds),
            Tracks = CopyB5Array(payload.Tracks), Declared = payload.Declared, Budget = payload.Budget,
            VirtualUse = payload.VirtualUse == null ? null : new B4VirtualUse
            {
                CardId = payload.VirtualUse.CardId, OptionId = payload.VirtualUse.OptionId, Label = payload.VirtualUse.Label,
                TargetPlayerIds = CopyB5Array(payload.VirtualUse.TargetPlayerIds), CardType = payload.VirtualUse.CardType,
                PayloadJson = payload.VirtualUse.PayloadJson, SourceSkillId = payload.VirtualUse.SourceSkillId,
                SourceEventId = payload.VirtualUse.SourceEventId, RewardRandomCount = payload.VirtualUse.RewardRandomCount
            },
            BoardOption = CloneB5BoardOption(payload.BoardOption), OtherPlayerId = payload.OtherPlayerId
        };

        private static B5BoardOption CloneB5BoardOption(B5BoardOption option) => option == null ? null : new B5BoardOption
        {
            Id = option.Id, Kind = option.Kind, PlayerId = option.PlayerId, SourceId = option.SourceId,
            TargetId = option.TargetId, OtherPlayerId = option.OtherPlayerId, AssetKind = option.AssetKind,
            Level = option.Level, Cost = CopyB5Array(option.Cost), PaidCount = option.PaidCount,
            PayloadJson = option.PayloadJson, Label = option.Label, RootEventId = option.RootEventId,
            Argument = option.Argument, Budget = option.Budget
        };
    }
}
