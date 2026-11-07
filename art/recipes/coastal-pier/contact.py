"""从最终货物网格向桥板烘焙接触阴影，六个货位共用一个小图集。"""
import math
import struct
import zlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree

def bake(objects,kinds,path):
    size=128;width=3*size;height=2*size
    pixels=bytearray([255])*(width*height)
    directions=[]
    for i in range(18):
        z=.25+.74*(i+.5)/18
        radius=math.sqrt(1-z*z);a=i*2.399963
        directions.append(Vector((radius*math.cos(a),radius*math.sin(a),z)))
    for tile,kind in enumerate(kinds):
        mesh=next(o.data for o in objects if o.name=='Cargo_'+kind)
        tree=BVHTree.FromPolygons([v.co*1000 for v in mesh.vertices],[p.vertices[:] for p in mesh.polygons])
        for j in range(size):
            y=((j+.5)/size-.5)*34
            if y>2 or y<-16: continue
            for i in range(size):
                x=((i+.5)/size-.5)*34
                if x>9:continue
                p=Vector((x,y,1.247))
                occ=0
                for d in directions:
                    distance=tree.ray_cast(p,d,8.5)[3]
                    if distance is not None:
                        occ+=(1-distance/8.5)**1.45
                row=(tile//3+1)*size-1-j
                pixels[row*width+(tile%3)*size+i]=round(255*(1-.76*occ/len(directions)))
    def chunk(tag,data):
        return struct.pack('!I',len(data))+tag+data+struct.pack('!I',zlib.crc32(tag+data))
    raw=b''.join(b'\0'+pixels[row*width:(row+1)*width] for row in range(height))
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',width,height,8,0,0,0,0))
                     +chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))
