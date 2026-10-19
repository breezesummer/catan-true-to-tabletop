#!/usr/bin/env python3
"""macOS 构建驱动；使用系统 Python 标准库，不依赖 PowerShell。"""
import argparse
import contextlib
import datetime
import fcntl
import getpass
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shlex
import shutil
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'Unity/Catan'
LOCAL = ROOT / '.local/development'
SERVICE = 'com.catan.development.android-signing'
ACCOUNT = hashlib.sha256(str(ROOT).encode()).hexdigest()[:20]


def run(args, log=None, env=None):
    """命令不经 shell；日志不记录环境变量和签名密码。"""
    if log:
        with log.open('w') as output:
            result = subprocess.run([str(x) for x in args], cwd=ROOT, env=env,
                                    stdout=output, stderr=subprocess.STDOUT)
    else:
        result = subprocess.run([str(x) for x in args], cwd=ROOT, env=env)
    if result.returncode:
        raise RuntimeError('命令执行失败' + (f'，请查看 {log}' if log else '。'))


def version():
    value = re.search(r'版本号：内测版(\d+\.\d+\.\d+)', (ROOT / 'AGENTS.md').read_text()).group(1)
    source = (PROJECT / 'Assets/Scripts/M6App.cs').read_text()
    if not re.search(r'BuildVersion\s*=\s*"' + re.escape(value) + '"', source):
        raise RuntimeError('AGENTS.md 与客户端版本号不一致。')
    return value


def tools():
    development = Path(os.environ['CATAN_DEVELOPMENT_ROOT'])
    unity = Path(os.environ['UNITY_EDITOR'])
    android = Path(os.environ['CATAN_ANDROID_PLAYER'])
    return {'dotnet': development / 'dotnet/dotnet', 'unity': unity,
            'windows': unity.parents[1] / 'PlaybackEngines/WindowsStandaloneSupport',
            'android': android, 'java': android / 'OpenJDK/bin/java',
            'keytool': android / 'OpenJDK/bin/keytool',
            'apksigner': android / 'SDK/build-tools/36.0.0/apksigner'}


def require(paths):
    for label, path in paths.items():
        if not path.exists():
            raise RuntimeError(f'缺少 {label}：{path}')


def integrity():
    extensions = {'.meta', '.unity', '.prefab', '.mat', '.asset', '.cs', '.json'}
    count = 0
    for folder in ('Assets', 'ProjectSettings', 'Packages'):
        for path in (PROJECT / folder).rglob('*'):
            if path.is_file() and path.suffix in extensions:
                content = path.read_bytes()
                if not content or b'\0' in content:
                    raise RuntimeError(f'Unity 源文件为空或含 NUL 字节：{path}')
                count += 1
    print(f'Unity 源文件完整性检查通过：{count} 个文件。', flush=True)


def same_copy(source, target):
    if not target.exists() or source.read_bytes() != target.read_bytes():
        shutil.copy2(source, target)


def assemblies(tool, logs):
    print('正在编译规则、AI 和网络协议程序集。', flush=True)
    # 显式覆盖本机旧环境的自定义 MSBuild 输出，保持与 Windows 相同的输出位置。
    for name in ('Catan.AI', 'Catan.Network.Contracts'):
        run([tool['dotnet'], 'build', ROOT / f'src/{name}/{name}.csproj', '-c', 'Release',
             '--disable-build-servers', '-m:1', '-p:UseArtifactsOutput=false',
             f'-p:DirectoryBuildPropsPath={ROOT / "Directory.Build.props"}'], logs / f'{name}.log')
    for name in ('Catan.Core', 'Catan.AI', 'Catan.Network.Contracts'):
        same_copy(ROOT / f'src/{name}/bin/Release/netstandard2.1/{name}.dll',
                  PROJECT / f'Assets/Plugins/{name}.dll')
    same_copy(ROOT / 'docs/acceptance/v0.1/scenario.json', PROJECT / 'Assets/Resources/m1-scenario.json')


