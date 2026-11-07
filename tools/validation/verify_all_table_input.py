"""独立验收客户端的鼠标驱动。只向当前验收窗口点击，遮挡或失焦时停止。"""
import ctypes as c
import ctypes.wintypes as w
import os
from pathlib import Path
import subprocess, sys, time
u=c.windll.user32
u.SetThreadDpiAwarenessContext(c.c_void_p(-4))
exe,save,out=map(lambda v:Path(v).resolve(),sys.argv[1:4])
fixtures=Path(sys.argv[4]).resolve() if len(sys.argv)>4 else out.parent/'fixtures'
if not fixtures.is_dir():raise SystemExit('找不到验收局面目录')
out.mkdir(parents=True,exist_ok=False)
mode=['--audit-all',str(fixtures),'--direct-input',str(save)]
if '--skin-clicks' in sys.argv[5:]:
    mode=['--zhang-liao',str(fixtures),str(save),'--skin-catalog','--skin-clicks']
p=subprocess.Popen([str(exe),'-force-d3d11','-screen-fullscreen','0','-screen-width','1280','-screen-height','800','--ocean-view-verify',str(out),*mode,'-logFile',str(out/'player.log')] + (['--audit-items',os.environ['TABLE_ITEMS']] if os.environ.get('TABLE_ITEMS') else []))
enum=c.WINFUNCTYPE(w.BOOL,w.HWND,w.LPARAM)
u.WindowFromPoint.argtypes=[w.POINT]
def find():
    windows=[]
    @enum
    def read(h,_):
        pid=w.DWORD();u.GetWindowThreadProcessId(h,c.byref(pid))
        name=c.create_unicode_buffer(100);u.GetClassNameW(h,name,100)
        if pid.value==p.pid and u.IsWindowVisible(h) and name.value=='UnityWndClass':windows.append(h)
        return True
    u.EnumWindows(read,0)
    return windows[0] if windows else None
from PIL import ImageGrab, ImageDraw
def snap(h,point,name):
    r=w.RECT();u.GetClientRect(h,c.byref(r));a=w.POINT(0,0);u.ClientToScreen(h,c.byref(a))
    if u.GetForegroundWindow()!=h:return
    for px,py in [(1,1),(r.right-2,1),(r.right-2,r.bottom-2),(1,r.bottom-2)]:
        at=u.WindowFromPoint(w.POINT(a.x+px,a.y+py))
        if at!=h and not u.IsChild(h,at):return
    img=ImageGrab.grab(bbox=(a.x,a.y,a.x+r.right,a.y+r.bottom),all_screens=True).convert("RGB")
    x,y=point.x-a.x,point.y-a.y
    ImageDraw.Draw(img).ellipse((x-10,y-10,x+10,y+10),outline="#ffd564",width=3)
    (out/'鼠标操作').mkdir(exist_ok=True)
    img.thumbnail((896,560));img.save(out/'鼠标操作'/name,quality=75)
step=1
started=time.monotonic()
try:
    while p.poll() is None:
        if time.monotonic()-started>1800:raise RuntimeError('验收超时')
        request=out/f'pointer-{step}.txt'
        if not request.exists():time.sleep(.05);continue
        h=find()
        if not h:time.sleep(.1);continue
        if u.GetForegroundWindow()!=h or step==1:
            fg=u.GetForegroundWindow()
            current=c.windll.kernel32.GetCurrentThreadId()
            thread=u.GetWindowThreadProcessId(fg,None)
            if thread!=current:u.AttachThreadInput(current,thread,True)
            try:
                u.ShowWindow(h,9)
                u.SetWindowPos(h,-1,10,10,0,0,0x0001|0x0040)
                u.BringWindowToTop(h)
                u.SetForegroundWindow(h)
            finally:
                if thread!=current:u.AttachThreadInput(current,thread,False)
            time.sleep(.25)
        x,y,button=map(int,request.read_text().split())
        point=w.POINT(x,y);u.ClientToScreen(h,c.byref(point))
        def valid():
            at=u.WindowFromPoint(point)
            return u.GetForegroundWindow()==h and (at==h or u.IsChild(h,at))
        if not valid():raise RuntimeError(f'window={h} at={u.WindowFromPoint(point)} foreground={u.GetForegroundWindow()} point={point.x},{point.y} desktop={u.GetSystemMetrics(0)}x{u.GetSystemMetrics(1)}')
        u.SetCursorPos(point.x,point.y)
        time.sleep(.05)
        if not valid():raise RuntimeError('失焦')
        u.mouse_event(8 if button else 2,0,0,0,0)
        time.sleep(.075)
        u.mouse_event(16 if button else 4,0,0,0,0)
        time.sleep(.17);snap(h,point,f'{step:04d}.jpg')
        (out/f'done-{step}').touch()
        step+=1
finally:
    if p.poll() is None:p.terminate();p.wait()
if p.returncode or not(out/'result.json').exists():raise RuntimeError('验收客户端未正常完成')
print('实际输入次数',step-1)

