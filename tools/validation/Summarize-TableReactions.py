"""汇总同机四客户端的单调时钟记录，只导出计数、延迟和帧时间。"""
import csv
import json
import math
import statistics
import sys
from pathlib import Path


def stats(values):
    if not values:
        return {"样本": 0}
    values = sorted(values)
    def percentile(p):
        return round(values[math.ceil(p * len(values)) - 1], 1)
    return {"样本": len(values), "P50毫秒": percentile(.5), "P95毫秒": percentile(.95),
            "P99毫秒": percentile(.99), "最大毫秒": round(max(values), 1)}


def summarize(root):
    events = {}
    for path in root.glob("peer-*/*-events.csv"):
        with path.open(encoding="utf-8-sig", newline="") as stream:
            events[path.name[:2]] = list(csv.DictReader(stream))
    if len(events) != 4:
        raise ValueError("需要四席完整记录")
    start, frequency = map(int, (root / "peer-0/timing").read_text().split(","))
    def delta(a, b):
        return (int(b["ticks"]) - int(a["ticks"])) * 1000 / frequency
    own = []
    remote = []
    receiver_draw = []
    observer_draw = []
    commands = []
    exposures = 0
    combos = {"shoe": 0, "wine": 0}
    combo_run = any(row["event"] == "select" for rows in events.values() for row in rows)
    for source, rows in events.items():
        clicks = [row for row in rows if row["event"] == "click"]
        if len(clicks) != 80:
            raise ValueError("单席未投满 80 次")
        expected = []
        target = None
        count = 0
        for row in rows:
            if row["event"] == "select":
                target, count = None, 0
            if row["event"] != "click":
                continue
            if row["target"] != target:
                target, count = row["target"], 0
            count += 1
            kind = row["kind"]
            if combo_run and count == 6:
                kind = "shoe" if kind == "egg" else "wine"
                combos[kind] += 1
                count = 0
            expected.append((target, kind))
        for watcher, seen in events.items():
            tags = ["receive"] if watcher != source else []
            if watcher == "P1":
                tags.append("draw")
            for tag in tags:
                shots = [r for r in seen if r["event"] == tag and r["source"] == source]
                if expected != [(r["target"], r["kind"]) for r in shots]:
                    raise ValueError(f"{source}→{watcher} {tag} 次数或顺序有误，收到 {len(shots)} 次")
                values = [delta(c, s) for c, s in zip(clicks, shots)]
                if tag == "receive":
                    exposures += len(values)
                    remote.extend(values)
                elif watcher == source:
                    own.extend(values)
                else:
                    for click, ms in zip(clicks, values):
                        (receiver_draw if watcher == click["target"] else observer_draw).append(ms)
        pending = None
        for r in rows:
            if r["event"] == "submit":
                pending = r
            if r["event"] == "confirmed" and pending:
                commands.append((int(pending["ticks"]), delta(pending, r)))
                pending = None

    if combo_run and combos != {"shoe": 24, "wine": 24}:
        raise ValueError(f"连击验收样本不足：{combos}")
    timing = [int(v) for v in (root / "peer-0/P1-frames.csv").read_text().splitlines()]
    phases = {}
    action_times = {}
    for label, low, high in [("对照", 0, 10), ("连续互动", 10, 20)]:
        frames = [(b - a) * 1000 / frequency for a, b in zip(timing, timing[1:])
                  if low <= (a-start)/frequency and (b-start)/frequency < high]
        phases[label] = dict(stats(frames), 平均FPS=round(1000/statistics.mean(frames), 1),
                             超过50毫秒帧数=sum(t > 50 for t in frames))
        action_times[label] = stats([ms for t, ms in commands if low <= (t-start)/frequency < high])
    return {"验证环境": "同机启动四个 Windows 客户端，经公网房间服务通信。P1 以 1440×900 实际绘制，另外三席关闭图形渲染。",
            "场景": "四人双扩展、三国、海港、捕鱼、穿越沙漠",
            "负载": "每席每秒点击 8 次，持续 10 秒，前后各 40 次选用鸡蛋、鲜花。"
                  + ("每组含同道具右键重选、改投另一席和返回原席。" if combo_run else "")
                  + "对照段和互动段均提交游戏操作。",
            "第六下拖鞋": combos["shoe"], "第六下碰杯": combos["wine"], "实际投掷": 320, "其他席位预期接收": 960, "其他席位实际接收": exposures,
            "本地点击至首次绘制": stats(own), "点击至其他席位收到": stats(remote),
            "点击至接收者首次绘制": stats(receiver_draw), "点击至旁观者首次绘制": stats(observer_draw),
            "绘图席帧间隔": phases, "游戏操作至客户端确认": action_times}


if __name__ == "__main__":
    result = summarize(Path(sys.argv[1]))
    Path(sys.argv[2]).write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
