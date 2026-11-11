// 席位框保留固定半径，由 Unity 九宫格拉伸直边。
const sharp=require('sharp');
const path=require('node:path');
const root=path.resolve('Unity/Catan/Assets/Resources/GeneralUI');
async function glow(name,mask,width,height,core,spread){
    const raw={width,height,channels:1};
    const blur=async sigma=>sharp(mask,{raw}).blur(sigma).greyscale().raw().toBuffer();
    const [near,far]=await Promise.all([blur(core),blur(spread)]);
    const pixels=Buffer.alloc(width*height*4);
    for(let i=0;i<mask.length;i++){
        const light=Math.min(1,(near[i]*1.85+far[i]*.85)/255);
        pixels.fill(255,i*4,i*4+3);
        pixels[i*4+3]=Math.round(light*(255-mask[i]));
    }
    await sharp(pixels,{raw:{width,height,channels:4}}).png().toFile(path.join(root,'v002',name+'.png'));
}
async function main(){
    const rect=await sharp(Buffer.from('<svg width="64" height="64"><rect x="8" y="8" width="48" height="48" rx="5" fill="white"/></svg>'))
        .ensureAlpha().extractChannel(3).raw().toBuffer();
    await glow('panel-halo',rect,64,64,1.5,3.1);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
