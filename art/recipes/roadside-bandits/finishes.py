"""按裁片烘焙固有色、切线法线及金属度／光滑度。仅可见且材质相符的正面取样原型。"""
from math import ceil
from pathlib import Path
import zlib
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from mathutils.bvhtree import BVHTree
from sculpt import V,P,C,S,BASE_ROW
from surface_study import LUMA,sample,chart_frame,contact_map,fabric_relief,hand_studies,worked_skin,finish_wear
from hand_mesh import TILES
from head_form import FACES,head_template
from portrait_finish import head_tile,eye_tile,fur_tile

ROOT=Path(__file__).resolve().parents[3]
REF=ROOT/"assets/references/inbox/单位/强盗团三人四视原型.png"
ART=ROOT/"Unity/Catan/Assets/Resources/NeutralPieces/v001"
SOURCE=ROOT/"art/source/roadside-bandits/强盗团.blend"
VIEWS=ROOT/"art/previews/roadside-bandits"
SIZE=2048
PAD=6
# 色值为 sRGB 染色底，后二项为金属度与光滑度。非金属的漫反射与金属高光分别烘焙。
MATERIALS={
    "skin":("bc8969","skin",0,.35),"eye":("b99f85","skin",0,.57),"palm":("c99570","skin",0,.29),
    "nail":("c3977d","nail",0,.44),"brow":("49362a","hair",0,.22),
    "hair":("4e3b30","hair",0,.26),"hairlight":("6c5240","hair",0,.27),
    "beard":("49392b","hair",0,.22),"russet":("69412f","boucle",0,.11),
    "vestpatch":("755640","suede",0,.16),"linen":("b49b7c","twill",0,.135),
    "hem":("b29b75","bast",0,.12),"smock":("88745e","twill",0,.13),
    "underlinen":("755847","twill",0,.10),
    "moss":("58583b","fulled",0,.14),"lining":("3c3c2b","fulled",0,.11),
    "slate":("575965","fulled",0,.145),"cap":("4b4850","fulled",0,.13),
    "hatwelt":("514950","fulled",0,.13),"binding":("75704b","wool",0,.16),
    "jerkin":("79523a","hide",0,.30),"trousers":("70604a","twill",0,.12),
    "olive":("827550","twill",0,.12),"slatepants":("645e59","twill",0,.13),
    "cuff":("75644f","suede",0,.165),"patch":("94694e","suede",0,.14),
    "sack":("a38559","bast",0,.105),"sackpatch":("6d5946","bast",0,.12),
    "twine":("a48b64","cord",0,.15),"hemp":("a99575","cord",0,.13),
    "thread":("887152","cord",0,.14),"repairthread":("725a3b","cord",0,.16),"boot":("654735","hide",0,.39),
    "sole":("493b30","hide",0,.17),"belt":("60432f","hide",0,.405),
    "strap":("936c48","hide",0,.32),"satchel":("96734e","hide",0,.33),
    "welt":("70513c","hide",0,.24),"lace":("79614d","cord",0,.23),
    "wrap":("a59272","bast",0,.14),"wood":("816348","wood",0,.20),
    "bark":("70523c","wood",0,.16),"brass":("b09a75","alloy",.77,.45),
    "steel":("b8bbb9","steel",.91,.54),"edge":("d8d9d3","steel",.94,.67),
    "bucklecore":("57565b","alloy",.64,.32),"crease":("43372d","hide",0,.19)
}

# 在同一盏灯下与正面彩稿逐料比对。毛呢吸光，蓝灰披肩冷于亚麻，皮靴保留暖棕。
DYES={
    "moss":(.73,.75,.66),"lining":(.89,.92,.83),"binding":(.76,.78,.66),
    "slate":(.81,.82,.85),"cap":(.76,.76,.79),"hatwelt":(.77,.78,.81),
    "russet":(.76,.69,.67),"jerkin":(.84,.76,.69),"belt":(.86,.79,.72),
    "sack":(.85,.80,.70),"sackpatch":(.88,.82,.73),"twine":(.92,.84,.72),
    "linen":(.92,.88,.81),"wrap":(.87,.82,.72),"hem":(.90,.85,.76),
    "satchel":(.90,.81,.70),"strap":(.89,.82,.74),"boot":(.88,.83,.78),
    "patch":(.78,.70,.66),"trousers":(.86,.82,.76),"olive":(.87,.84,.73),
    "vestpatch":(.81,.75,.69),"brass":(.89,.81,.67),"wood":(.91,.83,.72)
}


