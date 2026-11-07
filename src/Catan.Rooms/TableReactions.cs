using Catan.Network.Contracts;
using System.Diagnostics;

namespace Catan.Rooms;

public sealed partial class RoomDirectory
{
    private sealed partial class Room
    {
        private readonly Queue<(TableReaction Item, DateTimeOffset At)> recentReactions = new();
        private readonly Queue<(string Seat, string RequestId)> reactionReceipts = new();
        private int reactionGame;
        private long reactionSequence;

        private void TrimReactions(DateTimeOffset now)
        {
            if (reactionGame != state.GameNumber)
            {
                recentReactions.Clear();
                reactionReceipts.Clear();
                reactionGame = state.GameNumber;
                reactionSequence = 0;
            }
            while (recentReactions.Count > 0 && now - recentReactions.Peek().At > TimeSpan.FromSeconds(2))
                recentReactions.Dequeue();
        }

        private static TableReaction ForLegacyClient(TableReaction item)
        {
            if (item.Kind != "shoe" && item.Kind != "wine") return item;
            return new() { Sequence = item.Sequence, Source = item.Source, Target = item.Target,
                Kind = item.Kind == "shoe" ? "egg" : "flower" };
        }

        private TableReactionFeed RecentReactions(bool combos)
        {
            TrimReactions(owner.options.Clock());
            return new() { GameNumber = reactionGame, Through = reactionSequence,
                Items = recentReactions.Select(entry => combos ? entry.Item : ForLegacyClient(entry.Item)).ToArray() };
        }

        private NetworkResponse React(NetworkRequest request, SavedSeat seat)
        {
            var gift = WireJson.Deserialize<ThrowReaction>(request.Payload ?? "{}");
            if (game == null || gift == null || gift.GameNumber != state.GameNumber ||
                gift.Throws == null || gift.Throws.Length is < 1 or > 16 ||
                gift.Throws.Any(item => item == null || item.Kind is not ("egg" or "flower" or "shoe" or "wine") || !SeatIds(state.Settings.PlayerCount).Contains(item.Target)) ||
                !Guid.TryParseExact(request.RequestId, "N", out _))
                return Reject("InvalidReaction", "互动目标无效。");

            var now = owner.options.Clock();
            TrimReactions(now);
            var receipt = (seat.SeatId, request.RequestId);
            if (!reactionReceipts.Contains(receipt))
            {
                if (reactionReceipts.Count == 128) reactionReceipts.Dequeue();
                reactionReceipts.Enqueue(receipt);
                foreach (var item in gift.Throws)
                {
                    if (recentReactions.Count == 128) recentReactions.Dequeue();
                    recentReactions.Enqueue((new() { Kind = item.Kind, Source = seat.SeatId, Target = item.Target,
                        Sequence = ++reactionSequence }, now));
                }
                pendingChange = new(state.RoomId, state.Revision, Stopwatch.GetTimestamp());
            }
            return new() { Success = true, Code = "ReactionAccepted" };
        }
    }
}
