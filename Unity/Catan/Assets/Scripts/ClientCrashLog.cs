using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

static class ClientCrashLog
{
    static readonly object Gate = new object();
    static string errorLog;
    static int records;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Start()
    {
        if (Application.isEditor) return;
        try
        {
            string reports = Path.Combine(Application.persistentDataPath, "CrashReports");
            Directory.CreateDirectory(reports);
            foreach (var path in Directory.GetFiles(reports, "Errors-*.txt").OrderByDescending(Path.GetFileName).Skip(4))
                File.Delete(path);
            errorLog = Path.Combine(reports, "Errors-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N") + ".txt");
            Application.logMessageReceivedThreaded += OnLog;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
#if UNITY_STANDALONE_WIN
            string crashes = UnityEngine.Windows.CrashReporting.crashReportFolder;
            ThreadPool.QueueUserWorkItem(_ => {
                try { NativeCrashReports.Collect(crashes, reports); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            });
#endif
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
    {
        var error = args.ExceptionObject as Exception;
        if (error != null) Write("未处理异常 " + error.GetType().Name, error.StackTrace, true);
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
        // 异常消息可能包含联机数据，只保存异常类型和代码位置。
        string label = type.ToString();
        if (type == LogType.Exception && message != null)
        {
            int end = message.IndexOf(':');
            if (end > 0 && end < 120 && message.Take(end).All(c => char.IsLetterOrDigit(c) || c == '.' || c == '_'))
                label += " " + message.Substring(0, end);
        }
        Write(label, stack, false);
    }

    static void Write(string label, string stack, bool fatal)
    {
        lock (Gate)
        {
            if (records >= 64 && !fatal) return;
            records++;
            try
            {
                stack = stack ?? "";
                File.AppendAllText(errorLog, DateTime.UtcNow.ToString("O") + " " + label + "\n" +
                    stack.Substring(0, Math.Min(8192, stack.Length)) + "\n");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
