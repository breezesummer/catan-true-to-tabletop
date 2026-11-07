using System;
using UnityEngine;

// 仅控制本机棋盘显示；收牌与建造仍能在隐藏期间更新。
internal static class TabletopMapMarks
{
    internal static bool Visible { get; private set; } = true;
    internal static event Action<bool> Changed;
    static readonly int Hidden = Shader.PropertyToID("_CatanMapMarksHidden");

    internal static void SetVisible(bool visible)
    {
        Visible = visible;
        Shader.SetGlobalFloat(Hidden, visible ? 0 : 1);
        Changed?.Invoke(visible);
    }
}
