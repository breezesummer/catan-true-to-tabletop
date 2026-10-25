using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Catan.Network.Contracts
{
    /// <summary>显式类型 JSON，不接受类型名或多态。兼容 Unity 与 internal setter 的可见视图。</summary>
    public static partial class WireJson
    {
        private static readonly string[] Roots = { "Catan.Core.M2.Command", "Catan.Core.M3.Command", "Catan.Core.M4.Command", "Catan.Core.M5.Command", "Catan.Core.M2.PlayerView", "Catan.Core.M3.PlayerView", "Catan.Core.M4.PlayerView", "Catan.Core.M5.PlayerView", "Catan.Core.GameHistoryEntry" };
        public static string Serialize<T>(T value) => SerializeObject(value);
        public static string SerializeObject(object value)
        {
            if (value == null) return "null";
            CheckRoot(value.GetType());
            var root = Encode("root", value, value.GetType(), 0);
            using (var stream = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, false, false)) root.WriteTo(writer);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static T Deserialize<T>(string json) => (T)Deserialize(json, typeof(T));
        public static object Deserialize(string json, Type type)
        {
            CheckRoot(type);
            CheckSize(json);
            return new DirectReader(json).ReadRoot(type);
        }
        // 仅用于诊断对照与可控回退；线格式、类型白名单及字段合同不变。
        public static T DeserializeLegacy<T>(string json) => (T)DeserializeLegacy(json, typeof(T));
        public static object DeserializeLegacy(string json, Type type)
        {
            CheckRoot(type);
            CheckSize(json);
            var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 48, MaxArrayLength = 100000, MaxStringContentLength = NetworkProtocol.MaximumResponseBytes, MaxBytesPerRead = NetworkProtocol.MaximumResponseBytes };
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), quotas)) return Decode(XElement.Load(reader), type, 0);
        }
        private static void CheckSize(string json)
        {
            if (json == null || json.Length > NetworkProtocol.MaximumResponseBytes || Encoding.UTF8.GetByteCount(json) > NetworkProtocol.MaximumResponseBytes) throw new SerializationException("消息大小无效。");
        }
        private static void CheckRoot(Type type)
        {
            var element = type.IsArray ? type.GetElementType() : type;
            var messages = new[] { typeof(NetworkRequest), typeof(NetworkResponse), typeof(RoomSettings), typeof(CreateRoomPayload), typeof(JoinRoomPayload), typeof(SeatPayload), typeof(NamePayload), typeof(ReadyPayload), typeof(HistoryPayload), typeof(RoomSummary), typeof(SeatSummary), typeof(SeatStatePacket), typeof(VisibleJsonChange) };
            if (!messages.Contains(element) && !Roots.Contains(element.FullName)) throw new SerializationException("不允许传输此对象类型。");
        }
        // 类型布局不可变；避免每个地块、边和历史项重复反射及构造查找表。
        private sealed class Layout
        {
            public readonly MemberInfo[] Members;
            public readonly Dictionary<string, MemberInfo> ByName;
            public readonly string[] Names;
            public readonly Type[] Types;
            public readonly bool[] OmitDefault;
            public readonly object[] Defaults;
            public Layout(Type type)
            {
                Members = DiscoverMembers(type).ToArray();
                ByName = Members.ToDictionary(Name, StringComparer.Ordinal);
                Names = Members.Select(Name).ToArray(); Types = Members.Select(MemberType).ToArray();
                OmitDefault = Members.Select(m => m.GetCustomAttribute<DataMemberAttribute>()?.EmitDefaultValue == false).ToArray();
                Defaults = Types.Select(t => t.IsValueType ? Activator.CreateInstance(t) : null).ToArray();
            }
        }
        private static readonly ConcurrentDictionary<Type, Layout> Layouts = new ConcurrentDictionary<Type, Layout>();
        private static Layout GetLayout(Type type) => Layouts.GetOrAdd(type, t => new Layout(t));
        private static IEnumerable<MemberInfo> DiscoverMembers(Type type)
        {
            bool contract = type.IsDefined(typeof(DataContractAttribute), false);
            return type.GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(m =>
                (m is FieldInfo || m is PropertyInfo p && p.GetIndexParameters().Length == 0 && p.GetGetMethod() != null && p.GetSetMethod(true) != null)
                && !m.IsDefined(typeof(IgnoreDataMemberAttribute), false)
                && (!contract || m.IsDefined(typeof(DataMemberAttribute), false))).OrderBy(m => m.Name, StringComparer.Ordinal);
        }
        private static string Name(MemberInfo member) => member.GetCustomAttribute<DataMemberAttribute>()?.Name ?? member.Name;
        private static Type MemberType(MemberInfo member) => member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
        private static object Get(MemberInfo member, object owner) => member is FieldInfo field ? field.GetValue(owner) : ((PropertyInfo)member).GetValue(owner);
        private static void Set(MemberInfo member, object owner, object value) { if (member is FieldInfo field) field.SetValue(owner, value); else ((PropertyInfo)member).SetValue(owner, value); }
        private static XElement Encode(string name, object value, Type type, int depth)
        {
            if (depth > 40) throw new SerializationException("消息嵌套过深。");
            var node = new XElement(name);
            if (value == null) { node.SetAttributeValue("type", "null"); return node; }
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(string)) { node.SetAttributeValue("type", "string"); node.Value = (string)value; }
            else if (type == typeof(bool)) { node.SetAttributeValue("type", "boolean"); node.Value = (bool)value ? "true" : "false"; }
            else if (type.IsEnum || type.IsPrimitive || type == typeof(decimal)) { node.SetAttributeValue("type", "number"); node.Value = type.IsEnum ? Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture); }
            else if (type.IsArray) { node.SetAttributeValue("type", "array"); foreach (var item in (Array)value) node.Add(Encode("item", item, type.GetElementType(), depth + 1)); }
            else
            {
                node.SetAttributeValue("type", "object");
                foreach (var member in GetLayout(type).Members)
                {
                    var memberValue = Get(member, value);
                    // 可选扩展关闭时省略空字段，保持既有协议3客户端的严格字段布局。
                    if (member.GetCustomAttribute<DataMemberAttribute>()?.EmitDefaultValue == false)
                    {
                        var memberType=MemberType(member);
                        if(memberValue==null||memberType.IsValueType&&Nullable.GetUnderlyingType(memberType)==null&&memberValue.Equals(Activator.CreateInstance(memberType)))continue;
                    }
                    node.Add(Encode(Name(member), memberValue, MemberType(member), depth + 1));
                }
            }
            return node;
        }
        private static object Decode(XElement node, Type type, int depth)
        {
            if (depth > 40) throw new SerializationException("消息嵌套过深。");
            if (node.Attributes().Any(a => a.Name.LocalName != "type")) throw new SerializationException("不允许消息携带类型信息。");
            if ((string)node.Attribute("type") == "null") { if (type.IsValueType && Nullable.GetUnderlyingType(type) == null) throw new SerializationException("字段不能为 null。"); return null; }
            type = Nullable.GetUnderlyingType(type) ?? type;
            string expectedKind = type == typeof(string) ? "string" : type == typeof(bool) ? "boolean" : type.IsEnum || type.IsPrimitive || type == typeof(decimal) ? "number" : type.IsArray ? "array" : "object";
            if ((string)node.Attribute("type") != expectedKind) throw new SerializationException("字段 JSON 类型不匹配。");
            if (type == typeof(string)) return node.Value;
            if (type.IsEnum) { var number = long.Parse(node.Value, CultureInfo.InvariantCulture); var value = Enum.ToObject(type, number); if (!Enum.IsDefined(type, value)) throw new SerializationException("未知枚举值。"); return value; }
            if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(node.Value, type, CultureInfo.InvariantCulture);
            if (type.IsArray) { var children = node.Elements().ToArray(); var values = Array.CreateInstance(type.GetElementType(), children.Length); for (int i = 0; i < children.Length; i++) values.SetValue(Decode(children[i], type.GetElementType(), depth + 1), i); return values; }
            if ((string)node.Attribute("type") != "object") throw new SerializationException("需要对象。");
            var instance = Activator.CreateInstance(type);
            var members = GetLayout(type).ByName;
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in node.Elements())
            {
                if (!found.Add(child.Name.LocalName) || !members.TryGetValue(child.Name.LocalName, out var member)) throw new SerializationException("未知或重复字段。");
                Set(member, instance, Decode(child, MemberType(member), depth + 1));
            }
            return instance;
        }
        // 不建立 XML/通用 JSON 对象树，也不使用动态代码生成，兼容 Mono、IL2CPP/AOT。
        private sealed partial class DirectReader
        {
            readonly string json;
            int at;
            internal DirectReader(string json) { this.json = json; }
            internal object ReadRoot(Type type)
            {
                var value = Read(type, 0);
                Space();
                if (at != json.Length) throw Invalid();
                return value;
            }
            static SerializationException Invalid() => new SerializationException("消息 JSON 格式或字段类型无效。");
            void Space() { while (at < json.Length && (json[at] == ' ' || json[at] == '\r' || json[at] == '\n' || json[at] == '\t')) at++; }
            bool Take(char c) { Space(); if (at < json.Length && json[at] == c) { at++; return true; } return false; }
            void Need(char c) { if (!Take(c)) throw Invalid(); }
            bool Literal(string value)
            {
                if (json.Length - at < value.Length || string.CompareOrdinal(json, at, value, 0, value.Length) != 0) return false;
                at += value.Length; return true;
            }
            object Read(Type type, int depth)
            {
                if (depth > 40) throw new SerializationException("消息嵌套过深。");
                Space();
                if (Literal("null"))
                {
                    if (type.IsValueType && Nullable.GetUnderlyingType(type) == null) throw new SerializationException("字段不能为 null。");
                    return null;
                }
                type = Nullable.GetUnderlyingType(type) ?? type;
                if (type == typeof(string)) return String();
                if (type == typeof(bool)) { if (Literal("true")) return true; if (Literal("false")) return false; throw Invalid(); }
                if (type.IsEnum || type.IsPrimitive || type == typeof(decimal))
                {
                    string number = Number();
                    if (type.IsEnum)
                    {
                        long n = long.Parse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                        var value = Enum.ToObject(type, n);
                        // Enum.ToObject 会截断窄枚举的溢出值，须同时验证数值未改变。
                        if (!Enum.IsDefined(type, value) || Convert.ToInt64(value, CultureInfo.InvariantCulture) != n) throw new SerializationException("未知枚举值。");
                        return value;
                    }
                    var scalar = Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
                    if (scalar is double d && (double.IsInfinity(d) || double.IsNaN(d)) || scalar is float f && (float.IsInfinity(f) || float.IsNaN(f))) throw Invalid();
                    return scalar;
                }
                if (type.IsArray)
                {
                    Need('[');
                    Type element = type.GetElementType();
                    var values = new List<object>();
                    if (!Take(']'))
                    {
                        do { if (values.Count >= 100000) throw new SerializationException("消息数组过长。"); values.Add(Read(element, depth + 1)); } while (Take(','));
                        Need(']');
                    }
                    var array = Array.CreateInstance(element, values.Count);
                    for (int i = 0; i < values.Count; i++) array.SetValue(values[i], i);
                    return array;
                }
                Need('{');
                var instance = Activator.CreateInstance(type);
                var members = GetLayout(type).ByName;
                var found = new HashSet<string>(StringComparer.Ordinal);
                if (!Take('}'))
                {
                    do
                    {
                        string name = String(); Need(':');
                        if (!found.Add(name) || !members.TryGetValue(name, out var member)) throw new SerializationException("未知或重复字段。");
                        Set(member, instance, Read(MemberType(member), depth + 1));
                    } while (Take(','));
                    Need('}');
                }
                return instance;
            }
            string Number()
            {
                int start = at;
                if (at < json.Length && json[at] == '-') at++;
                if (at >= json.Length) throw Invalid();
                if (json[at] == '0') at++;
                else { if (json[at] < '1' || json[at] > '9') throw Invalid(); Digits(); }
                if (at < json.Length && json[at] == '.') { at++; int before = at; Digits(); if (at == before) throw Invalid(); }
                if (at < json.Length && (json[at] == 'e' || json[at] == 'E'))
                { at++; if (at < json.Length && (json[at] == '+' || json[at] == '-')) at++; int before = at; Digits(); if (at == before) throw Invalid(); }
                return json.Substring(start, at - start);
            }
            void Digits() { while (at < json.Length && json[at] >= '0' && json[at] <= '9') at++; }
            string String()
            {
                Need('"');
                int start = at;
                StringBuilder text = null;
                while (at < json.Length)
                {
                    char c = json[at++];
                    if (c == '"') { if (text == null) return json.Substring(start, at - start - 1); text.Append(json, start, at - start - 1); return text.ToString(); }
                    if (c < 32) throw Invalid();
                    if (c != '\\') continue;
                    if (text == null) text = new StringBuilder();
                    text.Append(json, start, at - start - 1);
                    if (at >= json.Length) throw Invalid();
                    char escape = json[at++];
                    switch (escape)
                    {
                        case '"': case '\\': case '/': text.Append(escape); break;
                        case 'b': text.Append('\b'); break; case 'f': text.Append('\f'); break;
                        case 'n': text.Append('\n'); break; case 'r': text.Append('\r'); break; case 't': text.Append('\t'); break;
                        case 'u':
                            if (at + 4 > json.Length) throw Invalid();
                            int code = 0;
                            for (int i = 0; i < 4; i++) { char h = json[at++]; int n = h >= '0' && h <= '9' ? h - '0' : h >= 'a' && h <= 'f' ? h - 'a' + 10 : h >= 'A' && h <= 'F' ? h - 'A' + 10 : -1; if (n < 0) throw Invalid(); code = code * 16 + n; }
                            text.Append((char)code); break;
                        default: throw Invalid();
                    }
                    start = at;
                }
                throw Invalid();
            }
        }
    }
}
