# 海盗转化 v001

2026-10-09，已实际查看 `assets/references/inbox/单位/海盗（海洋板块上的单位）.jpg` 这张原始照片。来源 SHA-256 在 `recipe.json` 固定；原件只读保留。此类型对应航海家海洋板块上的海盗，入侵轨道上照片文件名同样称“海盗”的船由 `barbarian-ship` 类型单独映射为蛮族船。

## 可见特征与制作内容

照片显示黑色塑料般的表面、低且宽的条状波纹底座、较厚船体、上翘端部、中央桅杆、两块大小不同的弧形帆，以及桅顶小而不规则的凸旗。船体附近可见深色凸起，但细小图案无法读清；本次只用简化船舷、肋条与后甲板浮雕表达层次。照片不足以确认骷髅，因此不添加白色骷髅或其他未经确认的标志。两块帆之间的下部空隙和倾斜的小帆上沿依照片轮廓近似。

`builder.py` 自包含参数化建模代码，不导入玩家船或蛮族船的脚本。实际几何包含封闭支承底座、波纹凸条、凹甲板船体、船舷与宽侧肋条、首尾翘端、后甲板、桅杆和桅顶凸旗、两块有厚度的弧面帆、帆边与少量帆褶。各部件保留封闭壳，再合为一个黑色材质渲染网格；每个面使用确定性 UV 图集，不依赖 Smart Project 的打包顺序。本类没有纹理依赖，照片用于形状和材质颜色参考，未把平面照片当作立体网格。

## 尺度与轴心

估算包围盒为 `12 × 28 × 30 mm`，没有带尺照片或实测值。配方本地 X 宽、Y 长、Z 高；网格烘焙为米制，底面 Z=0、X/Y 包围盒中心为0，所有网格原点归零。显式 FBX 导出使长轴对应 Unity Z。底心枢轴位于低波纹支承面底部；水线基准估算为底面以上 `1.8 mm`，约在底座上方、船体下缘附近，只是工作估算。

## 复现

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type pirate
# 已有正式输出时，改用全新输出目录，不能覆盖已有版本。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type pirate -OutputRoot .local/art-pipeline/rebuild/pirate-next -SkipPreviews -CompareRoot art
```

## 实际验证结果

Blender `4.5.14 LTS` 正式生成 1 件 `pirate-black`，源文件在 `art/source/entity-conversion-v001/pirate/pirate-black.blend`，显式模型在 `art/exports/entity-conversion-v001/pirate/pirate-black.fbx`。55 个封闭建模部件按同材质合为 1 个渲染网格，共 13,908 三角面，1 套黑色塑料材质，无外部纹理依赖。

正式 wrapper `tools/art/Build-EntityAssets.ps1 -Type pirate` 完成独立 FBX 回读，报告 `verification.json` 为 `passed`：全部边使用两次、无退化三角形、UV 完整、材质槽实际使用、各对象原点为0、底面为0、X/Y 包围盒居中，实际尺寸约 `[0.012, 0.028, 0.030] m`。原照 SHA-256 未变，配方与 builder 校验值进入 manifest。

`art/previews/entity-conversion-v001/pirate` 的 `top.png`、`side.png`、`game-camera.png`、`crowded-vertex.png` 四张正式 1600×1000 图已逐一实际查看，支承面、船体、黑色表面、帆厚度和小凸旗可辨。通用侧相机沿船长轴拍摄，主要显示端部与帆厚度；为核对双帆外轮廓，另实际查看 `.local/art-pipeline/pirate-debug-001/broadside-check.png` 横侧检查图。该临时图清楚显示宽主帆、上沿倾斜的较小副帆和下部空隙，不属于正式四预览 manifest。`crowded-vertex` 是本类型相机预览，尚不代表游戏中实际相邻组件或节点拾取验证。

另经正式 wrapper 在全新 `.local/art-pipeline/rebuild/pirate` 中重新生成，跳过重复预览并与 `art` 正式目录比较。`clean-rebuild-verification.json` 为 `passed`，`sameGeometryMaterialsTexturesReferences` 为 `true`：几何与 UV 签名、网格数、面数、尺寸、轴心、材质、纹理依赖、来源、种子和导出设置一致。两次 wrapper 均成功；README 完成后再次运行 `tools/art/update_entity_checklist.py` 按实际证据刷新类型清单。

## 限制

一张斜视照片较暗且模糊，背面、底面、内部和精确浮雕未知；未见侧面采用对称近似，底部采用平支承面。帆厚度、波纹排列和细小凸旗形状是可见轮廓的参数化近似，黑色材质受到原照照明影响。结果不是扫描复原，尺寸和水线不是实测。Unity 导入后的实际轴向、材质渲染、拾取、邻件遮挡和游戏内替换，以及用户外观确认，由主任务集成验收；本子任务没有启动 Unity，也未改变规则或开发路线。
