"""汇总数值帧样本；嵌套阶段为包含耗时，不可直接相加。"""
import csv
import json
import sys
from pathlib import Path


def summarize(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        rows = [{k: float(v) for k, v in r.items()} for r in csv.DictReader(stream)]
    # 去除启动和验证驱动本地 AI 决策帧；保留同步期间的正常卡顿。
    game = [r for r in rows if r["playing"] and r["HarnessDecision"] == 0 and r["frame"] >= 120]
    animated = [r for r in game if r["build_flights"] or r["card_busy"] or r["dice_busy"]]
    result = {"file": str(path), "total_frames": len(rows), "excluded_harness_frames": sum(r["HarnessDecision"] > 0 for r in rows)}
    for name, samples in [("game", game), ("animated", animated)]:
        metrics = {}
        for key in ["interval_ms", "NetworkUpdate", "NetworkApply", "ResponseDecode", "ViewDecode", "HistoryDecode", "BoardRefresh", "GameUpdate", "BuildAnimation", "Gui"]:
            values = sorted(r[key] for r in samples)
            positive = sorted(v for v in values if v > 0)
            metrics[key] = {
                "p95": values[min(len(values)-1, int(len(values)*.95))] if values else 0,
                "max": max(values, default=0),
                "active_mean": round(sum(positive)/len(positive), 3) if positive else 0,
                "active_count": len(positive),
            }
        result[name] = {"frames": len(samples), "over33ms": sum(r["interval_ms"] > 33.333 for r in samples), "gc_frames": sum(r["gc0"] > 0 for r in samples), "metrics": metrics,
                        "worst": sorted(samples, key=lambda r: r["interval_ms"], reverse=True)[:10]}
    return result


if __name__ == "__main__":
    print(json.dumps([summarize(Path(p)) for p in sys.argv[1:]], ensure_ascii=False, indent=2))
