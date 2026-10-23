"""汇总客户端和服务端的通信 JSONL，只读取白名单元数据，不输出原始消息。"""
import argparse
from collections import Counter, defaultdict
from datetime import datetime, timezone
import json
import math
from pathlib import Path


def timestamp(value):
    if not isinstance(value, str):
        return None
    try:
        result = datetime.fromisoformat(value.replace("Z", "+00:00"))
        return result.astimezone(timezone.utc) if result.tzinfo else None
    except ValueError:
        return None


def label(value):
    return "".join(c if c.isalnum() or c in "_.:-+" else "_" for c in str(value or "")[:128])


def number(value):
    return float(value) if isinstance(value, (int, float)) and math.isfinite(value) else 0.0


def normalize(raw):
    if not isinstance(raw, dict):
        return None
    server = isinstance(raw.get("data"), dict)
    event = raw["data"] if server else raw
    when = timestamp(raw.get("timestampUtc") if server else raw.get("utc"))
    if when is None or not event.get("event"):
        return None
    result = {key: label(event.get(key)) for key in
              ("event", "transport", "operation", "requestId", "commandId", "roomId", "code", "status", "exceptionType")}
    result.update(utc=when.isoformat(), component="server" if server else label(raw.get("component", "client")),
                  session=label(raw.get("sessionId") if server else raw.get("session")),
                  durationMs=number(event.get("elapsedMs") if server else event.get("durationMs")),
                  bytes=number(event.get("bytes")), revision=number(event.get("revision")),
                  failed=event.get("success") is False or event.get("status") == "rejected")
    for key in ("expectedRevision", "waitMs", "queueWaitMs", "queueDepth", "decodeMs", "frameMs", "networkApplyMs", "boardRefreshMs",
                "viewDecodeMs", "guiMs", "gameUpdateMs", "buildAnimationMs", "presentationApplyMs",
                "historyDecodeMs", "workerDecodeMs", "slowFrames", "gcCollections", "droppedEvents", "writeFailures", "dropped"):
        result[key] = number(event.get(key))
    return result


def read_events(paths, counts, since=None, until=None):
    for path in paths:
        with path.open("r", encoding="utf-8-sig", errors="replace") as source:
            while True:
                # 正常事件远小于此上限；不接受误传的大型存档作为诊断记录。
                line = source.readline(65537)
                if not line:
                    break
                if len(line) > 65536:
                    while line and not line.endswith("\n"):
                        line = source.readline(65537)
                    counts["跳过超长行"] += 1
                    continue
                try:
                    event = normalize(json.loads(line))
                except (ValueError, TypeError):
                    event = None
                if event is None:
                    counts["无效或截断行"] += 1
                    continue
                when = timestamp(event["utc"])
                if (since and when < since) or (until and when > until):
                    continue
                counts["有效事件"] += 1
                yield event


def stats(values):
    if not values:
        return {"样本数": 0}
    ordered = sorted(values)
    return {"样本数": len(ordered), "平均ms": round(sum(ordered) / len(ordered), 3),
            "P50ms": round(ordered[math.ceil(len(ordered) * .50) - 1], 3),
            "P95ms": round(ordered[math.ceil(len(ordered) * .95) - 1], 3), "最大ms": round(ordered[-1], 3)}


def summarize(paths, since=None, until=None, request_id=None):
    counts = Counter()
    groups = defaultdict(lambda: {"count": 0, "bytes": 0, "duration": []})
    maxima = defaultdict(float)
    dropped = defaultdict(float)
    failures = defaultdict(float)
    reasons = Counter()
    recent = []
    trace = []
    first = last = None
    for event in read_events(paths, counts, since, until):
        first = min(first or event["utc"], event["utc"])
        last = max(last or event["utc"], event["utc"])
        key = (event["component"], event["transport"], event["event"], event["operation"],
               event["code"] if event["event"].startswith("input_wait_") else "")
        group = groups[key]
        group["count"] += 1
        group["bytes"] += event["bytes"]
        if event["durationMs"] > 0:
            group["duration"].append(event["durationMs"])
        for name in ("frameMs", "networkApplyMs", "boardRefreshMs", "viewDecodeMs", "guiMs", "gameUpdateMs",
                     "buildAnimationMs", "presentationApplyMs", "historyDecodeMs", "workerDecodeMs", "waitMs", "queueWaitMs", "queueDepth"):
            maxima[name] = max(maxima[name], event[name])
        session = (event["component"], event["session"])
        dropped[session] = max(dropped[session], event["droppedEvents"])
        failures[session] = max(failures[session], event["writeFailures"])
        counts["服务端丢弃日志事件"] += int(event["dropped"])
        if event["event"] == "input_wait_begin":
            reasons[event["code"] or event["status"]] += 1
        if event["failed"] or any(word in event["event"] for word in ("failed", "error", "disconnect", "closed", "timeout", "fallback")):
            recent.append({k: v for k, v in event.items() if v or k == "utc"})
            recent.sort(key=lambda item: item["utc"])
            recent = recent[-100:]
        if request_id and event["requestId"] == request_id:
            trace.append({k: v for k, v in event.items() if v or k == "utc"})
    counts["客户端丢弃日志事件"] = int(sum(dropped.values()))
    counts["客户端写盘失败次数"] = int(sum(failures.values()))
    return {"说明": "耗时按事件分别统计；嵌套阶段和后台解析不可相加当作帧耗时。字节数仅为该事件计数，收发与投影不可相加当作网络总流量。跨设备时钟可能有偏差。",
            "文件数": len(paths), "开始UTC": first, "结束UTC": last, "统计": dict(counts),
            "事件分组": [{"组件": k[0], "传输": k[1], "事件": k[2], "操作": k[3], "等待原因": k[4], "次数": v["count"],
                         "累计字节": int(v["bytes"]), "耗时": stats(v["duration"])} for k, v in sorted(groups.items())],
            "指标最大值（阶段耗时为窗口累计值）": dict(maxima), "操作等待原因次数": dict(reasons), "最近异常或传输切换": list(recent),
            "指定请求链路": sorted(trace, key=lambda item: item["utc"])}


def main():
    parser = argparse.ArgumentParser(description="汇总通信日志，可合并多名玩家与服务端目录；不读取凭据或存档。")
    parser.add_argument("paths", nargs="+", type=Path, help="JSONL 文件或诊断目录（ZIP 需先解压）")
    parser.add_argument("--since", help="带时区起始时间，例如 2026-10-22T22:00:00+08:00")
    parser.add_argument("--until", help="带时区结束时间")
    parser.add_argument("--request-id", help="列出同一请求在客户端和服务端的所有记录")
    parser.add_argument("--output", type=Path, help="保存汇总 JSON")
    args = parser.parse_args()
    for value in (args.since, args.until):
        if value and timestamp(value) is None:
            parser.error("时间须为带时区的 ISO 格式。")
    since, until = timestamp(args.since), timestamp(args.until)
    if since and until and since > until:
        parser.error("起始时间不可晚于结束时间。")
    paths = sorted({f.resolve() for path in args.paths for f in
                    (path.rglob("*.jsonl") if path.is_dir() else [path]) if f.is_file() and f.suffix == ".jsonl"})
    if not paths:
        parser.error("没有找到 JSONL 日志。")
    result = summarize(paths, since, until, args.request_id)
    encoded = json.dumps(result, ensure_ascii=False, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded + "\n", encoding="utf-8")
        print(f"已汇总 {len(paths)} 个日志，{result['统计'].get('有效事件', 0)} 条事件：{args.output}")
    else:
        print(encoded)


if __name__ == "__main__":
    main()
