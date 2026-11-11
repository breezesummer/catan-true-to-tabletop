// 逐帧重采样原光带。沿中心到牌沿的射线平移纹理坐标，保留原光带的宽度和运动。
const sharp=require('sharp');
const path=require('node:path');
const root=path.resolve('Unity/Catan/Assets/Resources/GeneralUI');
const art=path.join(root,'v002');
const width=256,height=352,pad=40,fw=width+2*pad,fh=height+2*pad;
const directions=2048,sx=67.5/(width/2),sy=111/(height/2);

function sample(data,stride,x,y,channel){
    const ix=Math.floor(x),iy=Math.floor(y),fx=x-ix,fy=y-iy;
    const i=(iy*stride+ix)*4+channel;
    return (data[i]*(1-fx)+data[i+4]*fx)*(1-fy)+
        (data[i+stride*4]*(1-fx)+data[i+stride*4+4]*fx)*fy;
}

function offsets(mask,source){
    const shifts=[];
    for(let n=0;n<directions;n++){
        const angle=2*Math.PI*n/directions,dx=Math.cos(angle),dy=Math.sin(angle);
        let lo=0,hi=240;
        for(let i=0;i<13;i++){
            const r=(lo+hi)/2,x=Math.floor(width/2+dx*r),y=Math.floor(height/2+dy*r);
            if(x>=0&&x<width&&y>=0&&y<height&&mask[y*width+x]>127)lo=r;
            else hi=r;
        }
        // 原单帧左右亮脊为 52、187，底部为 251。两半仍以 y=140 镜像闭合。
        const outer=Math.min(128/Math.abs(dx),176/Math.abs(dy));
        let ridge=outer,best=-1;
        for(let r=outer-20;r<=outer+4;r+=.5){
            const x=119.5+r*dx*sx,y=140+r*Math.abs(dy)*sy;
            let light=0;
            for(let f=0;f<10;f++)
                light+=sample(source,2400,x+240*f,y,0)*sample(source,2400,x+240*f,y,3);
            if(light>best){best=light;ridge=r;}
        }
        shifts.push(ridge-lo-3);
    }
    return shifts;
}

async function fit(name,mask,source,colors){
    const shifts=offsets(mask,source);
    const pixels=Buffer.alloc(fw*5*fh*2*4);
    for(const [color,sheet] of colors){
        pixels.fill(0);
        for(let y=0;y<fh;y++)for(let x=0;x<fw;x++){
            const dx=x+.5-pad-width/2,dy=y+.5-pad-height/2,r=Math.hypot(dx,dy);
            if(r<1)continue;
            const angle=(Math.atan2(dy,dx)/(Math.PI*2)+1)%1*directions;
            const index=Math.floor(angle),frac=angle-index;
            const shift=shifts[index]*(1-frac)+shifts[(index+1)%directions]*frac;
            const scale=(r+shift)/r;
            const u=119.5+dx*sx*scale,v=140+Math.abs(dy)*sy*scale;
            if(u<0||u>=239||v<140||v>=269)continue;
            const inPanel=name==='seat'&&x>=pad&&x<pad+width&&y>=pad&&y<pad+height;
            const outside=inPanel?1-mask[(y-pad)*width+x-pad]/255:1;
            if(!outside)continue;
            for(let f=0;f<10;f++){
                const i=4*((Math.floor(f/5)*fh+y)*fw*5+(f%5)*fw+x);
                for(let c=0;c<4;c++)pixels[i+c]=Math.round(sample(sheet,2400,u+240*f,v,c));
                pixels[i+3]=Math.round(pixels[i+3]*outside);
            }
        }
        await sharp(pixels,{raw:{width:fw*5,height:fh*2,channels:4}})
            .png().toFile(path.join(art,`portrait-glow-${name}-${color}.png`));
    }
}
async function main(){
    const colors=new Map();
    for(const [color,file] of [['blue','turn-glow-blue'],['green','turn-glow'],['yellow','turn-glow-yellow']])
        colors.set(color,await sharp(path.join(art,file+'.png')).ensureAlpha().raw().toBuffer());
    const seat=await sharp(Buffer.from('<svg width="256" height="352"><rect width="256" height="352" rx="12" ry="9" fill="white"/></svg>'))
        .ensureAlpha().extractChannel(3).raw().toBuffer();
    await fit('seat',seat,colors.get('yellow'),new Map([['yellow',colors.get('yellow')]]));
    if(process.argv.includes('--seat-only'))return;
    const dynamic=await sharp(path.join(root,'Dynamic/tk-g01/idle.png'))
        .extract({left:0,top:0,width,height}).extractChannel('alpha').raw().toBuffer();
    const fixed=await sharp(path.join(art,'frame-wei.png')).resize(width,height,{fit:'fill'})
        .extractChannel('alpha').raw().toBuffer();
    for(const [name,mask] of [['dynamic',dynamic],['static',fixed]])
        await fit(name,mask,colors.get('blue'),colors);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
