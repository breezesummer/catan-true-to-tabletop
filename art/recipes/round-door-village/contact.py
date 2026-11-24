"""在整屋独立 UV 上烘焙环境遮蔽、局部烟熏／返潮和铜扣抛光。"""
import math

import bpy

from roofing import CHIMNEY_X, CHIMNEY_Y


UV = "整屋接触"
CORNER_ID = "source_corner"


def index_copy(mesh, start):
    attr = mesh.attributes.new(CORNER_ID,"INT","CORNER")
    attr.data.foreach_set("value",range(start,start+len(mesh.loops)))


def unwrap(joined, originals):
    mesh = joined.data
    mesh.uv_layers.new(name=UV)
    mesh.uv_layers.active_index = 1
    bpy.ops.object.mode_set(mode="EDIT")
    try:
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(63),area_weight=.30,
                                 island_margin=.0015,margin_method="FRACTION",
                                 correct_aspect=False)
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")
    # 展开单一组合网格，按角点编号写回源文件的十四个分件，所有分件共用一张图。
    corner_ids = mesh.attributes[CORNER_ID]
    packed = [None]*len(mesh.loops)
    for i, datum in enumerate(mesh.uv_layers[UV].data):
        packed[corner_ids.data[i].value] = tuple(datum.uv)
    assert all(uv is not None for uv in packed)
    offset = 0
    for obj in originals:
        layer = obj.data.uv_layers.new(name=UV)
        for i,loop in enumerate(layer.data):
            loop.uv = packed[offset+i]
        offset += len(obj.data.loops)
        obj.data.uv_layers.active_index = 0
        obj.data.uv_layers[0].active_render = True
    assert offset==len(packed)
    mesh.attributes.remove(corner_ids)
    mesh.uv_layers.active_index = 0
    mesh.uv_layers[0].active_render = True


