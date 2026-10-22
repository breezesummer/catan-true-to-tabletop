using UnityEngine;

// IMGUI 补充快速释放和键盘确认；普通点击由 TabletopAudio.Update 的真实输入兜底。
// 只在释放后给反馈，过滤地图/卡牌拖动；同一帧显式 Click 与此监听去重。
public sealed class TabletopAudioGuiTap : MonoBehaviour
{
    TabletopAudio ownerAudio;
    public void Initialize(TabletopAudio value) { ownerAudio = value; }
    void OnGUI()
    {
        GUI.depth = -32000;
        var e = Event.current;
        if (ownerAudio != null && e != null && e.type == EventType.Used && e.rawType == EventType.MouseDown && (e.button == 0 || e.button == 1)) ownerAudio.ObserveGuiPointerDown(e.button);
        // OnGUI 可能在一次 Update 之后处理完整快速点击；直接处理释放，Update 留作兼容回退。
        else if (ownerAudio != null && e != null && e.rawType == EventType.MouseUp && (e.button == 0 || e.button == 1)) ownerAudio.ObserveGuiPointerUp(e.button);
        else if (ownerAudio != null && e != null && e.type == EventType.Used && e.rawType == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space)) ownerAudio.Click();
    }
}
