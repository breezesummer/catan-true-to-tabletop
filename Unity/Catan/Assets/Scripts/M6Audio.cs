using System.Linq;
using UnityEngine;

public sealed partial class M6App
{
    TabletopAudio tabletopAudio;
    void InitializeTabletopAudio()
    {
        tabletopAudio = gameObject.AddComponent<TabletopAudio>();
        tabletopAudio.Initialize(this);
    }
    void ResetTabletopAudioHistory() { if (tabletopAudio != null) tabletopAudio.ResetHistory(); }
    partial void DrawPastoralAudioSettings(Rect rect) { if (tabletopAudio != null) tabletopAudio.DrawSettings(rect, font); }
    internal string AudioScope => (presentation == null ? "menu" : presentation.GetInstanceID().ToString()) + ":" + HumanSeat;
    internal int AudioBarbarianPosition => presentation is M4App cities && cities.M6View != null ? cities.M6View.BarbarianPosition :
        presentation is M5App combined && combined.M6View != null ? combined.M6View.BarbarianPosition : -1;
    internal long AudioPublicSequence => presentation is M2App basic ? basic.M6View?.Events?.LastOrDefault()?.Sequence ?? 0 :
        presentation is M3App sailing ? sailing.M6View?.Events?.LastOrDefault()?.Sequence ?? 0 :
        presentation is M4App cities ? cities.M6View?.Events?.LastOrDefault()?.Sequence ?? 0 :
        presentation is M5App combined ? combined.M6View?.Events?.LastOrDefault()?.Sequence ?? 0 : 0;
    internal string AudioPirateTile => presentation is M3App sailing ? sailing.M6View?.Board?.PirateTileId :
        presentation is M5App combined ? combined.M6View?.Board?.PirateTileId : null;
}
