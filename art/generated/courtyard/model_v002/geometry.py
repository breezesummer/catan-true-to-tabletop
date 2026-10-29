"""高细节庭院几何工具：组件内批量组装，避免为每块砖瓦创建场景对象。"""
import bpy,bmesh,math,random
from mathutils import Vector,Matrix
from collections import defaultdict
random.seed(20261029)
DATA=defaultdict(lambda:[[],[],[],[]]);GROUP='';TRANS=Matrix.Identity(4);CACHE={}
def group(name):
 global GROUP,TRANS
 GROUP=name;TRANS=Matrix.Identity(4)
def transform(x=0,y=0,z=0,angle=0):
 global TRANS
 TRANS=Matrix.Translation((x,y,z))@Matrix.Rotation(angle,4,'Z')
def add(v,f,ma,smooth=False):
 a=DATA[GROUP];n=len(a[0]);a[0].extend(tuple(TRANS@Vector(p)) for p in v);a[1].extend(tuple(n+i for i in face) for face in f);a[2].extend([ma]*len(f));a[3].extend(smooth if isinstance(smooth,list) else [smooth]*len(f))
def box(pos,size,ma='stone',r=.02,rot=0):
 key=tuple(round(a,5) for a in (*size,r))
 if key not in CACHE:
  bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
  for v in bm.verts:v.co.x*=size[0];v.co.y*=size[1];v.co.z*=size[2]
  if r:bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(r,min(size)*.42),segments=3,affect='EDGES')
  bm.verts.ensure_lookup_table();bm.verts.index_update();CACHE[key]=([tuple(v.co) for v in bm.verts],[tuple(v.index for v in f.verts) for f in bm.faces]);bm.free()
 v,f=CACHE[key];c,s=math.cos(rot),math.sin(rot);add([(pos[0]+x*c-y*s,pos[1]+x*s+y*c,pos[2]+z) for x,y,z in v],f,ma)
def lathe(pos,profile,ma='stone',n=40):
 x,y,z=pos;v=[(x+r*math.cos(i*2*math.pi/n),y+r*math.sin(i*2*math.pi/n),z+h) for r,h in profile for i in range(n)]
 f=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(profile)-1) for i in range(n)]+[tuple(range(n-1,-1,-1)),tuple((len(profile)-1)*n+i for i in range(n))];add(v,f,ma,True)
def ball(pos,scale,ma='stone',n=20,m=12):
 x,y,z=pos;v=[(x,y,z-scale[2])]+[(x+scale[0]*math.sin(math.pi*j/m)*math.cos(i*2*math.pi/n),y+scale[1]*math.sin(math.pi*j/m)*math.sin(i*2*math.pi/n),z-scale[2]*math.cos(math.pi*j/m)) for j in range(1,m) for i in range(n)]+[(x,y,z+scale[2])]
 f=[(0,1+(i+1)%n,1+i) for i in range(n)]+[(1+j*n+i,1+j*n+(i+1)%n,1+(j+1)*n+(i+1)%n,1+(j+1)*n+i) for j in range(m-2) for i in range(n)]+[(len(v)-1,1+(m-2)*n+i,1+(m-2)*n+(i+1)%n) for i in range(n)];add(v,f,ma,True)
def tube(points,r,ma='wood',n=10,closed=False,radii=None):
 pts=[Vector(p) for p in points];v=[];L=len(pts);previous_u=None
 for j,p in enumerate(pts):
  tangent=(pts[(j+1)%L]-pts[(j-1)%L]).normalized() if closed else (pts[min(j+1,L-1)]-pts[max(0,j-1)]).normalized()
  if previous_u is None:
   up=Vector((0,0,1)) if abs(tangent.z)<.92 else Vector((0,1,0));u=tangent.cross(up).normalized()
  else:u=(previous_u-tangent*previous_u.dot(tangent)).normalized()
  previous_u=u;w=tangent.cross(u);radius=radii[j] if radii else r
  v.extend(tuple(p+radius*(u*math.cos(i*2*math.pi/n)+w*math.sin(i*2*math.pi/n))) for i in range(n))
 f=[(j*n+i,j*n+(i+1)%n,((j+1)%L)*n+(i+1)%n,((j+1)%L)*n+i) for j in range(L if closed else L-1) for i in range(n)]
 if not closed:f.extend([tuple(range(n-1,-1,-1)),tuple((L-1)*n+i for i in range(n))])
 add(v,f,ma,True)
