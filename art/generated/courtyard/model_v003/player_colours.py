"""通过 Blender MCP 生成玩家配色贴图及实模缩略图；几何和原场景材质保持共享。"""
import bpy, json, re
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
REPO=ROOT.parents[3]
ART=REPO/'Unity/Catan/Assets/Art/Courtyard/v003'
RESOURCE=REPO/'Unity/Catan/Assets/Resources/ThreeKingdoms/v003'
S=bpy.context.scene
C=next(c for c in S.collection.children if c.name.startswith('庭院_高细节组件'))
materials={m for o in C.objects for m in o.data.materials}
source=next(m for m in materials if re.match(r'v003_red\.',m.name))
image=next(n.image for n in source.node_tree.nodes if n.type=='TEX_IMAGE' and 'basecolor' in n.image.name)
pixels=np.array(image.pixels[:],dtype=np.float32).reshape(-1,4)
shade=pixels[:,0]/pixels[:,0].mean()
colors={}
generated={}
for key in ['blue','orange','beige']:
    path=next((REPO/'Unity/Catan/Assets/Art/EntityAssets/v002/player-knights/Materials').glob(f'knight-{key}-level-2-bare_*_Moulded_Plastic.mat'))
    match=re.search(r'_BaseColor: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)',path.read_text())
    srgb=tuple(map(float,match.groups()))
    linear=tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in srgb)
    colors[key]={'srgb':list(srgb),'linear':list(linear),'source':str(path.relative_to(REPO))}
    out=bpy.data.images.new('courtyard_'+key, width=image.size[0],height=image.size[1],alpha=True)
    rgba=np.ones_like(pixels)
    value=np.clip(shade[:,None]*np.array(linear)[None,:],0,1)
    # 字节型 sRGB 图像的 pixels 接口接收编码值；避免 Unity 再次解码后偏暗偏红。
    rgba[:,:3]=np.where(value<=.0031308,value*12.92,1.055*np.power(value,1/2.4)-.055)
    generated[key]=out
    out.pixels.foreach_set(rgba.ravel());out.filepath_raw=str(ART/f'player-{key}.png');out.file_format='PNG';out.save()
(ART/'player-colours.json').write_text(json.dumps({'colours':[{'key':k,**v} for k,v in colors.items()]},ensure_ascii=False,indent=2))
# 临时替换材质槽并渲染，完成后恢复编辑器中原有模型。
original={o:list(o.data.materials) for o in C.objects}
old_camera=(S.camera.location.copy(),S.camera.rotation_euler.copy(),S.camera.data.ortho_scale)
old_render=(S.render.resolution_x,S.render.resolution_y,S.render.resolution_percentage,S.render.film_transparent,S.render.filepath,S.cycles.samples)
hidden={o:o.hide_render for o in S.objects if o.type=='MESH' and o not in C.objects.values()}
try:
    for o in hidden:o.hide_render=True
    S.camera.location=(10,-22,20);S.camera.rotation_euler=(Vector((0,0,1.9))-S.camera.location).to_track_quat('-Z','Y').to_euler();S.camera.data.ortho_scale=15.2
    S.render.resolution_x=768;S.render.resolution_y=768;S.render.resolution_percentage=100;S.render.film_transparent=True;S.cycles.samples=24
    for key in ['red','blue','orange','beige']:
        mapping={}
        if key!='red':
            for m in materials:
                if not re.match(r'v003_(red\.|red_dark\.|cloth\.)',m.name):continue
                new=m.copy();new.name='玩家_'+key+'_'+m.name
                p=new.node_tree.nodes.get('Principled BSDF');linear=colors[key]['linear']
                if m==source:
                    for n in new.node_tree.nodes:
                        if n.type=='TEX_IMAGE' and n.image==image:n.image=generated[key]
                else:
                    factor=.48 if 'red_dark' in m.name else 1
                    p.inputs['Base Color'].default_value=(*[v*factor for v in linear],1)
                mapping[m]=new
        for o,mats in original.items():
            for i,m in enumerate(mats):o.data.materials[i]=mapping.get(m,m)
        S.render.filepath=str(RESOURCE/f'thumbnail-{key}.png');bpy.ops.render.render(write_still=True)
finally:
    for o,mats in original.items():
        for i,m in enumerate(mats):o.data.materials[i]=m
    for o,value in hidden.items():o.hide_render=value
    S.camera.location,S.camera.rotation_euler,S.camera.data.ortho_scale=old_camera
    S.render.resolution_x,S.render.resolution_y,S.render.resolution_percentage,S.render.film_transparent,S.render.filepath,S.cycles.samples=old_render
result={'配色':colors,'组件数':len(C.objects),'输出':'四色透明缩略图、三张玩家色贴图；共享原网格和其余PBR材质'}
