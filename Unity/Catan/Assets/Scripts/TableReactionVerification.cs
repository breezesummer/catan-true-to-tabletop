using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public sealed partial class TabletopUiVerification
{
    IEnumerator VerifyReactionViews(string[] args)
    {
        mode = "combined";
        app.ReadSave(args[Array.IndexOf(args, "--reaction-views") + 1]);
        yield return null;
        yield return null;
        Require(app.ThreeKingdoms && app.Fishing && app.Harbormaster && app.PlayerCount == 4 &&
            app.ScenarioId == "through-the-desert", "验收对局配置不符");
        for (int i = 0; i < 600 && !Table.Ocean.MaskReady; i++) yield return null;
        yield return new WaitForSeconds(1);
        string authority = Authority;
        var seats = app.HistoryPlayers;
        var others = seats.Where(s => s != app.HumanSeat).ToArray();
        var avatar = TabletopUI.PlayerAvatarRect(Array.IndexOf(seats, others[0]), seats.Length).center;
        Require(M6App.ReactionButton.xMax < OperationDock.HandRack.x &&
            M6App.ReactionButton.y == OperationDock.HandRack.y && M6App.ReactionButton.yMax == OperationDock.HandRack.yMax,
            "互动按钮未与手牌栏左侧齐平");
        yield return new WaitForEndOfFrame();
        SaveOceanFrame("互动按钮与手牌栏.png");
        Dock.Fishing.Show();
        yield return null;
        Require(!Dock.Fishing.Contains(M6App.ReactionButton.center), "捕鱼操作遮住互动按钮");
        yield return new WaitForEndOfFrame();
        SaveOceanFrame("捕鱼与互动按钮.png");
        Dock.Fishing.Hide();
        app.ReactionClick(M6App.ReactionButton.center, 0);
        Require(app.ReactionMenu, "互动按钮未展开");
        yield return new WaitForEndOfFrame();
        SaveOceanFrame("互动选项.png");
        foreach (string kind in new[] { "egg", "flower" })
        {
            if (!app.ReactionMenu) app.ReactionClick(M6App.ReactionButton.center, 0);
            app.ReactionClick((kind == "egg" ? M6App.EggOption : M6App.FlowerOption).center, 0);
            Require(app.SelectedReaction == kind && !app.ReactionMenu, "道具选择失败");
            app.ReactionClick(avatar, 0);
            yield return new WaitForSeconds(.11f);
            app.ReactionClick(avatar, 0);
            app.ReactionClick(new Vector2(720, 440), 0);
            Require(app.SelectedReaction == kind && app.ReactionOwnsPointer(avatar), "连点或空白点击退出了互动");
            Require(app.TryReactionPath(app.HumanSeat, others[0], out var from, out var to) &&
                from == M6App.ReactionButton.center && to == avatar, "发起者轨迹不符");
            yield return new WaitForSeconds(.13f);
            yield return new WaitForEndOfFrame();
            SaveOceanFrame(kind == "egg" ? "连续砸蛋.png" : "连续送花.png");
            for (int i = 0; i < 3; i++) app.ReactionClick(avatar, 0);
            yield return new WaitForSeconds(.9f);
            app.ReactionClick(avatar, 0);
            yield return new WaitForSeconds(.20f);
            yield return new WaitForEndOfFrame();
            SaveOceanFrame(kind == "egg" ? "第六下拖鞋.png" : "第六下碰杯.png");
            app.ReactionClick(avatar, 1);
            Require(app.SelectedReaction == null, "右键未取消互动");
            yield return new WaitForSeconds(1);
        }
        var center = MobileTabletop.Logical(new Vector2(Screen.width, Screen.height) * .5f);
        Require(app.TryReactionPath(others[0], app.HumanSeat, out var start, out var end) &&
            start == avatar && end == center, "接收者视角未落在屏幕中央");
        app.PlayReaction("egg", others[0], app.HumanSeat);
        yield return new WaitForSeconds(.48f);
        yield return new WaitForEndOfFrame();
        SaveOceanFrame("收到鸡蛋.png");
        yield return new WaitForSeconds(.5f);
        Require(app.TryReactionPath(others[0], others[1], out start, out end) &&
            start == avatar && end == TabletopUI.PlayerAvatarRect(Array.IndexOf(seats, others[1]), seats.Length).center,
            "旁观者轨迹未连接两个头像");
        app.PlayReaction("flower", others[0], others[1]);
        yield return new WaitForSeconds(.2f);
        yield return new WaitForEndOfFrame();
        SaveOceanFrame("旁观送花.png");
        Require(authority == Authority, "互动改变了权威局面");
        configurations++;
    }
}
