using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace Catan.Network.Contracts
{
    [DataContract] public sealed class SeatStatePacket
    {
        [DataMember] public int Version = 1;
        [DataMember] public string RoomId, SeatId, ConnectionId, RuleSet;
        [DataMember] public int GameNumber;
        [DataMember] public long BaseRevision, Revision;
        [DataMember] public string ViewHash, MapVersion;
        [DataMember] public long EventBaseVersion, EventVersion, EventThroughSequence;
        [DataMember(EmitDefaultValue=false)] public string Code;
        [DataMember(EmitDefaultValue=false)] public bool Full;
        [DataMember(EmitDefaultValue=false)] public string FullViewJson;
        [DataMember(EmitDefaultValue=false)] public VisibleJsonChange[] Changes;
        [DataMember(EmitDefaultValue=false)] public string EventUpdatesJson;
    }

    /// <summary>每条连接独立，仅在发送队列取出消息时编码；未发送的快照可安全合并。</summary>
    public sealed class SeatStateSender
    {
        private WireJson.JsonValue view;
        private string scope;
        private long revision, eventVersion;
        private Dictionary<string, string> events = new Dictionary<string, string>(StringComparer.Ordinal);
        public NetworkResponse Encode(NetworkResponse response, bool forceFull = false)
        {
            if (!response.Success || response.Room == null || response.SeatId == null || response.ConnectionId == null) return response;
            var next = WireJson.Document(response.ViewJson ?? "null");
            var history = EventIndex(response.PresentationHistoryJson ?? "[]", out long through);
            string nextScope = Scope(response);
            bool full = forceFull || view == null || scope != nextScope || revision > response.Revision || events.Keys.Except(history.Keys).Any();
            var changes = full ? null : VisibleJsonDelta.Between(view, next);
            var updates = full ? history.Values.ToArray() : history.Where(p => !events.TryGetValue(p.Key, out var old) || old != p.Value).Select(p => p.Value).ToArray();
            long nextVersion = scope != nextScope || view == null ? 1 : eventVersion + (updates.Length > 0 || events.Count != history.Count ? 1 : 0);
            var packet = new SeatStatePacket
            {
                RoomId = response.RoomId, SeatId = response.SeatId, ConnectionId = response.ConnectionId, RuleSet = response.RuleSet,
                GameNumber = response.Room.GameNumber, Full = full, FullViewJson = full ? next.Json : null, Changes = changes,
                BaseRevision = full ? 0 : revision, Revision = response.Revision, ViewHash = WireJson.Digest(next.Json), MapVersion = MapVersion(next),
                EventBaseVersion = full ? 0 : eventVersion, EventVersion = nextVersion, EventThroughSequence = through,
                EventUpdatesJson = updates.Length == 0 ? null : "[" + string.Join(",", updates) + "]", Code = response.Code
            };
            // 大范围变更时用完整快照，避免增量反而更大；两条路径包含同一批事件。
            if (!full && WireJson.SerializeDirect(changes).Length > next.Json.Length)
            { packet.Full = true; packet.BaseRevision = 0; packet.FullViewJson = next.Json; packet.Changes = null; packet.EventBaseVersion = 0; packet.EventUpdatesJson = "[" + string.Join(",", history.Values) + "]"; }
            var result = response.Copy();
            result.Code = NetworkProtocol.SeatStateCode; result.ViewJson = WireJson.SerializeDirect(packet);
            result.PresentationHistoryJson = null; result.PresentationHistoryAfterSequence = 0;
            if (result.ViewJson.Length > NetworkProtocol.MaximumResponseBytes) throw VisibleJsonDelta.Invalid();
            view = next; scope = nextScope; revision = response.Revision; events = history; eventVersion = nextVersion;
            return result;
        }
        internal static string Scope(NetworkResponse response) => string.Join("\n", response.RoomId, response.SeatId, response.ConnectionId, response.RuleSet, response.Room?.GameNumber.ToString(CultureInfo.InvariantCulture));
        internal static string MapVersion(WireJson.JsonValue view)
        {
            if (view.Object == null || !view.Object.TryGetValue("Board", out var board) || board.Object == null) return WireJson.Digest("null");
            return WireJson.Digest(string.Join("\n", new[] { "Tiles", "Vertices", "Edges" }.Select(k => board.Object.TryGetValue(k, out var value) ? value.Json : "null")));
        }
        internal static Dictionary<string, string> EventIndex(string json, out long through)
        {
            var values = WireJson.Document(json).Array;
            if (values == null) throw VisibleJsonDelta.Invalid();
            var result = new Dictionary<string, string>(StringComparer.Ordinal); through = 0;
            foreach (var entry in values)
            {
                if (entry.Object == null || !entry.Object.TryGetValue("Sequence", out var sequence) || !long.TryParse(sequence.Raw, NumberStyles.None, CultureInfo.InvariantCulture, out long number) || number < 0) throw VisibleJsonDelta.Invalid();
                entry.Object.TryGetValue("SkillEventId", out var skill);
                if (skill != null && skill.Raw != "null" && skill.Text == null) throw VisibleJsonDelta.Invalid();
                string key = skill == null || skill.Raw == "null" ? "command:" + number.ToString(CultureInfo.InvariantCulture) : "skill:" + skill.Text;
                if (result.ContainsKey(key)) throw VisibleJsonDelta.Invalid();
                result.Add(key, entry.Json); through = Math.Max(through, number);
            }
            return result;
        }
    }

    /// <summary>后台复原完整逻辑视图，校验通过后才提交基准；实例不能跨连接复用。</summary>
    public sealed class SeatStateReceiver
    {
        private readonly string roomId, seatId, connectionId;
        private WireJson.JsonValue view;
        private string scope, viewHash;
        private int gameNumber = -1;
        private long revision = -1, eventVersion, eventThroughSequence;
        public SeatStateReceiver(string roomId, string seatId, string connectionId) { this.roomId = roomId; this.seatId = seatId; this.connectionId = connectionId; }
        public SeatStateApplication Prepare(NetworkResponse response)
        {
            if (response.Code != NetworkProtocol.SeatStateCode || !response.Success || response.Room == null) throw VisibleJsonDelta.Invalid();
            var packet = WireJson.Deserialize<SeatStatePacket>(response.ViewJson);
            string nextScope = SeatStateSender.Scope(response);
            if (packet == null || packet.Version != 1 || packet.RoomId != roomId || packet.SeatId != seatId || packet.ConnectionId != connectionId ||
                packet.RoomId != response.RoomId || packet.SeatId != response.SeatId || packet.ConnectionId != response.ConnectionId || packet.RuleSet != response.RuleSet ||
                packet.GameNumber != response.Room.GameNumber || packet.Revision != response.Revision || packet.Revision < 0 || packet.EventThroughSequence < 0) throw VisibleJsonDelta.Invalid();
            var expanded = response.Copy(); expanded.Code = packet.Code; expanded.ViewJson = null;
            expanded.PresentationHistoryJson = null; expanded.PresentationHistoryAfterSequence = 0;
            if (packet.GameNumber < gameNumber || packet.GameNumber == gameNumber && packet.Revision < revision ||
                packet.GameNumber == gameNumber && packet.Revision == revision && packet.EventVersion == eventVersion && packet.ViewHash == viewHash)
                return new SeatStateApplication(expanded, () => { });
            if (packet.EventVersion < 1 || packet.Full && (packet.BaseRevision != 0 || packet.EventBaseVersion != 0 || packet.FullViewJson == null || packet.Changes != null) ||
                !packet.Full && (view == null || nextScope != scope || packet.BaseRevision != revision || packet.EventBaseVersion != eventVersion || packet.FullViewJson != null || packet.EventVersion < eventVersion || packet.EventVersion > eventVersion + 1)) throw VisibleJsonDelta.Invalid();
            var next = packet.Full ? WireJson.Document(packet.FullViewJson) : VisibleJsonDelta.Apply(view, packet.Changes);
            if (WireJson.Digest(next.Json) != packet.ViewHash || SeatStateSender.MapVersion(next) != packet.MapVersion) throw VisibleJsonDelta.Invalid();
            var updates = SeatStateSender.EventIndex(packet.EventUpdatesJson ?? "[]", out long through);
            if (through > packet.EventThroughSequence || packet.Full && through != packet.EventThroughSequence ||
                packet.GameNumber == gameNumber && packet.EventThroughSequence < eventThroughSequence ||
                !packet.Full && packet.EventVersion != eventVersion + (updates.Count > 0 ? 1 : 0)) throw VisibleJsonDelta.Invalid();
            expanded.ViewJson = next.Raw == "null" ? null : next.Json;
            expanded.PresentationHistoryJson = packet.EventUpdatesJson ?? "[]";
            return new SeatStateApplication(expanded, () =>
            {
                view = next; viewHash = packet.ViewHash; scope = nextScope; revision = packet.Revision; gameNumber = packet.GameNumber; eventVersion = packet.EventVersion; eventThroughSequence = packet.EventThroughSequence;
            });
        }
    }
    public sealed class SeatStateApplication
    {
        public NetworkResponse Response { get; }
        private readonly Action commit;
        internal SeatStateApplication(NetworkResponse response, Action commit) { Response = response; this.commit = commit; }
        public void Commit() => commit();
    }
}
