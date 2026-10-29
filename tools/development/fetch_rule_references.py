"""按锁恢复官方规则原文；不覆盖现存文件，不更新来源锁。"""
import argparse
import hashlib
import json
import os
import uuid
from pathlib import Path
from urllib.parse import urlparse
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[2]
MANIFESTS = ('assets/references/rules/v001/sources.json',
             'assets/references/rules/traders-barbarians-v001/sources.json')


def verify(path, source):
    data = path.read_bytes()
    if len(data) != source['byteLength'] or hashlib.sha256(data).hexdigest() != source['sha256']:
        raise ValueError(f"来源指纹不匹配，已保留文件：{path}")


def restore(root, manifest, verify_only=False):
    cache = (root / manifest['cacheRoot']).resolve()
    if root.resolve() not in cache.parents:
        raise ValueError('缓存目录必须位于项目内')
    for source in manifest['sources']:
        target = (root / source['cachePath']).resolve()
        if cache not in target.parents:
            raise ValueError('来源路径越出锁定缓存目录')
        digest = source['sha256']
        if len(digest) != 64 or any(c not in '0123456789abcdef' for c in digest) or source['byteLength'] <= 0:
            raise ValueError('无效的来源指纹')
        uri = urlparse(source['url'])
        if uri.scheme != 'https' or uri.netloc != 'www.catan.com':
            raise ValueError('来源必须是官方HTTPS地址')
        if target.exists():
            verify(target, source)
        else:
            if verify_only:
                raise FileNotFoundError(f'缺少锁定原文：{target}')
            target.parent.mkdir(parents=True, exist_ok=True)
            download = target.with_name(target.name + '.download-' + uuid.uuid4().hex)
            with urlopen(source['url'], timeout=90) as response, download.open('xb') as output:
                while chunk := response.read(1024 * 1024):
                    output.write(chunk)
            verify(download, source)
            # 硬链接是排他创建，另一个进程已写入目标时不覆盖。
            os.link(download, target)
            download.unlink()
        print(f"已核验：{source['id']}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify-only', action='store_true')
    parser.add_argument('--set', choices=('all', 'official', 'traders'), default='all')
    args = parser.parse_args()
    selected = MANIFESTS if args.set == 'all' else (MANIFESTS[0 if args.set == 'official' else 1],)
    for path in selected:
        restore(ROOT, json.loads((ROOT / path).read_text(encoding='utf-8-sig')), args.verify_only)


if __name__ == '__main__':
    main()
