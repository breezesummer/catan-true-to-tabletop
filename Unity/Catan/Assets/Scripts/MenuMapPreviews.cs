using System.Collections.Generic;
using UnityEngine;

/// <summary>版本化公开示意图。只使用菜单选择，不接触对局、存档或权威随机状态。</summary>
public static class MenuMapPreviews
{
    const string Root = "MenuPreviews/v001/";
    static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
    static readonly string[] Rules = { "base", "seafarers", "cities-knights", "combined" };

    public static Texture2D RuleTexture(int ruleIndex) => Load("rule-" + Rules[Mathf.Clamp(ruleIndex, 0, 3)]);

    public static Texture2D ScenarioTexture(int ruleIndex, string scenarioId, int playerCount)
    {
        int rule = Mathf.Clamp(ruleIndex, 0, 3);
        string id = rule == 0 || rule == 2 ? "standard" : scenarioId;
        if (string.IsNullOrEmpty(id)) return null;
        return Load(Rules[rule] + "-" + id + "-" + (playerCount == 3 ? "3" : "4")) ?? (rule==3?Load("seafarers-"+id+"-"+(playerCount==3?"3":"4")):null);
    }

    public static string ScenarioCaption(int ruleIndex, string scenarioId, int playerCount)
    {
        string people = (playerCount == 3 ? "3" : "4") + " 人布局示意";
        if (scenarioId == "fog-islands") return people + " · 云雾区域将在探索后揭晓";
        if (ruleIndex == 0 || ruleIndex == 2 || ruleIndex == 3 || scenarioId == "new-world")
            return people + " · 地形与点数以本局开局为准";
        return people + " · 展示公开初始地图";
    }

    public static void DrawRule(Rect rect, int ruleIndex) => Draw(rect, RuleTexture(ruleIndex));
    public static void DrawScenario(Rect rect, int ruleIndex, string scenarioId, int playerCount) => Draw(rect, ScenarioTexture(ruleIndex, scenarioId, playerCount));

    // 小卡片只取完整棋盘区域，省去图片内的标题留白；人数字样与说明由菜单正常字号绘制。
    public static void DrawScenarioThumbnail(Rect rect, int ruleIndex, string scenarioId, int playerCount)
    {
        var texture = ScenarioTexture(ruleIndex, scenarioId, playerCount);
        if (texture == null) { Draw(rect, null); return; }
        var crop = new Rect(.18f, .15f, .64f, .74f);
        float aspect = texture.width * crop.width / (texture.height * crop.height);
        float width = Mathf.Min(rect.width, rect.height * aspect), height = width / aspect;
        GUI.DrawTextureWithTexCoords(new Rect(rect.center.x - width * .5f, rect.center.y - height * .5f, width, height), texture, crop, true);
    }

    static Texture2D Load(string key)
    {
        if (!Textures.TryGetValue(key, out var texture))
        {
            texture = Resources.Load<Texture2D>(Root + key);
            Textures[key] = texture;
        }
        return texture;
    }

    static void Draw(Rect rect, Texture2D texture)
    {
        if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        else PastoralMenuUI.Round(rect, new Color(.15f, .31f, .31f), 8);
    }
}
