"""验证F0合同资料、固定案例及来源恢复边界，不执行游戏规则。"""
import contextlib
import hashlib
import io
import json
import re
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/development'))
from fetch_rule_references import restore
from verify_three_kingdoms_b0 import source_inventory


def verify_sources(require):
    manifest = json.loads((ROOT / 'assets/references/rules/traders-barbarians-v001/sources.json').read_text(encoding='utf-8'))
    require({s['id'] for s in manifest['sources']} == {'TB25','TB20','FCK','FSF','FAQ','INDEX'}, '四份PDF与FAQ／目录快照完整')
    for s in manifest['sources']:
        with contextlib.redirect_stdout(io.StringIO()):
            restore(ROOT, dict(cacheRoot=manifest['cacheRoot'], sources=[s]), True)
        require(True, f"{s['id']}原始字节数及SHA-256一致")
        if 'pdfPageCount' in s:
            require(all(1 <= p <= s['pdfPageCount'] for p in s['applicablePages']), f"{s['id']}引用页码范围有效")


def verify_contracts(require):
    folder = ROOT / 'docs/harbor-fishing'
    fingerprints = json.loads((folder / 'F0技能来源指纹.json').read_text(encoding='utf-8'))
    source = source_inventory((ROOT / fingerprints['源文件']).read_text(encoding='utf-8'))['武将']
    require(source == fingerprints['武将'], 'F0采用的54将资料与当期指纹一致')
    audit = (folder / 'F0三国技能审查.md').read_text(encoding='utf-8')
    rows = re.findall(r'^\| W(\d{2}) \| (.+?) \| (.+?) \| (.+?) \|$', audit, re.M)
    require([int(r[0]) for r in rows] == list(range(1,55)), 'W01—W54逐将覆盖无遗漏')
    require(sum(len(g['技能']) for g in source) == 97, '97项技能完整')
    for row,g in zip(rows, source):
        require(row[2].split('、') == g['技能'] and bool(row[3].strip()), f"W{row[0]}技能与裁决登记一致")
    contract = (folder / 'F0规则定稿.md').read_text(encoding='utf-8')
    require(re.findall(r'^### (D\d{2}) ', contract, re.M) == [f'D{i:02}' for i in range(1,8)], '七项闭合裁决完整')
    cases = json.loads((folder / 'F0固定局面.json').read_text(encoding='utf-8'))['cases']
    require(len(cases) == 32 and len({c['id'] for c in cases}) == 32, '32项固定案例无重复')
    require({c['decision'] for c in cases} == {f'D{i:02}' for i in range(1,8)}, '案例覆盖七项裁决')
    for c in cases:
        require(all(c.get(k) for k in ('given','expected','source','stage')), f"{c['id']}输入、预期、来源和执行边界齐全")
    maps = (folder / 'F0地图兼容表.md').read_text(encoding='utf-8')
    require(len(re.findall(r'^\| (?!地图|---).+? \|',maps,re.M)) == 10, '标准与九海图覆盖完整')
    for file in list(folder.glob('*.md')) + [ROOT/'docs/海港大师与捕鱼实施方案.md',ROOT/'assets/references/rules/README.md']:
        for target in re.findall(r'\]\(([^)]+)\)',file.read_text(encoding='utf-8')):
            if '://' in target or target.startswith('#'):
                continue
            require((file.parent/target.split('#')[0]).resolve().exists(), f'{file.name}链接有效：{target}')


def verify_documents():
    checks = []
    def require(ok, label):
        if not ok:
            raise AssertionError(label)
        checks.append(label)
    verify_contracts(require)
    verify_sources(require)
    return checks


class RestoreTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = dict(id='样本', url='https://www.catan.com/sample.pdf',cachePath='cache/sample.pdf',byteLength=3,sha256=hashlib.sha256(b'pdf').hexdigest())
        self.manifest = dict(cacheRoot='cache',sources=[self.source])
        self.path = self.root/'cache/sample.pdf'
    def run_restore(self, verify=False):
        with contextlib.redirect_stdout(io.StringIO()):
            restore(self.root,self.manifest,verify)
    def test_existing(self):
        self.path.parent.mkdir();self.path.write_bytes(b'pdf')
        with patch('fetch_rule_references.urlopen',side_effect=AssertionError('不应访问网络')):
            self.run_restore(True)
    def test_missing_verify(self):
        with self.assertRaises(FileNotFoundError):self.run_restore(True)
    def test_mismatch_preserved(self):
        self.path.parent.mkdir();self.path.write_bytes(b'bad')
        with self.assertRaises(ValueError):self.run_restore()
        self.assertEqual(self.path.read_bytes(),b'bad')
    def test_download(self):
        with patch('fetch_rule_references.urlopen',return_value=io.BytesIO(b'pdf')):self.run_restore()
        self.assertEqual(self.path.read_bytes(),b'pdf')
        self.assertEqual(list(self.path.parent.glob('*.download-*')),[])
    def test_bad_download_preserved(self):
        with patch('fetch_rule_references.urlopen',return_value=io.BytesIO(b'bad')):
            with self.assertRaises(ValueError):self.run_restore()
        self.assertFalse(self.path.exists())
        self.assertEqual(len(list(self.path.parent.glob('*.download-*'))),1)
    def test_escape(self):
        self.source['cachePath']='outside.pdf'
        with self.assertRaises(ValueError):self.run_restore()
    def test_foreign_host(self):
        self.source['url']='https://example.org/sample.pdf'
        with self.assertRaises(ValueError):self.run_restore()
    def test_race_preserves_existing(self):
        def response(*args,**kwargs):
            self.path.write_bytes(b'other')
            return io.BytesIO(b'pdf')
        with patch('fetch_rule_references.urlopen',side_effect=response):
            with self.assertRaises(FileExistsError):self.run_restore()
        self.assertEqual(self.path.read_bytes(),b'other')


if __name__ == '__main__':
    checks=verify_documents()
    result=unittest.TextTestRunner().run(unittest.defaultTestLoader.loadTestsFromTestCase(RestoreTests))
    if not result.wasSuccessful():sys.exit(1)
    report=dict(date='2026-10-29',scope='F0资料与Python来源恢复边界；不是游戏规则运行结果',documentChecks=len(checks),checks=checks,restoreTests=result.testsRun,restoreFailures=len(result.failures)+len(result.errors))
    (ROOT/'docs/verification/harbor-fishing/F0/检查结果.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(f'资料检查{len(checks)}项通过；恢复边界{result.testsRun}项通过。')
