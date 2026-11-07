using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class M6App
{
    internal static readonly Rect ReactionButton = new Rect(OperationDock.HandRack.x - 4 - 78, OperationDock.HandRack.y, 78, OperationDock.HandRack.height);
    internal static readonly Rect MapMarksButton = new Rect(ReactionButton.x, ReactionButton.y - 4 - ReactionButton.height, ReactionButton.width, ReactionButton.height);
    internal static readonly Rect FlowerOption = new Rect(ReactionButton.x - 4 - 40, ReactionButton.y, 40, ReactionButton.height);
    internal static readonly Rect EggOption = new Rect(FlowerOption.x - 4 - 40, ReactionButton.y, 40, ReactionButton.height);
    const float FlightSeconds = .44f, ImpactSeconds = .42f;
    const string ReactionAssets = "TableReactions/";

    sealed class ReactionArt
    {
        internal readonly Texture2D Flying, Impact;
        internal readonly AudioClip LaunchSound, HitSound;
        internal ReactionArt(string kind)
        {
            Flying = Resources.Load<Texture2D>(ReactionAssets + kind + "1");
            Impact = Resources.Load<Texture2D>(ReactionAssets + kind + "2");
            LaunchSound = Resources.Load<AudioClip>(ReactionAssets + "throw_" + kind + "1");
            HitSound = Resources.Load<AudioClip>(ReactionAssets + "throw_" + kind + "2");
        }
    }

    sealed class ReactionFlight
    {
        internal ReactionArt Art;
        internal Vector2 From, To;
        internal float Start;
        internal bool CenterHit, Launched, Landed;
        internal string Source, Target, Kind;
        internal bool Drawn;
    }
    internal event Action<string, string, string> ReactionFirstDraw;
    readonly Dictionary<string, ReactionArt> reactionArt = new Dictionary<string, ReactionArt>(4);
    string comboTarget;
    int comboCount;
    readonly List<ReactionFlight> reactionFlights = new List<ReactionFlight>(24);
    internal bool ReactionMenu { get; private set; }
    internal string SelectedReaction { get; private set; }
    bool reactionCursorHidden;
    int reactionCancelFrame = -1;

    bool ReactionTableVisible => Ready && !MenuVisible && !networkMenu && !tableMenu;
    bool ReactionButtonEnabled => !NetworkGame || Network.ReactionsAvailable;
    internal bool ReactionOwnsPointer(Vector2 point) => ReactionTableVisible &&
        (SelectedReaction != null || ReactionMenu || reactionCancelFrame == Time.frameCount ||
         MapMarksButton.Contains(point) || ReactionButtonEnabled && ReactionButton.Contains(point));

    void LoadReactionArt()
    {
        if (reactionArt.Count != 0) return;
        foreach (var kind in new[] { "egg", "flower", "shoe", "wine" })
            reactionArt.Add(kind, new ReactionArt(kind));
    }
    void ClearReactionSelection()
    {
        SelectedReaction = null;
        ReactionMenu = false;
        comboTarget = null;
        comboCount = 0;
    }

    string NextReaction(string target)
    {
        if (comboTarget != target)
        {
            comboTarget = target;
            comboCount = 0;
        }
        if (++comboCount < 6) return SelectedReaction;
        comboCount = 0;
        return SelectedReaction == "egg" ? "shoe" : "wine";
    }

    internal void ReactionClick(Vector2 point, int button)
    {
        if (button == 1)
        {
            ClearReactionSelection();
            reactionCancelFrame = Time.frameCount;
            SyncReactionCursor();
            return;
        }
        if (button != 0) return;
        if (MapMarksButton.Contains(point))
        {
            TabletopMapMarks.SetVisible(!TabletopMapMarks.Visible);
            ClearReactionSelection();
            SyncReactionCursor();
            return;
        }
        if (SelectedReaction != null)
        {
            if (!ReactionButtonEnabled) return;
            string target=presentation is M2App m2?m2.PlayerPanelAt(point):presentation is M3App m3?m3.PlayerPanelAt(point):
                presentation is M4App m4?m4.PlayerPanelAt(point):presentation is M5App m5?m5.PlayerPanelAt(point):null;
            if(target!=null){
                var kind=NextReaction(target);
                PlayReaction(kind,HumanSeat,target);
                if(NetworkGame)Network.SendReaction(kind,target);
            }
            return;
        }
        if (ReactionMenu)
        {
            if (EggOption.Contains(point)) SelectedReaction = "egg";
            if (FlowerOption.Contains(point)) SelectedReaction = "flower";
            ReactionMenu = false;
            SyncReactionCursor();
        }
        else if (ReactionButtonEnabled && ReactionButton.Contains(point)) ReactionMenu = true;
    }
    internal bool TryReactionPath(string source, string target, out Vector2 from, out Vector2 to)
    {
        var seats = HistoryPlayers;
        int sourceIndex = Array.IndexOf(seats, source), targetIndex = Array.IndexOf(seats, target);
        from = to = Vector2.zero;
        if (sourceIndex < 0 || targetIndex < 0) return false;
        from = source == HumanSeat ? ReactionButton.center : TabletopUI.PlayerAvatarRect(sourceIndex, seats.Length).center;
        to = target == HumanSeat && source != HumanSeat ? MobileTabletop.Logical(new Vector2(Screen.width, Screen.height) * .5f) :
            TabletopUI.PlayerAvatarRect(targetIndex, seats.Length).center;
        return true;
    }
    internal void PlayReaction(string kind, string source, string target)
    {
        if (!ReactionTableVisible || kind == null ||
            !TryReactionPath(source, target, out var from, out var to)) return;
        LoadReactionArt();
        if (!reactionArt.TryGetValue(kind, out var art)) return;
        if (reactionFlights.Count >= 64) reactionFlights.RemoveAt(0);
        float now = Time.unscaledTime, start = now;
        if (source != HumanSeat)
            for (int i = reactionFlights.Count - 1; i >= 0; i--)
                if (reactionFlights[i].Source == source)
                {
                    start = Mathf.Max(now, reactionFlights[i].Start + .045f);
                    break;
                }
        reactionFlights.Add(new ReactionFlight { Art = art, From = from, To = to, Start = start, Source = source, Target = target, Kind = kind,
            Launched = start == now, CenterHit = target == HumanSeat && source != HumanSeat });
        if (start == now) TabletopAudio.Instance?.PlayEffectClip(art.LaunchSound);
    }
    void UpdateReactions()
    {
        SyncReactionCursor();
        for (int i = reactionFlights.Count - 1; i >= 0; i--)
        {
            var flight = reactionFlights[i];
            float age = Time.unscaledTime - flight.Start;
            if (age >= FlightSeconds + ImpactSeconds) { reactionFlights.RemoveAt(i); continue; }
            if (!flight.Launched && age >= 0)
            {
                flight.Launched = true;
                if (ReactionTableVisible) TabletopAudio.Instance?.PlayEffectClip(flight.Art.LaunchSound);
            }
            if (!flight.Landed && age >= FlightSeconds)
            {
                flight.Landed = true;
                if (ReactionTableVisible) TabletopAudio.Instance?.PlayEffectClip(flight.Art.HitSound);
            }
        }
    }
    void SyncReactionCursor()
    {
        bool hidden = SelectedReaction != null && ReactionTableVisible && Application.isFocused && !MobileTabletop.Enabled;
        if (hidden == reactionCursorHidden) return;
        reactionCursorHidden = hidden;
        Cursor.visible = !hidden;
    }
    void ResetReactions()
    {
        ClearReactionSelection();
        reactionFlights.Clear();
        SyncReactionCursor();
    }
    void DrawReactions()
    {
        if (Event.current.type != EventType.Repaint) return;
        GeneralFrameUI.Skill(MapMarksButton, TabletopMapMarks.Visible ? "隐藏" : "显示", true, true, !TabletopMapMarks.Visible, false);
        if (ReactionButtonEnabled)
        {
            GeneralFrameUI.Skill(ReactionButton, "互动", true, true, ReactionMenu || SelectedReaction != null, false);
            if (ReactionMenu)
            {
                LoadReactionArt();
                DrawReactionOption(EggOption, reactionArt["egg"].Flying);
                DrawReactionOption(FlowerOption, reactionArt["flower"].Flying);
            }
        }
        foreach (var flight in reactionFlights)
        {
            float age = Time.unscaledTime - flight.Start;
            if (age < 0) continue;
            float t = Mathf.Clamp01(age / FlightSeconds);
            var point = Vector2.Lerp(flight.From, flight.To, t);
            // 头像贴近屏幕顶边时向桌内抛出，避免轨迹越过窗口。
            float arc = Mathf.Min(82, Vector2.Distance(flight.From, flight.To) * .16f);
            point.y += Mathf.Sin(t * Mathf.PI) * (Mathf.Min(flight.From.y, flight.To.y) < 80 && flight.From.y < 80 ? arc : -arc);
            float hit = Mathf.Clamp01((age - FlightSeconds) / ImpactSeconds);
            float size = t < 1 ? Mathf.Lerp(44, flight.CenterHit ? 100 : 54, t) :
                (flight.CenterHit ? 158 : 78) * (1 + .13f * Mathf.Sin(hit * Mathf.PI));
            var color = GUI.color;
            GUI.color = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1, hit)));
            var matrix = GUI.matrix;
            if (flight.Kind == "shoe" && t < 1) GUIUtility.RotateAroundPivot(1440 * t, point);
            DrawReactionImage(point, size, t < 1 ? flight.Art.Flying : flight.Art.Impact);
            GUI.matrix = matrix;
            GUI.color = color;
            if (!flight.Drawn) { flight.Drawn = true; ReactionFirstDraw?.Invoke(flight.Kind, flight.Source, flight.Target); }
        }
        if (SelectedReaction != null && !MobileTabletop.Enabled && Application.isFocused)
        {
            LoadReactionArt();
            DrawReactionImage(Event.current.mousePosition, 36, reactionArt[SelectedReaction].Flying);
        }
    }
    static void DrawReactionOption(Rect area, Texture2D image)
    {
        ModernOperationUI.Panel(area);
        DrawReactionImage(area.center, area.width - (area.Contains(Event.current.mousePosition) ? 2 : 6), image);
    }
    static void DrawReactionImage(Vector2 center, float size, Texture2D image)
    {
        if (image != null) GUI.DrawTexture(new Rect(center.x - size/2, center.y - size/2, size, size), image, ScaleMode.ScaleToFit);
    }
}
