"""Use the frozen generic mesh recipe with a new version, leaving earlier outputs intact."""
import importlib.util
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
spec = importlib.util.spec_from_file_location('ore_tile_generator', REPO / 'tools/art/entity_pipeline.py')
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)
generator.VERSION = 'entity-conversion-v004'
generator.main()
