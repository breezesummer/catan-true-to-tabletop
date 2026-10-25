"""汇总第一批的程序结果和白名单阶段数值，不导出凭据、存档或网络正文。"""
import argparse
from collections import defaultdict
import json
import math
from pathlib import Path


def distribution(values):
    values = sorted(float(value) for value in values if value is not None)
    if not values:
        return {"样本数": 0}
    return {"样本数": len(values), "P50": values[math.ceil(len(values) * .5) - 1],
            "P95": values[math.ceil(len(values) * .95) - 1],
            "P99": values[math.ceil(len(values) * .99) - 1], "最大值": values[-1]}


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--root", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
programs = []
allowed = ("RuleSet", "ScenarioId", "Result", "Transport", "PlayerCount", "Turn", "HumanCommands",
           "ClientAiCommands", "LocalAuthorityAbsent", "LocalSaveRejected", "ReconnectConfirmed",
           "OwnSeatOnly", "RoomSwitchConfirmed", "OriginalRoomClosed", "NameUpdateConfirmed")
for group in ("player-local", "player-cloud", "player-legacy", "android-local", "player-recovery", "android-recovery"):
    for path in sorted((args.root / group).rglob("summary.json")):
        result = json.loads(path.read_text(encoding="utf-8-sig"))
        if result.get("Result") != "passed":
            raise ValueError("程序未通过，不能生成通过汇总：" + str(path))
        programs.append({"范围": group, **{key: result.get(key) for key in allowed}})

# 收集器可能在同一文件仍增长时取得多份副本；同名仅选最长副本，避免重复计数。
logs = {}
for folder in ("windows-diagnostics", "android-local/m7-logs", "android-recovery/m7-logs"):
    for path in (args.root / folder).rglob("comm-*.jsonl"):
        if path.name not in logs or path.stat().st_size > logs[path.name].stat().st_size:
            logs[path.name] = path
sessions = defaultdict(list)
for path in logs.values():
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        sessions[event.get("session")].append(event)
platforms = defaultdict(list)
builds = set()
for events in sessions.values():
    if not any(event.get("event") == "response_decode" for event in events):
        continue
    start = next((event for event in events if event.get("event") == "client_start"), {})
    platforms[start.get("platform", "未知")].extend(events)
    builds.add(start.get("buildId", "未知"))
measurements = []
for platform, events in sorted(platforms.items()):
    decoded = [event for event in events if event.get("event") == "response_decode"]
    large = [event for event in decoded if event.get("bytes", 0) >= 10000]
    applied = [event for event in events if event.get("event") == "response_apply"]
    measurements.append({"平台": platform,
        "范围": "功能程序后台运行；含首次与恢复，不作为前台帧率或稳态目标验收。本机与云端样本合并，不据此比较网络单程延迟。",
        "完整后台解析毫秒": distribution(event.get("durationMs", 0) for event in decoded),
        "后台排队毫秒": distribution(event.get("queueWaitMs", 0) for event in decoded),
        "至少10KB回复解析毫秒": distribution(event.get("durationMs", 0) for event in large),
        "至少10KB回复字节": distribution(event.get("bytes", 0) for event in large),
        "主线程数据应用毫秒": distribution(event.get("durationMs", 0) for event in applied),
        "主线程排队毫秒": distribution(event.get("queueWaitMs", 0) for event in applied),
        "有分配计数的解析样本": sum("allocatedBytes" in event for event in decoded),
        "最慢解析": [{key: event.get(key, 0) for key in ("operation", "bytes", "durationMs", "queueWaitMs")}
                     for event in sorted(decoded, key=lambda event: event.get("durationMs", 0), reverse=True)[:5]]})

report = {"日期": "2026-10-25", "程序检查": programs, "后台诊断": measurements,
          "客户端构建GUID": sorted(builds), "前台流畅性验收": False,
          "前台说明": "窗口激活失败后按用户要求停止前台对照；中止的原始采样保留，不计入通过。"}
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("已汇总程序检查：", len(programs), "；后台平台：", len(measurements))
