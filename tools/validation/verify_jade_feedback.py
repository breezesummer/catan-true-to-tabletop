"""检查真实 GameView 按下、释放、移出截图，拒绝把静态截图当成交互验收。"""
import json
import sys
from pathlib import Path
from PIL import Image, ImageChops

root = Path(sys.argv[1])
base = Image.open(root / "青玉鎏金-实机.png").convert("RGB")
assert base.size == (1440, 900), "基准截图尺寸不符"
cases = [
    ("整理按下", "整理手牌-按下.png", (1170, 825, 1280, 875), True),
    ("整理释放", "整理手牌-释放.png", (1170, 825, 1280, 875), False),
    ("圆钮按下", "圆钮-按下.png", (10, 15, 103, 112), True),
    ("圆钮移出取消", "圆钮-移出取消.png", (10, 15, 103, 112), False),
]
result = {}
for label, name, bounds, changed in cases:
    frame = Image.open(root / name).convert("RGB")
    assert frame.size == base.size, f"{label}截图尺寸不符"
    delta = ImageChops.difference(base, frame).crop(bounds)
    pixels = sum(max(pixel) > 12 for pixel in delta.getdata())
    result[label] = pixels
    assert pixels >= 300 if changed else pixels == 0, f"{label}像素反馈异常：{pixels}"
for name, size in [("青玉鎏金-1280x720.png", (1280, 720)), ("青玉鎏金-1672x941.png", (1672, 941))]:
    assert Image.open(root / name).size == size, f"{name}尺寸不符"
report = json.dumps({"结果": "通过", "变化像素": result}, ensure_ascii=False, indent=2)
(root / "按钮像素验证.json").write_text(report + "\n", encoding="utf-8")
print(report)
