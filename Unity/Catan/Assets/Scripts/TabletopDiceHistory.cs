#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;

// 只统计已投影历史里的公开骰点；筛选语义与操作历史保持一致。
public sealed class TabletopDiceHistory
{
    readonly int[] counts = new int[11];
    public GameHistoryEntry[] Rolls { get; private set; }
    public int Total => Rolls.Length;
    public int MaximumCount => counts.Max();

    public TabletopDiceHistory(IEnumerable<GameHistoryEntry> entries, string player = null)
    {
        Rolls = (entries ?? Array.Empty<GameHistoryEntry>()).Where(e => e != null &&
            e.Kind == "RollDice" && e.Dice1 >= 1 && e.Dice1 <= 6 && e.Dice2 >= 1 && e.Dice2 <= 6 &&
            (player == null || e.Involves(player))).ToArray();
        foreach (var roll in Rolls) counts[roll.Dice1 + roll.Dice2 - 2]++;
    }

    public int Count(int total) => total >= 2 && total <= 12 ? counts[total - 2] : 0;
    public double Frequency(int total) => Total == 0 ? 0 : (double)Count(total) / Total;
}
