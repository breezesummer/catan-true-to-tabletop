using System.Text.Json;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

/// <summary>独立有限银行夹具：只执行真实移牌，不实现任何武将技能。</summary>
internal sealed class ThreeKingdomsB2TestHost : IThreeKingdomsCardHost
{
    public ThreeKingdomsState State { get; set; } = new();
    public Dictionary<string, int[]> Hands { get; set; } = new();
    public int[] Bank { get; set; } = Enumerable.Repeat(30, 8).ToArray();
    public Dictionary<string, List<B2ProgressCard>> Progress { get; set; } = new();
    public List<B2ProgressCard>[] Decks { get; set; } = Enumerable.Range(0, 3).Select(color => Enumerable.Range(0, 8)
        .Select(index => new B2ProgressCard { Id = $"发展牌-{color}-{index}", Color = color }).ToList()).ToArray();
    public Dictionary<string, B2HarvestTile[]> Tiles { get; set; } = new();
    public int FinalDiceTotal { get; set; } = 6;
    public uint RandomState { get; set; } = 97;
    public int RandomCalls { get; set; }

    public static ThreeKingdomsB2TestHost Create(string first, string second = "tk-g06", string third = "tk-g09")
    {
        string[] ids = { first, second, third };
        var host = new ThreeKingdomsB2TestHost
        {
            State = new ThreeKingdomsState
            {
                RulesVersion = GeneralCatalog.B2RulesVersion, PackageVersion = GeneralCatalog.B2PackageVersion,
                PackageContentHash = GeneralCatalog.B2ContentHash, SkillsEnabled = true, OpeningComplete = true,
                SeatPlayerIds = new[] { "P1", "P2", "P3" },
                Players = Enumerable.Range(1, 3).Select(i => new ThreeKingdomsPlayer { PlayerId = "P" + i, SelectionComplete = true }).ToArray(),
                Generals = ids.Select((id, index) => new GeneralInstance
                {
                    GeneralId = id, InstanceId = "实体-" + index, TenureId = "任次-" + index,
                    OwnerPlayerId = "P" + (index + 1), RemainingLife = 3, Region = GeneralRegion.Active
                }).ToArray(),
                Clock = new ThreeKingdomsClock { Phase = ThreeKingdomsPhase.Play, PhaseId = 5, TurnInstanceId = 1, OpportunityId = 1, ActivePlayerId = "P1", Round = 1 },
                WindowId = "夹具窗口"
            }
        };
        foreach (string player in host.State.SeatPlayerIds)
        {
            host.Hands[player] = new int[8]; host.Progress[player] = new List<B2ProgressCard>(); host.Tiles[player] = Array.Empty<B2HarvestTile>();
        }
        return host;
    }
    public int[] GetHand(string playerId) => (int[])Hands[playerId].Clone();
    public int[] GetBank() => (int[])Bank.Clone();
    public void RemoveHand(string playerId, int[] cards) => Subtract(Hands[playerId], cards);
    public void AddHand(string playerId, int[] cards) => Add(Hands[playerId], cards);
    public void RemoveBank(int[] cards) => Subtract(Bank, cards);
    public void AddBank(int[] cards) => Add(Bank, cards);
    public int DrawRandom(int exclusiveMaximum)
    {
        Assert.True(exclusiveMaximum > 0);
        uint random = RandomState; random ^= random << 13; random ^= random >> 17; random ^= random << 5;
        RandomState = random; RandomCalls++; return (int)(random % (uint)exclusiveMaximum);
    }
    public B2ProgressCard[] GetProgressCards(string playerId) => Progress[playerId].ToArray();
    public int GetProgressDeckCount(int color) => Decks[color].Count;
    public void RemoveProgress(string playerId, string cardId)
    {
        var card = Progress[playerId].Single(x => x.Id == cardId); Progress[playerId].Remove(card);
    }
    public B2ProgressCard DrawProgress(string playerId, int color)
    {
        var card = Decks[color][0]; Decks[color].RemoveAt(0); Progress[playerId].Add(card); return card;
    }
    public void ReturnProgress(B2ProgressCard card) => Decks[card.Color].Add(card);
    public B2HarvestTile[] GetHarvestTiles(string playerId) => Tiles[playerId];
    public void Give(string player, params int[] cards)
    {
        int[] complete = cards.Concat(Enumerable.Repeat(0, 8)).Take(8).ToArray(); RemoveBank(complete); AddHand(player, complete);
    }
    public void EmptyBank(int first = 0, int lastExclusive = 8)
    {
        var cards = new int[8]; for (int i = first; i < lastExclusive; i++) cards[i] = Bank[i];
        RemoveBank(cards); AddHand("P3", cards);
    }
    public string Snapshot() => JsonSerializer.Serialize(this);
    public ThreeKingdomsB2TestHost Copy() => JsonSerializer.Deserialize<ThreeKingdomsB2TestHost>(Snapshot())!;
    public void AssertConserved()
    {
        for (int type = 0; type < 8; type++)
        {
            Assert.True(Bank[type] >= 0); Assert.All(Hands.Values, hand => Assert.True(hand[type] >= 0));
            Assert.Equal(30, Bank[type] + Hands.Values.Sum(hand => hand[type]));
        }
        var allProgress = Decks.SelectMany(x => x).Concat(Progress.Values.SelectMany(x => x)).ToArray();
        Assert.Equal(24, allProgress.Length); Assert.Equal(24, allProgress.Select(card => card.Id).Distinct().Count());
    }
    private static void Add(int[] target, int[] values) { Assert.Equal(8, values.Length); for (int i = 0; i < 8; i++) { Assert.True(values[i] >= 0); target[i] += values[i]; } }
    private static void Subtract(int[] target, int[] values) { Assert.Equal(8, values.Length); for (int i = 0; i < 8; i++) { Assert.InRange(values[i], 0, target[i]); target[i] -= values[i]; } }
}
