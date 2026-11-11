import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { elevenlabs } from './elevenlabs-client.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const recipeDirectory = path.join(root, 'art/recipes/knight-voices-en-v001');
const outputDirectory = path.join(root, 'art/exports/knight-voices-en-v001');
const config = JSON.parse(await readFile(path.join(recipeDirectory, '试听台本.json'), 'utf8'));
const candidates = JSON.parse(await readFile(path.join(recipeDirectory, '候选清单.json'), 'utf8'));
const recordPath = path.join(recipeDirectory, '制作记录.json');
const options = { timeoutInSeconds: 120, maxRetries: 0 };
let record;
try {
  record = JSON.parse(await readFile(recordPath, 'utf8'));
} catch (error) {
  if (error.code !== 'ENOENT') throw error;
  record = {
    date: config.date,
    provider: config.provider,
    modelId: config.modelId,
    languageCode: config.languageCode,
    outputFormat: config.outputFormat,
    voiceSettings: config.voiceSettings,
    seed: config.seed,
    text: config.text,
    sceneOrder: config.sceneOrder,
    source: 'https://elevenlabs.io/docs/overview/capabilities/text-to-speech/best-practices',
    status: '英语音色待玩家试听',
    voices: [],
  };
}
for (const field of ['modelId', 'languageCode', 'outputFormat', 'seed', 'text', 'voiceSettings']) {
  if (JSON.stringify(record[field]) !== JSON.stringify(config[field])) throw new Error('现有试听与配置不同，请另存素材版本。');
}
await mkdir(outputDirectory, { recursive: true });
const persist = () => writeFile(recordPath, `${JSON.stringify(record, null, 2)}\n`);
try {
  const available = await elevenlabs.voices.getAll({}, options);
  for (const plan of config.voices) {
    const candidate = candidates.selected.find(v => v.voiceId === plan.sourceVoiceId);
    if (!candidate) throw new Error(`未找到${plan.name}的来源记录。`);
    let saved = record.voices.find(v => v.sourceVoiceId === plan.sourceVoiceId);
    if (!saved) {
      const existing = available.voices.find(v => v.voiceId === plan.sourceVoiceId || v.name === plan.libraryName);
      const voice = existing ?? { voiceId: candidate.voiceId };
      saved = {
        rank: plan.rank,
        name: plan.name,
        direction: plan.direction,
        libraryName: plan.libraryName,
        sourceVoiceId: plan.sourceVoiceId,
        voiceId: voice.voiceId,
        sourceCategory: candidate.category,
        access: existing ? '现有音色' : '公开音色ID直接合成',
        sourceAccent: candidate.accent,
        sourceDescription: candidate.description,
        selectionReason: candidate.selectionReason,
        sourceUrl: `https://elevenlabs.io/app/voice-library?voiceId=${candidate.voiceId}`,
      };
      record.voices.push(saved);
      await persist();
    }
    if (saved.file) {
      const original = await readFile(path.join(root, saved.file));
      if (createHash('sha256').update(original).digest('hex') !== saved.sha256) throw new Error(`${plan.name}的文件与记录不一致。`);
      console.log(`复用已完成试听：${plan.name}`);
      continue;
    }
    console.log(`正在生成英语试听：${plan.name}`);
    const stream = await elevenlabs.textToSpeech.convert(saved.voiceId, {
      modelId: config.modelId,
      languageCode: config.languageCode,
      outputFormat: config.outputFormat,
      text: config.text,
      voiceSettings: config.voiceSettings,
      seed: config.seed,
    }, options);
    const audio = Buffer.from(await new Response(stream).arrayBuffer());
    if (audio.length < 1000) throw new Error('返回音频为空或过短。');
    const filename = path.join(outputDirectory, plan.filename);
    await writeFile(filename, audio);
    saved.file = path.relative(root, filename);
    saved.bytes = audio.length;
    saved.sha256 = createHash('sha256').update(audio).digest('hex');
    saved.generatedAt = new Date().toISOString();
    await persist();
    console.log(`英语试听已生成：${plan.name}`);
  }
} catch (error) {
  console.error(JSON.stringify({ message: '制作中断，已完成的音色与样片记录已保留。', statusCode: error.statusCode, detail: error.body?.detail ?? error.message }));
  process.exitCode = 1;
}
