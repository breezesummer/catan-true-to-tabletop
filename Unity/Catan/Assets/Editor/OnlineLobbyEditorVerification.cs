using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 在 macOS 编辑器中运行实际菜单与传输验收；仅通过显式命令启用。
[InitializeOnLoad]
public static class OnlineLobbyEditorVerification
{
    const string Key = "Catan.OnlineLobbyVerification.Output";
    const string DeadlineKey = "Catan.OnlineLobbyVerification.Deadline";

    static OnlineLobbyEditorVerification()
    {
        EditorApplication.update += CheckResult;
        Application.logMessageReceived += CheckFailure;
    }

    public static void Run()
    {
        var arguments = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(arguments, "--online-lobby-verify");
        if (flag < 0 || flag + 1 >= arguments.Length)
            throw new InvalidOperationException("请提供 --online-lobby-verify 验收输出目录及服务地址。");
        string output = Path.GetFullPath(arguments[flag + 1]);
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../..", ".local")) + Path.DirectorySeparatorChar;
        if (!output.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException("在线大厅验收输出必须位于项目 .local 内。");
        Directory.CreateDirectory(output);
        foreach (string file in new[] { "summary.json", "server-stop.request", "server-stopped.signal", "server-start.request", "server-started.signal" })
            if (File.Exists(Path.Combine(output, file))) File.Delete(Path.Combine(output, file));
        SessionState.SetString(Key, output);
        SessionState.SetString(DeadlineKey, DateTime.UtcNow.AddMinutes(8).ToString("O"));
        EditorSceneManager.OpenScene("Assets/M6.unity", OpenSceneMode.Single);
        var gameView=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        gameView.maximized=true;
        gameView.Focus();
        EditorApplication.isPlaying = true;
    }

    static void CheckResult()
    {
        string output = SessionState.GetString(Key, "");
        if (output.Length == 0) return;
        string result = Path.Combine(output, "summary.json");
        if (File.Exists(result))
        {
            string text = File.ReadAllText(result);
            Finish(text.Contains("\"passed\"") ? 0 : 1);
        }
        else if (DateTime.TryParse(SessionState.GetString(DeadlineKey, ""), null,
                     System.Globalization.DateTimeStyles.RoundtripKind, out var deadline) && DateTime.UtcNow > deadline)
        {
            Debug.LogError("CATAN_ONLINE_LOBBY_EDITOR_TIMEOUT");
            Finish(1);
        }
    }

    static void CheckFailure(string message, string trace, LogType type)
    {
        if (SessionState.GetString(Key, "").Length > 0 &&
            (message.Contains("CATAN_ONLINE_LOBBY_FAILED") || message.Contains("CATAN_ONLINE_LOBBY_VERIFY_FAILED")))
            Finish(1);
    }

    static void Finish(int code)
    {
        SessionState.EraseString(Key);
        SessionState.EraseString(DeadlineKey);
        EditorApplication.Exit(code);
    }
}
