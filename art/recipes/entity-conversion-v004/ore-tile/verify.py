"""Reimport and inspect the actual exported flat hex mesh and compare clean rebuilds."""
import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
spec = importlib.util.spec_from_file_location('ore_tile_verifier', REPO / 'tools/art/verify_entity_pipeline.py')
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)
verifier.VERSION = 'entity-conversion-v004'
verifier.main()