NAP={"fulled":.79,"boucle":.98,"wool":.78,"twill":.50,"bast":.32,"cord":.20}


def masks():
    groups={}
    def zone(key,*polys):
        canvas=groups.setdefault(key,Image.new("L",(448,432)))
        pen=ImageDraw.Draw(canvas)
        for p in polys:
            xy=[tuple(map(int,v.split(","))) for v in p.split()]
            pen.polygon([(x-140,y-66) for x,y in xy],fill=255)
    # 这些轮廓遮住衣料参考中的脸与头发，防止把肤色烘进兜帽和衣领。
    zone("face-carrier","252,93 260,86 286,84 307,99 305,120 297,135 281,149 260,144 250,133 247,122 252,113")
    zone("hair-carrier","247,101 252,87 264,78 280,74 296,78 306,90 315,111 313,124 302,135 293,135 297,114 287,93 260,94")
    zone("face-leader","364,179 374,157 388,143 399,143 411,150 420,166 423,182 413,197 399,205 380,199 365,191")
    zone("face-scout","469,149 482,137 493,125 508,124 514,138 519,145 518,150 515,151 517,158 515,166 501,170 494,185 478,191 466,171")
    zone("hair-scout","460,144 476,138 492,133 483,151 489,161 478,171 464,167 457,159")
    zone("cap","453,137 460,118 472,106 483,100 493,102 508,112 515,119 517,123 493,130 473,145 458,154")
    zone("linen","159,235 170,206 184,182 202,160 214,153 218,187 210,214 203,235 199,246 184,241 163,245",
         "296,209 302,184 312,153 326,145 335,151 347,172 346,186 336,200 326,212 315,205 306,203",
         "314,253 321,243 339,237 340,251 353,249 350,268 339,283 324,282 314,270",
         "444,253 456,252 468,265 474,283 477,312 457,323 449,314 446,287")
    zone("russet","198,302 202,273 208,245 208,215 211,190 202,162 202,150 218,142 247,133 254,133 265,148 277,144 295,132 311,137 333,145 321,152 313,179 304,200 302,267 299,303 281,313 252,313 240,307 214,311")
    zone("sack","216,280 210,262 210,240 220,216 238,201 258,188 250,183 245,181 242,173 251,164 266,158 280,159 292,164 300,174 298,184 281,186 278,191 291,206 298,222 294,245 288,279 279,290 257,293 232,289")
    zone("wood","161,311 172,307 176,339 177,362 181,388 186,427 188,459 180,466 175,463 172,432 170,399 168,362")
    zone("staff","161,241 182,243 198,252 189,268 181,288 185,304 188,316 185,327 180,327 178,319 177,316 173,320 176,333 170,337 162,333 154,325 148,318 148,306 154,289 158,265")
    zone("embrace","309,204 321,205 330,211 322,226 306,239 289,251 280,264 265,274 257,275 245,268 243,259 249,250 257,244 263,238 251,235 248,231 250,227 267,228 281,225 294,218")
    zone("palm","340,242 339,230 342,216 345,205 350,203 351,199 354,197 357,198 358,193 362,192 365,195 367,194 370,197 372,201 372,223 378,218 384,217 388,220 383,228 378,237 373,245 363,251 356,258 347,258")
    zone("knifehand","452,327 459,319 471,317 480,326 484,341 479,350 470,359 463,358 458,352 453,351 447,346")
    zone("steel","453,355 465,359 459,377 453,397 448,409 437,420 439,404 446,378")
    zone("visor","511,129 509,125 515,121 524,119 538,120 549,125 555,132 562,142 566,153 556,158 548,151 543,141 535,136 525,133")
    zone("straphand","475,277 484,265 496,268 505,274 512,283 511,291 503,301 494,298 482,288")
    zone("moss","353,186 348,183 352,167 360,147 369,131 380,123 391,123 407,131 422,148 434,171 439,186 434,196 422,203 414,207 399,214 376,208 361,201",
         "318,235 316,228 326,213 350,199 359,190 377,202 395,208 411,199 423,193 439,203 451,218 469,247 477,264 463,261 448,257 425,251 411,247 400,221 391,241 375,241 355,237 339,240",
         "315,240 338,236 347,264 348,307 336,345 322,377 300,380 291,371 298,339",
         "371,344 431,339 426,392 418,398 407,403 393,414 377,410 366,400")
    zone("jerkin","346,264 374,237 390,241 402,225 414,242 439,247 449,267 449,291 439,300 452,337 427,347 410,349 398,317 381,340 370,346 329,336 342,306 348,290")
    zone("hem","337,327 374,335 394,309 404,319 409,342 436,342 442,351 417,358 400,365 390,359 378,366 350,359 329,358")
    zone("smock","447,230 474,209 504,201 544,199 550,223 541,257 535,273 545,315 535,329 500,342 476,343 471,305 461,283",
         "520,183 536,177 552,165 548,153 558,147 570,144 579,157 582,170 568,188 544,208")
    zone("slate","438,193 449,176 466,165 478,171 492,184 498,195 509,180 519,169 535,164 546,161 542,187 533,207 532,223 514,219 498,205 484,222 470,235 449,243 442,229")
    zone("belt","344,286 369,285 392,290 413,290 439,296 445,308 421,308 402,303 375,300 348,297",
         "476,263 506,264 531,267 533,277 503,280 480,273")
    zone("satchel","505,280 515,280 538,282 546,293 548,310 550,321 542,331 526,335 515,330 508,315")
    zone("pants","200,309 244,314 261,314 251,351 254,376 245,397 246,409 196,411 195,397 201,376",
         "256,313 291,312 310,322 326,378 323,396 314,406 285,401 276,386 272,357",
         "333,354 376,360 387,368 375,394 366,416 357,424 323,421 318,412 324,388",
         "396,360 439,349 450,364 447,393 439,415 425,424 405,420 400,397",
         "456,338 484,333 492,349 481,371 472,389 452,391",
         "500,337 537,326 541,349 552,369 546,379 512,384 505,369")
    zone("boot","201,411 241,409 240,431 244,454 232,465 190,470 183,466 185,453 198,443",
         "285,401 317,402 321,428 335,444 329,457 292,457 287,450",
         "324,422 359,421 354,443 349,464 344,482 331,488 303,487 301,480 309,466 321,453",
         "403,421 439,418 441,446 462,456 475,465 473,473 454,478 429,474 407,467",
         "501,435 538,430 546,443 560,452 559,461 551,465 510,464 504,456",
         "445,434 469,435 479,451 476,460 449,460")
    zone("wrap","448,388 475,381 477,393 469,416 470,440 449,441 443,425",
         "506,381 547,377 541,400 540,428 541,438 508,443 505,423")
    zone("repairs","213,337 233,340 236,369 215,374 210,360","339,375 360,374 365,393 351,404 336,397",
         "424,374 444,380 439,401 422,402","217,239 236,244 233,273 215,269")
    zone("hardware","392,209 406,209 411,219 396,224","391,241 412,240 414,251 390,252",
         "390,268 413,268 413,279 390,280","388,287 414,288 415,310 387,310",
         "489,191 509,190 512,209 488,211")
    zones={k:np.asarray(m.filter(ImageFilter.GaussianBlur(.88)),dtype=np.float32)/255 for k,m in groups.items()}
    for name,fronts in {"sack":("embrace","repairs"),"russet":("sack","embrace","linen"),
                        "moss":("face-leader","palm","knifehand","hardware"),"slate":("face-scout","visor","hardware"),
                        "cap":("visor","face-scout"),"pants":("repairs","satchel","jerkin","hem","wood"),
                        "jerkin":("belt","palm","hardware"),"belt":("hardware",),"smock":("slate","satchel","straphand","belt")}.items():
        for f in fronts:zones[name]*=1-zones[f]
    return zones


