/* 本函数在离屏页面中执行，按原始配置取景，攻击动作使用整段动作共同的取景范围。 */
module.exports=async function render({idle,bg,attack,arc,fallback}) {
    const modern=Boolean(idle?.json);
    const sp=modern?window.spine_4:window.spine;
    const web=modern?sp:sp.webgl;
    const cv=document.querySelector('canvas');
    const gl=cv.getContext('webgl',{alpha:true,premultipliedAlpha:true,preserveDrawingBuffer:true,antialias:true});
    if(!gl)throw new Error('离屏 WebGL 不可用');
    const renderer=new web.SceneRenderer(cv,gl,true);
    function canvas(w,h){const c=document.createElement('canvas');c.width=w;c.height=h;return c;}
    async function image(data){const i=new Image();i.src='data:image/png;base64,'+data;await i.decode();return i;}
    async function load(spec){
        if(!spec)return null;
        const textures={};
        for(const [key,data] of Object.entries(spec.images))textures[key]=new web.GLTexture(gl,await image(data));
        const atlas=new sp.TextureAtlas(spec.atlas,name=>textures[name]);
        if(modern)for(const p of atlas.pages)p.setTexture(textures[p.name]);
        const loader=new sp.AtlasAttachmentLoader(atlas);
        const bytes=Uint8Array.from(atob(spec.model),c=>c.charCodeAt(0));
        const data=spec.json?new sp.SkeletonJson(loader).readSkeletonData(JSON.parse(new TextDecoder().decode(bytes))):
            new sp.SkeletonBinary(loader).readSkeletonData(bytes);
        const sk=new sp.Skeleton(data);sk.opacity=1;
        return {spec,sk,data};
    }
    function anim(obj,name){
        const a=obj.data.findAnimation(name)||obj.data.animations[0];
        if(!a)throw new Error('骨骼没有动作');
        return a;
    }
    function paint(obj,a,t,box,w,h){
        const sk=obj.sk;sk.setToSetupPose();
        a.apply(sk,0,t,false,null,1,modern?sp.MixBlend.setup:sp.MixPose.setup,sp.MixDirection.mixIn);
        if(obj.spec.angle && box.portrait)sk.bones[0].rotation+=obj.spec.angle;
        sk.updateWorldTransform();
        cv.width=w;cv.height=h;gl.viewport(0,0,w,h);
        renderer.camera.setViewport(box.w,box.h);renderer.camera.position.x=box.x;renderer.camera.position.y=box.y;
        gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);
        renderer.begin();renderer.drawSkeleton(sk,false);renderer.end();
    }
    function portraitBox(spec){
        const w=131/spec.scale,h=184/spec.scale;
        return {x:(.5-spec.x[1])*w-spec.x[0]/spec.scale,y:(.5-spec.y[1])*h-spec.y[0]/spec.scale,w,h,portrait:true};
    }
    function centered(obj,a){
        const probe=canvas(192,192),ctx=probe.getContext('2d',{willReadFrequently:true});
        const limit=4800,unit=limit/192,centers=[],widths=[];
        for(let i=0;i<=24;i++){
            let left=192,right=0,top=192,bottom=0;
            paint(obj,a,a.duration*i/24,{x:0,y:0,w:limit,h:limit},192,192);
            ctx.clearRect(0,0,192,192);ctx.drawImage(cv,0,0);
            const pixels=ctx.getImageData(0,0,192,192).data;
            for(let y=0;y<192;y++)for(let x=0;x<192;x++)if(pixels[(y*192+x)*4+3]>44){
                left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);
            }
            const valid=right>left&&bottom>top;
            centers.push(valid?{x:((left+right+1)/2-96)*unit,y:(96-(top+bottom+1)/2)*unit}:null);
            if(valid)widths.push(Math.max(right-left+2,(bottom-top+2)*1.2)*unit);
        }
        if(!widths.length)throw new Error('攻击动作采样为空');
        widths.sort((a,b)=>a-b);
        const w=widths[Math.floor((widths.length-1)*.87)]*1.1;
        const first=centers.find(Boolean);
        for(let i=0;i<centers.length;i++)centers[i]||=centers[i-1]||first;
        return fraction=>{
            const index=Math.min(24,fraction*24),lo=Math.floor(index),ratio=index-lo;
            const a=centers[lo],b=centers[Math.min(lo+1,24)];
            return {x:a.x+(b.x-a.x)*ratio,y:a.y+(b.y-a.y)*ratio,w,h:w/1.2};
        };
    }
    const primary=await load(idle), background=await load(bg);
    const actor=attack?await load(attack):primary;
    const hit=anim(actor,attack?.action || (fallback?'play':'GongJi'));
    if(primary && !fallback && !attack && hit.name!=='GongJi')throw new Error('缺少攻击动作');
    const result={idleTime:0,attackTime:hit.duration};
    if(primary){
        const motion=anim(primary,idle.action);
        const w=144,h=192,n=Math.max(48,Math.min(256,Math.ceil(motion.duration*16/16)*16));
        const sheet=canvas(16*w,h*n/16),dest=sheet.getContext('2d');
        const crown=await image(arc);
        const frame=canvas(w,h),ctx=frame.getContext('2d');
        const mask=canvas(w,h),mc=mask.getContext('2d',{willReadFrequently:true});
        mc.drawImage(crown,0,0,w,h*.285);
        const alpha=mc.getImageData(0,0,w,h).data;
        mc.clearRect(0,0,w,h);mc.fillStyle='white';
        for(let x=0;x<w;x++){
            let y=0;while(y<28&&alpha[(y*w+x)*4+3]<60)y++;
            mc.fillRect(x,y+1,1,h-y-1);
        }
        const idleBox=portraitBox(idle),bgBox=bg&&portraitBox(bg);
        const bgMotion=background&&anim(background,'BeiJing');
        for(let i=0;i<n;i++){
            ctx.clearRect(0,0,w,h);
            ctx.fillStyle='#32313c';ctx.fillRect(0,0,w,h);
            if(background){paint(background,bgMotion,bgMotion.duration*i/n,bgBox,w,h);ctx.drawImage(cv,0,0);}
            paint(primary,motion,motion.duration*i/n,idleBox,w,h);ctx.drawImage(cv,0,0);
            ctx.globalCompositeOperation='destination-in';ctx.drawImage(mask,0,0);ctx.globalCompositeOperation='source-over';
            ctx.drawImage(crown,0,0,w,h*.285);dest.drawImage(frame,(i%16)*w,Math.floor(i/16)*h);
        }
        result.idle=sheet.toDataURL().split(',')[1];result.idleTime=motion.duration;result.idleFrames=n;
    }
    const w=384,h=320,n=48,sheet=canvas(w*8,h*6),dest=sheet.getContext('2d');
    const box=centered(actor,hit);
    if(fallback)result.attackTime=Math.min(1.25,hit.duration);
    for(let i=0;i<n;i++){
        paint(actor,hit,result.attackTime*i/n,box(result.attackTime*i/n/hit.duration),w,h);
        dest.drawImage(cv,(i%8)*w,Math.floor(i/8)*h);
    }
    result.attack=sheet.toDataURL().split(',')[1];
    return result;
};