def keychain_password():
    result = subprocess.run(['/usr/bin/security', 'find-generic-password', '-a', ACCOUNT,
                             '-s', SERVICE, '-w'], capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError('钥匙串中没有 Android 签名密码。请导入原签名，或设置 CATAN_ANDROID_KEYSTORE_PASS。')
    return result.stdout.rstrip('\n')


def import_signing(tool, source):
    source = source.expanduser().resolve()
    require({'签名密钥': source, 'keytool': tool['keytool']})
    destination = LOCAL / 'credentials/android/catan-internal.keystore'
    if destination.exists() and destination.read_bytes() != source.read_bytes():
        raise RuntimeError('本机已存在不同的 Android 签名密钥，停止导入以免替换原签名。')
    password = getpass.getpass('请输入原 keystore 密码（不会显示）：')
    environment = dict(os.environ, CATAN_ANDROID_KEYSTORE_PASS=password)
    checked = subprocess.run([str(tool['keytool']), '-list', '-keystore', str(source),
                              '-alias', 'catan-internal', '-storepass:env', 'CATAN_ANDROID_KEYSTORE_PASS'],
                             env=environment, capture_output=True)
    if checked.returncode:
        raise RuntimeError('密钥密码或 catan-internal 别名验证失败，未保存签名。')
    # security 交互模式从 stdin 接收密码，避免在命令行参数中暴露。
    command = 'add-generic-password -U -a {} -s {} -w {}\n'.format(
        shlex.quote(ACCOUNT), shlex.quote(SERVICE), shlex.quote(password))
    stored = subprocess.run(['/usr/bin/security', '-i'], input=command, text=True, capture_output=True)
    if stored.returncode or 'Error:' in stored.stderr or 'SecKeychain' in stored.stderr:
        raise RuntimeError('Android 密码写入 macOS 钥匙串失败，未导入密钥文件。')
    destination.parent.mkdir(parents=True, exist_ok=True)
    if source != destination:
        shutil.copyfile(source, destination)
    destination.chmod(0o600)
    print('原 Android 签名已导入，密码保存在本机钥匙串。')


@contextlib.contextmanager
def signing(tool, test, logs):
    if test:
        credential_root = LOCAL / 'credentials/android'
        credential_root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='signing-test-', dir=credential_root) as temporary:
            keystore = Path(temporary) / 'test.keystore'
            environment = dict(os.environ, CATAN_ANDROID_KEYSTORE=str(keystore),
                               CATAN_ANDROID_KEYSTORE_PASS=secrets.token_urlsafe(32))
            run([tool['keytool'], '-genkeypair', '-keystore', keystore, '-storetype', 'JKS',
                 '-alias', 'catan-internal', '-storepass:env', 'CATAN_ANDROID_KEYSTORE_PASS',
                 '-keypass:env', 'CATAN_ANDROID_KEYSTORE_PASS', '-keyalg', 'RSA', '-keysize', '2048',
                 '-validity', '30', '-dname', 'CN=Catan Build Verification, O=CatanDevelopment, C=CN',
                 '-noprompt'], logs / 'android-test-key.log', environment)
            print('使用独立临时测试签名；此 APK 不能覆盖安装此前内测版。', flush=True)
            yield environment
    else:
        keystore = Path(os.environ.get('CATAN_ANDROID_KEYSTORE',
                        str(LOCAL / 'credentials/android/catan-internal.keystore'))).expanduser().resolve()
        if not keystore.is_file():
            raise RuntimeError('缺少原 Android 签名。请使用 --import-android-keystore 导入；仅验证打包可使用 --test-signing。')
        password = os.environ.get('CATAN_ANDROID_KEYSTORE_PASS') or keychain_password()
        yield dict(os.environ, CATAN_ANDROID_KEYSTORE=str(keystore), CATAN_ANDROID_KEYSTORE_PASS=password)


