"""用独立日志样例验证跨端关联、过滤与不完整日志容错。"""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("summary", Path(__file__).with_name("Summarize-M7Diagnostics.py"))
summary = importlib.util.module_from_spec(spec)
spec.loader.exec_module(summary)


class SummaryTests(unittest.TestCase):
    def write(self, directory, name, rows):
        path = Path(directory) / name
        path.write_text("\n".join(json.dumps(row) for row in rows) + "\n", encoding="utf-8")
        return path

    def test_correlates_request_and_keeps_stage_measurements_separate(self):
        with tempfile.TemporaryDirectory() as directory:
            client = self.write(directory, "client.jsonl", [
                {"utc": "2026-10-22T14:01:00Z", "event": "response_received", "requestId": "abc", "durationMs": 120, "bytes": 500, "Credential": "不可输出"},
                {"utc": "2026-10-22T14:01:01Z", "component": "presentation", "event": "frame_window", "durationMs": 5000, "networkApplyMs": 15, "frameMs": 45}])
            server = self.write(directory, "server.jsonl", [
                {"timestampUtc": "2026-10-22T14:00:59.9999999+00:00", "sessionId": "xyz", "data": {"event": "request.completed", "requestId": "abc", "elapsedMs": 3, "bytes": 500}}])
            result = summary.summarize([client, server], request_id="abc")
            self.assertEqual(3, result["统计"]["有效事件"])
            self.assertEqual(["server", "client"], [e["component"] for e in result["指定请求链路"]])
            self.assertNotIn("不可输出", json.dumps(result, ensure_ascii=False))
            self.assertEqual(15, result["指标最大值（阶段耗时为窗口累计值）"]["networkApplyMs"])

    def test_truncated_and_oversized_lines_do_not_hide_following_events(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self.write(directory, "client.jsonl", [{"utc": "2026-10-22T14:01:00Z", "event": "request_send"}])
            with path.open("a", encoding="utf-8") as out:
                out.write("x" * 70000 + "\n{\"utc\":\n")
                out.write(json.dumps({"utc": "2026-10-22T14:02:00Z", "event": "request_failed"}) + "\n")
            result = summary.summarize([path])
            self.assertEqual(2, result["统计"]["有效事件"])
            self.assertEqual(1, result["统计"]["无效或截断行"])
            self.assertEqual(1, result["统计"]["跳过超长行"])

    def test_local_time_filter_and_cumulative_loss_counts(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self.write(directory, "client.jsonl", [
                {"utc": "2026-10-22T14:00:00Z", "session": "a", "event": "session_start", "droppedEvents": 2},
                {"utc": "2026-10-22T14:01:00Z", "session": "a", "event": "input_wait_begin", "code": "command_pending", "droppedEvents": 4},
                {"utc": "2026-10-22T14:02:00Z", "session": "a", "event": "input_wait_long", "code": "command_pending", "droppedEvents": 4}])
            result = summary.summarize([path], since=summary.timestamp("2026-10-22T22:00:30+08:00"))
            self.assertEqual(2, result["统计"]["有效事件"])
            self.assertEqual(4, result["统计"]["客户端丢弃日志事件"])
            self.assertEqual({"command_pending": 1}, result["操作等待原因次数"])


if __name__ == "__main__":
    unittest.main()
