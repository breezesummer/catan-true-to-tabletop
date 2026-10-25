using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;

namespace Catan.Network.Contracts
{
    public static partial class WireJson
    {
        /// <summary>相同类型白名单与字段合同的直接编码；原编码保留作兼容对照。</summary>
        public static string SerializeDirect<T>(T value) => SerializeObjectDirect(value);
        public static string SerializeObjectDirect(object value)
        {
            if (value == null) return "null";
            CheckRoot(value.GetType());
            var output = new StringBuilder(); WriteDirect(output, value, value.GetType(), 0); return output.ToString();
        }
        private static void WriteDirect(StringBuilder output, object value, Type type, int depth)
        {
            if (depth > 40) throw new SerializationException("消息嵌套过深。");
            if (value == null) { output.Append("null"); return; }
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(string)) { output.Append(Quote((string)value)); return; }
            if (type == typeof(bool)) { output.Append((bool)value ? "true" : "false"); return; }
            if (type.IsEnum) { output.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture)); return; }
            if (type.IsPrimitive || type == typeof(decimal))
            {
                if (value is double d && (double.IsNaN(d) || double.IsInfinity(d)) || value is float f && (float.IsNaN(f) || float.IsInfinity(f))) throw new SerializationException("数值必须有限。");
                output.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return;
            }
            output.Append(type.IsArray ? '[' : '{'); bool first = true;
            if (type.IsArray)
            {
                if (((Array)value).Length > 100000) throw new SerializationException("消息数组过长。");
                foreach (var item in (Array)value)
                {
                    if (!first) output.Append(','); first = false; WriteDirect(output, item, type.GetElementType(), depth + 1);
                }
            }
            else
            {
                var layout = GetLayout(type);
                for (int i = 0; i < layout.Members.Length; i++)
                {
                    var child = Get(layout.Members[i], value);
                    if (layout.OmitDefault[i] && Equals(child, layout.Defaults[i])) continue;
                    if (!first) output.Append(','); first = false;
                    output.Append(Quote(layout.Names[i])).Append(':'); WriteDirect(output, child, layout.Types[i], depth + 1);
                }
            }
            output.Append(type.IsArray ? ']' : '}');
        }
    }
}
