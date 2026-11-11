import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { elevenlabs } from './elevenlabs-client.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const recipeName = process.argv[2] ?? 'knight-voices-v002';
if (!/^knight-voices-v\d{3}$/.test(recipeName)) throw new Error('素材目录名称格式错误。');
const recipeDirectory = path.join(root, 'art/recipes', recipeName);
const config = JSON.parse(await readFile(path.join(recipeDirectory, '音色设计.json'), 'utf8'));
const voices = JSON.parse(await readFile(path.join(recipeDirectory, '制作记录.json'), 'utf8')).voices;
const recordPath = path.join(recipeDirectory, '短句制作记录.json');
const outputDirectory = path.join(root, 'art/exports', recipeName, '短句原音');
let record;
try {
  record = JSON.parse(await readFile(recordPath, 'utf8'));
} catch (error) {
  if (error.code !== 'ENOENT') throw error;
  record = {
    date: config.date,
    provider: config.provider,
    modelId: config.auditionModelId,
    languageCode: 'zh',
    outputFormat: config.outputFormat,
    voiceSettings: config.auditionVoiceSettings,
    source: 'https://elevenlabs.io/docs/overview/capabilities/text-to-speech/best-practices',
    clips: [],
  };
}
if (record.modelId !== config.auditionModelId || JSON.stringify(record.voiceSettings) !== JSON.stringify(config.auditionVoiceSettings)) {
  throw new Error('已有短句参数不同，请使用新的素材版本。');
}
await mkdir(outputDirectory, { recursive: true });
try {
  for (const voice of voices) {
    if (!voice.voiceId) throw new Error(`请先保存${voice.name}到音色库。`);
    for (const [index, line] of config.auditionLines.entries()) {
      const existing = record.clips.find(clip => clip.level === voice.level && clip.id === line.id);
      if (existing) {
        if (existing.voiceId !== voice.voiceId || existing.text !== line.text) {
          throw new Error(`${voice.name}的短句音色或台词已改变，请使用新素材版本。`);
        }
        const original = await readFile(path.join(root, existing.file));
        if (createHash('sha256').update(original).digest('hex') !== existing.sha256) {
          throw new Error(`${existing.file}与原始记录不一致。`);
        }
        continue;
      }
      console.log(`正在生成短句：${voice.name} · ${line.name}`);
      const seed = voice.seed + 100 + index;
      const stream = await elevenlabs.textToSpeech.convert(voice.voiceId, {
        modelId: config.auditionModelId,
        languageCode: 'zh',
        text: line.text,
        outputFormat: config.outputFormat,
        voiceSettings: config.auditionVoiceSettings,
        seed,
      }, { timeoutInSeconds: 120, maxRetries: 0 });
      const buffer = Buffer.from(await new Response(stream).arrayBuffer());
      if (buffer.length < 1000) throw new Error('返回的短句音频为空或过短。');
      const filename = path.join(outputDirectory, `${voice.name}-${line.id}-${line.name}.mp3`);
      await writeFile(filename, buffer);
      record.clips.push({
        level: voice.level,
        voiceName: voice.name,
        voiceId: voice.voiceId,
        id: line.id,
        name: line.name,
        text: line.text,
        seed,
        file: path.relative(root, filename),
        bytes: buffer.length,
        sha256: createHash('sha256').update(buffer).digest('hex'),
        generatedAt: new Date().toISOString(),
      });
      await writeFile(recordPath, `${JSON.stringify(record, null, 2)}\n`);
    }
  }
} catch (error) {
  console.error(JSON.stringify({ message: '短句制作中断，已完成原音与记录已保留。', statusCode: error.statusCode, detail: error.body?.detail ?? error.message }));
  process.exitCode = 1;
}
