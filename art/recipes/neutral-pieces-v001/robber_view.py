"""通过 Blender MCP 回读现行强盗模型，联用正式图集拍摄各角度。"""
from pathlib import Path
import runpy

entry=Path(__file__).resolve().parents[1]/"roadside-bandits"/"inspect_piece.py"
result=runpy.run_path(str(entry))["result"]
