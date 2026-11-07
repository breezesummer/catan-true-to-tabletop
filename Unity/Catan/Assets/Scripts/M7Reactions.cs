using System;
using System.Collections.Generic;
using UnityEngine;
using Catan.Network.Contracts;

public sealed partial class M7Client
{
    public event Action<string,string,string> ReactionReceived;
    string reactionConnection;
    int reactionGame;
    bool reactionSending;
    float nextReactionSend;
    readonly Queue<ReactionTarget> outgoingReactions = new Queue<ReactionTarget>();
    long reactionThrough;
    internal bool ReactionsAvailable => Connected && Playing && Room?.Started == true &&
        reactionConnection == connectionId && reactionGame == Room.GameNumber;

    void ReadReactionFeed(NetworkResponse response)
    {
        var feed = response.Reactions;
        if (feed == null || response.RoomId != RoomId || response.ConnectionId != connectionId ||
            feed.GameNumber != Room?.GameNumber) return;
        if (reactionConnection != connectionId || reactionGame != feed.GameNumber)
        {
            outgoingReactions.Clear();
            reactionConnection = connectionId;
            reactionGame = feed.GameNumber;
            reactionThrough = feed.Through;
            return;
        }
        if (feed.Through <= reactionThrough) return;
        foreach (var item in feed.Items ?? Array.Empty<TableReaction>())
            if (item.Sequence > reactionThrough && item.Source != SeatId)
                ReactionReceived?.Invoke(item.Kind, item.Source, item.Target);
        reactionThrough = feed.Through;
    }

    internal void SendReaction(string kind, string target)
    {
        if (!ReactionsAvailable || outgoingReactions.Count >= 64) return;
        outgoingReactions.Enqueue(new ReactionTarget { Kind = kind, Target = target });
        FlushReactions();
    }

    async void FlushReactions()
    {
        if (outgoingReactions.Count == 0) return;
        if (!ReactionsAvailable) { outgoingReactions.Clear(); return; }
        if (reactionSending || Time.unscaledTime < nextReactionSend) return;
        var batch = new ReactionTarget[Math.Min(16, outgoingReactions.Count)];
        for (int i = 0; i < batch.Length; i++) batch[i] = outgoingReactions.Dequeue();
        var request = Envelope("React", WireJson.Serialize(new ThrowReaction { Throws = batch, GameNumber = Room.GameNumber }));
        int current = generation;
        reactionSending = true;
        nextReactionSend = Time.unscaledTime + .08f;
        try { await Send(request, lifetime.Token); }
        catch (Exception error) { if (current == generation) Diagnose("React", error); }
        finally { if (current == generation) reactionSending = false; }
    }
}
