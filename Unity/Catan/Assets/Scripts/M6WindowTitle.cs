using System.Collections;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

public sealed partial class M6App
{
    // 保留既有产品存储标识，窗口标题独立跟随版本，避免丢失存档、姓名和联机恢复凭据。
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    static extern bool SetWindowText(IntPtr window,string title);
#endif
    IEnumerator ApplyGameWindowTitle()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        for(int attempt=0;attempt<40;attempt++)
        {
            using(var process=System.Diagnostics.Process.GetCurrentProcess())
                if(process.MainWindowHandle!=IntPtr.Zero&&SetWindowText(process.MainWindowHandle,ProductFileName))yield break;
            yield return new WaitForSecondsRealtime(.1f);
        }
#endif
        yield break;
    }
}
