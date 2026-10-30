"""独立英文工作区验证双平台开发包，不修改源工程设置或发布目录。"""
import sys,os,subprocess,json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
os.chdir(ROOT)
sys.path.insert(0,str(ROOT/'tools/development'))
import build_macos as b
out=b.LOCAL/'harbor-f6/build';logs=out/'logs';logs.mkdir(parents=True,exist_ok=True)
tool=b.tools();stage=b.android_workspace(logs,Path('/tmp')/('catan-harbor-f6-'+str(os.getuid())))
project=stage/'Unity/Catan';unity=b.ascii_link(tool['unity'].parents[2],'editor')/'Contents/MacOS/Unity'
records=[]
with b.signing(tool,True,logs) as env:
 player=unity.parents[1]/'PlaybackEngines/AndroidPlayer'
 env.update(JAVA_HOME=str(player/'OpenJDK'),ANDROID_HOME=str(player/'SDK'),ANDROID_SDK_ROOT=str(player/'SDK'),ANDROID_NDK_ROOT=str(player/'NDK'))
 key=Path(env['CATAN_ANDROID_KEYSTORE']);env['CATAN_ANDROID_KEYSTORE']=str(b.ascii_link(key.parent,'f6-signing')/key.name)
 for target,method in [('Win64','M6Build.Build'),('Android','M6Build.BuildAndroid')]:
  log=logs/(target+'.log')
  args=[unity,'-batchmode','-nographics','-quit','-projectPath',project,'-buildTarget',target,'-executeMethod',method,'--m6-output-root',stage/'output','--m6-android-arm64-only','--ultra-development-build','-logFile',log]
  b.run(args,logs/(target+'-console.log'),env)
  marker='CATAN_M6_ANDROID_BUILD_OK' if target=='Android' else 'CATAN_M6_UNITY_BUILD_OK'
  if marker not in log.read_text(errors='replace'):raise RuntimeError('构建缺少成功标记 '+target)
  print(target+' 开发包完成',flush=True)
import shutil
shutil.copytree(stage/'output/Builds',out/'Builds',dirs_exist_ok=True)
b.server(tool,out,'win-x64')
for p in (out/'Builds').rglob('*'):
 if p.suffix in ['.exe','.apk'] or p.name in ['Assembly-CSharp.dll','Catan.Server.dll']:
  records.append({'文件':str(p.relative_to(b.ROOT)),'字节数':p.stat().st_size,'SHA256':hashlib.sha256(p.read_bytes()).hexdigest()})
(out/'构建结果.json').write_text(json.dumps({'状态':'通过','类型':'开发包，APK采用临时测试签名','说明':'沿用当前客户端BuildVersion 2.0.2；AGENTS指定2.1.0尚未发布，本轮不升版本。没有Windows或Android设备运行证据。','产物':records},ensure_ascii=False,indent=2))