def unity_build(tool, output, target, arm64, environment):
    logs = output / 'logs'
    android = target == 'android'
    log = logs / ('unity-android-build.log' if android else 'unity-build.log')
    project, executable, build_output = PROJECT, tool['unity'], output
    if android:
        # Unity 会还原工程目录的短链接，故工程使用真实英文临时目录。
        # 只同步构建所需源文件，完成后把 APK 回收到 game version。
        stage = android_workspace(logs)
        project = stage / 'Unity/Catan'
        executable = ascii_link(tool['unity'].parents[2], 'editor') / 'Contents/MacOS/Unity'
        build_output = stage / 'output'
        player = executable.parents[1] / 'PlaybackEngines/AndroidPlayer'
        environment = dict(environment, JAVA_HOME=str(player / 'OpenJDK'),
                           ANDROID_HOME=str(player / 'SDK'), ANDROID_SDK_ROOT=str(player / 'SDK'),
                           ANDROID_NDK_ROOT=str(player / 'NDK'))
        keystore = Path(environment['CATAN_ANDROID_KEYSTORE'])
        environment['CATAN_ANDROID_KEYSTORE'] = str(ascii_link(keystore.parent, 'signing') / keystore.name)
    args = [executable, '-batchmode', '-nographics', '-quit', '-buildTarget',
            'Android' if android else 'Win64', '-projectPath', project, '-executeMethod',
            'M6Build.BuildAndroid' if android else 'M6Build.Build', '--m6-output-root', build_output,
            '-logFile', log]
    if android and arm64:
        args.append('--m6-android-arm64-only')
    print(f'正在构建 {"Android" if android else "Windows"}，详细日志：{log}', flush=True)
    try:
        run(args, logs / f'unity-{target}-console.log', environment)
    except RuntimeError:
        raise RuntimeError(f'Unity {target} 构建失败，请查看 {log}') from None
    marker = 'CATAN_M6_ANDROID_BUILD_OK' if android else 'CATAN_M6_UNITY_BUILD_OK'
    if not log.exists() or marker not in log.read_text(errors='replace'):
        raise RuntimeError(f'未找到 Unity 构建成功标记，请查看 {log}')
    if android:
        shutil.copytree(build_output / 'Builds/Android', output / 'Builds/Android', dirs_exist_ok=True)
        shutil.rmtree(build_output)
    artifact = output / 'Builds' / ('Android' if android else 'Windows') / ('Catan' + version() + ('.apk' if android else '.exe'))
    if not artifact.is_file():
        raise RuntimeError(f'未找到构建产物：{artifact}')
    print(f'{target} 构建完成：{artifact}', flush=True)
    return artifact


def android_workspace(logs, stage=None):
    stage = stage or Path('/tmp') / f'catan-{os.getuid()}-{ACCOUNT}-build'
    marker = stage / '.catan-build-root'
    if stage.is_symlink() or (stage.exists() and (not marker.is_file() or marker.read_text() != str(ROOT))):
        raise RuntimeError(f'英文构建目录被其他文件占用：{stage}')
    stage.mkdir(parents=True, exist_ok=True)
    marker.write_text(str(ROOT))
    # 缓存可复用，旧产物不可混入下一版本。
    staged_output = stage / 'output'
    if staged_output.exists():
        shutil.rmtree(staged_output)
    project = stage / 'Unity/Catan'
    project.mkdir(parents=True, exist_ok=True)
    for folder in ('Assets', 'Packages', 'ProjectSettings'):
        run(['/usr/bin/rsync', '-a', '--delete', str(PROJECT / folder) + '/',
             str(project / folder) + '/'], logs / f'android-sync-{folder}.log')
    library = project / 'Library'
    if not library.exists() and (PROJECT / 'Library').exists():
        # APFS 写时复制复用已导入缓存；失败时从空缓存导入即可。
        cloned = subprocess.run(['/bin/cp', '-cR', str((PROJECT / 'Library').resolve()), str(library)],
                                capture_output=True)
        if cloned.returncode:
            print('缓存克隆未完成，Unity 将重新导入所需资产。', flush=True)
    print(f'Android 英文构建工作区：{stage}', flush=True)
    return stage


