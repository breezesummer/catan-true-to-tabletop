"""在独立的 Windows 验收客户端中点击，录制其客户区，核对规则结果。"""
import argparse
import ctypes
import subprocess
import time
from pathlib import Path

from PIL import ImageDraw, ImageGrab
import win32api
import win32con
import win32gui
import win32process


def window_for(process):
    matches = []
    def add(handle, _):
        if (win32gui.IsWindowVisible(handle)
                and win32process.GetWindowThreadProcessId(handle)[1] == process.pid
                and win32gui.GetClassName(handle) == "UnityWndClass"):
            matches.append(handle)
    win32gui.EnumWindows(add, None)
    return matches[0] if matches else None


def client_box(window):
    _, _, width, height = win32gui.GetClientRect(window)
    x, y = win32gui.ClientToScreen(window, (0, 0))
    return x, y, x + width, y + height


def belongs(window, point):
    leaf = win32gui.WindowFromPoint(point)
    return leaf == window or win32gui.IsChild(window, leaf)


def record(executable, fixture, output):
    ctypes.windll.user32.SetThreadDpiAwarenessContext(ctypes.c_void_p(-4))
    output.mkdir(parents=True, exist_ok=True)
    if any(output.glob("pointer-*.txt")):
        raise ValueError("请指定新的验收目录")
    player = subprocess.Popen([str(executable), "-force-d3d11", "-screen-fullscreen", "0",
                               "-screen-width", "1440", "-screen-height", "900",
                               "--ocean-view-verify", str(output), "--direct-input", str(fixture),
                               "-logFile", str(output / "player.log")])
    frames = []
    frame_times = []
    click_at = 0
    step = 1
    box = None
    start_time = time.monotonic()
    next_sample = start_time
    pending = None
    try:
        while player.poll() is None:
            now = time.monotonic()
            if now - start_time > 180:
                raise TimeoutError("窗口验收超时，请检查 player.log")
            window = window_for(player)
            request = output / f"pointer-{step}.txt"
            if window and request.exists() and pending is None:
                x, y, button = map(int, request.read_text().split())
                if box is None:
                    win32gui.ShowWindow(window, win32con.SW_RESTORE)
                    if win32gui.GetForegroundWindow() != window:
                        foreground = win32gui.GetForegroundWindow()
                        active_thread = win32process.GetWindowThreadProcessId(foreground)[0]
                        current_thread = win32api.GetCurrentThreadId()
                        user32 = ctypes.windll.user32
                        user32.AttachThreadInput(current_thread, active_thread, True)
                        try:
                            user32.BringWindowToTop(window)
                            user32.SetForegroundWindow(window)
                        finally:
                            user32.AttachThreadInput(current_thread, active_thread, False)
                box = client_box(window)
                point = box[0] + x, box[1] + y
                if not belongs(window, point) or win32gui.GetForegroundWindow() != window:
                    raise RuntimeError("验收窗口被遮挡，已停止鼠标输入")
                win32api.SetCursorPos(point)
                pending = (point, button, "hover", now + .38)
            if pending and now >= pending[3]:
                point, button, phase, _ = pending
                if not belongs(window, point) or win32gui.GetForegroundWindow() != window:
                    raise RuntimeError("验收窗口失去焦点，已停止鼠标输入")
                if phase == "hover":
                    click_at = now
                    win32api.mouse_event(win32con.MOUSEEVENTF_RIGHTDOWN if button else win32con.MOUSEEVENTF_LEFTDOWN, 0, 0)
                    pending = (point, button, "pressed", now + .12)
                else:
                    win32api.mouse_event(win32con.MOUSEEVENTF_RIGHTUP if button else win32con.MOUSEEVENTF_LEFTUP, 0, 0)
                    (output / f"done-{step}").touch()
                    step += 1
                    pending = None
            if box and now >= next_sample and len(frames) < 300:
                next_sample = now + .115
                corners = [(box[0]+3, box[1]+3), (box[2]-4, box[1]+3),
                           (box[0]+3, box[3]-4), (box[2]-4, box[3]-4)]
                if win32gui.GetForegroundWindow() == window and all(belongs(window, c) for c in corners):
                    image = ImageGrab.grab(bbox=box, all_screens=True).convert("RGB")
                    point = win32api.GetCursorPos()
                    px, py = point[0]-box[0], point[1]-box[1]
                    painter = ImageDraw.Draw(image)
                    if now-click_at < .35:
                        painter.ellipse((px-15, py-15, px+15, py+15), outline="white", width=2)
                    painter.polygon([(px,py),(px+1,py+18),(px+5,py+14),(px+10,py+21),
                                     (px+13,py+19),(px+9,py+12),(px+15,py+11)],
                                    fill="white",outline="black")
                    frames.append(image.resize((1120, round(image.height*1120/image.width))))
                    frame_times.append(now)
            time.sleep(.025)
    finally:
        if pending and pending[2] == "pressed":
            win32api.mouse_event(win32con.MOUSEEVENTF_RIGHTUP if pending[1] else win32con.MOUSEEVENTF_LEFTUP, 0, 0)
        if player.poll() is None:
            player.terminate()
            player.wait(10)
    if player.returncode or not (output / "result.json").exists():
        raise RuntimeError("实机验证失败，请检查 player.log")
    if len(frames) < 12:
        raise RuntimeError("实机录像缺帧")
    durations = [max(50,round(1000*(b-a))) for a,b in zip(frame_times,frame_times[1:])] + [1000]
    frames[0].save(output/"工程师与仁德-实机操作.webp", save_all=True, append_images=frames[1:],
                   duration=durations, loop=0, quality=77, method=3)
    for scratch in list(output.glob("pointer-*.txt")) + list(output.glob("done-*")):
        scratch.unlink()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("executable", type=Path)
    parser.add_argument("fixture", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    record(args.executable.resolve(), args.fixture.resolve(), args.output.resolve())