def bake(scene, joined, origin, scale, path):
    image = bpy.data.images.new(path.name,1024,1024,alpha=False,is_data=True)
    image.generated_color = (0,1,0,1)
    image.colorspace_settings.name = "Non-Color"
    mat = bpy.data.materials.new("整屋接触烘焙")
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    def node(kind):
        return tree.nodes.new(kind)
    def attach(value, target):
        if isinstance(value,(float,int)):
            target.default_value = value
        else:
            tree.links.new(value,target)
    def calc(op,a,b=0):
        n = node("ShaderNodeMath")
        n.operation = op
        attach(a,n.inputs[0])
        attach(b,n.inputs[1])
        return n.outputs[0]
    def ramp(value,start,end):
        n = node("ShaderNodeMapRange")
        n.interpolation_type = "SMOOTHSTEP"
        attach(value,n.inputs["Value"])
        n.inputs["From Min"].default_value,n.inputs["From Max"].default_value = start,end
        return n.outputs["Result"]
    pos = node("ShaderNodeVectorMath")
    pos.operation = "MULTIPLY_ADD"
    tree.links.new(node("ShaderNodeNewGeometry").outputs["Position"],pos.inputs[0])
    pos.inputs[1].default_value = (1/scale,)*3
    pos.inputs[2].default_value = origin
    coords = node("ShaderNodeSeparateXYZ")
    tree.links.new(pos.outputs[0],coords.inputs[0])
    x,y,z = coords.outputs
    noise = node("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 17
    noise.inputs["Detail"].default_value = 2.5
    tree.links.new(pos.outputs[0],noise.inputs["Vector"])
    grain = calc("SUBTRACT",noise.outputs["Fac"],.5)
    def radius(dx,dy):
        return calc("MAXIMUM",calc("ABSOLUTE",calc("SUBTRACT",x,dx)),calc("ABSOLUTE",calc("SUBTRACT",y,dy)))
    # 烟口内缘沉积最深。压顶迎风侧保留本色，背风侧有一条散开的烟迹。
    inner = ramp(radius(CHIMNEY_X,CHIMNEY_Y),.228,.122)
    drift = ramp(radius(CHIMNEY_X+.070,CHIMNEY_Y-.035),.235,.119)
    soot = calc("MAXIMUM",inner,calc("MULTIPLY",drift,.79))
    soot = calc("MULTIPLY",soot,ramp(calc("ADD",z,calc("MULTIPLY",grain,.10)),2.883,3.043))
    grain_gain = calc("ADD",.91,calc("MULTIPLY",grain,.46))
    soot = calc("MULTIPLY",soot,grain_gain)
    # 门脚返潮只在门板色样内起效，横向噪声使各块门板水线略有高低。
    atlas = node("ShaderNodeUVMap")
    atlas.uv_map = joined.data.uv_layers[0].name
    uv = node("ShaderNodeSeparateXYZ")
    tree.links.new(atlas.outputs[0],uv.inputs[0])
    u,v = uv.outputs[0],uv.outputs[1]
    column = calc("MULTIPLY",calc("GREATER_THAN",u,.25),calc("LESS_THAN",u,.50))
    wood = calc("MULTIPLY",column,calc("GREATER_THAN",v,.75))
    damp = ramp(calc("ADD",z,calc("MULTIPLY",grain,.12)),.202,.008)
    damp = calc("MULTIPLY",wood,calc("MULTIPLY",damp,.57))
    # 铜扣自壁面向外的凸缘最常触摸，背板和铰链铆钉保留铜锈。
    bronze = calc("MULTIPLY",column,calc("LESS_THAN",v,.25))
    polish = calc("MULTIPLY",bronze,ramp(y,-1.291,-1.341))
    ao = node("ShaderNodeAmbientOcclusion")
    ao.only_local, ao.samples = True, 32
    ao.inputs["Distance"].default_value = .00096
    channels = node("ShaderNodeCombineColor")
    for value,channel in ((calc("MAXIMUM",soot,damp),"Red"),(ao.outputs["AO"],"Green"),(polish,"Blue")):
        tree.links.new(value,channels.inputs[channel])
    emit = node("ShaderNodeEmission")
    tree.links.new(channels.outputs[0],emit.inputs["Color"])
    tree.links.new(emit.outputs[0],node("ShaderNodeOutputMaterial").inputs["Surface"])
    target = node("ShaderNodeTexImage")
    target.image = image
    tree.nodes.active = target
    saved = list(joined.data.materials)
    for i in range(len(saved)):
        joined.data.materials[i] = mat
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 6
    try:
        bpy.ops.object.bake(type="EMIT",uv_layer=UV,use_clear=False,margin=3,margin_type="EXTEND")
    finally:
        for i, original in enumerate(saved):
            joined.data.materials[i] = original
        bpy.data.materials.remove(mat)
    image.filepath_raw,image.file_format = str(path),"PNG"
    image.save()
    image.pack()
    return image


def apply_finish(mat,image):
    tree = mat.node_tree
    nodes = tree.nodes
    shader = nodes.get("Principled BSDF")
    def feed(socket,target):
        tree.links.new(socket,target)
    def mix(label, blend, source, factor, color, loc):
        n = nodes.new("ShaderNodeMixRGB")
        n.label,n.blend_type,n.location = label,blend,loc
        feed(factor,n.inputs[0])
        feed(source,n.inputs[1])
        n.inputs[2].default_value = color
        return n.outputs[0]
    uv = nodes.new("ShaderNodeUVMap")
    uv.uv_map,uv.location = UV,(-790,-640)
    tex = nodes.new("ShaderNodeTexImage")
    tex.image,tex.location = image,(-565,-640)
    feed(uv.outputs[0],tex.inputs[0])
    ch = nodes.new("ShaderNodeSeparateColor")
    ch.label,ch.location = "烟熏与返潮／接触可见度／铜扣抛光",(-310,-620)
    feed(tex.outputs["Color"],ch.inputs[0])
    base = shader.inputs["Base Color"].links[0].from_socket
    patina = mix("烟口与门脚","MULTIPLY",base,ch.outputs["Red"],(.34,.30,.26,1),(350,480))
    burnished = mix("铜扣凸缘","MIX",patina,ch.outputs["Blue"],(.50,.26,.067,1),(555,480))
    # 棚拍已经有路径追踪遮蔽，只给无实时阴影的预览补少量接触色。
    cavity = nodes.new("ShaderNodeMath")
    cavity.operation,cavity.location = "SUBTRACT",(120,-535)
    cavity.inputs[0].default_value = 1
    feed(ch.outputs["Green"],cavity.inputs[1])
    feed(mix("缝隙接触色","MULTIPLY",burnished,cavity.outputs[0],(.86,.86,.86,1),(765,440)),shader.inputs["Base Color"])
    roughness = shader.inputs["Roughness"].links[0].from_socket
    polish = nodes.new("ShaderNodeMixRGB")
    polish.label,polish.location = "手触铜面光泽",(540,-200)
    feed(ch.outputs["Blue"],polish.inputs[0])
    feed(roughness,polish.inputs[1])
    polish.inputs[2].default_value = (.20,.20,.20,1)
    feed(polish.outputs[0],shader.inputs["Roughness"])
    shader.location = (990,350)
    nodes.get("Material Output").location = (1290,360)
