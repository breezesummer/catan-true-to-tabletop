"""使用版本化闭合网格生成器，不修改既有版本。"""
import importlib.util
from pathlib import Path
REPO=Path(__file__).resolve().parents[4]
spec=importlib.util.spec_from_file_location('tabletop_markers_generator',REPO/'tools/art/entity_pipeline.py')
generator=importlib.util.module_from_spec(spec);spec.loader.exec_module(generator)
generator.VERSION='entity-conversion-v005'
generator.main()
