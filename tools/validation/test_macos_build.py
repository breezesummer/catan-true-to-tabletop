"""构建脚本的失效保护验证：清理边界、损坏源文件与签名保管。"""
import contextlib
import importlib.util
import io
import os
from pathlib import Path
import tempfile
import unittest
from unittest import mock

SPEC = importlib.util.spec_from_file_location('catan_build', Path(__file__).resolve().parents[1] / 'development/build_macos.py')
BUILD = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BUILD)


class BuildProtectionTests(unittest.TestCase):
    def test_android_staging_does_not_overwrite_unowned_directory(self):
        with tempfile.TemporaryDirectory() as folder:
            stage = Path(folder) / 'occupied'
            stage.mkdir()
            original = stage / '原件.txt'
            original.write_text('保留')
            with self.assertRaisesRegex(RuntimeError, '被其他文件占用'):
                BUILD.android_workspace(Path(folder), stage)
            self.assertEqual(original.read_text(), '保留')

    def test_android_staging_removes_deleted_sources_and_preserves_originals(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            project = root / 'original'
            for name in ('Assets', 'Packages', 'ProjectSettings'):
                (project / name).mkdir(parents=True)
            (project / 'Assets/保留.cs').write_text('源文件')
            stage = root / 'stage'
            logs = root / 'logs'
            logs.mkdir()
            with mock.patch.object(BUILD, 'PROJECT', project), contextlib.redirect_stdout(io.StringIO()):
                BUILD.android_workspace(logs, stage)
                (stage / 'Unity/Catan/Assets/已删除.cs').write_text('过期内容')
                (stage / 'output').mkdir()
                (stage / 'output/旧版.apk').write_bytes(b'old-apk')
                BUILD.android_workspace(logs, stage)
            self.assertFalse((stage / 'Unity/Catan/Assets/已删除.cs').exists())
            self.assertFalse((stage / 'output').exists())
            self.assertEqual((project / 'Assets/保留.cs').read_text(), '源文件')
            self.assertEqual((stage / 'Unity/Catan/Assets/保留.cs').read_text(), '源文件')

    def test_retention_keeps_numeric_latest_five_and_protected_releases(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            versions = root / 'game version/1.6'
            versions.mkdir(parents=True)
            for number in (1, 2, 3, 4, 5, 6, 7, 10):
                path = versions / f'Catan1.6.{number}-Test'
                path.mkdir()
                if number == 1:
                    (path / 'release').mkdir()
                if number == 2:
                    (path / '正式发布.txt').touch()
            unrelated = versions / '原始资料'
            unrelated.mkdir()
            with mock.patch.object(BUILD, 'ROOT', root), contextlib.redirect_stdout(io.StringIO()):
                BUILD.retain()
            self.assertFalse((versions / 'Catan1.6.3-Test').exists())
            for number in (1, 2, 4, 5, 6, 7, 10):
                self.assertTrue((versions / f'Catan1.6.{number}-Test').is_dir())
            self.assertTrue(unrelated.is_dir())

    def test_retention_ignores_linked_versions_and_linked_archives(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            versions = root / 'game version'
            versions.mkdir()
            external = root / 'external'
            external.mkdir()
            (external / '保留原件').touch()
            (versions / '1.0').symlink_to(external, target_is_directory=True)
            (versions / 'Catan0.0.1-Test').symlink_to(external, target_is_directory=True)
            for number in range(6):
                (versions / f'Catan1.0.{number}-Test').mkdir()
            with mock.patch.object(BUILD, 'ROOT', root), contextlib.redirect_stdout(io.StringIO()):
                BUILD.retain()
            self.assertTrue((external / '保留原件').exists())
            self.assertTrue((versions / 'Catan0.0.1-Test').is_symlink())

    def test_empty_or_nul_unity_sources_stop_before_build(self):
        for content in (b'', b'guid: 12\0ab'):
            with self.subTest(content=content), tempfile.TemporaryDirectory() as folder:
                project = Path(folder)
                (project / 'Assets').mkdir()
                (project / 'Assets/texture.png.meta').write_bytes(content)
                with mock.patch.object(BUILD, 'PROJECT', project):
                    with self.assertRaisesRegex(RuntimeError, 'NUL'):
                        BUILD.integrity()

    def test_missing_original_signing_does_not_create_replacement(self):
        with tempfile.TemporaryDirectory() as folder:
            local = Path(folder)
            with mock.patch.object(BUILD, 'LOCAL', local), mock.patch.dict(os.environ, {}, clear=True):
                with self.assertRaisesRegex(RuntimeError, '缺少原 Android 签名'):
                    with BUILD.signing({}, False, local):
                        self.fail('缺失原签名时不应进入构建。')
            self.assertEqual(list(local.iterdir()), [])

    def test_import_does_not_replace_existing_different_key(self):
        with tempfile.TemporaryDirectory() as folder:
            local = Path(folder)
            original = local / 'credentials/android/catan-internal.keystore'
            original.parent.mkdir(parents=True)
            original.write_bytes(b'original-key')
            incoming = local / 'incoming.keystore'
            incoming.write_bytes(b'different-key')
            with mock.patch.object(BUILD, 'LOCAL', local), mock.patch.object(BUILD.getpass, 'getpass') as prompt:
                with self.assertRaisesRegex(RuntimeError, '不同的 Android 签名'):
                    BUILD.import_signing({'keytool': Path('/usr/bin/true')}, incoming)
                prompt.assert_not_called()
            self.assertEqual(original.read_bytes(), b'original-key')

    def test_failed_password_does_not_save_key_or_password(self):
        with tempfile.TemporaryDirectory() as folder:
            local = Path(folder)
            incoming = local / 'incoming.keystore'
            incoming.write_bytes(b'key')
            with mock.patch.object(BUILD, 'LOCAL', local), mock.patch.object(BUILD.getpass, 'getpass', return_value='wrong'):
                with self.assertRaisesRegex(RuntimeError, '验证失败'):
                    BUILD.import_signing({'keytool': Path('/usr/bin/false')}, incoming)
            self.assertFalse((local / 'credentials').exists())

    def test_temporary_signing_is_cleaned_even_when_build_fails(self):
        with tempfile.TemporaryDirectory() as folder:
            local = Path(folder)
            with mock.patch.object(BUILD, 'LOCAL', local), mock.patch.object(BUILD, 'run') as run:
                with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(RuntimeError, '模拟编译失败'):
                    with BUILD.signing({'keytool': Path('/usr/bin/true')}, True, local) as environment:
                        key = Path(environment['CATAN_ANDROID_KEYSTORE'])
                        self.assertTrue(key.parent.is_dir())
                        self.assertNotIn(environment['CATAN_ANDROID_KEYSTORE_PASS'], run.call_args.args[0])
                        raise RuntimeError('模拟编译失败')
            self.assertFalse(key.parent.exists())


if __name__ == '__main__':
    unittest.main()
