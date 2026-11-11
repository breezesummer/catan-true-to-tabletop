import { readFile, writeFile, mkdir, rename } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { elevenlabs } from './elevenlabs-client.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const recipeDirectory = path.join(root, 'art/recipes/knight-lines-en-v001');
const outputDirectory = path.join(root, 'art/exports/knight-lines-en-v001/原音WAV');
const recordPath = path.join(recipeDirectory, '制作记录.json');
const config = JSON.parse(await readFile(path.join(recipeDirectory, '配音台本.json'), 'utf8'));
const approved = JSON.parse(await readFile(path.join(root, config.sourceAudition), 'utf8'));
const only = process.argv.find(arg => arg.startsWith('--only='))?.slice(7);
const voiceMap = new Map(config.voices.map(voice => [voice.level, voice]));
const sha256 = buffer => createHash('sha256').update(buffer).digest('hex');
if (config.lines.length !== 36 || new Set(config.lines.map(line => line.id)).size !== 36) throw new Error('台本必须包含36条唯一台词。');
for (const voice of config.voices) {
  if (!approved.voices.some(v => v.voiceId === voice.voiceId && v.name === voice.name)) throw new Error('音色与已验收试听不一致。');
  if (config.lines.filter(line => line.level === voice.level).length !== 12) throw new Error('每个等级必须有12句。');
}
for (const line of config.lines) {
  const spoken = line.promptText.replace(/\[[^\]]+\]/g, '').replace(/\s+/g, ' ').trim();
  if (spoken !== line.text) throw new Error(`${line.id}的表演标签改变了已确认的英文台词。`);
}
if (only && !config.lines.some(line => line.id === only)) throw new Error('指定台词不存在。');
const actionOrder = ['招募', '晋升', '激活', '驱赶海盗', '驱赶强盗', '进攻', '移动'];
const lines = config.lines.map((line, index) => ({ ...line, seed: 2026112000 + index }))
  .filter(line => !only || line.id === only)
  .sort((a, b) => a.level - b.level || actionOrder.indexOf(a.action) - actionOrder.indexOf(b.action) || a.variant.localeCompare(b.variant));
let record;
try {
  record = JSON.parse(await readFile(recordPath, 'utf8'));
} catch (error) {
  if (error.code !== 'ENOENT') throw error;
  record = { date: config.date, provider: config.provider, modelId: config.modelId, languageCode: config.languageCode, outputFormat: config.outputFormat, sampleRate: config.sampleRate, channels: 1, sampleWidth: 16, voiceSettings: config.voiceSettings, status: '录制中', clips: [] };
}
for (const field of ['modelId', 'languageCode', 'outputFormat', 'sampleRate', 'voiceSettings']) {
  if (JSON.stringify(record[field]) !== JSON.stringify(config[field])) throw new Error('现有录制设置不同，请另存素材版本。');
}
await mkdir(outputDirectory, { recursive: true });
let persistence = Promise.resolve();
const persist = () => {
  const snapshot = JSON.stringify(record, null, 2) + '\n';
  persistence = persistence.then(async () => {
    await writeFile(`${recordPath}.tmp`, snapshot);
    await rename(`${recordPath}.tmp`, recordPath);
  });
  return persistence;
};
const makeWav = pcm => {
  const header = Buffer.alloc(44);
  header.write('RIFF', 0); header.writeUInt32LE(pcm.length + 36, 4); header.write('WAVEfmt ', 8);
  header.writeUInt32LE(16, 16); header.writeUInt16LE(1, 20); header.writeUInt16LE(1, 22);
  header.writeUInt32LE(config.sampleRate, 24); header.writeUInt32LE(config.sampleRate * 2, 28);
  header.writeUInt16LE(2, 32); header.writeUInt16LE(16, 34); header.write('data', 36); header.writeUInt32LE(pcm.length, 40);
  return Buffer.concat([header, pcm]);
};
let next = 0;
const failures = [];
const worker = async () => {
  while (!failures.length && next < lines.length) {
    const line = lines[next++];
    const voice = voiceMap.get(line.level);
    try {
      const request = { modelId: config.modelId, languageCode: config.languageCode, outputFormat: config.outputFormat, voiceSettings: config.voiceSettings, text: line.promptText, seed: line.seed };
      const requestHash = sha256(JSON.stringify({ voiceId: voice.voiceId, ...request }));
      const previous = record.clips.find(clip => clip.id === line.id);
      if (previous) {
        if (previous.requestHash !== requestHash) throw new Error(`${line.id}的配音参数已改变，请另存素材版本。`);
        const file = await readFile(path.join(root, previous.sourceFile));
        if (sha256(file) !== previous.sourceSha256) throw new Error(`${line.id}的原音文件与记录不一致。`);
        console.log(`复用原音：${line.id}`);
        continue;
      }
      console.log(`正在录制：${line.level}级 ${voice.name} · ${line.action} ${line.variant}`);
      const stream = await elevenlabs.textToSpeech.convert(voice.voiceId, request, { timeoutInSeconds: 120, maxRetries: 0 });
      const pcm = Buffer.from(await new Response(stream).arrayBuffer());
      if (pcm.length < 2400 || pcm.length % 2 || pcm.subarray(0, 4).toString() === 'RIFF') throw new Error('返回音频与24 kHz裸PCM格式不一致。');
      const wav = makeWav(pcm);
      const filename = path.join(outputDirectory, `${line.id}.wav`);
      await writeFile(filename, wav);
      record.clips.push({ ...line, voiceName: voice.name, voiceId: voice.voiceId, requestHash, sourceFile: path.relative(root, filename), sourceSha256: sha256(wav), sourceBytes: wav.length, sourceDurationSecs: pcm.length / 2 / config.sampleRate, generatedAt: new Date().toISOString() });
      record.clips.sort((a, b) => a.id.localeCompare(b.id));
      await persist();
      console.log(`录制完成：${line.id} · ${(pcm.length / 2 / config.sampleRate).toFixed(2)}秒 · ${record.clips.length}/36`);
    } catch (error) {
      failures.push({ id: line.id, statusCode: error.statusCode, detail: error.body?.detail ?? error.message });
    }
  }
};
const results = await Promise.allSettled(Array.from({ length: only ? 1 : 2 }, worker));
for (const result of results) if (result.status === 'rejected') failures.push({ detail: result.reason?.message ?? String(result.reason) });
await persistence;
if (failures.length) {
  console.error(JSON.stringify({ message: '录制中断；已完成原音和记录已保存，后续运行会复用。', failures }));
  process.exitCode = 1;
} else {
  record.status = record.clips.length === 36 ? '36句原音已生成，待内容核对与导出' : '部分原音已生成';
  await persist();
  console.log(record.status);
}