def back_weave(rgb,height,points,normals,mat,role,reference):
    # 背视图为无彩泥稿。将原片的褶脊、粗呢结粒和磨痕作为明度细节转移到染色布上。
    guide={
        ("carrier","russet"):(271,66,1186,1.01,493,[(1117,648),(1140,635),(1166,635),(1195,637),(1228,638),
              (1242,648),(1238,701),(1242,758),(1253,797),(1246,805),(1224,796),(1188,800),(1146,797),(1119,807),(1120,770),(1127,712)]),
        ("leader","moss"):(395,0,1073,.93,499,[(1063,632),(1075,630),(1092,643),(1104,664),(1110,684),
              (1096,700),(1097,732),(1115,771),(1124,866),(1113,886),(1112,913),(1085,931),(1058,918),(1032,900),(1027,854),
              (1009,785),(1015,727),(1025,710),(1023,688),(1034,659)]),
        ("scout","slate"):(499,68,975,.97,493,[(916,683),(944,669),(978,670),(1006,683),(1026,700),(1031,749),
              (1036,765),(1013,775),(982,778),(952,773),(918,765),(907,724)]),
    }.get((role,mat))
    if not guide:return rgb
    cx,cy,bx,sx,off,outline=guide
    x,d,z=np.moveaxis(points,2,0)
    u=bx-(x-cx)*sx;v=BASE_ROW-C*z+S*(d-2*cy)+off
    # 截去相邻角色与摄影底纸，布色仍由该裁片决定。
    mask=Image.new("L",(540,460))
    pen=ImageDraw.Draw(mask);pen.polygon([(a-815,b-560) for a,b in outline],fill=255)
    if mat=="russet":pen.polygon([(x-815,y-560) for x,y in ((1145,738),(1208,745),(1198,786),(1141,780))],fill=0)
    back=np.clip((d-cy-4)/14,0,1)*np.clip((normals[:,:,1]-.08)/.49,0,1)
    back*=sample(np.asarray(mask.filter(ImageFilter.GaussianBlur(3)),dtype=np.float32)/255,u-815,v-560)
    lum,local=sample(reference,u-815,v-560).transpose(2,0,1)
    # 灰稿只提供同一块织物的局部起伏。裁片外缘、投影与棚灯形成的大色块不进入固有色。
    residual=(lum-local)/np.maximum(68,local)
    support=back*np.clip((lum-62)/40,0,1)*np.clip((207-lum)/34,0,1)*np.clip((.48-np.abs(residual))/.23,0,1)
    detail=np.clip(residual,-.19,.19)*support
    height+=detail*.70
    return rgb*(1+.37*detail[...,None])


