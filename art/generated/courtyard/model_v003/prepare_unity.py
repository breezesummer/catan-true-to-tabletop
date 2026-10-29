"""提取已验收 GLB 的 PBR 贴图和材质描述；FBX 通过 Blender MCP 单独导出。"""
from pathlib import Path
import json,struct
root=Path(__file__).resolve().parent;repo=root.parents[3];out=repo/'Unity/Catan/Assets/Art/Courtyard/v003';out.mkdir(parents=True,exist_ok=True)
b=(root/'courtyard_high.glb').read_bytes();n=struct.unpack_from('<I',b,12)[0];g=json.loads(b[20:20+n]);start=20+n+8;files=[]
for i,im in enumerate(g['images']):
 v=g['bufferViews'][im['bufferView']];name=f'tex_{i:02}.png';(out/name).write_bytes(b[start+v.get('byteOffset',0):start+v.get('byteOffset',0)+v['byteLength']]);files.append(name)
def texture(info):return files[g['textures'][info['index']]['source']] if info else ''
materials=[]
for m in g['materials']:
 p=m.get('pbrMetallicRoughness',{});materials.append({'name':m['name'],'color':p.get('baseColorFactor',[1,1,1,1]),'baseColor':texture(p.get('baseColorTexture')),'metallic':p.get('metallicFactor',1),'roughness':p.get('roughnessFactor',1),'metalRough':texture(p.get('metallicRoughnessTexture')),'normal':texture(m.get('normalTexture')),'normalScale':m.get('normalTexture',{}).get('scale',1)})
(out/'materials.json').write_text(json.dumps({'materials':materials},ensure_ascii=False,indent=2))
print(f'{len(materials)} 材质，{len(files)} 贴图已提取。')
