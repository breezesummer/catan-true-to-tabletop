"""复用已固定的闭合六边形厚板导出器，独立保存高山照片版本。"""
import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
spec = importlib.util.spec_from_file_location('mountain_tile_generator', REPO / 'tools/art/entity_pipeline.py')
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)
generator.VERSION = 'entity-conversion-v006'
generator.main()
