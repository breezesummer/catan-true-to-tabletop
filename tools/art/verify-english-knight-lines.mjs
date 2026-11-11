import { createReadStream } from 'node:fs';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { elevenlabs } from './elevenlabs-client.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const recipeDirectory = path.join(root, 'art/recipes/knight-lines-en-v001');
const cacheDirectory = path.join(root, '.local/tmp/knight-lines-en-v001');
const record = JSON.parse(await readFile(path.join(recipeDirectory, '制作记录.json'), 'utf8'));
await mkdir(cacheDirectory, { recursive: true });
const norm = text => text.toLowerCase().replace(/[’‘]/g, "'")
  .replace(/\bi'll\b/g, 'i will').replace(/\bi've\b/g, 'i have').replace(/\bi'm\b/g, 'i am')
  .replace(/\byou're\b/g, 'you are').replace(/\bthat's\b/g, 'that is').replace(/\blet's\b/g, 'let us')
  .replace(/\bship's\b/g, 'ship is').replace(/\bwhere'd\b/g, 'where do').replace(/\barmour\b/g, 'armor')
  .match(/[a-z]+/g) ?? [];
const transcriptResults = await Promise.allSettled(record.montages.map(async montage => {
  const audioPath = path.join(root, montage.file);
  const hash = createHash('sha256').update(await readFile(audioPath)).digest('hex');
  if (hash !== montage.sha256) throw new Error('分级试听与制作记录不一致。');
  const cachePath = path.join(cacheDirectory, `转写-${montage.level}.json`);
  let cached;
  try {
    cached = JSON.parse(await readFile(cachePath, 'utf8'));
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
  }
  if (!cached || cached.audioSha256 !== hash) {
    console.log(`正在转写核对：${montage.level}级 ${montage.voiceName}`);
    const result = await elevenlabs.speechToText.convert({ modelId: 'scribe_v2', file: createReadStream(audioPath), diarize: false, tagAudioEvents: true, numSpeakers: 1, timestampsGranularity: 'word' }, { timeoutInSeconds: 120, maxRetries: 0 });
    cached = { level: montage.level, voiceName: montage.voiceName, audioFile: montage.file, audioSha256: hash, transcriptionModel: 'scribe_v2', result };
    await writeFile(cachePath, JSON.stringify(cached, null, 2) + '\n');
  }
  return { montage, cached };
}));
const report = { date: '2026-11-11', method: '独立英语自动转写，按分级试听时间段逐句对照；统一常用缩写与英美拼写。', status: '待核对', clips: [] };
const transcripts = [];
for (const response of transcriptResults) {
  if (response.status === 'rejected') {
    console.error(JSON.stringify({ statusCode: response.reason.statusCode, detail: response.reason.body?.detail ?? response.reason.message }));
    process.exitCode = 1;
    continue;
  }
  const { montage, cached } = response.value;
  const result = cached.result;
  transcripts.push(cached);
  for (const segment of montage.segments) {
    const within = word => (word.start + word.end) / 2 >= segment.startSecs - 0.08 && (word.start + word.end) / 2 <= segment.endSecs + 0.08;
    const words = (result.words ?? []).filter(word => word.type === 'word' && within(word));
    const events = (result.words ?? []).filter(word => word.type === 'audio_event' && within(word));
    const actual = words.map(word => word.text).join(' ');
    const expectedTokens = norm(segment.text);
    const actualTokens = norm(actual);
    if (expectedTokens[0] === 'aye' && ['i', 'ay'].includes(actualTokens[0])) actualTokens[0] = 'aye';
    if (expectedTokens[0] === 'ha' && actualTokens[0] !== 'ha' && events.some(event => /laugh|chuckle/i.test(event.text))) expectedTokens.shift();
    const matches = expectedTokens.join('') === actualTokens.join('');
    const source = record.clips.find(clip => clip.id === segment.id);
    const passed = matches && ['eng', 'en'].includes(result.languageCode);
    const item = { id: segment.id, level: montage.level, voiceName: montage.voiceName, text: segment.text, translation: segment.translation, transcript: actual, normalizedExpected: expectedTokens.join(' '), normalizedActual: actualTokens.join(' '), recognizedLanguage: result.languageCode, languageProbability: result.languageProbability, audioEvents: events, file: source.file, sha256: source.sha256, status: passed ? '通过' : '待复核' };
    report.clips.push(item);
    if (!passed) console.log(JSON.stringify({ id: segment.id, expected: segment.text, actual, events: events.map(event => event.text) }));
  }
}
report.status = report.clips.length === 36 && report.clips.every(clip => clip.status === '通过') ? '36句自动转写核对通过' : '部分台词待复核';
await writeFile(path.join(recipeDirectory, '内容核对.json'), JSON.stringify(report, null, 2) + '\n');
await writeFile(path.join(recipeDirectory, '转写记录.json'), JSON.stringify(transcripts, null, 2) + '\n');
console.log(JSON.stringify({ total: report.clips.length, passed: report.clips.filter(clip => clip.status === '通过').length, needsReview: report.clips.filter(clip => clip.status !== '通过').map(clip => clip.id) }));
