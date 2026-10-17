using System.Security.Cryptography;
using System.Text.Json;

// 只替代 Unity 的平台／JSON 边界，发现、TLS 和列表读取使用正式客户端源码。
namespace UnityEngine
{
    internal enum RuntimePlatform { WindowsPlayer,WindowsEditor,Android }
    internal static class Application
    {
        internal static RuntimePlatform platform=RuntimePlatform.Android;
        internal static string dataPath="";
    }
    internal static class JsonUtility
    {
        internal static T FromJson<T>(string json)=>JsonSerializer.Deserialize<T>(json,new JsonSerializerOptions{IncludeFields=true})!;
    }
}
internal static class M7Client
{
    internal static string NormalizeFingerprint(string value)=>new string((value??"").Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
    internal static bool CertificateMatches(byte[] certificate,string fingerprint)=>Convert.ToHexString(SHA256.HashData(certificate)).Equals(fingerprint,StringComparison.OrdinalIgnoreCase);
}
