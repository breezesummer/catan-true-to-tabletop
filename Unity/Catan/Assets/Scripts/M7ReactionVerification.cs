using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Catan.AI;
using Catan.Core.ThreeKingdoms;
using Catan.Network.Contracts;
using UnityEngine;

// 仅在显式验收配置下启动。四个独立客户端用同机单调时钟对齐采样，云端仍执行实际房间协议。
public sealed partial class M7PlayerVerification
{
    IEnumerator VerifyPublicReactions()
    {
        Application.runInBackground = true;
        var client = app.Network;
        var shared = config.ReactionRunDirectory;
        string roomFile = Path.Combine(shared, "room");
        bool host = config.PeerIndex == 0;
        string name = "验收" + (config.PeerIndex + 1);
        report.Stage = "创建或加入私密验收桌";
        if (host)
        {
            client.Create(new RoomSettings { Name = "互动性能验收", Listed = false, RuleSet = "combined",
                ScenarioId = "through-the-desert", PlayerCount = 4, Harbormaster = true, Fishing = true,
                TradeModulesVersion = NetworkProtocol.TradeModulesVersion,
                ThreeKingdomsPackageVersion = GeneralCatalog.B6PackageVersion,
                ThreeKingdomsContentHash = GeneralCatalog.B6ContentHash }, name, null);
        }
        else
        {
            while (Waiting(File.Exists(roomFile))) yield return new WaitForSecondsRealtime(.1f);
            client.Join(File.ReadAllText(roomFile), name, null);
        }
        while (Waiting(client.Room != null && !client.Busy)) yield return null;
        if (host) Signal(roomFile, client.RoomId);
        while (Waiting(client.Room.OccupiedSeats == 4)) yield return null;
        report.Stage = "四席准备";
        while (Waiting(client.Room.Seats.Single(s => s.SeatId == client.SeatId).Ready))
        {
            if (!client.Busy) client.Ready(true);
            yield return new WaitForSecondsRealtime(.4f);
        }
        if (host)
        {
            while (Waiting(client.Room.Seats.All(s => s.Ready) && !client.Busy)) yield return null;
            client.StartRoom();
        }
        while (Waiting(app.Ready && client.ReactionsAvailable && app.NetworkGame && client.TransportMode.StartsWith("WS"))) yield return null;
        if(config.VerifySkinPlayback)
        {
            var run=VerifySharedSkill(shared);
            while(run.MoveNext())yield return run.Current;
            yield break;
        }
        var seat = client.SeatId;
        int seatNumber = int.Parse(seat.Substring(1));
        string mainTarget = "P" + (1 + seatNumber % 4);
        string alternateTarget = "P" + (1 + (seatNumber + 1) % 4);
        string clockFile = Path.Combine(shared, "start");
        Signal(Path.Combine(shared, "ready-" + seat), "OK");
        if (host)
        {
            while (Waiting(Directory.GetFiles(shared, "ready-P*").Length == 4)) yield return new WaitForSecondsRealtime(.1f);
            Signal(clockFile, (Stopwatch.GetTimestamp() + Stopwatch.Frequency * 4).ToString());
        }
        while (Waiting(File.Exists(clockFile))) yield return new WaitForSecondsRealtime(.1f);
        var events = new List<string>(1800) { "ticks,event,source,target,kind" };
        var frames = new List<long>(4000);
        void Record(string tag, string kind, string src, string dest) =>
            events.Add(string.Join(",", Stopwatch.GetTimestamp(), tag, src, dest, kind));
        void Received(string kind, string src, string dest) => Record("receive", kind, src, dest);
        void Drawn(string kind, string src, string dest) => Record("draw", kind, src, dest);
        client.ReactionReceived += Received;
        app.ReactionFirstDraw += Drawn;
        try
        {
            long start = long.Parse(File.ReadAllText(clockFile));
            string selected = null;
            int sent = 0;
            double nextMove = 1.5;
            long commandSentAt = 0, commandRevision = 0;
            var ai = new CombinedAi();
            var game = GetComponent<M5App>();
            while (true)
            {
                long now = Stopwatch.GetTimestamp();
                double seconds = (now - start) / (double)Stopwatch.Frequency;
                if (seconds >= 25) break;
                if (seconds >= 0) frames.Add(now);
                if (seconds >= 10 && sent < 80 && seconds >= 10 + sent * .125)
                {
                    string kind = sent < 40 ? "egg" : "flower";
                    int position = sent % 40;
                    string target = position >= 27 && position < 34 ? alternateTarget : mainTarget;
                    if (selected != kind || position == 13)
                    {
                        if (selected != null) app.ReactionClick(M6App.ReactionButton.center, 1);
                        app.ReactionClick(M6App.ReactionButton.center, 0);
                        app.ReactionClick((kind == "egg" ? M6App.EggOption : M6App.FlowerOption).center, 0);
                        Require(app.SelectedReaction == kind, "网络道具选择");
                        selected = kind;
                        Record("select", kind, seat, "");
                    }
                    var order = app.HistoryPlayers;
                    var point = TabletopUI.PlayerAvatarRect(Array.IndexOf(order, target), order.Length).center;
                    Record("click", kind, seat, target);
                    app.ReactionClick(point, 0);
                    sent++;
                }
                if (commandSentAt != 0 && !client.CommandPending)
                {
                    Record(client.Revision > commandRevision ? "confirmed" : "unconfirmed", "", seat, "");
                    commandSentAt = 0;
                }
                if (seconds >= nextMove && seconds < 20 && client.CanInteract && !app.TablePresentationBlocked)
                {
                    var view = game.M6View;
                    bool chooser = view.ThreeKingdoms?.PendingDecision?.PlayerId == seat;
                    if (app.IsHumanTurn || chooser)
                    {
                        var command = ai.Decide(view);
                        if (command != null)
                        {
                            command.Id = Guid.NewGuid().ToString("N");
                            commandRevision = client.Revision;
                            Record("submit", command.Kind.ToString(), seat, "");
                            game.Submit(command);
                            if (client.CommandPending) commandSentAt = now;
                            report.HumanCommands++;
                        }
                    }
                    nextMove = seconds + .8;
                }
                yield return null;
            }
            app.ReactionClick(Vector2.zero, 1);
            report.Stage = "验收桌清理";
            File.WriteAllLines(Path.Combine(config.OutputDirectory, seat + "-events.csv"), events);
            File.WriteAllLines(Path.Combine(config.OutputDirectory, seat + "-frames.csv"), frames.Select(v => v.ToString()));
            File.WriteAllText(Path.Combine(config.OutputDirectory, "timing"), start + "," + Stopwatch.Frequency);
            report.Transport = client.TransportMode;
            report.Revision = client.Revision;
            report.Turn = Turn();
            Require(sent == 80 && report.Transport.StartsWith("WS"), "公网持续投掷与实时同步");
        }
        finally { client.ReactionReceived -= Received; app.ReactionFirstDraw -= Drawn; }
        Signal(Path.Combine(shared, "done-" + seat), "OK");
        while (Waiting(Directory.GetFiles(shared, "done-P*").Length == 4)) yield return new WaitForSecondsRealtime(.1f);
        client.Leave();
        while (Waiting(client.RoomId == null && !client.Busy)) yield return null;
    }

    static void Signal(string path, string value)
    {
        File.WriteAllText(path + ".tmp", value);
        File.Move(path + ".tmp", path);
    }
}
