import argparse
import array
import hashlib
import json
import math
from pathlib import Path
import wave
import zipfile

ROOT = Path(__file__).resolve().parents[2]
RECIPE = ROOT / 'art/recipes/knight-lines-en-v001'
OUTPUT = ROOT / 'art/exports/knight-lines-en-v001'
ORDER = {'招募': 0, '晋升': 0, '激活': 1, '驱赶海盗': 2, '驱赶强盗': 3, '进攻': 4, '移动': 5}


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8'))


def save_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_wav(path):
    with wave.open(str(path), 'rb') as source:
        if (source.getframerate(), source.getnchannels(), source.getsampwidth()) != (24000, 1, 2):
            raise ValueError(f'音频格式不符：{path.name}')
        return array.array('h', source.readframes(source.getnframes()))


def write_wav(path, samples):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(24000)
        output.writeframes(samples.tobytes())


def db(value):
    return 20 * math.log10(max(value, 1e-12))


def prepare():
    config = read_json(RECIPE / '配音台本.json')
    record_path = RECIPE / '制作记录.json'
    record = read_json(record_path)
    if len(record['clips']) != 36:
        raise ValueError('请先完成36句原音。')
    planned = {line['id']: line for line in config['lines']}
    voices = {voice['level']: voice for voice in config['voices']}
    processed = []
    for clip in record['clips']:
        plan = planned[clip['id']]
        if clip['text'] != plan['text'] or clip['voiceId'] != voices[clip['level']]['voiceId']:
            raise ValueError(f'台词或音色不一致：{clip["id"]}')
        source = ROOT / clip['sourceFile']
        if digest(source) != clip['sourceSha256']:
            raise ValueError(f'原音校验失败：{clip["id"]}')
        samples = load_wav(source)
        source_clipped = sum(abs(value) >= 32760 for value in samples)
        # 保留短呼吸和笑声，只修整首尾多余静音。
        window = 240
        active = []
        for start in range(0, len(samples), window):
            block = samples[start:start + window]
            if block and math.sqrt(sum(value * value for value in block) / len(block)) > 50:
                active.append(start)
        if not active:
            raise ValueError(f'音频为空：{clip["id"]}')
        first = max(0, active[0] - 2880)
        last = min(len(samples), active[-1] + window + 4800)
        trimmed = samples[first:last]
        rms = math.sqrt(sum(value * value for value in trimmed) / len(trimmed)) / 32768
        peak = max(abs(value) for value in trimmed) / 32768
        gain = min(10 ** (-21 / 20) / rms, 10 ** (-1.5 / 20) / peak)
        rendered = array.array('h', (round(value * gain) for value in trimmed))
        fade = min(120, len(rendered) // 2)
        for index in range(fade):
            factor = index / max(1, fade - 1)
            rendered[index] = round(rendered[index] * factor)
            rendered[-index - 1] = round(rendered[-index - 1] * factor)
        output = OUTPUT / '成品WAV' / f'{clip["id"]}.wav'
        write_wav(output, rendered)
        final_rms = math.sqrt(sum(value * value for value in rendered) / len(rendered)) / 32768
        clipped = sum(abs(value) >= 32760 for value in rendered)
        if clipped or not 0.2 < len(rendered) / 24000 < 12:
            raise ValueError(f'成品长度或峰值异常：{clip["id"]}')
        clip.update({
            'file': str(output.relative_to(ROOT)),
            'sha256': digest(output),
            'durationSecs': round(len(rendered) / 24000, 6),
            'trimRangeSecs': [round(first / 24000, 6), round(last / 24000, 6)],
            'gainDb': round(db(gain), 3),
            'sourceClippedSamples': source_clipped,
            'clippedSamples': clipped,
            'peakDbfs': round(db(max(abs(value) for value in rendered) / 32768), 3),
            'rmsDbfs': round(db(final_rms), 3),
        })
        processed.append(clip)

    montages = []
    for level in [1, 2, 3]:
        voice = voices[level]
        clips = sorted([clip for clip in processed if clip['level'] == level], key=lambda clip: (ORDER[clip['action']], clip['variant']))
        combined = array.array('h')
        segments = []
        for index, clip in enumerate(clips):
            samples = load_wav(ROOT / clip['file'])
            start = len(combined) / 24000
            combined.extend(samples)
            segments.append({'id': clip['id'], 'action': clip['action'], 'variant': clip['variant'], 'text': clip['text'], 'translation': clip['translation'], 'startSecs': round(start, 6), 'endSecs': round(len(combined) / 24000, 6), 'file': clip['file']})
            if index < len(clips) - 1:
                gap = 0.5 if clips[index + 1]['action'] == clip['action'] else 0.9
                combined.extend(array.array('h', [0]) * round(24000 * gap))
        filename = OUTPUT / '分级试听' / f'{level}级骑士-{voice["name"].replace(" ", "-")}-12句试听.wav'
        write_wav(filename, combined)
        montages.append({'level': level, 'voiceName': voice['name'], 'file': str(filename.relative_to(ROOT)), 'sha256': digest(filename), 'durationSecs': round(len(combined) / 24000, 6), 'segments': segments})

    record['clips'] = sorted(processed, key=lambda clip: (clip['level'], ORDER[clip['action']], clip['variant']))
    record['processing'] = {'targetRmsDbfs': -21, 'maxPeakDbfs': -1.5, 'headMarginSecs': 0.12, 'tailMarginSecs': 0.2, 'edgeFadeSecs': 0.005, 'note': '单句使用固定增益，保留句内动态。'}
    record['montages'] = montages
    record['status'] = '36句成品与分级试听已导出，待转写核对'
    save_json(record_path, record)
    save_json(OUTPUT / '试听索引.json', {'date': config['date'], 'montages': montages})
    save_json(OUTPUT / '音色与台本.json', config)
    text = ['# 骑士英语配音', '', '一级：John North；二级：George；三级：Lord Griff。', '', '分级试听顺序：建造／晋升、激活、驱赶海盗、驱赶强盗、进攻、移动。每个动作依次播放 A、B。', '', '成品规格：24 kHz、16位、单声道 WAV。', '']
    for level in [1, 2, 3]:
        text.extend([f'## {level}级骑士 · {voices[level]["name"]}', '', '| 动作 | 版本 | 文件 | 英文台词 | 中文翻译 | 表演说明 |', '|---|---|---|---|---|---|'])
        for clip in record['clips']:
            if clip['level'] == level:
                filename = Path(clip['file']).name
                text.append(f'| {clip["action"]} | {clip["variant"]} | [试听](成品WAV/{filename}) | {clip["text"]} | {clip["translation"]} | {clip["direction"]} |')
        text.append('')
    (OUTPUT / '配音清单.md').write_text('\n'.join(text) + '\n', encoding='utf-8')
    print(json.dumps({'成品数量': len(processed), '原音截幅样本数': sum(clip['sourceClippedSamples'] for clip in processed), '成品截幅样本数': sum(clip['clippedSamples'] for clip in processed), '分级试听秒数': [item['durationSecs'] for item in montages]}, ensure_ascii=False))


def package():
    record = read_json(RECIPE / '制作记录.json')
    report = read_json(RECIPE / '内容核对.json')
    reviewed = {clip['id']: clip for clip in report['clips']}
    if len(record['clips']) != 36 or len(reviewed) != 36:
        raise ValueError('成品或逐句内容核对不完整。')
    for clip in record['clips']:
        if reviewed[clip['id']]['status'] != '通过' or reviewed[clip['id']]['sha256'] != clip['sha256']:
            raise ValueError(f'内容尚待核对：{clip["id"]}')
        if digest(ROOT / clip['file']) != clip['sha256']:
            raise ValueError(f'成品校验失败：{clip["id"]}')
    for montage in record['montages']:
        if digest(ROOT / montage['file']) != montage['sha256']:
            raise ValueError('分级试听文件校验失败。')
    record['status'] = '36句配音已制作并完成文件与自动转写核对，待玩家试听表演'
    save_json(RECIPE / '制作记录.json', record)
    save_json(OUTPUT / '制作与核对记录.json', {'production': record, 'contentVerification': report})
    bundle = OUTPUT / '骑士英语配音-36句.zip'
    files = [ROOT / clip['file'] for clip in record['clips']]
    files += [ROOT / montage['file'] for montage in record['montages']]
    files += [OUTPUT / name for name in ['配音清单.md', '音色与台本.json', '试听索引.json', '制作与核对记录.json']]
    with zipfile.ZipFile(bundle, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
        for file in files:
            archive.write(file, str(Path('骑士英语配音36句') / file.relative_to(OUTPUT)))
    with zipfile.ZipFile(bundle) as archive:
        if archive.testzip() is not None or len(archive.namelist()) != 43:
            raise ValueError('配音包校验失败。')
    print(json.dumps({'配音包': str(bundle), '文件数': len(files), '大小字节': bundle.stat().st_size, 'sha256': digest(bundle)}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='整理骑士英语配音成品与试听包。')
    parser.add_argument('--package', action='store_true', help='内容核对完成后打包。')
    args = parser.parse_args()
    package() if args.package else prepare()
