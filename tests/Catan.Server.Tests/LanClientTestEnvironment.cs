using System.Text.Json;

// 只替代 Unity 的平台／JSON 边界，发现、TLS 和列表读取使用正式客户端源码。
namespace UnityEngine
{
    internal enum RuntimePlatform { WindowsPlayer,WindowsEditor,Android }
    internal static class Application
    {
        internal static RuntimePlatform platform=RuntimePlatform.Android;
        internal static string dataPath="";
        internal static string persistentDataPath=Path.GetTempPath();
        internal static string version="2.0.1-test";
        internal static string buildGUID="0123456789abcdef0123456789abcdef";
        internal static void Quit(int code) { }
    }
    internal static class JsonUtility
    {
        internal static T FromJson<T>(string json)=>JsonSerializer.Deserialize<T>(json,new JsonSerializerOptions{IncludeFields=true})!;
    }
    public class MonoBehaviour { }
    internal static class Time
    {
        static readonly System.Diagnostics.Stopwatch elapsed=System.Diagnostics.Stopwatch.StartNew();
        internal static float unscaledTime=>(float)elapsed.Elapsed.TotalSeconds;
    }
    internal static class Debug
    {
        internal static void LogWarning(string value) { }
        internal static void LogError(string value) { }
        internal static void Log(string value) { }
    }
    internal enum RuntimeInitializeLoadType {AfterSceneLoad}
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        internal RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
}
