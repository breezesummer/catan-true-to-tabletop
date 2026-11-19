"""使用第二版生成器核验实体资产。"""
import importlib.util
from pathlib import Path


def main():
    path = Path(__file__).with_name('verify_entity_pipeline.py')
    spec = importlib.util.spec_from_file_location('entity_verifier', path)
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    verifier.VERSION = 'entity-conversion-v002'
    verifier.main(generator_script='entity_pipeline_v002.py')


if __name__ == '__main__':
    main()