def beam(a,b,r,ma='wood',n=16):tube([a,b],r,ma,n)
def torus(pos,R,r,ma='gold',axis='Z',n=32,m=8):
 pts=[]
 for i in range(n):
  a=2*math.pi*i/n;v= (R*math.cos(a),R*math.sin(a),0) if axis=='Z' else (R*math.cos(a),0,R*math.sin(a)) if axis=='Y' else (0,R*math.cos(a),R*math.sin(a));pts.append(tuple(pos[j]+v[j] for j in range(3)))
 tube(pts,r,ma,m,True)
def ribbon(points,width,height,ma='red'):
 pts=[Vector(p) for p in points];v=[];rad=min(.025,height*.25);profile=[]
 for cx,cy,angle in [(width/2-rad,height/2-rad,0),(-width/2+rad,height/2-rad,math.pi/2),(-width/2+rad,-height/2+rad,math.pi),(width/2-rad,-height/2+rad,math.pi*1.5)]:
  for k in range(5):
   a=angle+k*math.pi/8;profile.append((cx+rad*math.cos(a),cy+rad*math.sin(a)))
 n=len(profile)
 for j,p in enumerate(pts):
  t=(pts[min(j+1,len(pts)-1)]-pts[max(0,j-1)]).normalized();u=Vector((-t.y,t.x,0)).normalized()
  for a,b in profile:v.append(tuple(p+u*a+Vector((0,0,b))))
 f=[tuple(range(n-1,-1,-1)),tuple((len(pts)-1)*n+i for i in range(n))]+[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(pts)-1) for i in range(n)];add(v,f,ma,True)
def extrude(poly,z0,z1,ma='stone'):
 n=len(poly);v=[(x,y,z) for z in [z0,z1] for x,y in poly];f=[tuple(range(n-1,-1,-1)),tuple(n+i for i in range(n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)];add(v,f,ma)
def finish(collection,materials):
 result=[]
 for name,(v,f,ms,sm) in DATA.items():
  me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);collection.objects.link(o)
  used=list(dict.fromkeys(ms))
  for ma in used:me.materials.append(materials[ma])
  ids={ma:i for i,ma in enumerate(used)}
  for p,ma,s in zip(me.polygons,ms,sm):p.material_index=ids[ma];p.use_smooth=s
  uv=me.uv_layers.new(name='UVMap')
  for p in me.polygons:
   axis=max(range(3),key=lambda k:abs(p.normal[k]));ii=([1,2] if axis==0 else [0,2] if axis==1 else [0,1])
   for li in p.loop_indices:
    co=me.vertices[me.loops[li].vertex_index].co;uv.data[li].uv=(co[ii[0]]/2,co[ii[1]]/2)
  result.append(o)
 return result

def sheet(v,f,ma,offset=(0,0,-.07),smooth=False):
 n=len(v);vv=v+[tuple(p[k]+offset[k] for k in range(3)) for p in v];ff=list(f)+[tuple(n+i for i in reversed(face)) for face in f]
 edges={}
 for face in f:
  for a,b in zip(face,face[1:]+face[:1]):
   key=tuple(sorted((a,b)));edges.setdefault(key,[]).append((a,b))
 for key,entries in edges.items():
  if len(entries)==1:
   a,b=entries[0];ff.append((a,b,n+b,n+a))
 add(vv,ff,ma,smooth)
