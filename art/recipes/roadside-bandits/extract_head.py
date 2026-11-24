"""从 CC0 版 MakeHuman HM08 截取相连的头、颈与锁骨，保留原有五官和耳部的布线。"""
from pathlib import Path
import numpy as np


def extract_head(obj, dest):
    vertices, texcoords, faces = [], [], []
    group = ""
    for line in Path(obj).read_text(encoding="utf-8").splitlines():
        s = line.split()
        if not s:
            continue
        if s[0] == "v":
            vertices.append(tuple(map(float,s[1:4])))
        elif s[0] == "vt":
            texcoords.append(tuple(map(float,s[1:3])))
        elif s[0] == "g":
            group=s[1]
        elif s[0] == "f" and group=="body":
            faces.append([[int(k)-1 for k in corner.split("/")[:2]] for corner in s[1:]])
    xyz=np.asarray(vertices); faces=np.asarray(faces)
    cut=(xyz[:,1]>5.35)&(np.abs(xyz[:,0])<1.27)
    faces=faces[cut[faces[:,:,0]].all(axis=1)]
    native,indices=np.unique(faces[:,:,0],return_inverse=True)
    uv=np.asarray(texcoords)[faces[:,:,1]]
    # 原头部展开横置。把正脸和耳部重新分配到较宽的区域，口腔、眼窝、颈部留独立边距。
    u,v=uv[:,:,0],uv[:,:,1]
    packed=np.zeros_like(uv)
    head=(u.mean(axis=1)>.63)&(v.mean(axis=1)>.14)
    packed[head,:,0]=np.interp(v[head],[.148,.30,.36,.404,.4835,.563,.607,.666,.819],
                              [.01,.055,.115,.205,.50,.795,.885,.945,.99])
    packed[head,:,1]=np.interp(u[head],[.641,.72,.77,.805,.836,.868,.900,.954,.995],
                              [.01,.092,.157,.22,.315,.431,.548,.705,.807])
    for mask,box in ((v.mean(axis=1)>.825,(.012,.833,.710,.987)),
                     (v.mean(axis=1)<.138,(.726,.832,.820,.984)),
                     (u.mean(axis=1)<.08,(.834,.832,.905,.984)),
                     ((u.mean(axis=1)>.08)&(u.mean(axis=1)<.16),(.919,.832,.988,.984))):
        points=uv[mask]; low=points.min(axis=(0,1)); high=points.max(axis=(0,1))
        packed[mask]=np.array(box[:2])+(points-low)/(high-low)*(np.array(box[2:])-box[:2])
    assert np.all(packed>.001) and np.all(packed<1)
    np.savez_compressed(dest,vertices=xyz[native].astype("f4"),faces=indices.reshape(-1,4).astype("i2"),
                        uv=packed.astype("f4"),front=head)
    return {"顶点":len(native),"四边面":len(faces)}
