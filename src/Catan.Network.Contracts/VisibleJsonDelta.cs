using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Catan.Network.Contracts
{
    // 操作路径只穿过对象；实体数组按明确的稳定键增删改，顺序独立维护。
    [DataContract] public sealed class VisibleJsonChange
    {
        [DataMember] public string[] Path;
        [DataMember(EmitDefaultValue=false)] public string KeyField;
        [DataMember(EmitDefaultValue=false)] public string Key;
        [DataMember(EmitDefaultValue=false)] public string Json;
        [DataMember(EmitDefaultValue=false)] public bool Remove;
        [DataMember(EmitDefaultValue=false)] public string[] Order;
    }

    public static partial class WireJson
    {
        internal sealed class JsonValue
        {
            internal Dictionary<string, JsonValue> Object;
            internal List<JsonValue> Array;
            internal string Raw, Text;
            private string json;
            internal string Json => json ?? (json = Object != null ? "{" + string.Join(",", Object.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => Quote(p.Key) + ":" + p.Value.Json)) + "}" : Array != null ? "[" + string.Join(",", Array.Select(v => v.Json)) + "]" : Raw);
            internal string Identity => Text ?? Raw;
        }
        internal static string Quote(string text)
        {
            var result = new StringBuilder("\"");
            foreach (char c in text)
                if (c == '"' || c == '\\') result.Append('\\').Append(c);
                else if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else result.Append(c);
            return result.Append('"').ToString();
        }
        internal static JsonValue Document(string json)
        {
            CheckSize(json);
            return new DirectReader(json).ReadDocument();
        }
        internal static string Digest(string json)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "");
        }
        private sealed partial class DirectReader
        {
            internal JsonValue ReadDocument()
            {
                var result = ReadValue(0); Space(); if (at != json.Length) throw Invalid(); return result;
            }
            JsonValue ReadValue(int depth)
            {
                if (depth > 40) throw Invalid();
                Space(); int start = at;
                if (at >= json.Length) throw Invalid();
                if (Take('{'))
                {
                    var fields = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                    if (!Take('}'))
                    {
                        do { string key = String(); Need(':'); if (fields.Count >= 100000 || fields.ContainsKey(key)) throw Invalid(); fields.Add(key, ReadValue(depth + 1)); } while (Take(','));
                        Need('}');
                    }
                    return new JsonValue { Object = fields };
                }
                if (Take('['))
                {
                    var values = new List<JsonValue>();
                    if (!Take(']'))
                    {
                        do { if (values.Count >= 100000) throw Invalid(); values.Add(ReadValue(depth + 1)); } while (Take(','));
                        Need(']');
                    }
                    return new JsonValue { Array = values };
                }
                string text = null;
                if (json[at] == '"') text = String();
                else if (!Literal("null") && !Literal("true") && !Literal("false")) Number();
                return new JsonValue { Raw = json.Substring(start, at - start), Text = text };
            }
        }
    }

    internal static class VisibleJsonDelta
    {
        static readonly string[] Keys = { "id", "Id", "locationId", "LocationId", "PlayerId", "SeatId", "EntityId", "Sequence" };
        internal static VisibleJsonChange[] Between(WireJson.JsonValue before, WireJson.JsonValue after)
        {
            var changes = new List<VisibleJsonChange>();
            Compare(before, after, Array.Empty<string>(), changes);
            return changes.ToArray();
        }
        static void Compare(WireJson.JsonValue before, WireJson.JsonValue after, string[] path, List<VisibleJsonChange> changes)
        {
            if (before.Json == after.Json) return;
            if (before.Object != null && after.Object != null)
            {
                foreach (var key in before.Object.Keys.Except(after.Object.Keys)) changes.Add(new VisibleJsonChange { Path = path.Concat(new[] { key }).ToArray(), Remove = true });
                foreach (var pair in after.Object)
                {
                    var next = path.Concat(new[] { pair.Key }).ToArray();
                    if (before.Object.TryGetValue(pair.Key, out var old)) Compare(old, pair.Value, next, changes);
                    else changes.Add(new VisibleJsonChange { Path = next, Json = pair.Value.Json });
                }
                return;
            }
            if (before.Array != null && after.Array != null)
            {
                string key = Keys.FirstOrDefault(k => Keyed(before.Array, k) && Keyed(after.Array, k));
                if (key != null)
                {
                    var old = before.Array.ToDictionary(v => v.Object[key].Identity, StringComparer.Ordinal);
                    var current = after.Array.ToDictionary(v => v.Object[key].Identity, StringComparer.Ordinal);
                    foreach (var id in old.Keys.Except(current.Keys)) changes.Add(new VisibleJsonChange { Path = path, KeyField = key, Key = id, Remove = true });
                    foreach (var pair in current)
                        if (!old.TryGetValue(pair.Key, out var previous) || pair.Value.Json != previous.Json)
                            changes.Add(new VisibleJsonChange { Path = path, KeyField = key, Key = pair.Key, Json = pair.Value.Json });
                    var order = after.Array.Select(v => v.Object[key].Identity).ToArray();
                    if (!before.Array.Select(v => v.Object[key].Identity).SequenceEqual(order)) changes.Add(new VisibleJsonChange { Path = path, KeyField = key, Order = order });
                    return;
                }
            }
            changes.Add(new VisibleJsonChange { Path = path, Json = after.Json });
        }
        static bool Keyed(List<WireJson.JsonValue> array, string key)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            return array.All(v => v.Object != null && v.Object.TryGetValue(key, out var id) && id.Object == null && id.Array == null && id.Raw != "null" && found.Add(id.Identity));
        }
        internal static WireJson.JsonValue Apply(WireJson.JsonValue state, VisibleJsonChange[] changes)
        {
            if (changes == null || changes.Length > 100000) throw Invalid();
            foreach (var change in changes)
            {
                if (change?.Path == null || change.Path.Length > 40 || change.Path.Any(p => p == null)) throw Invalid();
                state = Update(state, change, 0);
            }
            WireJson.Document(state.Json); // 合并后同样受正文、深度与数组上限约束。
            return state;
        }
        static WireJson.JsonValue Update(WireJson.JsonValue node, VisibleJsonChange change, int depth)
        {
            if (depth < change.Path.Length)
            {
                if (node.Object == null) throw Invalid();
                var fields = new Dictionary<string, WireJson.JsonValue>(node.Object, StringComparer.Ordinal);
                string field = change.Path[depth];
                if (depth + 1 == change.Path.Length && change.KeyField == null)
                {
                    if (change.Remove) { if (change.Json != null || !fields.Remove(field)) throw Invalid(); }
                    else fields[field] = WireJson.Document(change.Json);
                }
                else { if (!fields.TryGetValue(field, out var child)) throw Invalid(); fields[field] = Update(child, change, depth + 1); }
                return new WireJson.JsonValue { Object = fields };
            }
            if (change.KeyField == null)
            {
                if (change.Remove) throw Invalid();
                return WireJson.Document(change.Json);
            }
            if (!Keys.Contains(change.KeyField) || node.Array == null || !Keyed(node.Array, change.KeyField)) throw Invalid();
            var array = new List<WireJson.JsonValue>(node.Array);
            if (change.Order != null)
            {
                if (change.Key != null || change.Json != null || change.Remove || change.Order.Length != array.Count || change.Order.Distinct(StringComparer.Ordinal).Count() != array.Count) throw Invalid();
                var keyed = array.ToDictionary(v => v.Object[change.KeyField].Identity, StringComparer.Ordinal);
                if (change.Order.Any(k => k == null || !keyed.ContainsKey(k))) throw Invalid();
                array = change.Order.Select(k => keyed[k]).ToList();
            }
            else
            {
                if (change.Key == null) throw Invalid();
                int index = array.FindIndex(v => v.Object[change.KeyField].Identity == change.Key);
                if (change.Remove) { if (index < 0 || change.Json != null) throw Invalid(); array.RemoveAt(index); }
                else
                {
                    var value = WireJson.Document(change.Json);
                    if (value.Object == null || !value.Object.TryGetValue(change.KeyField, out var id) || id.Identity != change.Key) throw Invalid();
                    if (index < 0) array.Add(value); else array[index] = value;
                }
            }
            return new WireJson.JsonValue { Array = array };
        }
        internal static SerializationException Invalid() => new SerializationException("增量基准、字段或事件水位无效。");
    }
}
