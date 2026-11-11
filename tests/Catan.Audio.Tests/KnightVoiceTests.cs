using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Catan.Core;
using Xunit;

namespace Catan.Audio.Tests;

public sealed class KnightVoiceTests
{
    static T Set<T>(T target, string name, object? value) where T : class
    { typeof(T).GetProperty(name)!.SetValue(target, value); return target; }
    static GameHistoryChange Piece(int level, bool active, int amount, string owner = "P1", string location = "v1", bool hidden = false)
    {
        var piece = new GameHistoryChange();
        Set(piece, "Category", "piece"); Set(piece, "Name", level + "级骑士（" + (active ? "激活" : "休眠") + "）");
        Set(piece, "Amount", amount); Set(piece, "PlayerId", owner); Set(piece, "LocationId", location); Set(piece, "IsPrivate", hidden);
        return piece;
    }
    static GameHistoryEntry Entry(string kind, long sequence = 1, params GameHistoryChange[] changes)
    {
        var entry = new GameHistoryEntry(); Set(entry, "Kind", kind); Set(entry, "Sequence", sequence);
        Set(entry, "PlayerId", "P1"); Set(entry, "Changes", changes); return entry;
    }
    static TabletopAudioEvent Spoken(GameHistoryEntry entry) => Assert.Single(TabletopAudioModel.Classify(entry), e => e.KnightVoice.Valid);

    [Theory]
    [InlineData("ActivateKnight", KnightVoiceAction.Activate, 1)]
    [InlineData("ActivateKnight", KnightVoiceAction.Activate, 2)]
    [InlineData("ActivateKnight", KnightVoiceAction.Activate, 3)]
    [InlineData("MoveKnight", KnightVoiceAction.Move, 1)]
    [InlineData("MoveKnight", KnightVoiceAction.Move, 2)]
    [InlineData("MoveKnight", KnightVoiceAction.Move, 3)]
    [InlineData("ChasePirate", KnightVoiceAction.ChasePirate, 1)]
    [InlineData("ChasePirate", KnightVoiceAction.ChasePirate, 2)]
    [InlineData("ChasePirate", KnightVoiceAction.ChasePirate, 3)]
    [InlineData("ChaseRobber", KnightVoiceAction.ChaseRobber, 1)]
    [InlineData("ChaseRobber", KnightVoiceAction.ChaseRobber, 2)]
    [InlineData("ChaseRobber", KnightVoiceAction.ChaseRobber, 3)]
    [InlineData("DisplaceKnight", KnightVoiceAction.Attack, 1)]
    [InlineData("DisplaceKnight", KnightVoiceAction.Attack, 2)]
    [InlineData("DisplaceKnight", KnightVoiceAction.Attack, 3)]
    public void EachKnightActionUsesItsOwnVoiceAndPhysicalLevel(string kind, KnightVoiceAction action, int level)
    {
        bool activating = action == KnightVoiceAction.Activate;
        string destination = action == KnightVoiceAction.Move || action == KnightVoiceAction.Attack ? "v2" : "v1";
        var sound = Spoken(Entry(kind, 1, Piece(level, !activating, -1), Piece(level, activating, 1, location: destination)));
        Assert.Equal(action, sound.KnightVoice.Action); Assert.Equal(level, sound.KnightVoice.Level);
        Assert.Equal(KnightVoiceCatalog.Effect(sound.KnightVoice), sound.Cue);
    }

    [Theory][InlineData(1)][InlineData(2)][InlineData(3)]
    public void RecruitmentAndPromotionSpeakInTheResultingLevel(int level)
    {
        var entry = level == 1 ? Entry("RecruitKnight", 1, Piece(1, false, 1)) :
            Entry("PromoteKnight", 1, Piece(level - 1, false, -1), Piece(level, false, 1));
        var voice = Spoken(entry).KnightVoice;
        Assert.Equal(KnightVoiceAction.Build, voice.Action); Assert.Equal(level, voice.Level);
    }

    [Fact]
    public void AlliedAttackerKeepsItsVoiceInsteadOfUsingTheDefendersHigherLevel()
    {
        var entry = Entry("DisplaceKnight", 1,
            Piece(3, true, -1, "P3", "enemy"), Piece(1, true, -1, "P2", "ally-source"), Piece(1, false, 1, "P2", "enemy"));
        var voice = Spoken(entry).KnightVoice;
        Assert.Equal(KnightVoiceAction.Attack, voice.Action); Assert.Equal(1, voice.Level);
    }

    [Fact]
    public void HiddenPieceChangesCannotChangeSpeechOrIntroduceAnotherPromotionSound()
    {
        var baseline = Entry("ActivateKnight", 1, Piece(1, false, -1), Piece(1, true, 1));
        var hidden = Entry("ActivateKnight", 1, baseline.Changes.Concat(new[] {
            Piece(1, false, -1, location: "secret", hidden: true), Piece(3, true, 1, location: "secret", hidden: true)
        }).ToArray());
        static string Shape(GameHistoryEntry entry) => string.Join("|", TabletopAudioModel.Classify(entry).Select(e => $"{e.Cue}:{e.Delay}:{e.KnightVoice.Action}:{e.KnightVoice.Level}"));
        Assert.Equal(Shape(baseline), Shape(hidden));
        Assert.DoesNotContain(TabletopAudioModel.Classify(Entry("ActivateKnight", 2, Piece(3, false, -1, hidden: true), Piece(3, true, 1, hidden: true))), e => e.KnightVoice.Valid);
    }

