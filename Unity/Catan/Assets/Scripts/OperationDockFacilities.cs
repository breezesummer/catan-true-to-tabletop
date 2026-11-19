using UnityEngine;

public sealed partial class OperationDock
{
    static readonly Rect FacilityRect = new Rect(270, 100, 900, 640);
    static readonly string[] FacilityPageIds = {"trade","politics","science"};
    static readonly string[] FacilityGoodIds = {"cloth","coin","paper"};
    static readonly Vector2Int[] FacilityCells = {
        new Vector2Int(1,2), new Vector2Int(0,2), new Vector2Int(0,1),
        new Vector2Int(0,0), new Vector2Int(1,0)
    };
    Texture2D facilityTray;

    static Material facilityPrint;
    static void DrawFacilityPrint(Rect rect, Texture2D texture)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (facilityPrint == null)
        {
            var shader = Resources.Load<Shader>("FacilityTray/Ink");
            if (shader != null) facilityPrint = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        if (facilityPrint == null) GUI.DrawTexture(rect, texture);
        else Graphics.DrawTexture(rect, texture, facilityPrint);
    }

    void FacilityText(Rect rect, string value, int size, bool bold = false, bool brass = false)
    {
        var style = new GUIStyle(text) {
            alignment = TextAnchor.MiddleCenter, fontSize = size,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, wordWrap = true
        };
        var ink = brass ? new Color(1f,.91f,.71f) : new Color(.22f,.14f,.085f);
        style.normal.textColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? ink.linear : ink;
        Label(rect, value, style);
    }

    void DrawFacilities(OperationSnapshot s)
    {
        int track = facilityTrack;
        var build = OperationPanelOptions.Facility(s, track);
        if (build == null) { facilityTrack = -1; return; }

        if (facilityTray == null) facilityTray = Resources.Load<Texture2D>("FacilityTray/tray");
        Register(FacilityRect);
        if (StrategyHudTheme.HandPainted || StrategyHudTheme.Ocean) ModernOperationUI.Panel(FacilityRect);
        else if (facilityTray != null) DrawFacilityPrint(FacilityRect, facilityTray);
        else Paper(FacilityRect);

        for (int level = 1; level <= 5; level++)
        {
            var cell = FacilityCells[level - 1];
            var slot = new Rect(325 + cell.x * 202, 151 + cell.y * 181, 172, 172);
            bool next = level == build.Level + 1;
            Fill(new Rect(slot.x + 2, slot.y + 4, slot.width, slot.height), new Color(0,0,0,.48f));
            if (next) Fill(new Rect(slot.x-3,slot.y-3,slot.width+6,slot.height+6), Gold);
            var face = level <= build.Level ? EntityCardAssets.ImprovementBack(track,level)
                                            : EntityCardAssets.ImprovementFront(track,level);
            if (face != null) DrawFacilityPrint(slot, face);
            if (next)
            {
                var corners = EntityCardAssets.Texture("FacilityTray/selected");
                if (corners != null) DrawFacilityPrint(slot,corners);
            }
            if (GUI.enabled && next && slot.Contains(Event.current.mousePosition)
                && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                Event.current.Use();
                PrepareBuild(build,s);
            }
        }

        string sealState = build.Level >= 3 ? "gold" : "ink";
        var seal = EntityCardAssets.Texture("FacilityTray/Seals/" + FacilityPageIds[track] + "-" + sealState);
        DrawSealNote(track);
        float slide = Mathf.SmoothStep(0f, 1f, sealNoteReveal);
        var crest = new Rect(Vector2.Lerp(FacilitySealRect.position, SlipSealRect.position, slide),
                             Vector2.Lerp(FacilitySealRect.size, SlipSealRect.size, slide));
        if (seal != null) DrawFacilityPrint(crest, seal);
        if (slide == 0f)
        {
            var laurel = EntityCardAssets.Texture("FacilityTray/Seals/laurel-" + sealState);
            if (laurel != null)
            {
                // 月桂的透明中心给主体留位，纸签展开后仅保留主体。
                var branches = new Rect(crest.x - crest.width * .15f, crest.y - crest.height * .06f,
                                        crest.width * 1.3f, crest.height * 1.16f);
                DrawFacilityPrint(branches, laurel);
            }
        }

        var page = EntityCardAssets.Texture("FacilityTray/Pages/" + FacilityPageIds[track] + "-" + build.Level);
        var pageRect = new Rect(767,149,374,546);
        if (page != null) DrawFacilityPrint(pageRect,page);
        bool complete = build.Level >= 5;
        var action = new Rect(790,624,333,51);
        Register(action);
        if (complete)
        {
            FacilityText(new Rect(868,628,198,40),"已全部建成",21,true,true);
        }
        else
        {
            int good = 5+track;
            bool available = build.CanPrepare(s);
            var icon = EntityCardAssets.Texture("TabletopUI/v010/"+FacilityGoodIds[track]+"-cutout");
            using (TabletopButtonFeedback.Begin(action, available))
            {
                if (StrategyHudTheme.HandPainted || StrategyHudTheme.Ocean) CopperTabletopSkin.Panel(action,StrategyHudTheme.Surface, available && action.Contains(Event.current.mousePosition), false);
                if (icon != null) DrawFacilityPrint(new Rect(993,633,29,29),icon);
                FacilityText(new Rect(876,628,86,40),"建设",23,true,!StrategyHudTheme.HandPainted);
                FacilityText(new Rect(1024,628,76,40),s.Goods[good]+"/"+build.Cost[good],21,true,!StrategyHudTheme.HandPainted);
            }
            if (!available) Fill(action,new Color(.20f,.14f,.09f,.16f));
            if (available && TabletopButtonFeedback.Button(action)) PrepareBuild(build,s);
        }
    }