def grid_array(chart):
    return np.array(chart.rows,dtype=np.float32)


class Layout:
    def __init__(self,charts):
        self.samples={}
        targets={}
        for i,ch in enumerate(charts):
            if not ch.density or ch.pigment_uv is not None:
                continue
            ar=grid_array(ch)
            if ch.wrap:
                across=np.concatenate((ar,ar[:,:1]),axis=1)
            else:
                across=ar
            u=np.linalg.norm(np.diff(across,axis=1),axis=2).sum(axis=1)
            v=np.linalg.norm(np.diff(ar,axis=0),axis=2).sum(axis=0)
            targets[i]=(max(10,float(np.percentile(u,74))*ch.density),
                        max(9,float(np.percentile(v,80))*ch.density))
            self.samples[i]=ar
        for mat in {ch.material for ch in charts}:
            targets[mat]=(77,77)
        for key in TILES:
            targets[key]=(202,274)
        for role in FACES:
            targets["head-"+role]=(410,446)
        for key in {ch.paint for ch in charts if ch.pigment_uv is not None}:
            targets[key]={"scalp":(380,140),"whiskers":(330,155),"iris":(86,86)}[key.split("-")[0]]
        def pack(scale):
            rect={k:(int(ceil(min(945,w*scale)))+2*PAD,int(ceil(min(945,h*scale)))+2*PAD)
                  for k,(w,h) in targets.items()}
            # 按高排列书架。相同键次序固定，烘焙与源场景始终使用同一排布。
            bins=[];out={}
            for key,(w,h) in sorted(rect.items(),key=lambda item:(-item[1][1],-item[1][0],str(item[0]))):
                for shelf in bins:
                    if shelf[1]+w<=SIZE:
                        break
                else:
                    y=sum(b[2] for b in bins)
                    if y+h>SIZE:return None
                    shelf=[y,0,h];bins.append(shelf)
                out[key]=(shelf[1]+PAD,shelf[0]+PAD,w-2*PAD,h-2*PAD)
                shelf[1]+=w
            return out
        lo,hi=.65,5.0
        for j in range(15):
            m=(lo+hi)*.5
            if pack(m):lo=m
            else:hi=m
        self.rect=pack(lo)
        if self.rect is None:raise ValueError("裁片超出图集")

    def uv(self,key,u,v):
        x,y,w,h=self.rect[key]
        return ((x+.5+u*(w-1))/SIZE,1-(y+.5+v*(h-1))/SIZE)


