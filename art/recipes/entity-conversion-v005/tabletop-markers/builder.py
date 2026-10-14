"""原照片轮廓拉伸为真实闭合厚纸板；米制底心轴心。"""
import bpy


def build(asset,helpers):
    w,d,h=[float(n)/1000 for n in asset['dimensionsMm']]
    outline=[(float(u)-.5,.5-float(v)) for u,v in asset['outlineUvImage']]
    area=sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(outline,outline[1:]+outline[:1]))
    if area<0: outline.reverse()
    n=len(outline)
    vertices=[(x*w,y*d,z) for z in (0,h) for x,y in outline]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    edge=helpers['mat'](asset['id']+'_PaperCoreEstimated',(.53,.45,.33),.9)
    front=helpers['mat'](asset['id']+'_OriginalPhoto', (1,1,1),.85,asset['frontTexture'])
    back=helpers['mat'](asset['id']+'_UnseenSurfaceNeutral',(.62,.57,.47),.9)
    obj=helpers['mesh'](asset['id'],vertices,faces,edge)
    obj.data.materials.append(front);obj.data.materials.append(back)
    uv=obj.data.uv_layers.active
    for polygon in obj.data.polygons:
        if polygon.normal.z>.9: polygon.material_index=1
        elif polygon.normal.z<-.9: polygon.material_index=2
        for index in polygon.loop_indices:
            point=obj.data.vertices[obj.data.loops[index].vertex_index].co
            uv.data[index].uv=(point.x/w+.5,point.y/d+.5)
    return [obj]