def ascii_link(source, label):
    link = Path('/tmp') / f'catan-{os.getuid()}-{ACCOUNT}-{label}'
    if link.is_symlink() and not link.exists():
        link.unlink()
    if not link.exists():
        link.symlink_to(source, target_is_directory=True)
    if not link.is_symlink() or link.resolve() != source.resolve():
        raise RuntimeError(f'英文构建路径被其他文件占用：{link}')
    return link


def verify_apk(tool, apk, environment, test, logs):
    log = logs / 'android-apk-signature.txt'
    run([tool['apksigner'], 'verify', '--verbose', '--print-certs', apk], log, environment)
    digest = re.search(r'Signer #1 certificate SHA-256 digest:\s*([a-fA-F0-9]+)', log.read_text())
    if not digest:
        raise RuntimeError('未找到 APK 签名摘要。')
    info_path = apk.parent / 'build-info.json'
    info = json.loads(info_path.read_text())
    info.update(signingCertificateSha256=digest.group(1).lower(),
                apkSha256=hashlib.sha256(apk.read_bytes()).hexdigest(),
                signingPurpose='build-verification-only' if test else 'internal-playtest')
    info_path.write_text(json.dumps(info, ensure_ascii=False, indent=2) + '\n')


def server(tool, output, runtime):
    print(f'正在构建 {runtime} 自包含房间服务。', flush=True)
    destination = output / ('Builds/Windows/Server' if runtime == 'win-x64' else f'Server/{runtime}')
    destination.mkdir(parents=True, exist_ok=True)
    run([tool['dotnet'], 'publish', ROOT / 'src/Catan.Server/Catan.Server.csproj', '-c', 'Release',
         '-r', runtime, '--self-contained', 'true', '-p:PublishSingleFile=false',
         '-p:UseArtifactsOutput=false', f'-p:DirectoryBuildPropsPath={ROOT / "Directory.Build.props"}',
         '-o', destination, '--disable-build-servers', '-m:1'], output / 'logs/server-publish.log')
    shutil.copy2(ROOT / 'docs/M7_SERVER.md', destination / '服务启动说明.md')
    print(f'自包含房间服务已生成：{destination}', flush=True)


def retain():
    root = ROOT / 'game version'
    parents = [root] + [p for p in root.iterdir() if p.is_dir() and not p.is_symlink()
                       and re.fullmatch(r'\d+\.\d+', p.name)]
    candidates = []
    for parent in parents:
        for path in parent.iterdir():
            match = re.match(r'^Catan(\d+)\.(\d+)\.(\d+)(?:-.+)?$', path.name)
            if (match and path.is_dir() and not path.is_symlink() and
                    not (path / 'release').exists() and not (path / '正式发布.txt').exists()):
                candidates.append((tuple(map(int, match.groups())), path.stat().st_mtime, path))
    for _, _, path in sorted(candidates, reverse=True)[5:]:
        shutil.rmtree(path)
        print(f'已清理超出最近五版的内测目录：{path.name}')