    [Fact]
    public void FreePromotionAndMassActivationSpeakOnceWithoutTreatingThemAsCombat()
    {
        var promotion = Spoken(Entry("ResolveProgressChoice", 1, Piece(1, false, -1), Piece(2, false, 1)));
        Assert.Equal(KnightVoiceAction.Build, promotion.KnightVoice.Action); Assert.Equal(2, promotion.KnightVoice.Level);
        var active = Entry("PlayProgressCard", 2, Enumerable.Range(1, 3).SelectMany(level => new[] {
            Piece(level, false, -1, location: "v" + level), Piece(level, true, 1, location: "v" + level)
        }).ToArray());
        var voice = Spoken(active);
        Assert.Equal(KnightVoiceAction.Activate, voice.KnightVoice.Action); Assert.Equal(3, voice.KnightVoice.Level);
        Assert.DoesNotContain(TabletopAudioModel.Classify(active), e => e.Cue == TabletopAudioCue.KnightCombat);
    }

    [Theory][InlineData("ResolveChoice")][InlineData("AttackFortress")][InlineData("PlayKnight")][InlineData("Rejected")]
    public void RetreatsShipsCardsAndRejectedOperationsDoNotBorrowKnightAttackLines(string kind)
    {
        Assert.DoesNotContain(TabletopAudioModel.Classify(Entry(kind, 1, Piece(2, false, 1))), e => e.KnightVoice.Valid);
    }

    [Fact]
    public void RestoresReconnectsAndDuplicateHistoryDoNotReplaySpeech()
    {
        var first = Entry("RecruitKnight", 1, Piece(1, false, 1));
        var second = Entry("ActivateKnight", 2, Piece(1, false, -1), Piece(1, true, 1));
        var model = new TabletopAudioModel();
        Assert.Empty(model.Observe("room:P1", new[] { first }));
        Assert.Single(model.Observe("room:P1", new[] { first, second }), e => e.KnightVoice.Valid);
        Assert.Empty(model.Observe("room:P1", new[] { first, second }));
        Assert.Empty(model.Observe("room:P1", new[] { first }));
        model.Reset();
        Assert.Empty(model.Observe("room:P1", new[] { first, second }));
        Assert.Empty(model.Observe("room:P2", new[] { first, second }));
    }

    [Fact]
    public void VoiceVariantsAlternateSeparatelyForEveryActionAndLevel()
    {
        var variants = new KnightVoiceVariants();
        Assert.Equal(0, variants.Take(KnightVoiceAction.Attack, 1));
        Assert.Equal(0, variants.Take(KnightVoiceAction.Attack, 2));
        Assert.Equal(0, variants.Take(KnightVoiceAction.Move, 1));
        Assert.Equal(1, variants.Take(KnightVoiceAction.Attack, 1));
        Assert.Equal(0, variants.Take(KnightVoiceAction.Attack, 1));
    }

    [Fact]
    public void SpeechQueueIsBoundedExpiresAndClearsOnReset()
    {
        var queue = new KnightVoiceQueue();
        for (int index = 0; index < 10; index++) queue.Enqueue(new KnightVoiceSelection(KnightVoiceAction.Move, 1), index);
        Assert.Equal(KnightVoiceQueue.Limit, queue.Count);
        Assert.True(queue.TryTake(9, out var next)); Assert.Equal(6, next.At);
        queue.Clear(); queue.Enqueue(new KnightVoiceSelection(KnightVoiceAction.Attack, 3), 1);
        Assert.False(queue.TryTake(1 + KnightVoiceQueue.Lifetime, out _)); Assert.Equal(0, queue.Count);
        queue.Enqueue(new KnightVoiceSelection((KnightVoiceAction)99, 2), 10);
        queue.Enqueue(new KnightVoiceSelection(KnightVoiceAction.Move, 4), 10);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void AllThirtySixRuntimeFilesAreTheApprovedDistinctRecordingsAndFitTheSpeechBus()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Unity", "Catan"))) root = root.Parent;
        Assert.NotNull(root);
        string resources = Path.Combine(root!.FullName, "Unity", "Catan", "Assets", "Resources");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(resources, KnightVoiceCatalog.Root, "manifest.json")));
        var entries = manifest.RootElement.GetProperty("entries").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!);
        Assert.Equal(36, entries.Count);
        var hashes = new HashSet<string>(); var indices = new HashSet<int>();
        for (int level = 1; level <= 3; level++) for (int action = 1; action <= 6; action++) for (int variant = 0; variant < 2; variant++)
        {
            var kind = (KnightVoiceAction)action;
            var asset = KnightVoiceCatalog.Path(kind, level, variant)!;
            var bytes = File.ReadAllBytes(Path.Combine(resources, asset + ".wav"));
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.Equal(entries[Path.GetFileName(asset)].GetProperty("sha256").GetString(), hash);
            Assert.True(hashes.Add(hash)); Assert.True(indices.Add(KnightVoiceCatalog.Index(kind, level, variant)));
            Assert.Equal(24000, BitConverter.ToInt32(bytes, 24)); Assert.Equal(1, BitConverter.ToInt16(bytes, 22));
            Assert.Equal(16, BitConverter.ToInt16(bytes, 34));
            float peak = 0;
            for (int sample = 44; sample + 1 < bytes.Length; sample += 2) peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(bytes, sample) / 32768f));
            Assert.InRange(peak * KnightVoiceCatalog.Gain, .01f, .701f);
        }
        Assert.Equal(36, indices.Count);
    }
}
