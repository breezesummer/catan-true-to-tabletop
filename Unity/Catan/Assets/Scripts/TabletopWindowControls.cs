using UnityEngine;

public sealed partial class M2App { internal TabletopUI WindowHud => tabletop; }
public sealed partial class M3App { internal TabletopUI WindowHud => tabletop; }
public sealed partial class M4App { internal TabletopUI WindowHud => tabletop; }
public sealed partial class M5App { internal TabletopUI WindowHud => tabletop; }

public sealed partial class M6App
{
    // 强制选择不能被取消。关闭其窗口后，从常驻的次级屏幕提示重新打开。
    OperationWindowState CurrentTableWindows => presentation is M2App basic ? basic.WindowHud.OperationWindows :
        presentation is M3App sailing ? sailing.WindowHud.OperationWindows :
        presentation is M4App cities ? cities.WindowHud.OperationWindows :
        presentation is M5App combined ? combined.WindowHud.OperationWindows : null;
    internal bool ClosedTableSelection => CurrentTableWindows?.Closed == true && HasRequiredTableSelection;
    static readonly Rect SelectionPromptArea = new Rect(264,126,912,112);
}