def visibility(charts,heads):
    verts=[];polys=[]
    for ch in charts:
        m,n=len(ch.rows),len(ch.rows[0])
        start=len(verts)
        verts.extend(p for row in ch.rows for p in row)
        for j in range(m-1):
            for k in range(n if ch.wrap else n-1):
                a=start+j*n+k;b=start+j*n+(k+1)%n
                polys.append((a,b,b+n,a+n))
        if ch.caps:
            polys.append(tuple(range(start,start+n))); end=start+(m-1)*n
            if ch.hand is None:polys.append(tuple(range(end,end+n)))
        if ch.hand is not None:
            start=len(verts)
            verts.extend(ch.hand.vertices.tolist())
            polys.extend((ch.hand.faces.astype(int)+start).tolist())
    for _,xyz in heads:
        start=len(verts)
        verts.extend(xyz.tolist())
        polys.extend((head_template()["faces"].astype(int)+start).tolist())
    tree=BVHTree.FromPolygons(verts,polys)
    depth=np.full((432,448),500,dtype=np.float32)
    direction=V((0,C,-S))
    for j,row in enumerate(depth):
        start=P(140,66+j,-220)
        for i in range(len(row)):
            hit=tree.ray_cast(start,direction,650)[0]
            if hit is not None:row[i]=hit.y
            start.x+=1
    return depth,tree


def grain(rng,w,h,frequency):
    nw=max(3,int(w/frequency));nh=max(3,int(h/frequency))
    data=rng.integers(0,256,(nh,nw),dtype=np.uint8)
    return np.asarray(Image.fromarray(data).resize((w,h),Image.Resampling.BICUBIC),dtype=np.float32)/127.5-1


def fibres():
    image=Image.open(SOURCE.parent/"织物与皮革母图.png").convert("RGB")
    cells={}
    for label,col,row in (("felt",0,0),("linen",1,0),("boucle",2,0),
                          ("leather",0,1),("sack",1,1),("blue",2,1)):
        box=(col*512+5,row*512+5,(col+1)*512-5,(row+1)*512-5)
        patch=image.crop(box).convert("L").resize((256,256),Image.Resampling.LANCZOS)
        grey=np.asarray(patch,dtype=np.float32)
        broad=np.asarray(patch.filter(ImageFilter.GaussianBlur(9)),dtype=np.float32)
        fine=grey-np.asarray(patch.filter(ImageFilter.GaussianBlur(2.4)),dtype=np.float32)
        tone=np.clip((grey-.70*broad-.30*grey.mean())/max(1,grey.mean()),-.70,.92)
        ridge=np.clip(fine/max(12,fine.std()),-2.1,2.1)
        planes=[Image.fromarray(x.astype(np.float32)) for x in (tone,ridge)]
        cells[label]=[np.stack([np.asarray(p.resize((n,n),Image.Resampling.BOX)) for p in planes],axis=2)
                      for n in (256,128,64,32,16,8,4)]
    return cells


def ground_contact(parts,path):
    """从六个鞋底和杖端取接触轮廓，用于着色占用地块，模型本身没有承托面。"""
    size=512
    mask=Image.new("L",(size,size))
    pen=ImageDraw.Draw(mask)
    def hull(points):
        pts=sorted(set(points))
        def side(seq):
            chain=[]
            for p in seq:
                while len(chain)>1:
                    a,b=chain[-2:]
                    if (b[0]-a[0])*(p[1]-a[1])>(b[1]-a[1])*(p[0]-a[0]):break
                    chain.pop()
                chain.append(p)
            return chain[:-1]
        return side(pts)+side(reversed(pts))
    for obj in parts:
        if "腿与靴" not in obj.name and "木杖" not in obj.name:continue
        footprint=[((.5+(v.co.x+.012)/.08)*size,(.5-v.co.y/.08)*size)
                   for v in obj.data.vertices if v.co.z<.00014]
        outline=hull(footprint)
        if len(outline)>2:pen.polygon(outline,fill=255)
    assert mask.getbbox()
    tight=np.asarray(mask.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(2.0)),dtype=float)/255
    soft=np.asarray(mask.filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(6.4)),dtype=float)/255
    shade=1-.48*tight-.37*soft
    Image.fromarray(np.uint8(255*np.clip(shade,0,1))).save(path)


