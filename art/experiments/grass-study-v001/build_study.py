"""草地局部试验：按阶段生成真实网格，所有长度由厘米工作尺度换算为米。"""
import bpy, math, random, sys, json
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent
S = .01
STAGE = sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'terrain'
def p(v): return tuple(x*S for x in v)
def height(x,y):
    # 两条不对称且连续相接的坡脊，中部留浅谷，避免独立圆球式鼓包。
    r1 = y - (.68 + .19*x - .10*x*x)
    r2 = y - (-.72 + .24*x + .06*x*x)
    return .10 + .45*math.exp(-(r1/.49)**2)*(.88+.12*math.cos(x*1.1)) + .36*math.exp(-(r2/.52)**2)*(.9+.1*math.sin(x*1.3))
def mat(name,color,rough=.72):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1)
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=rough
    return m
def mesh(name,vs,fs,material):
    me=bpy.data.meshes.new(name);me.from_pydata([p(v) for v in vs],[],fs);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob);me.materials.append(material)
    for f in me.polygons:f.use_smooth=True
    return ob
def camera(name,loc,target,scale):
    ca=bpy.data.cameras.new(name);ob=bpy.data.objects.new(name,ca);bpy.context.collection.objects.link(ob)
    ca.type='ORTHO';ca.ortho_scale=scale*S;ca.clip_start=.0001;ca.clip_end=10
    ob.location=p(loc);ob.rotation_euler=(Vector(p(target))-ob.location).to_track_quat('-Z','Y').to_euler();return ob
def area(name,loc,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size*S;d.color=color
    ob=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(ob);ob.location=p(loc)
    ob.rotation_euler=(Vector(p((0,0,.25)))-ob.location).to_track_quat('-Z','Y').to_euler()
def render(name,cam,res=1200,samples=48):
    sc=bpy.context.scene;sc.camera=cam;sc.render.resolution_x=res;sc.render.resolution_y=round(res*.78)
    sc.cycles.samples=samples;sc.render.filepath=str(OUT/'renders'/f'{name}.png');bpy.ops.render.render(write_still=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene;sc.unit_settings.system='METRIC';sc.render.engine='CYCLES';sc.cycles.use_denoising=True
sc.cycles.samples=48;sc.render.resolution_percentage=100
sc.world=bpy.data.worlds.new('柔和环境');sc.world.use_nodes=True
sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.70,.80,1,1)
sc.world.node_tree.nodes['Background'].inputs[1].default_value=.22
sc.view_settings.view_transform='AgX';sc.view_settings.look='AgX - Medium High Contrast'
try:
    pref=bpy.context.preferences.addons['cycles'].preferences;pref.compute_device_type='HIP';pref.get_devices()
    for d in pref.devices:d.use=d.type=='HIP'
    if any(d.type=='HIP' for d in pref.devices):sc.cycles.device='GPU'
except Exception:pass
(OUT/'renders').mkdir(exist_ok=True)
ground=mat('灰模',(.36,.39,.36))
vs=[];fs=[];nx=180;ny=150
for j in range(ny+1):
    y=-1.5+3*j/ny
    for i in range(nx+1):
        x=-1.8+3.6*i/nx;vs.append((x,y,height(x,y)))
for j in range(ny):
    for i in range(nx):
        a=j*(nx+1)+i;fs.append((a,a+1,a+nx+2,a+nx+1))
terrain=mesh('两缓坡与浅谷',vs,fs,ground)
terrain['说明']='40毫米级地块局部试验；36×30毫米制作边界，非最终底座；坡形为单张连续曲面。'
paper=mat('暖灰背景',(.30,.27,.22),.9)
mesh('摄影背景',[(-200,-200,-.05),(200,-200,-.05),(200,200,-.05),(-200,200,-.05)],[(0,1,2,3)],paper)
area('左后柔暖主光',(-3,3,4.5),.12,1.8,(1,.93,.79))
area('右侧大面积补光',(4,-2,5),.018,5,(.80,.9,1))
area('坡脊柔光',(-1,4,6),.035,3,(1,.96,.82))
main=camera('固定主相机',(0,-6.5,6.6),(0,0,.22),4.8)
side=camera('低角度侧相机',(4,-6,1.8),(0,0,.25),4.9)
top=camera('顶视相机',(0,0,8),(0,0,0),4.8)
sc.camera=main
for screen in bpy.data.screens:
    for ar in screen.areas:
        if ar.type=='VIEW_3D':
            ar.spaces.active.region_3d.view_perspective='CAMERA';ar.spaces.active.clip_start=.0001
bpy.ops.object.select_all(action='DESELECT');terrain.select_set(True);bpy.context.view_layer.objects.active=terrain
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'B-terrain.blend'))
render('B-terrain-main',main)
render('B-terrain-side',side,1000)
print('地形阶段完成')
