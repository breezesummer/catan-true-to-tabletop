"""通过 Blender MCP 将作者化程序材质烘焙为可移植 PBR 图；无灯光烘入颜色。"""
import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parent
main=bpy.context.scene
scene=bpy.data.scenes.new('材质烘焙工作台');bpy.context.window.scene=scene;scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.bake.margin=16;scene.render.bake.use_pass_direct=False;scene.render.bake.use_pass_indirect=False;scene.render.bake.use_pass_color=True
bpy.ops.mesh.primitive_plane_add(size=2);plane=bpy.context.object
recipes={
 'stone':((.44,.45,.465),(.55,.56,.57),.57,.73,0,100,.0013),
 'sculpt':((.47,.465,.455),(.66,.65,.635),.32,.47,0,180,.00065),
 'wood':((.23,.035,.009),(.43,.095,.025),.27,.40,0,120,.0006),
 'gold':((.55,.235,.045),(.79,.42,.11),.20,.32,.88,170,.00038),
 'red':((.49,.003,.007),(.64,.007,.012),.22,.31,0,180,.00018),
 'tile':((.024,.029,.037),(.045,.051,.062),.27,.39,0,120,.0006),
 'plaster':((.79,.68,.53),(.88,.78,.65),.75,.88,0,110,.0010),
}
report={}
for name,(lo,hi,rlo,rhi,metal,freq,depth) in recipes.items():
 mat=bpy.data.materials.new('烘焙源_'+name);mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;p=nodes.get('Principled BSDF');p.inputs['Metallic'].default_value=metal
 tex=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=(6,.16,1) if name=='wood' else (1,1,1);links.new(tex.outputs['UV'],scale.inputs[0])
 noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=6 if name=='sculpt' else 9;noise.inputs['Detail'].default_value=4;noise.inputs['Roughness'].default_value=.65;links.new(scale.outputs[0],noise.inputs['Vector'])
 ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.18;ramp.color_ramp.elements[0].color=(*lo,1);ramp.color_ramp.elements[1].position=.82;ramp.color_ramp.elements[1].color=(*hi,1);links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],p.inputs['Base Color'])
 if name=='wood':
  image=nodes.new('ShaderNodeTexImage');image.image=bpy.data.images.load(str(ROOT/'textures'/'wood_basecolor.png'),check_existing=True);links.new(tex.outputs['UV'],image.inputs['Vector'])
  mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.28;links.new(image.outputs['Color'],mix.inputs[1]);links.new(ramp.outputs['Color'],mix.inputs[2]);links.new(mix.outputs[0],p.inputs['Base Color'])
 rough=nodes.new('ShaderNodeMapRange');rough.inputs['To Min'].default_value=rlo;rough.inputs['To Max'].default_value=rhi;links.new(noise.outputs['Fac'],rough.inputs['Value']);links.new(rough.outputs[0],p.inputs['Roughness'])
 fine=nodes.new('ShaderNodeTexNoise');fine.inputs['Scale'].default_value=freq;fine.inputs['Detail'].default_value=2;links.new(scale.outputs[0],fine.inputs['Vector'])
 bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.35;bump.inputs['Distance'].default_value=depth;links.new(fine.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs[0],p.inputs['Normal'])
 plane.data.materials.clear();plane.data.materials.append(mat);target=nodes.new('ShaderNodeTexImage');nodes.active=target
 for suffix,typ in [('basecolor','EMIT'),('roughness','ROUGHNESS'),('normal','NORMAL')]:
  img=bpy.data.images.new('v003_'+name+'_'+suffix,width=1024,height=1024,alpha=False);img.colorspace_settings.name='sRGB' if suffix=='basecolor' else 'Non-Color';target.image=img
  output=nodes.get('Material Output')
  if typ=='EMIT':
   emission=nodes.new('ShaderNodeEmission');links.new(p.inputs['Base Color'].links[0].from_socket,emission.inputs['Color']);links.new(emission.outputs[0],output.inputs['Surface'])
  bpy.ops.object.bake(type=typ)
  if typ=='EMIT':links.new(p.outputs[0],output.inputs['Surface']);nodes.remove(emission)
  img.filepath_raw=str(ROOT/'textures'/f'{name}_{suffix}.png');img.file_format='PNG';img.save();img.pack()
 report[name]={'分辨率':1024,'颜色范围':[lo,hi],'粗糙度范围':[rlo,rhi],'金属度':metal,'微表面距离':depth}
bpy.context.window.scene=main
for image in bpy.data.images:
 if image.source=='FILE' and image.filepath and Path(bpy.path.abspath(image.filepath)).parent==ROOT/'textures':
  try:image.reload()
  except RuntimeError:pass
C=next(c for c in main.collection.children if c.name.startswith('庭院_高细节组件'))
materials=set(m for o in C.objects for m in o.data.materials)
for mat in materials:
 key=mat.name.split('v003_')[-1].split('.')[0]
 family='stone' if key in ['stone','stone1','stone2'] else 'gold' if key.startswith('gold') else 'wood' if key in ['wood','wood1','wood_lacquer'] else 'tile' if key.startswith('tile') else key
 if family not in recipes:continue
 nodes=mat.node_tree.nodes;links=mat.node_tree.links;p=nodes.get('Principled BSDF')
 for name in ['Base Color','Roughness','Normal']:
  for link in list(p.inputs[name].links):links.remove(link)
 uv=nodes.new('ShaderNodeUVMap');uv.uv_map='UVMap'
 for suffix,input_name in [('basecolor','Base Color'),('roughness','Roughness'),('normal','Normal')]:
  t=nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(ROOT/'textures'/f'{family}_{suffix}.png'),check_existing=True);t.image.colorspace_settings.name='sRGB' if suffix=='basecolor' else 'Non-Color';links.new(uv.outputs[0],t.inputs['Vector']);t.image.pack()
  out=t.outputs['Color']
  if suffix=='normal':
   n=nodes.new('ShaderNodeNormalMap');n.uv_map='UVMap';links.new(out,n.inputs['Color']);out=n.outputs[0]
  elif suffix=='basecolor' and key in ['stone1','stone2','tile1','tile2','gold_dark']:
   v={'stone1':1.07,'stone2':.91,'tile1':1.08,'tile2':.91,'gold_dark':.50}[key];mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(v,v,v,1);links.new(out,mix.inputs[1]);out=mix.outputs[0]
  links.new(out,p.inputs[input_name])
 p.inputs['Metallic'].default_value=recipes[family][4]
 if family in ['wood','red']:p.inputs['Coat Weight'].default_value=.25;p.inputs['Coat Roughness'].default_value=.22
(ROOT/'material_bake_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
result={'烘焙材质':len(recipes),'图像数量':len(recipes)*3,'场景':main.name}