def field(mat,w,h,rng,portrait):
    color,kind,metal,smooth=MATERIALS[mat]
    base=np.array(list(bytes.fromhex(color)),dtype=np.float32)
    y,x=np.indices((h,w),dtype=np.float32)
    low=grain(rng,w,h,27);middle=grain(rng,w,h,7);fine=rng.uniform(-1,1,(h,w)).astype(np.float32)
    drift=grain(rng,w,h,46)
    if kind in ("twill","bast","fulled","wool","boucle","cord"):
        pitch={"twill":4.15,"bast":5.25,"fulled":3.6,"wool":4.25,"boucle":4.8,"cord":4.0}[kind]
        warp=np.cos((x+.53*middle+.78*drift)*np.pi*2/pitch)
        weft=np.cos((y+.46*middle+.68*low)*np.pi*2/(pitch*1.16))
        twill=np.sin((x*.48+y*.86+.6*middle)*np.pi*2/7.3)
        if kind=="bast":
            relief=.48*np.maximum(warp,weft)+.18*warp*weft+.10*fine+.22*middle
        elif kind=="boucle":
            relief=.42*np.cos(2.6*warp+2.1*weft+middle)+.41*middle+.24*fine
        elif kind=="fulled":
            relief=.12*warp+.09*weft+.19*twill+.30*middle+.23*fine
        else:
            relief=.20*warp+.19*weft+.27*twill+.20*middle+.16*fine
        lumin=low*.036+middle*.020+relief*.048
    elif kind in ("hide","suede","skin","nail"):
        pitch=6.5 if kind=="hide" else 4.7
        u=x/pitch;v=y/(pitch*.87);ix=np.floor(u).astype(int);iy=np.floor(v).astype(int)
        jx=rng.uniform(.08,.92,(iy.max()+4,ix.max()+4));jy=rng.uniform(.08,.92,jx.shape)
        near=np.ones_like(x)*9;next_near=near.copy()
        for dy in (-1,0,1):
            for dx in (-1,0,1):
                dist=(ix+dx+jx[iy+1+dy,ix+1+dx]-u)**2+(iy+dy+jy[iy+1+dy,ix+1+dx]-v)**2
                next_near=np.minimum(next_near,np.maximum(near,dist));near=np.minimum(near,dist)
        line=np.exp(-((np.sqrt(next_near)-np.sqrt(near))/.17)**2)
        if kind=="hide":relief=.26*middle-.37*line+.10*fine
        elif kind=="suede":relief=.26*fine+.15*middle-.095*line
        elif kind=="skin":relief=.095*middle-.24*np.exp(-near/.034)+.037*fine
        else:relief=.05*np.cos(x*1.9)+.045*middle
        lumin=low*.076+middle*.031+relief*.115+(.018*fine if kind=="suede" else 0)
    elif kind=="wood":
        grainline=np.cos(x*.79+1.15*np.sin(y*.042)+middle*1.1+drift*2.9)
        relief=.4*grainline+.17*middle+.12*fine
        lumin=low*.085+.12*relief+.035*np.sin(.26*x+1.9*drift)
    elif kind=="hair":
        strand=np.cos(x*1.21+1.92*np.sin(.029*y+.4*low)+middle*.54)
        relief=.28*strand+.21*middle+.10*fine
        # 毛发小束也从肖像采集真实的发丝，不会形成整排均一的亮条。
        x0,y0,x1,y1=(300,534,464,628) if mat in ("beard","brow") else (255,43,406,100)
        sx=x0+x*(x1-x0)/max(1,w-1);sy=y0+y*(y1-y0)/max(1,h-1)
        strands=sample(portrait,sx,sy)@np.array([.2126,.7152,.0722])
        delta=np.clip((strands-np.median(strands))/88,-.55,.75)
        lumin=low*.07+relief*.09+delta*.67
        relief+=delta*.29
    else:
        score=np.maximum(0,np.cos(x*.42+y*.91+4.7*low)-.94)*4
        relief=.065*middle+.042*fine-.28*score
        lumin=low*.055+middle*.034+relief*.17
    rgb=np.clip(base*(1+lumin[...,None]),0,255)
    gloss=smooth+middle*(.028 if metal else .023)+relief*.025
    return rgb,relief.astype(np.float32),gloss,metal,kind


