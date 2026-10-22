// 将保留的 CC0 原件转成统一的单声道 24kHz PCM，原件不变。
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(__dirname,'../..');
const source=path.join(root,'assets/references/audio/adventurer-v001/files/RPG Male Adventurer');
const output=path.join(root,'art/source/audio-v003/voices');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const mappings={zh:['yes0','yes1','attack0','attack1'],en:['greet0','victory0','attackbig0','attackbig1']};
const lines=[];
for(const [language,files] of Object.entries(mappings))for(let index=0;index<files.length;index++){
 const original=fs.readFileSync(path.join(source,files[index]+'.wav'));
 let start=0,size=0;for(let p=12;p+8<=original.length;){const n=original.readUInt32LE(p+4);if(original.toString('ascii',p,p+4)==='data'){start=p+8;size=n;break;}p+=8+n+(n%2);}
 if(!start||original.readUInt16LE(20)!==1||original.readUInt16LE(22)!==2||original.readUInt16LE(34)!==16)throw Error('原件格式不符');
 const rate=original.readUInt32LE(24),count=Math.floor(size/4),length=Math.floor(count*24000/rate),b=Buffer.alloc(44+length*2);
 b.write('RIFF');b.writeUInt32LE(b.length-8,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);b.writeUInt32LE(24000,24);b.writeUInt32LE(48000,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);b.write('data',36);b.writeUInt32LE(length*2,40);
 // 带低通的窗化 sinc 重采样，避免直接抽点引入高频混叠。
 for(let i=0;i<length;i++){
   const at=i*rate/24000,center=Math.floor(at),cutoff=.9*24000/rate;let total=0,weight=0;
   for(let j=-24;j<=24;j++){const k=Math.max(0,Math.min(count-1,center+j)),x=(center+j-at)*cutoff;const w=(Math.abs(x)<1e-9?1:Math.sin(Math.PI*x)/(Math.PI*x))*(.5+.5*Math.cos(Math.PI*(center+j-at)/25));total+=(original.readInt16LE(start+k*4)+original.readInt16LE(start+k*4+2))/2*w;weight+=w;}
   b.writeInt16LE(Math.round(Math.max(-32767,Math.min(32767,total/weight))),44+i*2);
 }
 const file=(index<2?'KnightRecruit':'KnightActivate')+(index%2+1).toString().padStart(2,'0');
 fs.mkdirSync(path.join(output,language),{recursive:true});fs.writeFileSync(path.join(output,language,file+'.wav'),b);
 lines.push({language,file,text:'真人'+(index<2?'应答':'战呼'),source:files[index]+'.wav',sourceSha256:hash(original),sha256:hash(b)});
}
fs.writeFileSync(path.join(output,'manifest.json'),JSON.stringify({author:'Brandon Song (wolfwoot)',license:'CC0-1.0',url:'https://opengameart.org/content/voice-clip-pack-male-adventurer-rpg',note:'zh/en 为旧资源槽位名称，界面已改为应答/战呼风格；这些录音不是中文配音。',lines},null,2));
