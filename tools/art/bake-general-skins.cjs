/* 使用本机无名杀运行时离屏采样。仅导出贴图，客户端无需浏览器或 Spine 运行时。 */
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { chromium } = require('playwright');
const sharp = require('sharp');
const render = require('./render-general-skin.cjs');

const extension = process.argv[2];
const output = path.resolve(process.env.CATAN_SKIN_OUTPUT || 'Unity/Catan/Assets/Resources/GeneralUI/Dynamic');
const decade = path.join(extension, '十周年UI');
const assets = path.join(decade, 'assets/dynamic');
const switcher = path.join(extension, '皮肤切换');
const read = file => fs.readFileSync(file, 'utf8');
const encoded = file => fs.readFileSync(file).toString('base64');
const idleOnly = process.argv.includes('--idle-only');
const camps={魏:'wei',蜀:'shu',吴:'wu',群:'qun',神:'shen',晋:'jin'};
const configText = read(path.join(decade,'src/skins/dynamicSkin.js'));
const config = vm.runInNewContext('('+configText.match(/export const dynamicSkinConfig = (\{[\s\S]*?\n\});/)[1]+')');
const skins = Object.values(config).flatMap(Object.values);
const catalog = [...read('src/Catan.Core/ThreeKingdoms/GeneralCatalog.cs').matchAll(/new GeneralDefinition\("([^"]+)","([^"]+)","([^"]+)","([^"]+)"/g)];

function skeleton(stem, action, options={}) {
    const json = fs.existsSync(stem+'.json');
    const atlas = read(stem+'.atlas');
    const images = {};
    for (const name of atlas.split(/\r?\n/).map(line=>line.trim()).filter(line=>/\.(png|webp|jpg)$/i.test(line))) {
        images[name] = encoded(path.join(path.dirname(stem),name));
    }
    if (!Object.keys(images).length) throw new Error('缺少图集贴图 '+stem);
    return {atlas, images, model:encoded(stem+(json?'.json':'.skel')), json, action,
        scale:options.scale||1, angle:options.angle||0, x:options.x||[0,.5], y:options.y||[0,.5]};
}

async function saveSheet(file, data) {
    const staging=file+'.tmp';
    try {
        await sharp(Buffer.from(data,'base64')).png({compressionLevel:4}).toFile(staging);
        fs.renameSync(staging,file);
    } finally {
        fs.rmSync(staging,{force:true});
    }
}

async function main(){
    fs.mkdirSync(output,{recursive:true});
    const manifestPath=path.join(output,'manifest.json');
    function importSounds(entries){
        for(const entry of entries){
            const [,id,name,title]=catalog.find(row=>row[1]===entry.id);
            const folder=(id==='tk-g08'?'神甘宁':name)+'/'+(id==='tk-g31'?'鸩杀少帝':title);
            const skin=skins.find(s=>s?.name?.startsWith(folder+'/'));
            const source=[skin.gongji?.name,skin.name].filter(Boolean).map(stem=>path.join(assets,stem+'.mp3')).find(fs.existsSync);
            entry.sound=(source?id:'shared')+'/attack-sound';
            if(source){
                fs.mkdirSync(path.join(output,id),{recursive:true});
                fs.copyFileSync(source,path.join(output,entry.sound+'.mp3'));
            }
        }
        fs.mkdirSync(path.join(output,'shared'),{recursive:true});
        fs.copyFileSync(path.join(extension,'../audio/skill/qinggang_skill.mp3'),path.join(output,'shared/attack-sound.mp3'));
    }
    if(process.argv.includes('--audio-only')){
        const manifest=JSON.parse(read(manifestPath));importSounds(manifest.entries);
        fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2)+'\n');return;
    }
    const browser = await chromium.launch({executablePath:process.env.CATAN_BAKE_BROWSER,
        headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
    const entries=[];
    async function bake(id, args) {
        const page=await browser.newPage();
        try {
            await page.setContent('<canvas></canvas>');
            await page.addScriptTag({path:path.join(switcher, args.idle?.json?'spine-lib/spine_4_0_64.js':'spine.js')});
            const data=await page.evaluate(render,args);
            const dir=path.join(output,id);
            fs.mkdirSync(dir,{recursive:true});
            if(data.idle) await saveSheet(path.join(dir,'idle.png'),data.idle);
            if(data.attack) await saveSheet(path.join(dir,'attack.png'),data.attack);
            console.log(id,data.idleTime,data.attackTime);
            return {id,idleTime:data.idleTime,idleFrames:data.idleFrames,attackTime:data.attackTime};
        } finally {await page.close();}
    }
    try {
        for(const [,id,name,title,camp] of catalog){
            const folder=(id==='tk-g08'?'神甘宁':name)+'/'+(id==='tk-g31'?'鸩杀少帝':title);
            const skin=skins.find(s=>s?.name?.startsWith(folder+'/'));
            if(!skin || !fs.existsSync(path.join(assets,folder))) continue;
            const idle=skeleton(path.join(assets,skin.name),skin.action||'DaiJi',skin);
            const bg=skin.beijing && skeleton(path.join(assets,skin.beijing.name),null,skin.beijing);
            const configured=skin.gongji;
            const attack=configured?.name?skeleton(path.join(assets,configured.name),configured.action||'play',configured):null;
            const fallback=id==='tk-g03';
            entries.push(await bake(id,{idle,bg,attack,idleOnly,fallback,camp:camps[camp],
                idleFrames:id==='tk-g01'?64:undefined,attackFrames:id==='tk-g01'?66:48}));
        }
        if(entries.length!==33) throw new Error('动皮数量错误 '+entries.length);
        if(idleOnly)return;
        importSounds(entries);
        fs.writeFileSync(path.join(output,'manifest.json'),JSON.stringify({entries},null,2)+'\n');
    } finally {await browser.close();}
}
main().catch(error=>{console.error(error);process.exitCode=1;});