def hand_tile(key,w,h,study):
    left,top,right,bottom=TILES[key]
    patch=Image.fromarray(study[0][top:bottom,left:right].astype("uint8")).resize((w,h),Image.Resampling.LANCZOS)
    rgb=np.asarray(patch,dtype=float)
    skin=(rgb[:,:,0]-rgb[:,:,2]>16)&(rgb[:,:,0]-rgb[:,:,1]>7)
    # 照片轮廓外延色，防止指侧或缩小一级的纹理采到灰色底纸。
    filled=np.where(skin[:,:,None],rgb,0)
    mask=skin.astype(float)
    for radius in (2,7,24,70):
        filt=ImageFilter.GaussianBlur(radius)
        weight=np.asarray(Image.fromarray(np.uint8(mask*255)).filter(filt),dtype=float)/255
        blurred=np.asarray(Image.fromarray(np.uint8(np.clip(filled,0,255))).filter(filt),dtype=float)
        valid=(mask==0)&(weight>.08)
        filled[valid]=blurred[valid]/weight[valid,None]
        mask[valid]=1
    filled[mask==0]=[174,124,96]
    grey=Image.fromarray(np.uint8(np.clip(filled,0,255))).convert("L")
    fine=np.asarray(grey,dtype=float)-np.asarray(grey.filter(ImageFilter.GaussianBlur(2.1)),dtype=float)
    local=np.asarray(grey,dtype=float)-np.asarray(grey.filter(ImageFilter.GaussianBlur(8)),dtype=float)
    rgb=.78*filled*np.array([.94,.86,.76])+.22*np.array([179,130,91])+fine[:,:,None]*.09
    if key=="掌面":
        px=np.linspace(left,right-1,w)[None,:];py=np.linspace(top,bottom-1,h)[:,None]
        palm=np.exp(-((px-354)/145)**6-((py-459)/139)**6)
        rgb=rgb*np.array([.965,.94,.90])+(.44*local*palm)[:,:,None]
    else:
        rgb*=np.array([.98,.97,.94])
    forearm=np.clip((top+np.linspace(0,bottom-top-1,h)-545)/80,0,1)[:,None,None]
    neutral=np.array([177,126,94])*(1+np.clip(fine/580,-.025,.025)[:,:,None])
    rgb=rgb*(1-forearm)+neutral*forearm
    gloss=.265+np.clip(fine/270,-.035,.06)
    height=np.clip(fine/32,-.52,.38)*.65+np.clip(local/55,-.40,.27)*.21
    if key=="掌面":
        height+=np.clip(local/37,-.55,.35)*palm*.30
    if key=="手背":
        yy,xx=np.indices((h,w),dtype=float)
        sx=left+xx*(right-left-1)/(w-1);sy=top+yy*(bottom-top-1)/(h-1)
        for x,y,rx,ry in ((872,172,13,16),(967,80,16,20),(1051,39,16,17),
                          (1153,78,16,20),(1290,335,16,12)):
            plate=np.exp(-(((sx-x)/rx)**2+((sy-y)/ry)**2)**2)
            gloss+=.15*plate
    return rgb,height,gloss,0,"skin"


