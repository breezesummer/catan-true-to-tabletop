#!/usr/bin/env python3
"""在 macOS 上验收真实 HTTP 房间服务、Unity 在线大厅和服务重启恢复。"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid


ROOT = Path(__file__).resolve().parents[2]
LOCAL = (ROOT / ".local").resolve()


class VerificationError(Exception):
    """只携带可公开显示的验收说明，不附带凭据或原始响应。"""


class ChineseArgumentParser(argparse.ArgumentParser):
    def format_usage(self) -> str:
        return super().format_usage().replace("usage:", "用法：", 1)

    def format_help(self) -> str:
        return super().format_help().replace("usage:", "用法：", 1)


def new_output(value: str | None) -> Path:
    if value:
        candidate = Path(value).expanduser()
        if not candidate.is_absolute():
            candidate = ROOT / candidate
    else:
        stamp = dt.datetime.now(dt.timezone(dt.timedelta(hours=8))).strftime("%Y%m%d-%H%M%S")
        candidate = LOCAL / "verification" / f"online-lobby-{stamp}-{uuid.uuid4().hex[:8]}"
    output = candidate.resolve()
    if output == LOCAL or not output.is_relative_to(LOCAL):
        raise VerificationError("输出目录必须位于本项目 .local 内，且不能直接使用 .local。")
    try:
        output.mkdir(parents=True, mode=0o700, exist_ok=False)
    except FileExistsError:
        raise VerificationError("输出目录已经存在；请指定全新目录，避免复用旧身份或服务存档。") from None
    return output


def development_environment(output: Path) -> dict[str, str]:
    setup = ROOT / "tools/development/Enter-DevEnvironment.zsh"
    if sys.platform != "darwin":
        raise VerificationError("此验收脚本需要 macOS 和可显示画面的 Unity Editor。")
    if not setup.is_file():
        raise VerificationError(f"缺少项目开发环境入口：{setup}")
    # 路径通过位置参数传给 zsh，避免 shell 拼接；环境 JSON 仅在内存读取，绝不写入日志。
    command = [
        "/bin/zsh", "-f", "-c",
        'source "$1" >/dev/null && "$2" -c \'import json, os; print(json.dumps(dict(os.environ)))\'',
        "catan-online-environment", str(setup), sys.executable,
    ]
    try:
        loaded = subprocess.run(command, cwd=ROOT, capture_output=True, timeout=30, check=False)
    except (OSError, subprocess.TimeoutExpired):
        raise VerificationError("项目开发环境加载失败，请检查 zsh 和开发环境入口。") from None
    (output / "environment-errors.log").write_bytes(loaded.stderr)
    if loaded.returncode:
        raise VerificationError("项目开发环境加载失败，详见 environment-errors.log。")
    try:
        environment = json.loads(loaded.stdout)
    except (UnicodeError, json.JSONDecodeError):
        raise VerificationError("开发环境入口返回格式无效；未输出或保存任何环境变量。") from None
    if not isinstance(environment, dict) or not all(isinstance(k, str) and isinstance(v, str) for k, v in environment.items()):
        raise VerificationError("开发环境变量格式无效。")
    environment.pop("CATAN_CERTIFICATE_PASSWORD", None)
    return environment


def preflight(environment: dict[str, str], output: Path) -> tuple[Path, Path, Path]:
    dotnet = Path(environment.get("DOTNET_ROOT", "")) / "dotnet"
    unity = Path(environment.get("UNITY_EDITOR", ""))
    for label, executable in ((".NET SDK", dotnet), ("Unity Editor", unity)):
        if not executable.is_file() or not os.access(executable, os.X_OK):
            raise VerificationError(f"找不到可执行的 {label}：{executable}")
    with (output / "sdk-version.log").open("wb") as log:
        try:
            result = subprocess.run([str(dotnet), "--version"], cwd=ROOT, env=environment,
                                    stdout=log, stderr=subprocess.STDOUT, timeout=30, check=False)
        except (OSError, subprocess.TimeoutExpired):
            raise VerificationError(".NET SDK 预检查失败，详见 sdk-version.log。") from None
    if result.returncode:
        raise VerificationError(".NET SDK 与项目版本要求不匹配，详见 sdk-version.log。")
    candidates = [ROOT / f"src/Catan.Server/bin/{configuration}/net10.0/Catan.Server.dll"
                  for configuration in ("Release", "Debug")]
    server = next((path for path in candidates if path.is_file()), None)
    if server is None:
        raise VerificationError("缺少服务端 DLL；请先构建 src/Catan.Server/Catan.Server.csproj。")
    required = [
        server.with_suffix(".runtimeconfig.json"), server.with_suffix(".deps.json"),
        server.parent / "base-scenario.json", ROOT / "Unity/Catan/ProjectSettings/ProjectVersion.txt",
        ROOT / "Unity/Catan/Assets/M6.unity",
        ROOT / "Unity/Catan/Assets/Editor/OnlineLobbyEditorVerification.cs",
        ROOT / "Unity/Catan/Assets/Scripts/OnlineLobbyVerification.cs",
    ]
    for path in required:
        if not path.is_file():
            raise VerificationError(f"缺少验收必需文件：{path}")
    return dotnet, unity, server


def reserve_port() -> socket.socket:
    reservation = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    reservation.bind(("127.0.0.1", 0))
    return reservation


def stop_owned_process(process: subprocess.Popen | None) -> None:
    if process is None:
        return
    # 只操作本脚本以新会话启动的进程组，包括其子进程；不搜索或终止其他 Unity。
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except ProcessLookupError:
        pass
    deadline = time.monotonic() + 8
    while time.monotonic() < deadline:
        process.poll()
        try:
            os.killpg(process.pid, 0)
        except ProcessLookupError:
            break
        time.sleep(0.1)
    else:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)


def verify(environment: dict[str, str], output: Path, timeout: int) -> None:
    dotnet, unity, server_dll = preflight(environment, output)
    reservations = [reserve_port(), reserve_port()]
    port, offline_port = [reservation.getsockname()[1] for reservation in reservations]
    endpoint, offline = f"http://127.0.0.1:{port}", f"http://127.0.0.1:{offline_port}"
    server = editor = None
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    arguments = [str(dotnet), str(server_dll), "--mode", "online", "--port", str(port),
                 "--data-dir", str(output / "private-server")]
    (output / "endpoints.json").write_text(json.dumps({"online": endpoint, "offline": offline}), encoding="utf-8")
    with (output / "server.log").open("w", encoding="utf-8") as server_log, \
            (output / "editor-console.log").open("w", encoding="utf-8") as editor_log:
        def start_server() -> None:
            nonlocal server
            if server is not None and server.poll() is None:
                raise VerificationError("验收服务已经运行，拒绝重复启动。")
            server = subprocess.Popen(arguments, cwd=ROOT, stdout=server_log, stderr=subprocess.STDOUT,
                                      env=environment, start_new_session=True)
            deadline = time.monotonic() + 20
            while time.monotonic() < deadline:
                if server.poll() is not None:
                    raise VerificationError("独立 HTTP 服务启动失败，详见 server.log。")
                try:
                    with opener.open(endpoint + "/health", timeout=0.5) as response:
                        health = json.load(response)
                        if response.status == 200 and health.get("status") == "ok" and health.get("transport") == "http":
                            return
                except (OSError, urllib.error.URLError, json.JSONDecodeError):
                    pass
                time.sleep(0.1)
            raise VerificationError("独立 HTTP 服务健康检查超时，详见 server.log。")

        try:
            reservations[0].close()
            start_server()
            print(f"独立 HTTP 服务已启动，验收端口：{port}；全部身份和存档使用隔离目录。", flush=True)
            editor_arguments = [
                str(unity), "-projectPath", str(ROOT / "Unity/Catan"), "-buildTarget", "Win64",
                "-executeMethod", "OnlineLobbyEditorVerification.Run", "--online-lobby-verify",
                str(output), endpoint, offline, "-logFile", str(output / "editor.log"),
            ]
            reservations[1].close()
            editor = subprocess.Popen(editor_arguments, cwd=ROOT, stdout=editor_log, stderr=subprocess.STDOUT,
                                      env=environment, start_new_session=True)
            deadline = time.monotonic() + timeout
            while editor.poll() is None:
                if (output / "server-stop.request").exists() and not (output / "server-stopped.signal").exists():
                    stop_owned_process(server)
                    server = None
                    (output / "server-stopped.signal").touch()
                    print("验收服务已停止，正在检查大厅离线提示。", flush=True)
                if (output / "server-start.request").exists() and not (output / "server-started.signal").exists():
                    start_server()
                    (output / "server-started.signal").touch()
                    print("验收服务已恢复，正在检查自动重连。", flush=True)
                if time.monotonic() > deadline:
                    raise VerificationError("Unity 在线大厅验收超时，详见 editor.log。")
                time.sleep(0.2)
            if editor.returncode != 0:
                raise VerificationError(f"Unity 验收退出码为 {editor.returncode}，详见 editor.log 和 editor-console.log。")
            try:
                summary = json.loads((output / "summary.json").read_text(encoding="utf-8"))
            except (OSError, UnicodeError, json.JSONDecodeError):
                raise VerificationError("Unity 未产生有效的 summary.json，详见 editor.log。") from None
            if not isinstance(summary, dict) or summary.get("status") != "passed":
                raise VerificationError("summary.json 未确认验收通过，详见 editor.log。")
            checks, captures = summary.get("checks", 0), summary.get("renderableCaptures", 0)
            if not isinstance(checks, int) or checks <= 0 or not isinstance(captures, int) or captures <= 0:
                raise VerificationError("验收缺少有效检查或可渲染截图，详见 summary.json 和 editor.log。")
            if summary.get("realHttpService") is not True or summary.get("serverRestartRecovery") is not True:
                raise VerificationError("验收未确认真实 HTTP 服务和重启恢复，详见 summary.json。")
            print(f"在线大厅验收通过：{checks} 项检查、{captures} 张有效截图，Unity 退出码 0。", flush=True)
        finally:
            for reservation in reservations:
                reservation.close()
            try:
                stop_owned_process(editor)
            finally:
                stop_owned_process(server)


def interrupted(_signum: int, _frame: object) -> None:
    raise KeyboardInterrupt


def main() -> int:
    parser = ChineseArgumentParser(description=__doc__, add_help=False)
    parser._positionals.title = "位置参数"
    parser._optionals.title = "可选参数"
    parser.add_argument("-h", "--help", action="help", help="显示帮助信息并退出。")
    parser.add_argument("--output", help="本项目 .local 下的全新输出目录；相对路径以仓库根目录为起点。")
    parser.add_argument("--timeout", type=int, default=600, help="Unity 验收等待上限，单位秒，默认 600。")
    options = parser.parse_args()
    if options.timeout <= 0:
        parser.error("--timeout 必须大于 0。")
    output = None
    signal.signal(signal.SIGTERM, interrupted)
    try:
        output = new_output(options.output)
        print(f"验收输出目录：{output}", flush=True)
        verify(development_environment(output), output, options.timeout)
        return 0
    except KeyboardInterrupt:
        print("验收已中断，已清理本脚本启动的服务和 Unity 进程。", file=sys.stderr)
        return 130
    except VerificationError as error:
        print(f"验收失败：{error}", file=sys.stderr)
        return 1
    except Exception as error:
        # 不输出未知异常的原始消息，防止意外包含凭据或服务私有响应。
        print(f"验收异常：{type(error).__name__}；请检查输出目录中的日志。", file=sys.stderr)
        return 1
    finally:
        if output is not None:
            print(f"日志与验收证据：{output}", flush=True)


if __name__ == "__main__":
    raise SystemExit(main())
