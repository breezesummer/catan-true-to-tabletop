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
    public static class WireJson
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
            if (json == null || Encoding.UTF8.GetByteCount(json) > NetworkProtocol.MaximumResponseBytes) throw new SerializationException("消息大小无效。");
            var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 48, MaxArrayLength = 100000, MaxStringContentLength = NetworkProtocol.MaximumResponseBytes, MaxBytesPerRead = NetworkProtocol.MaximumResponseBytes };
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), quotas)) return Decode(XElement.Load(reader), type, 0);
        }
        private static void CheckRoot(Type type)
        {
            var element = type.IsArray ? type.GetElementType() : type;
            var messages = new[] { typeof(NetworkRequest), typeof(NetworkResponse), typeof(RoomSettings), typeof(CreateRoomPayload), typeof(JoinRoomPayload), typeof(SeatPayload), typeof(NamePayload), typeof(ReadyPayload), typeof(HistoryPayload), typeof(RoomSummary), typeof(SeatSummary) };
            if (!messages.Contains(element) && !Roots.Contains(element.FullName)) throw new SerializationException("不允许传输此对象类型。");
        }
        // 类型布局不可变；避免每个地块、边和历史项重复反射及构造查找表。
        private sealed class Layout
        {
            public readonly MemberInfo[] Members;
            public readonly Dictionary<string, MemberInfo> ByName;
            public Layout(Type type)
            {
                Members = DiscoverMembers(type).ToArray();
                ByName = Members.ToDictionary(Name, StringComparer.Ordinal);
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
            else { node.SetAttributeValue("type", "object"); foreach (var member in GetLayout(type).Members) node.Add(Encode(Name(member), Get(member, value), MemberType(member), depth + 1)); }
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
    }
}
