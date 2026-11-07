def bake_surface(obj,kind,resolution=1024,margin=.006):
    scene.render.engine='CYCLES';scene.cycles.samples=12
    prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='HIP';prefs.get_devices()
    for device in prefs.devices:device.use=device.type=='HIP'
    scene.cycles.device='GPU'
    mesh=obj.data;mesh.uv_layers.new(name='Bake');mesh.uv_layers.active_index=1;mesh.uv_layers[1].active_render=True
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(72),island_margin=margin,area_weight=.8)
    bpy.ops.object.mode_set(mode='OBJECT')
    image=bpy.data.images.new(kind+'_绘色',width=resolution,height=resolution,alpha=False)
    image.colorspace_settings.name='sRGB'
    for i,source in enumerate(mats):
        mat=source.copy();mat.name=['Expansion_Natural','Expansion_Seat','Expansion_Metal'][i]+'_'+kind
        mesh.materials[i]=mat;nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
        out=nodes.new('ShaderNodeOutputMaterial');emit=nodes.new('ShaderNodeEmission')
        uv=nodes.new('ShaderNodeUVMap');uv.uv_map='UVMap'
        canvas=nodes.new('ShaderNodeTexImage');canvas.image=tex;links.new(uv.outputs[0],canvas.inputs[0])
        ao=nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=.0026;ao.samples=16
        mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.65
        links.new(canvas.outputs[0],mix.inputs[1]);links.new(ao.outputs['AO'],mix.inputs[2])
        links.new(mix.outputs[0],emit.inputs[0]);links.new(emit.outputs[0],out.inputs['Surface'])
        target=nodes.new('ShaderNodeTexImage');target.image=image;nodes.active=target
    scene.render.bake.margin=10
    bpy.ops.object.bake(type='EMIT');image.filepath_raw=str(ART/(kind+'-surface.png'));image.file_format='PNG';image.save()
    mesh.uv_layers.remove(mesh.uv_layers['UVMap']);mesh.uv_layers[0].name='UVMap'
    for i,mat in enumerate(mesh.materials):
        nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
        out=nodes.new('ShaderNodeOutputMaterial');p=nodes.new('ShaderNodeBsdfPrincipled')
        p.inputs['Metallic'].default_value=.32 if i==2 else 0;p.inputs['Roughness'].default_value=.47 if i==2 else .82
        t=nodes.new('ShaderNodeTexImage');t.image=image
        tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(1,1,1,1)
        links.new(t.outputs[0],tint.inputs[1]);links.new(tint.outputs[0],p.inputs['Base Color']);links.new(p.outputs[0],out.inputs['Surface'])