def main():
    parser = argparse.ArgumentParser(description='macOS 构建 Windows 客户端、随包服务和 Android APK。',
                                     add_help=False, usage='%(prog)s [选项]')
    parser.add_argument('-h', '--help', action='help', help='显示参数说明并退出')
    parser._optionals.title = '可选参数'
    parser.add_argument('--target', choices=('all', 'windows', 'android', 'server'), default='all', help='构建目标，默认全部')
    parser.add_argument('--output-root', type=Path, help='game version 内的输出目录')
    parser.add_argument('--arm64-only', action='store_true', help='Android 仅构建 ARM64')
    parser.add_argument('--runtime', choices=('win-x64', 'linux-x64', 'linux-arm64'), default='win-x64', help='服务端目标运行时')
    parser.add_argument('--check', action='store_true', help='只检查路径、工具和源文件')
    parser.add_argument('--test-signing', action='store_true', help='用独立临时签名验证打包，不用于覆盖安装旧版')
    parser.add_argument('--import-android-keystore', type=Path, help='导入原签名密钥，交互输入密码后写入钥匙串')
    args = parser.parse_args()
    if sys.platform != 'darwin':
        raise RuntimeError('此驱动用于 macOS；Windows 请使用 Build-Game.ps1。')
    tool = tools()
    require({name: tool[name] for name in ('dotnet', 'unity')})
    value = version()
    if args.import_android_keystore:
        import_signing(tool, args.import_android_keystore)
        return
    if args.target in ('all', 'windows'):
        require({'Windows 构建模块': tool['windows']})
    if args.target in ('all', 'android'):
        require({name: tool[name] for name in ('android', 'java', 'keytool', 'apksigner')})
        require({'Android NDK': tool['android'] / 'NDK/source.properties',
                 'Android SDK API36': tool['android'] / 'SDK/platforms/android-36/android.jar'})
    integrity()
    if args.check:
        print(json.dumps({'游戏版本': value, '构建宿主': 'macOS', '工具路径': {k: str(v) for k, v in tool.items()},
                          '原内测签名文件存在': (LOCAL / 'credentials/android/catan-internal.keystore').exists()}, ensure_ascii=False, indent=2))
        return
    suffix = '-CrossPlatformBuild' + ('-SigningTest' if args.test_signing else '')
    requested = args.output_root or Path(f'game version/{".".join(value.split(".")[:2])}/Catan{value}{suffix}')
    output = (requested if requested.is_absolute() else ROOT / requested).resolve()
    if ROOT / 'game version' not in output.parents:
        raise RuntimeError('构建产物必须保存在本仓库 game version 的版本目录内。')
    if not re.fullmatch(r'Catan' + re.escape(value) + r'-.+', output.name):
        raise RuntimeError('输出目录名必须为 Catan当前版本号-英文更新说明。')
    if args.test_signing and 'SigningTest' not in output.name:
        raise RuntimeError('测试签名输出目录名必须包含 SigningTest，避免覆盖正式签名产物。')
    LOCAL.mkdir(parents=True, exist_ok=True)
    with (LOCAL / 'build.lock').open('w') as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise RuntimeError('已有本仓库构建运行，请等待完成。')
        logs = output / 'logs'
        logs.mkdir(parents=True, exist_ok=True)
        # 全量构建先检查签名，避免到 Android 阶段才发现缺少原密钥。
        manager = signing(tool, args.test_signing, logs) if args.target in ('all', 'android') else contextlib.nullcontext(dict(os.environ))
        with manager as environment:
            if args.target != 'server':
                assemblies(tool, logs)
            if args.target in ('all', 'windows'):
                unity_build(tool, output, 'windows', False, environment)
                server(tool, output, 'win-x64')
            if args.target == 'server':
                server(tool, output, args.runtime)
            if args.target in ('all', 'android'):
                apk = unity_build(tool, output, 'android', args.arm64_only, environment)
                verify_apk(tool, apk, environment, args.test_signing, logs)
        (output / '构建说明.json').write_text(json.dumps({'游戏版本': value, '构建宿主': 'macOS',
            '目标': args.target, '测试签名': args.test_signing,
            '完成时间': datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat(),
            '运行验收': '本次仅验证构建与签名；Windows 运行和 Android 真机另行验证。'}, ensure_ascii=False, indent=2) + '\n')
        retain()


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, OSError, KeyError) as error:
        print(f'构建停止：{error}', file=sys.stderr)
        sys.exit(1)