def bake(charts,layout,dest,heads):
    source=Image.open(REF).convert("RGB").crop((140,66,588,498))
    photo=np.asarray(source,dtype=np.float32)
    soft=np.asarray(source.filter(ImageFilter.GaussianBlur(5)),dtype=np.float32)
    zones=masks();depth,tree=visibility(charts,heads)
    rear_image=Image.open(REF).convert("L").crop((815,560,1355,1020))
    rear=np.stack([np.asarray(a,dtype=np.float32) for a in (rear_image,rear_image.filter(ImageFilter.GaussianBlur(8)))],axis=2)
    donor=Image.open(SOURCE.parent/"三人肖像母图.png").convert("RGB")
    portrait=np.asarray(donor,dtype=np.float32)
    portrait_soft=np.asarray(donor.filter(ImageFilter.GaussianBlur(4.2)),dtype=np.float32)
    hands=hand_studies(SOURCE.parent/"手掌手背与前臂母图.png")
    samples=fibres()
    base=Image.new("RGB",(SIZE,SIZE),(94,75,55))
    normal=Image.new("RGB",(SIZE,SIZE),(128,128,255))
    finish=Image.new("RGBA",(SIZE,SIZE),(0,255,0,50))
    for key,(x,y,w,h) in layout.rect.items():
        ch=None if isinstance(key,str) else charts[key]
        mat=ch.material if ch else key
        rng=np.random.default_rng(zlib.crc32((str(key)+mat).encode())^211720)
        if isinstance(key,str) and key.startswith("head-"):
            rgb,height,gloss,metal,kind=head_tile(key[5:],w,h,portrait,portrait_soft)
        elif isinstance(key,str) and key.startswith("iris-"):
            rgb,height,gloss,metal,kind=eye_tile(key[5:],w,h)
        elif isinstance(key,str) and key.startswith(("scalp-","whiskers-")):
            rgb,height,gloss,metal,kind=fur_tile(key,w,h,portrait,portrait_soft)
        elif key in TILES:
            rgb,height,gloss,metal,kind=hand_tile(key,w,h,hands)
        else:
            rgb,height,gloss,metal,kind=field(mat,w,h,rng,portrait)
        ar=None
        occlusion=np.ones((h,w),dtype=np.float32)
        if ch:
            ar=layout.samples[key]
            if ch.wrap:ar=np.concatenate((ar,ar[:,:1]),axis=1)
            yy,xx=np.indices((h,w),dtype=np.float32)
            u=xx/(w-1);v=yy/(h-1)
            points,n=chart_frame(ch,ar,u,v)
            occlusion=contact_map(points,n,tree)
        if ch and ch.paint in zones:
            dx,dy,dz=np.moveaxis(points,2,0)
            px=dx-140;py=BASE_ROW-C*dz-S*dy-66
            coarse=sample(soft,px,py);pic=sample(photo,px,py)
            angle=np.maximum(0,n[:,:,1]*(-C)+n[:,:,2]*S)
            vis=np.clip(1-(dy-sample(depth,px,py)-1.35)/5.5,0,1)
            support=sample(zones[ch.paint],px,py)*vis*np.clip((angle-.035)/.30,0,1)
            # 屏蔽底纸与展台，低频照明稍压平，轮廓外和背光面由裁片自身的材质接续。
            support*=np.clip((233-pic.min(axis=2))/29,0,1)
            support*=.90
            if ch.material in ("slate","cap"):
                support*=np.clip((pic[:,:,2]/(pic[:,:,0]+1)-.68)/.17,0,1)
            elif ch.material=="moss":
                support*=np.clip((pic[:,:,1]/(pic[:,:,0]+1)-.74)/.13,0,1)
            tone=coarse@np.array([.2126,.7152,.0722])
            target=rgb.mean(axis=2)
            # 染料的明暗过渡取自原图。经纬、皮粒与照明高光各自控制，避免袋纹重复成大格子。
            pigment=coarse+(pic-coarse)*(.80 if kind in ("skin","hair") else .54)
            pigment*=np.clip((target+35)/(tone+35),.65,1.55)[...,None]**.28
            pigment*=1+(.046 if kind=="skin" else .087)*height[...,None]
            rgb=rgb*(1-support[...,None])+pigment*support[...,None]
            detail=(pic-coarse)@np.array([.2126,.7152,.0722])/55
            height+=np.clip(detail,-1,1)*support*.28
            rgb=back_weave(rgb,height,points,n,mat,ch.part.split(" ")[0],rear)
        rgb=fabric_relief(rgb,height,gloss,kind,mat,samples,ar,rng)
        if ch:
            if ch.skin_region:rgb=worked_skin(rgb,height,gloss,ch,points,u,v,hands)
            rgb=finish_wear(rgb,height,gloss,mat,u,v,points,occlusion)
        dye=DYES.get(mat,(1.0,1.0,1.0))
        rgb=rgb*dye*(.84+.16*occlusion[...,None])
        grad_y,grad_x=np.gradient(height)
        strength=.71 if kind in ("skin","nail","steel","alloy") else .84
        norm=np.stack((-grad_x*strength,grad_y*strength,np.ones_like(height)),axis=2)
        norm/=np.linalg.norm(norm,axis=2)[...,None]
        pbr=np.zeros((h,w,4),np.uint8)
        pbr[:,:,0]=np.uint8(np.clip(metal*255,0,255))
        pbr[:,:,1]=np.uint8(occlusion*255+.5)
        if kind in NAP:
            nap=NAP[kind]*np.clip(.986+.014*height,.971,1.0)
            pbr[:,:,2]=np.uint8(nap*255+.5)
        pbr[:,:,3]=np.uint8(np.clip(gloss*255,0,255))
        for im,data in ((base,np.uint8(np.clip(rgb,0,255))),(normal,np.uint8(np.clip(127.5+127.5*norm,0,255))),(finish,pbr)):
            im.paste(Image.fromarray(np.pad(data,((PAD,PAD),(PAD,PAD),(0,0)),mode="edge")),(x-PAD,y-PAD))
    for name,img in (("surface",base),("normal",normal),("metal",finish)):
        img.save(dest/("robber-"+name+".png"),compress_level=7)
