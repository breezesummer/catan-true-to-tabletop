using System.Reflection;
using System.Security.Cryptography;
using Catan.Core;
using Xunit;

namespace Catan.Audio.Tests;

public sealed class TabletopAudioTests
{
    static T Set<T>(T value, string name, object? field) where T : class
    { typeof(T).GetProperty(name)!.SetValue(value, field); return value; }
    static GameHistoryEntry Entry(long sequence, string kind = "EndTurn", params GameHistoryChange[] changes)
    {
        var entry = new GameHistoryEntry(); Set(entry, "Sequence", sequence); Set(entry, "Kind", kind); Set(entry, "Changes", changes); return entry;
    }
    static GameHistoryChange Change(string category, string name, int amount, string player = "P2", string location = "v-4", int good = -1)
    {
        var c = new GameHistoryChange(); Set(c, "Category", category); Set(c, "Name", name); Set(c, "Amount", amount);
        Set(c, "PlayerId", player); Set(c, "LocationId", location); Set(c, "GoodIndex", good); return c;
    }
    static string Digest(float[] samples)
    { var bytes = new byte[samples.Length * sizeof(float)]; Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length); return Convert.ToHexString(SHA256.HashData(bytes)); }

    public static IEnumerable<object[]> Commands()
    {
        Type[] sets = [typeof(Catan.Core.M2.CommandKind), typeof(Catan.Core.M3.CommandKind), typeof(Catan.Core.M4.CommandKind), typeof(Catan.Core.M5.CommandKind)];
        foreach (var set in sets) foreach (string name in Enum.GetNames(set)) yield return [set.Namespace!, name];
    }
    [Theory][MemberData(nameof(Commands))]
    public void EveryExistingCommandInEachRuleSetHasAnAudioResponse(string rules, string kind)
    {
        Assert.NotEmpty(rules);
        var events = TabletopAudioModel.Classify(Entry(1, kind));
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.True(Enum.IsDefined(e.Cue)));
    }
    [Theory]
    [InlineData("RollDice", TabletopAudioCue.DiceRoll)]
    [InlineData("BuildSettlement", TabletopAudioCue.SettlementBuild)]
    [InlineData("BuildCity", TabletopAudioCue.CityBuild)]
    [InlineData("BuildWall", TabletopAudioCue.WallBuild)]
    [InlineData("BuildRoad", TabletopAudioCue.RoadBuild)]
    [InlineData("BuildShip", TabletopAudioCue.ShipBuild)]
    [InlineData("MoveShip", TabletopAudioCue.ShipMove)]
    [InlineData("MovePirate", TabletopAudioCue.PirateMove)]
    [InlineData("AttackFortress", TabletopAudioCue.KnightCombat)]
    [InlineData("DisplaceKnight", TabletopAudioCue.KnightCombat)]
    [InlineData("ImproveCity", TabletopAudioCue.FacilityUpgrade)]
    [InlineData("BankTrade", TabletopAudioCue.TradeComplete)]
    [InlineData("PlayProgressCard", TabletopAudioCue.CardPlay)]
    [InlineData("UnknownFutureOperation", TabletopAudioCue.Decision)]
    public void DistinctActionsUseExpectedAudioFamily(string kind, TabletopAudioCue cue)
        => Assert.Equal(cue, TabletopAudioModel.Classify(Entry(1, kind))[0].Cue);

    [Fact]
    public void FirstBindingSaveLoadingRoomChangesAndExplicitReconnectNeverReplayHistory()
    {
        var model = new TabletopAudioModel(); var old = new[] { Entry(5), Entry(8, "RollDice") };
        Assert.Empty(model.Observe("room1:P1", old)); Assert.Equal(8, model.Cursor);
        Assert.Empty(model.Observe("room1:P1", old));
        Assert.Single(model.Observe("room1:P1", [.. old, Entry(10)]));
        Assert.Empty(model.Observe("room2:P1", [Entry(200)])); Assert.Equal(200, model.Cursor);
        model.Reset(); Assert.Empty(model.Observe("room2:P1", [Entry(200), Entry(201)]));
        Assert.Empty(model.Observe("room2:P2", [Entry(201)]));
    }
    [Fact]
    public void EmptyNewGameObservesItsFirstRealActionAndIgnoresRejectedOperations()
    {
        var model = new TabletopAudioModel(); Assert.Empty(model.Observe("new:P1", []));
        Assert.Single(model.Observe("new:P1", [Entry(9, "BuildCity")]));
        Assert.Empty(model.Observe("new:P1", [Entry(9, "BuildCity")]));
        Assert.Equal(9, model.Cursor);
    }
    [Fact]
    public void NetworkCatchupIsBoundedAndWatermarkSkipsAllConsumedEntries()
    {
        var model = new TabletopAudioModel(); model.Observe("game", []);
        var events = Enumerable.Range(1, 100).Select(i => Entry(i)).ToArray();
        Assert.Equal(8, model.Observe("game", events).Length); Assert.Equal(100, model.Cursor);
        Assert.Empty(model.Observe("game", events));
    }
    [Fact]
    public void DelayedPartialHistoryCannotLowerPublicBaselineOrReplayOldNetworkEvents()
    {
        var model = new TabletopAudioModel(); Assert.Empty(model.Observe("game", [], 60));
        Assert.Equal(60, model.Cursor);
        Assert.Empty(model.Observe("game", [Entry(4)])); Assert.Equal(60, model.Cursor);
        Assert.Empty(model.Observe("game", Enumerable.Range(1, 60).Select(i => Entry(i)).ToArray()));
        Assert.Single(model.Observe("game", [Entry(60), Entry(61)])); Assert.Equal(61, model.Cursor);
        model.Reset(); Assert.Empty(model.Observe("game", [Entry(4)]));
        Assert.Single(model.Observe("game", [Entry(4), Entry(5)]));
    }
    [Fact]
    public void ResourceAndCardSoundsDoNotDependOnHiddenIdentityOrNumber()
    {
        var visible = TabletopAudioModel.Classify(Entry(2, "StealResource", Change("goods", "矿石", 1, good: 4), Change("progress", "间谍", 1)));
        var redacted = TabletopAudioModel.Classify(Entry(2, "StealResource", Change("resource-count", "手牌", 5), Change("progress", "进步卡", 4)));
        Assert.Equal(visible.Select(e => (e.Cue, e.Delay)), redacted.Select(e => (e.Cue, e.Delay)));
        Assert.Single(visible, e => e.Cue == TabletopAudioCue.ResourceCollect);
        Assert.Single(visible, e => e.Cue == TabletopAudioCue.CardDraw);
    }
    [Fact]
    public void DiceProductionFollowsRollAndBarbarianAdvanceUsesPublicEventDie()
    {
        var entry = Entry(3, "RollDice", Change("goods", "木材", 2)); Set(entry, "EventDie", "Barbarian");
        var events = TabletopAudioModel.Classify(entry);
        Assert.Contains(events, e => e.Cue == TabletopAudioCue.DiceRoll && e.Delay == 0);
        Assert.Contains(events, e => e.Cue == TabletopAudioCue.ResourceCollect && e.Delay >= .75);
        Assert.Contains(events, e => e.Cue == TabletopAudioCue.BarbarianAdvance);
        Set(entry, "EventDie", "Science"); Assert.DoesNotContain(TabletopAudioModel.Classify(entry), e => e.Cue == TabletopAudioCue.BarbarianAdvance);
    }
    [Fact]
    public void CityPillageCrumblesButCityUpgradeAndMovementDoNot()
    {
        var down = Entry(4, "ResolveChoice", Change("piece", "城市", -1), Change("piece", "村庄", 1));
        Assert.Contains(TabletopAudioModel.Classify(down), e => e.Cue == TabletopAudioCue.BuildingCollapse);
        var upgrade = Entry(5, "BuildCity", Change("piece", "村庄", -1), Change("piece", "城市", 1));
        Assert.DoesNotContain(TabletopAudioModel.Classify(upgrade), e => e.Cue == TabletopAudioCue.BuildingCollapse);
        var wrongPlace = Entry(6, "ResolveChoice", Change("piece", "城市", -1), Change("piece", "村庄", 1, location: "v-8"));
        Assert.DoesNotContain(TabletopAudioModel.Classify(wrongPlace), e => e.Cue == TabletopAudioCue.BuildingCollapse);
    }
    [Fact]
    public void PendingKnightConflictAndMetropolisPlacementHaveDedicatedCues()
    {
        Assert.Contains(TabletopAudioModel.Classify(Entry(1, "ResolveProgressChoice", Change("piece", "2级骑士（激活）", -1))), e => e.Cue == TabletopAudioCue.KnightCombat);
        Assert.Contains(TabletopAudioModel.Classify(Entry(2, "ResolveChoice", Change("piece", "科学大都会", 1))), e => e.Cue == TabletopAudioCue.FacilityUpgrade);
    }
    [Fact]
    public void RateLimiterDropsRapidDuplicatesWithoutBlockingOtherActions()
    {
        var limiter = new TabletopAudioLimiter();
        Assert.True(limiter.Admit(TabletopAudioCue.ResourceCollect, 1));
        Assert.False(limiter.Admit(TabletopAudioCue.ResourceCollect, 1.1));
        Assert.True(limiter.Admit(TabletopAudioCue.CityBuild, 1.1));
        Assert.True(limiter.Admit(TabletopAudioCue.ResourceCollect, 1.3));
        Assert.True(limiter.Admit(TabletopAudioCue.UiClick, 1.31));
        Assert.False(limiter.Admit(TabletopAudioCue.UiClick, 1.32));
        limiter.Reset(); Assert.True(limiter.Admit(TabletopAudioCue.UiClick, 1.32));
    }
    [Theory]
    [InlineData(6, 7, 1)]
    [InlineData(6, 0, 0)]
    [InlineData(5, 0, 2)]
    public void PublicBarbarianTrackReportsAttackIncludingImmediateReturnHome(int before, int after, int advances)
    {
        var state = new TabletopAudioPublicModel();
        Assert.Empty(state.Observe(false, false, before, "", false));
        Assert.Contains(state.Observe(false, false, after, "", false, advances), e => e.Cue == TabletopAudioCue.BarbarianInvasion);
        Assert.Empty(state.Observe(false, false, after, "", false));
    }
    [Fact]
    public void AutomaticPirateIslandMovementUsesOnlyPublicPositionAndIsSilentOnRestore()
    {
        var state = new TabletopAudioPublicModel();
        Assert.Empty(state.Observe(false, false, -1, "t-sea1", true));
        Assert.Contains(state.Observe(false, false, -1, "t-sea2", true), e => e.Cue == TabletopAudioCue.PirateMove);
        Assert.Empty(state.Observe(false, false, -1, "t-sea2", true));
        state.Reset(); Assert.Empty(state.Observe(false, false, -1, "t-sea2", true));
        Assert.Empty(state.Observe(false, false, -1, "t-sea3", false));
    }
    [Fact]
    public void TurnAndVictoryAlertsOnlyPlayOnTransitionsAndNotAfterRestore()
    {
        var state = new TabletopAudioPublicModel();
        Assert.Empty(state.Observe(true, false, -1, "", false));
        Assert.Empty(state.Observe(false, false, -1, "", false));
        Assert.Contains(state.Observe(true, false, -1, "", false), e => e.Cue == TabletopAudioCue.YourTurn);
        Assert.Contains(state.Observe(true, true, -1, "", false), e => e.Cue == TabletopAudioCue.Victory);
        Assert.Empty(state.Observe(true, true, -1, "", false));
        state.Reset(); Assert.Empty(state.Observe(true, true, -1, "", false));
    }
    public static IEnumerable<object[]> Cues() => Enum.GetValues<TabletopAudioCue>().Select(cue => new object[] { cue });
    [Theory][MemberData(nameof(Cues))]
    public void EveryCueHasDeterministicFiniteAudibleSamplesAndSilentEndpoints(TabletopAudioCue cue)
    {
        var samples = TabletopAudioSynth.Render(cue);
        Assert.True(samples.Length >= 2400); Assert.Equal(0, samples[0]); Assert.Equal(0, samples[^1]);
        Assert.All(samples, v => Assert.True(float.IsFinite(v) && Math.Abs(v) <= .701));
        Assert.True(samples.Max(v => Math.Abs(v)) > .025);
        Assert.Equal(Digest(samples), Digest(TabletopAudioSynth.Render(cue)));
    }
    [Fact]
    public void AllAudioRecipesAreDistinctAndAmbienceLoopHasQuietSeams()
    {
        var fingerprints = Enum.GetValues<TabletopAudioCue>().Select(c => Digest(TabletopAudioSynth.Render(c))).ToArray();
        Assert.Equal(fingerprints.Length, fingerprints.Distinct().Count());
        var wind = TabletopAudioSynth.RenderAmbience(); Assert.Equal(12 * 24000, wind.Length);
        Assert.Equal(0, wind[0]); Assert.Equal(0, wind[^1]);
        Assert.All(wind, v => Assert.True(float.IsFinite(v) && Math.Abs(v) <= .701));
        Assert.True(wind.Average(v => v * v) > .00001);
        Assert.Equal(Digest(wind), Digest(TabletopAudioSynth.RenderAmbience()));
    }

    [Theory]
    [InlineData("贸易改良（现为 2 级）", TabletopAudioCue.TradeUpgrade)]
    [InlineData("政治改良（现为 3 级）", TabletopAudioCue.PoliticsUpgrade)]
    [InlineData("科学改良（现为 4 级）", TabletopAudioCue.ScienceUpgrade)]
    public void ThreePublicImprovementTracksChooseSewingAnvilAndWriting(string name, TabletopAudioCue cue)
    {
        var entry = Entry(4, "ImproveCity", Change("status", name, 1));
        Assert.Equal(cue, TabletopAudioModel.Classify(entry)[0].Cue);
        // 相同名称放在隐藏手牌里不能变更音色，支付的商品也不参与分类。
        Assert.Equal(TabletopAudioCue.FacilityUpgrade, TabletopAudioModel.Classify(Entry(5, "ImproveCity", Change("goods", name, -2)))[0].Cue);
        var hidden = Change("status", name, 1); Set(hidden, "IsPrivate", true);
        Assert.Equal(TabletopAudioCue.FacilityUpgrade, TabletopAudioModel.Classify(Entry(6, "ImproveCity", hidden))[0].Cue);
        var crane = TabletopAudioModel.Classify(Entry(7, "PlayProgressCard", Change("status", name, 1)));
        Assert.Equal(TabletopAudioCue.CardPlay, crane[0].Cue);
        Assert.Contains(crane, e => e.Cue == cue && e.Delay >= .22);
    }

    [Fact]
    public void KnightDevelopmentCardSlidesBeforeCavalryAndAllKnightActionsHaveDedicatedFeedback()
    {
        var events = TabletopAudioModel.Classify(Entry(1, "PlayKnight"));
        Assert.Equal(TabletopAudioCue.CardPlay, events[0].Cue);
        Assert.Contains(events, e => e.Cue == TabletopAudioCue.KnightCombat && e.Delay > events[0].Delay);
        foreach (string command in new[] { "DisplaceKnight", "ChaseRobber", "ChasePirate", "AttackFortress" })
            Assert.Equal(TabletopAudioCue.KnightCombat, TabletopAudioModel.Classify(Entry(1, command))[0].Cue);
        Assert.Equal(TabletopAudioCue.KnightRecruit, TabletopAudioModel.Classify(Entry(1, "RecruitKnight"))[0].Cue);
        Assert.Equal(TabletopAudioCue.KnightActivate, TabletopAudioModel.Classify(Entry(1, "ActivateKnight"))[0].Cue);
    }

    [Fact]
    public void BarbarianSailingAndLandingAreSeparateAndOnlyExistingKnightsChargeForDefense()
    {
        var state = new TabletopAudioPublicModel(); state.Observe(false, false, 4, "", false, activeKnights: 2);
        var sailing = state.Observe(false, false, 5, "", false, 1, 2);
        Assert.Contains(sailing, e => e.Cue == TabletopAudioCue.BarbarianSail);
        Assert.DoesNotContain(sailing, e => e.Cue == TabletopAudioCue.BarbarianInvasion || e.Cue == TabletopAudioCue.KnightCombat);
        state.Observe(false, false, 6, "", false, 1, 2);
        var landing = state.Observe(false, false, 0, "", false, 1, 0);
        Assert.Contains(landing, e => e.Cue == TabletopAudioCue.KnightCombat);
        Assert.Contains(landing, e => e.Cue == TabletopAudioCue.BarbarianInvasion);
        Assert.DoesNotContain(landing, e => e.Cue == TabletopAudioCue.BarbarianSail);
        var undefended = new TabletopAudioPublicModel(); undefended.Observe(false, false, 6, "", false);
        Assert.DoesNotContain(undefended.Observe(false, false, 0, "", false, 1), e => e.Cue == TabletopAudioCue.KnightCombat);
    }

    [Fact]
    public void ResolvingPillageAndReturningFromSevenDoesNotRepeatInvasionOrCavalry()
    {
        var state = new TabletopAudioPublicModel(); state.Observe(false, false, 6, "", false, 0, 3);
        Assert.Contains(state.Observe(false, false, 7, "", false, 1, 3), e => e.Cue == TabletopAudioCue.BarbarianInvasion);
        Assert.Empty(state.Observe(false, false, 0, "", false, 0, 0));
        var pillage = Entry(10, "ResolveChoice", Change("piece", "城市", -1), Change("piece", "村庄", 1),
            Change("piece", "2级骑士（激活）", -1, location: "v-8"), Change("piece", "2级骑士（休眠）", 1, location: "v-8"));
        Assert.Contains(TabletopAudioModel.Classify(pillage), e => e.Cue == TabletopAudioCue.BuildingCollapse);
        Assert.DoesNotContain(TabletopAudioModel.Classify(pillage), e => e.Cue == TabletopAudioCue.KnightCombat);
        var defender = Entry(11, "ResolveChoice", Change("piece", "1级骑士（激活）", -1), Change("piece", "1级骑士（休眠）", 1));
        Assert.DoesNotContain(TabletopAudioModel.Classify(defender), e => e.Cue == TabletopAudioCue.KnightCombat);
        var displaced = Entry(12, "ResolveChoice", Change("piece", "1级骑士（激活）", -1), Change("piece", "1级骑士（休眠）", 1, location: "v-8"));
        Assert.Contains(TabletopAudioModel.Classify(displaced), e => e.Cue == TabletopAudioCue.KnightCombat);
    }

    [Theory]
    [InlineData("ResolveProgressChoice")]
    [InlineData("PlayProgressCard")]
    public void SmithingUsesPublicLevelIncreaseToPlayAnvilInsteadOfCavalry(string command)
    {
        var smithing = Entry(18, command, Change("piece", "1级骑士（激活）", -1), Change("piece", "2级骑士（激活）", 1));
        var events = TabletopAudioModel.Classify(smithing);
        Assert.Single(events, e => e.Cue == TabletopAudioCue.KnightPromote);
        Assert.DoesNotContain(events, e => e.Cue == TabletopAudioCue.KnightCombat);
        var ordinary = Entry(19, "PromoteKnight", Change("piece", "1级骑士（休眠）", -1), Change("piece", "2级骑士（休眠）", 1));
        Assert.Single(TabletopAudioModel.Classify(ordinary), e => e.Cue == TabletopAudioCue.KnightPromote);
    }

    [Fact]
    public void FiveEnvironmentsAreDistinctAudibleAndUseQuietLoopSeams()
    {
        var fingerprints = new List<string>();
        for (int theme = 0; theme < 5; theme++)
        {
            var data = TabletopAudioSynth.RenderAmbience(theme);
            Assert.Equal(12 * TabletopAudioSynth.SampleRate, data.Length);
            Assert.Equal(0, data[0]); Assert.Equal(0, data[^1]);
            Assert.True(data.Average(v => v * v) > .00001);
            Assert.All(data, v => Assert.True(float.IsFinite(v) && Math.Abs(v) <= .701));
            fingerprints.Add(Digest(data));
        }
        Assert.Equal(5, fingerprints.Distinct().Count());
    }

    [Fact]
    public void OriginalMusicHasFullPhraseStructureDeterministicLoopAndSafePeak()
    {
        var data = TabletopAudioSynth.RenderMusic();
        Assert.Equal(1280000, data.Length); Assert.True(data.Length / (double)TabletopAudioSynth.SampleRate > 50);
        Assert.All(data, v => Assert.True(float.IsFinite(v) && Math.Abs(v) <= .70));
        Assert.True(data.Average(v => v * v) > .0001);
        Assert.True(Math.Abs(data[0] - data[^1]) < .01, "音乐首尾接缝应小于 0.01");
        Assert.Equal(Digest(data), Digest(TabletopAudioSynth.RenderMusic()));
        int bar = 80000;
        Assert.NotEqual(Digest(data.Take(bar).ToArray()), Digest(data.Skip(bar * 8).Take(bar).ToArray()));
    }

    [Fact]
    public void RapidThemeSwitchesPreserveUnityGainAndAllFourBusesHaveHeadroom()
    {
        var weights = new[] { 1f, 0, 0, 0, 0 };
        for (int frame = 0; frame < 600; frame++)
        {
            TabletopAudioMix.FadeTheme(weights, frame / 7 % 5, .55f / 60);
            Assert.InRange(weights.Sum(), .9999f, 1.0001f);
            Assert.All(weights, w => Assert.InRange(w, 0, 1));
        }
        for (int frame = 0; frame < 240; frame++) TabletopAudioMix.FadeTheme(weights, 4, .55f / 60);
        Assert.Equal(1, weights[4]); Assert.All(weights.Take(4), w => Assert.Equal(0, w));
        Assert.InRange(TabletopAudioMix.PeakBound(.70f, .70f, .70f, .70f), .97f, .99f);
    }
}
