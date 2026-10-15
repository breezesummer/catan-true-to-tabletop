using System;
using System.Linq;
using Catan.Core;

public sealed partial class M6App
{
    Func<GameHistoryEntry[]> historySource;
    Func<int> historyRevision;
    int lastHistoryRevision = -1;
    GameHistoryEntry[] historyEntries = Array.Empty<GameHistoryEntry>();
    public GameHistoryEntry[] HistoryEntries
    {
        get
        {
            if (!Ready || historySource == null) return Array.Empty<GameHistoryEntry>();
            int revision = historyRevision();
            if (revision != lastHistoryRevision)
            {
                historyEntries = historySource();
                lastHistoryRevision = revision;
            }
            return historyEntries;
        }
    }
    public string[] HistoryPlayers => Enumerable.Range(1, PlayerCount).Select(i => "P" + i).ToArray();
    void BindHistory(Func<GameHistoryEntry[]> source, Func<int> revision)
    {
        historySource = source; historyRevision = revision; lastHistoryRevision = -1;
        historyEntries = Array.Empty<GameHistoryEntry>();
        ResetTabletopAudioHistory();
    }
}
