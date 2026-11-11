#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;

public enum KnightVoiceAction { None, Build, Activate, ChasePirate, ChaseRobber, Attack, Move }

public readonly struct KnightVoiceSelection
{
    public readonly KnightVoiceAction Action;
    public readonly int Level;
    public bool Valid => Action >= KnightVoiceAction.Build && Action <= KnightVoiceAction.Move && Level >= 1 && Level <= 3;
    public KnightVoiceSelection(KnightVoiceAction action, int level) { Action = action; Level = level; }
}

public static class KnightVoiceCatalog
{
    public const int ClipCount = 36;
    public const float Gain = .83f;
    public const string Root = "TabletopAudio/KnightVoices/v001/";
    public static int Index(KnightVoiceAction action, int level, int variant)
        => action >= KnightVoiceAction.Build && action <= KnightVoiceAction.Move && level >= 1 && level <= 3 && variant >= 0 && variant <= 1
            ? ((level - 1) * 6 + (int)action - 1) * 2 + variant : -1;
    public static string? Path(KnightVoiceAction action, int level, int variant)
    {
        if (Index(action, level, variant) < 0) return null;
        string stem = action == KnightVoiceAction.Build ? (level == 1 ? "KnightRecruit" : "KnightPromote") : "Knight" + action;
        return Root + stem + "_L" + level + "_" + (variant == 0 ? "A" : "B");
    }
    public static TabletopAudioCue Effect(KnightVoiceSelection voice)
        => voice.Action == KnightVoiceAction.Build ? (voice.Level == 1 ? TabletopAudioCue.KnightRecruit : TabletopAudioCue.KnightPromote) :
           voice.Action == KnightVoiceAction.Activate ? TabletopAudioCue.KnightActivate :
           voice.Action == KnightVoiceAction.Move ? TabletopAudioCue.KnightMove : TabletopAudioCue.KnightCombat;
}

// 只读取已投影的公开棋子变化；实体等级不受三国的临时战力加成影响。
public static class KnightVoiceSelector
{
    public static int Level(string? name)
    {
        name = name ?? string.Empty;
        int end = name.IndexOf("级骑士", StringComparison.Ordinal);
        return end > 0 && int.TryParse(name.Substring(0, end), out int level) && level >= 1 && level <= 3 ? level : 0;
    }
    static bool SameKnight(GameHistoryChange old, GameHistoryChange next)
        => old.PlayerId == next.PlayerId && Level(old.Name) == Level(next.Name);
    static bool Active(GameHistoryChange piece) => piece.Name.Contains("（激活）");
    static bool Resting(GameHistoryChange piece) => piece.Name.Contains("（休眠）");
    static GameHistoryChange? Pick(IEnumerable<GameHistoryChange> values, string actor)
        => values.OrderByDescending(piece => piece.PlayerId == actor).ThenByDescending(piece => Level(piece.Name)).FirstOrDefault();
    public static KnightVoiceSelection Select(GameHistoryEntry? entry)
    {
        if (entry == null) return default;
        var pieces = (entry.Changes ?? Array.Empty<GameHistoryChange>())
            .Where(piece => piece != null && !piece.IsPrivate && piece.Category == "piece" && Level(piece.Name) > 0).ToArray();
        var removed = pieces.Where(piece => piece.Amount < 0).ToArray();
        var added = pieces.Where(piece => piece.Amount > 0).ToArray();
        var promotions = added.Where(next => removed.Any(old => old.PlayerId == next.PlayerId && old.LocationId == next.LocationId && Level(old.Name) < Level(next.Name)));
        var activations = added.Where(next => Active(next) && removed.Any(old => SameKnight(old, next) && old.LocationId == next.LocationId && Resting(old)));
        var exhausted = added.Where(next => Resting(next) && removed.Any(old => SameKnight(old, next) && Active(old)));
        KnightVoiceAction action;
        GameHistoryChange? selected;
        switch (entry.Kind)
        {
            case "RecruitKnight": action = KnightVoiceAction.Build; selected = Pick(added.Where(piece => Level(piece.Name) == 1), entry.PlayerId); break;
            case "PromoteKnight": action = KnightVoiceAction.Build; selected = Pick(promotions, entry.PlayerId); break;
            case "ActivateKnight": action = KnightVoiceAction.Activate; selected = Pick(activations, entry.PlayerId); break;
            case "MoveKnight":
                action = KnightVoiceAction.Move;
                selected = Pick(added.Where(next => removed.Any(old => SameKnight(old, next) && old.LocationId != next.LocationId)), entry.PlayerId); break;
            case "DisplaceKnight": action = KnightVoiceAction.Attack; selected = Pick(exhausted, entry.PlayerId); break;
            case "ChaseRobber": action = KnightVoiceAction.ChaseRobber; selected = Pick(exhausted, entry.PlayerId); break;
            case "ChasePirate": action = KnightVoiceAction.ChasePirate; selected = Pick(exhausted, entry.PlayerId); break;
            default:
                selected = Pick(promotions, entry.PlayerId);
                if (selected != null) { action = KnightVoiceAction.Build; break; }
                if (entry.Kind != "PlayProgressCard" && entry.Kind != "ResolveProgressChoice" && entry.Kind != "UseGeneralSkill") return default;
                selected = Pick(activations, entry.PlayerId); action = KnightVoiceAction.Activate; break;
        }
        return selected == null ? default : new KnightVoiceSelection(action, Level(selected.Name));
    }
}

public sealed class KnightVoiceVariants
{
    readonly int[] next = new int[18];
    public int Take(KnightVoiceAction action, int level)
    {
        int index = KnightVoiceCatalog.Index(action, level, 0);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(action));
        index /= 2;
        int result = next[index]; next[index] = 1 - result; return result;
    }
}

public readonly struct PendingKnightVoice
{
    public readonly KnightVoiceSelection Voice;
    public readonly double At;
    public PendingKnightVoice(KnightVoiceSelection voice, double at) { Voice = voice; At = at; }
}

public sealed class KnightVoiceQueue
{
    public const int Limit = 4;
    public const double Lifetime = 4;
    readonly Queue<PendingKnightVoice> pending = new Queue<PendingKnightVoice>();
    public int Count => pending.Count;
    public void Clear() => pending.Clear();
    public void Enqueue(KnightVoiceSelection voice, double at)
    {
        if (!voice.Valid) return;
        if (pending.Count == Limit) pending.Dequeue();
        pending.Enqueue(new PendingKnightVoice(voice, at));
    }
    public bool TryTake(double now, out PendingKnightVoice voice)
    {
        while (pending.Count > 0)
        {
            voice = pending.Dequeue();
            if (now >= voice.At && now - voice.At < Lifetime) return true;
        }
        voice = default; return false;
    }
}