    void DrawSealNote(int track)
    {
        var e = Event.current;
        // 悬停后保留原落点，并允许鼠标跟随移动中的纹章。
        Rect hit = sealNoteReveal > 0 ? Rect.MinMaxRect(SlipSealRect.x, FacilitySealRect.y,
            FacilitySealRect.xMax, FacilitySealRect.yMax) : FacilitySealRect;
        if (MobileTabletop.Enabled && sealNotePinned && sealNoteReveal >= 1f) hit = SlipSealRect;
        bool overSeal = hit.Contains(e.mousePosition);
        bool open = GUI.enabled && overSeal;
        if (MobileTabletop.Enabled)
        {
            if (e.rawType == EventType.MouseDown && !overSeal) sealNotePinned = false;
            if (GUI.Button(hit, GUIContent.none, GUIStyle.none) && !MobileTabletop.TouchDragged)
                sealNotePinned = !sealNotePinned;
            open = GUI.enabled && sealNotePinned;
        }

        if (e.type != EventType.Repaint) return;
        sealNoteReveal = Mathf.MoveTowards(sealNoteReveal, open ? 1f : 0f, Time.unscaledDeltaTime / .18f);
        if (sealNoteReveal <= 0f) return;
        var note = EntityCardAssets.Texture("FacilityTray/Slips/" + FacilityPageIds[track]);
        if (note == null) return;
        float fraction = Mathf.SmoothStep(0f, 1f, sealNoteReveal);
        float width = SealNoteRect.width * fraction;
        var visible = new Rect(SealNoteRect.xMax-width, SealNoteRect.y, width, SealNoteRect.height);
        var uv = new Rect(0, 0, fraction, 1);
        if (facilityPrint != null)
            Graphics.DrawTexture(visible, note, uv, 0, 0, 0, 0, Color.white, facilityPrint);
        else GUI.DrawTextureWithTexCoords(visible, note, uv);
    }

    public static string FacilityAbility(int track,int level)
    {
        if(level<3)return "达到 3 级后解锁城市能力";
        return track==0?"同业公会：商品可以 2 : 1 兑换资源或商品":track==1?"要塞：允许晋升精锐骑士":"引水渠：无产出时可选择一张资源（7 除外）";
    }
}
