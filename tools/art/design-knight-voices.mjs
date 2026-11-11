import { readFile, writeFile, mkdir, copyFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { elevenlabs } from './elevenlabs-client.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const recipeName = process.argv[3] ?? 'knight-voices-v001';
if (!/^knight-voices-v\d{3}$/.test(recipeName)) {
  throw new Error('素材目录格式应为 knight-voices-v 加三位数字。');
}
const recipeDirectory = path.join(root, 'art/recipes', recipeName);
const outputDirectory = path.join(root, 'art/exports', recipeName);
const recordPath = path.join(recipeDirectory, '制作记录.json');
const config = JSON.parse(await readFile(path.join(recipeDirectory, '音色设计.json'), 'utf8'));
const mode = process.argv[2];

if (!['--generate', '--save'].includes(mode)) {
  console.log('用法：node tools/art/design-knight-voices.mjs --generate 或 --save，末尾可附素材目录名。已有生成记录会复用，不重复扣费；保存时不修改其他音色。');
  process.exit(0);
}

if (config.previewText.length < 100 || config.previewText.length > 1000) {
  throw new Error('试听文本应为100至1000字符。');
}

let record;
try {
  record = JSON.parse(await readFile(recordPath, 'utf8'));
} catch (error) {
  if (error.code !== 'ENOENT') throw error;
  record = {
    date: config.date,
    provider: config.provider,
    modelId: config.modelId,
    outputFormat: config.outputFormat,
    generationSettings: { guidanceScale: config.guidanceScale ?? 5, loudness: config.loudness ?? 0.5 },
    previewText: config.previewText,
    source: 'https://elevenlabs.io/docs/api-reference/text-to-voice/design',
    voices: [],
  };
}
if (record.previewText !== config.previewText || record.modelId !== config.modelId) {
  throw new Error('已有记录与设计不一致，请使用新的素材版本目录。');
}
if (record.generationSettings && (
  record.generationSettings.guidanceScale !== (config.guidanceScale ?? 5) ||
  record.generationSettings.loudness !== (config.loudness ?? 0.5)
)) {
  throw new Error('已有记录与生成参数不一致，请使用新的素材版本目录。');
}

await mkdir(path.join(outputDirectory, '候选'), { recursive: true });
const persist = () => writeFile(recordPath, `${JSON.stringify(record, null, 2)}\n`);
const relativePath = value => path.relative(root, value);
const requestOptions = { timeoutInSeconds: 180, maxRetries: 0 };

try {
  for (const design of config.voices) {
    let saved = record.voices.find(voice => voice.level === design.level);
    if (saved && saved.description !== design.description) {
      throw new Error(`${design.name}的描述已改变，请使用新的素材版本目录。`);
    }
    if (mode === '--generate') {
      if (!saved) {
        console.log(`正在生成：${design.name}`);
        const result = await elevenlabs.textToVoice.design({
          modelId: config.modelId,
          outputFormat: config.outputFormat,
          voiceDescription: design.description,
          text: config.previewText,
          autoGenerateText: false,
          seed: design.seed,
          guidanceScale: config.guidanceScale ?? 5,
          loudness: config.loudness ?? 0.5,
          streamPreviews: false,
          shouldEnhance: false,
        }, requestOptions);
        if (!result.previews?.length) throw new Error(`${design.name}未返回试听。`);
        saved = {
          level: design.level,
          name: design.name,
          libraryName: design.libraryName,
          description: design.description,
          seed: design.seed,
          generatedAt: new Date().toISOString(),
          selectedPreview: design.selectedPreview,
          returnedText: result.text,
          previews: [],
        };
        for (const [index, preview] of result.previews.entries()) {
          const filename = path.join(outputDirectory, '候选', `${design.name}-候选${index + 1}.mp3`);
          const audio = Buffer.from(preview.audioBase64, 'base64');
          if (audio.length < 1000) throw new Error(`${design.name}的试听音频为空或过短。`);
          await writeFile(filename, audio);
          saved.previews.push({
            index: index + 1,
            generatedVoiceId: preview.generatedVoiceId,
            mediaType: preview.mediaType,
            durationSecs: preview.durationSecs,
            language: preview.language,
            file: relativePath(filename),
            bytes: audio.length,
            sha256: createHash('sha256').update(audio).digest('hex'),
          });
        }
        record.voices.push(saved);
        await persist();
        console.log(`生成完成：${design.name}，${saved.previews.length}个候选。`);
      }
      const selected = saved.previews.find(preview => preview.index === design.selectedPreview);
      if (!selected) throw new Error(`${design.name}的所选候选不存在。`);
      if (saved.voiceId && saved.selectedPreview !== design.selectedPreview) {
        throw new Error(`${design.name}已保存到音色库，请使用新的素材版本保留原音色映射。`);
      }
      saved.selectedPreview = design.selectedPreview;
      saved.auditionFile = relativePath(path.join(outputDirectory, `${design.name}.mp3`));
      await copyFile(path.join(root, selected.file), path.join(root, saved.auditionFile));
      await persist();
    } else {
      if (!saved) throw new Error(`请先生成${design.name}。`);
      if (saved.voiceId) {
        console.log(`音色已保存：${design.name}`);
        continue;
      }
      const existing = await elevenlabs.voices.search({ search: design.libraryName, pageSize: 100 }, requestOptions);
      let voice = existing.voices.find(item => item.name === design.libraryName);
      if (!voice) {
        const subscription = await elevenlabs.user.subscription.get(requestOptions);
        if (subscription.voiceSlotsUsed >= subscription.voiceLimit) {
          throw new Error('音色库已满；试听文件已保留。');
        }
        console.log(`正在保存音色：${design.name}`);
        const selected = saved.previews.find(preview => preview.index === saved.selectedPreview);
        voice = await elevenlabs.textToVoice.create({
          voiceName: design.libraryName,
          voiceDescription: design.description,
          generatedVoiceId: selected.generatedVoiceId,
          labels: { language: 'zh', gender: 'male', use_case: 'characters_animation' },
        }, requestOptions);
      }
      saved.voiceId = voice.voiceId;
      saved.librarySavedAt = new Date().toISOString();
      await persist();
      console.log(`音色保存完成：${design.name}`);
    }
  }
} catch (error) {
  console.error(JSON.stringify({
    message: '制作中断，已完成的音频与记录均已保留。',
    statusCode: error.statusCode,
    detail: error.body?.detail ?? error.message,
  }));
  process.exitCode = 1;
}
