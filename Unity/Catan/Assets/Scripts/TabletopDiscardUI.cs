using System;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUI
{
    OperationSnapshot discardSnapshot;
    OperationDock discardDock;
    public void BindDiscard(OperationSnapshot snapshot, OperationDock dock)
    { discardSnapshot = snapshot; discardDock = dock; }
    internal bool DiscardActive => discardSnapshot?.Discard != null;
    internal bool DiscardReady => DiscardActive && discardSnapshot.CanInteract && discardDock.DiscardMatches(discardSnapshot) &&
        discardSnapshot.Discard.Ready(discardDock.Selection.Selected, discardSnapshot.Goods, discardDock.SelectedDiscardCard);
    internal string DiscardCaption => discardSnapshot.Discard.Caption(DiscardReady);
    internal void ConfirmDiscard()
    {
        if (!DiscardReady) return;
        discardSnapshot.Discard.Confirm?.Invoke((int[])discardDock.Selection.Selected.Clone(), discardDock.SelectedDiscardCard);
    }
    internal void DrawDiscardCount()
    {
        if (!DiscardActive) return;
        var choice = discardSnapshot.Discard;
        int selected = !discardDock.DiscardMatches(discardSnapshot) ? 0 : choice.Special ? (discardDock.SelectedDiscardCard == null ? 0 : 1) : discardDock.Selection.Selected.Sum();
        var area = new Rect(EndTurnRect.x - 20, EndTurnRect.y - 35, EndTurnRect.width + 40, 29);
        ModernOperationUI.Round(area, ModernOperationUI.Background);
        var style = new GUIStyle(ModernOperationUI.Center) { fontSize = 15, fontStyle = FontStyle.Bold };
        style.normal.textColor = DiscardReady ? StrategyHudArt.Gold : ModernOperationUI.TextColor;
        GUI.Label(area, choice.Prompt + "  " + selected + "/" + choice.Amount, style);
    }
}
