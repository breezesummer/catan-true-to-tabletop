// 从六张高清母稿烘焙透明海面和岸端散料。运行时沿用木材港原有的单次绘制。
const sharp = require('sharp');
const path = require('node:path');
const originals = 'art/concepts/木材港高清';
const output = 'Unity/Catan/Assets/Resources/PaintedMap/Coast/Timber';
const frames = [['北',0],['东北',32],['东南',150],['南',180],['西南',212],['西北',330]];

function smooth(low, high, value) {
    const t = Math.min(1, Math.max(0, (value-low)/(high-low)));
    return t*t*(3-2*t);
}

async function bake(name, angle, scatter) {
    const {data, info:{width:w,height:h}} = await sharp(path.join(originals,name+'.png')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    const sin = Math.sin(angle*Math.PI/180), cos = Math.cos(angle*Math.PI/180);
    const water = Buffer.alloc(w*h);
    let min=Infinity,max=-Infinity;
    for(let y=0;y<h;y++) for(let x=0;x<w;x++) {
        const i=y*w+x, p=4*i;
        const r=data[p],g=data[p+1],b=data[p+2],a=data[p+3];
        const sea=(x-w/2)*sin+(h/2-y)*cos;
        const along=(x-w/2)*cos-(h/2-y)*sin;
        if(a>100){min=Math.min(min,along);max=Math.max(max,along);}
        // 瓦片偏蓝，水面偏绿。岸线以内保留屋瓦、青衣和草木。
        if(sea > -w*.085 && g>r+9 && b>r+4 && g>b*.95 && a>12) water[i]=255;
    }
    const blur=async (mask,radius)=>sharp(mask,{raw:{width:w,height:h,channels:1}})
        .blur(radius).greyscale().raw().toBuffer();
    const nearWater=await blur(water,4);
    const solid=Buffer.alloc(w*h);
    for(let i=0;i<solid.length;i++) {
        const p=4*i;
        if(data[p+3]>150 && !water[i] && data[p]>data[p+2]+15) solid[i]=255;
    }
    const nearSolid=await blur(solid,8);
    const span=max-min;
    const {data:chips,info:{width:sw,height:sh}}=scatter;
    for(let y=0;y<h;y++) for(let x=0;x<w;x++) {
        const i=y*w+x, p=i*4;
        const r=data[p],g=data[p+1],b=data[p+2];
        const sea=((x-w/2)*sin+(h/2-y)*cos)/span;
        const along=(((x-w/2)*cos-(h/2-y)*sin)-min)/span;
        let alpha=data[p+3]/255;
        // 浅色浪尖只在靠近桩、船或木排处留下细小的半透明水花。
        if(water[i] || (sea>-.08 && nearWater[i]>36 && g>=r-4 && b>=r-6)) {
            alpha*=.30*smooth(6,65,nearSolid[i])*smooth(110,225,Math.min(r,g,b));
        }
        const end=Math.min(along,1-along);
        const grain=.009*Math.sin(x*.117+y*.073)+.012*Math.sin(x*.027-y*.032);
        alpha*=smooth(-.006,.10,end+grain);
        // 两端用画稿不同半幅，投影到岸边而非水面。保留散料之间的真实空隙。
        const u=(end-.005)/.16;
        const v=(sea+.093)/.077;
        if(u>=0 && u<1 && v>=0 && v<1) {
            const tx=Math.floor((along<.5 ? u*.52 : 1-u*.47)* (sw-1));
            const ty=Math.floor(v*(sh-1));
            const s=(ty*sw+tx)*4;
            const pigment=smooth(4,31,Math.max(chips[s],chips[s+1])-chips[s+2]);
            const coverage=chips[s+3]/255*pigment*.86*smooth(0,.16,u)*(1-smooth(.79,1,u));
            const a=alpha+coverage*(1-alpha);
            if(a>0) for(let c=0;c<3;c++) data[p+c]=(data[p+c]*alpha+chips[s+c]*coverage*(1-alpha))/a;
            alpha=a;
        }
        data[p+3]=Math.round(alpha*255);
    }
    await sharp(data,{raw:{width:w,height:h,channels:4}}).png().toFile(path.join(output,name+'.png'));
}

async function main() {
    const scatter=await sharp('art/concepts/木材港岸端散料.png').ensureAlpha().raw().toBuffer({resolveWithObject:true});
    for(const [name,angle] of frames) await bake(name,angle,scatter);
}
main().catch(error=>{console.error(error);process.exitCode=1;});
