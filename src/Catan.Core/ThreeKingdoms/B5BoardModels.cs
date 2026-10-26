using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Core.ThreeKingdoms
{
    /// <summary>稳定空间身份与实际棋子独立；界面和技能只提交已经枚举的完整用途。</summary>
    public interface IThreeKingdomsB5Host : IThreeKingdomsB4ResponseHost
    {
        B5BoardAsset[] GetBoardAssets(string playerId = null);
        B5BoardOption[] GetBoardOptions(string playerId, string kind, string argument = null, int budget = 0);
        void ApplyBoardOption(string playerId, B5BoardOption option);
        int GetScore(string playerId);
        int[] GetImprovements(string playerId);
        int[] GetActualImprovements(string playerId);
        void ChangeImprovement(string playerId, int track, int delta, bool temporary = false);
        void RestoreImprovements(string playerId);
        void SetCollapseProtection(string playerId);
        void AddPermanentScore(string playerId, int delta);
        void ResolveInvasion(string eventId, string[] participants, bool isVirtual);
        int[] GetAllRedResourceTypes();
        string[] GetProducingTileIds(string playerId);
    }
    [DataContract] public sealed class B5BoardAsset
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public string Kind { get; set; }
        [DataMember(Order=4)] public string LocationId { get; set; }
        [DataMember(Order=5)] public int Level { get; set; }
        [DataMember(Order=6)] public int EffectiveLevel { get; set; }
        [DataMember(Order=7)] public bool Active { get; set; }
        [DataMember(Order=8)] public bool Damaged { get; set; }
        [DataMember(Order=9)] public bool Open { get; set; }
        [DataMember(Order=10)] public int[] RepairCost { get; set; } = new int[8];
        [DataMember(Order=11)] public int PaidCount { get; set; }
    }
    [DataContract] public sealed class B5BoardOption
    {
        [DataMember(Order=1)] public string Id { get; set; }
        [DataMember(Order=2)] public string Kind { get; set; }
        [DataMember(Order=3)] public string PlayerId { get; set; }
        [DataMember(Order=4)] public string SourceId { get; set; }
        [DataMember(Order=5)] public string TargetId { get; set; }
        [DataMember(Order=6)] public string OtherPlayerId { get; set; }
        [DataMember(Order=7)] public string AssetKind { get; set; }
        [DataMember(Order=8)] public int Level { get; set; }
        [DataMember(Order=9)] public int[] Cost { get; set; } = new int[8];
        [DataMember(Order=10)] public int PaidCount { get; set; }
        [DataMember(Order=11)] public string PayloadJson { get; set; }
        [DataMember(Order=12)] public string Label { get; set; }
        [DataMember(Order=13)] public string RootEventId { get; set; }
        [DataMember(Order=14)] public string Argument { get; set; }
        [DataMember(Order=15)] public int Budget { get; set; }
    }
    [DataContract] public sealed class B5Damage
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public string Kind { get; set; }
        [DataMember(Order=3)] public string LocationId { get; set; }
        [DataMember(Order=4)] public bool Priority { get; set; }
    }
    [DataContract] public sealed class B5ConstructionReceipt
    {
        [DataMember(Order=1)] public string AssetId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public int Level { get; set; }
        [DataMember(Order=4)] public int PaidCount { get; set; }
    }
    [DataContract] public sealed class B5SuppressedTechnology
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int[] Levels { get; set; } = new int[3];
    }
    [DataContract] public sealed class B5ScoreAdjustment
    {
        [DataMember(Order=1)] public string PlayerId { get; set; }
        [DataMember(Order=2)] public int Amount { get; set; }
    }
    [DataContract] public sealed class B5BoardState
    {
        [DataMember(Order=1)] public B5Damage[] Damages { get; set; } = Array.Empty<B5Damage>();
        [DataMember(Order=2)] public B5ConstructionReceipt[] Receipts { get; set; } = Array.Empty<B5ConstructionReceipt>();
        [DataMember(Order=3)] public B5SuppressedTechnology[] Suppressed { get; set; } = Array.Empty<B5SuppressedTechnology>();
        [DataMember(Order=4)] public string[] CollapseProtectedPlayers { get; set; } = Array.Empty<string>();
        [DataMember(Order=5)] public B5ScoreAdjustment[] ScoreAdjustments { get; set; } = Array.Empty<B5ScoreAdjustment>();
        [DataMember(Order=6)] public B5BoardOption[] DeferredOperations { get; set; } = Array.Empty<B5BoardOption>();
        [DataMember(Order=7)] public string[] ProcessedInvasions { get; set; } = Array.Empty<string>();
        [DataMember(Order=8)] public B5InvasionRecord[] Invasions { get; set; } = Array.Empty<B5InvasionRecord>();
        [DataMember(Order=9)] public string TokenController { get; set; }
        [DataMember(Order=10)] public int LastFormalTotal { get; set; }
    }
    [DataContract] public sealed class B5InvasionRecord
    {
        [DataMember(Order=1)] public string EventId { get; set; }
        [DataMember(Order=2)] public bool IsVirtual { get; set; }
        [DataMember(Order=3)] public string[] Participants { get; set; } = Array.Empty<string>();
        [DataMember(Order=4)] public bool CityCollapsed { get; set; }
        [DataMember(Order=5)] public bool PointAwarded { get; set; }
        [DataMember(Order=6)] public int AttackStrength { get; set; }
        [DataMember(Order=7)] public int DefenseStrength { get; set; }
    }
    public static class B5BoardRules
    {
        public static bool Enabled(ThreeKingdomsState s) => GeneralCatalog.UsesB5Rules(s?.RulesVersion);
        public static B5BoardState Clone(B5BoardState b) => B2SkillEngine.CloneB5Board(b);
        public static bool Damaged(ThreeKingdomsState s, string kind, string location) => Enabled(s) && s.Board?.Damages.Any(d=>d.Kind==kind&&d.LocationId==location)==true;
        public static bool MustRepair(ThreeKingdomsState s, string player, string kind) => Enabled(s) && s.Board?.Damages.Any(d=>d.PlayerId==player&&d.Kind==kind&&d.Priority)==true;
        public static bool CollapseProtected(ThreeKingdomsState s, string player) => Enabled(s)&&s.Board?.CollapseProtectedPlayers.Contains(player)==true;
        public static int Score(ThreeKingdomsState s,string player) => Enabled(s)?s.Board?.ScoreAdjustments.Where(x=>x.PlayerId==player).Sum(x=>x.Amount)??0:0;
        public static int Suppressed(ThreeKingdomsState s,string player,int track) => Enabled(s)?s.Board?.Suppressed.FirstOrDefault(x=>x.PlayerId==player)?.Levels[track]??0:0;
        public static string[] Validate(ThreeKingdomsState s)
        {
            if(!Enabled(s))return Array.Empty<string>(); var b=s.Board;
            if(b==null||b.Damages==null||b.Receipts==null||b.Suppressed==null||b.CollapseProtectedPlayers==null||b.ScoreAdjustments==null||b.DeferredOperations==null||b.ProcessedInvasions==null||b.Invasions==null)return new[]{"B5棋盘状态缺失。"};
            if(b.Damages.Any(d=>d==null||!s.SeatPlayerIds.Contains(d.PlayerId)||!new[]{"Road","Ship","Settlement","City"}.Contains(d.Kind)||string.IsNullOrEmpty(d.LocationId))||b.Damages.Select(d=>d.Kind+":"+d.LocationId).Distinct().Count()!=b.Damages.Length)return new[]{"B5损害记录无效或重复。"};
            if(b.Receipts.Any(r=>r==null||!s.SeatPlayerIds.Contains(r.PlayerId)||r.Level<1||r.PaidCount<0||string.IsNullOrEmpty(r.AssetId))||b.Receipts.Select(r=>r.AssetId+":"+r.Level).Distinct().Count()!=b.Receipts.Length)return new[]{"B5建造实付记录无效。"};
            if(b.Suppressed.Any(t=>t==null||!s.SeatPlayerIds.Contains(t.PlayerId)||t.Levels==null||t.Levels.Length!=3||t.Levels.Any(n=>n<0||n>5))||b.Suppressed.Select(t=>t.PlayerId).Distinct().Count()!=b.Suppressed.Length)return new[]{"B5科技扣置记录无效。"};
            if(b.CollapseProtectedPlayers.Any(p=>!s.SeatPlayerIds.Contains(p))||b.CollapseProtectedPlayers.Distinct().Count()!=b.CollapseProtectedPlayers.Length||b.ScoreAdjustments.Any(p=>p==null||!s.SeatPlayerIds.Contains(p.PlayerId))||b.ScoreAdjustments.Select(p=>p.PlayerId).Distinct().Count()!=b.ScoreAdjustments.Length)return new[]{"B5永久效果身份无效。"};
            if(b.TokenController!=null&&!s.SeatPlayerIds.Contains(b.TokenController)||b.LastFormalTotal<0||b.LastFormalTotal>15||b.Invasions.Any(i=>i==null||string.IsNullOrEmpty(i.EventId)||i.Participants==null||i.Participants.Any(p=>!s.SeatPlayerIds.Contains(p))||i.Participants.Distinct().Count()!=i.Participants.Length||i.AttackStrength<0||i.DefenseStrength<0))return new[]{"B5入侵或移动控制状态无效。"};
            return Array.Empty<string>();
        }
    }
}
