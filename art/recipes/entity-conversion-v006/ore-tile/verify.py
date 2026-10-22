"""回读实际 Blend/FBX，检查闭合网格、UV、尺度、来源及干净重建。"""
import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
spec = importlib.util.spec_from_file_location('mountain_tile_verifier', REPO / 'tools/art/verify_entity_pipeline.py')
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)
verifier.VERSION = 'entity-conversion-v006'
verifier.main()
