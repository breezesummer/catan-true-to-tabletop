# 城墙转化 v001

2026-10-09 已实际查看 `assets/references/inbox/建筑/红色城市+城墙.jpg` 与 `红色大都会+城墙.jpg` 两张组合照片，并对照 `红色城市.jpg` 区分城市圆底座和外围城墙。三张参考原件均保留，完整 SHA-256 和用途记录在 `recipe.json`；第三张为额外比较参考，不是独立城墙照片。

## 制作内容

独立参数化实际网格包括连续环形墙体、下沿台阶、上沿凸边、两道水平浮雕、16 条竖向扶壁、32 个主墙垛和八座带小墙垛的圆形突出塔。中央保留真正贯穿到底的通孔，城市和大都会插入件没有包含在本资产中。所有同色部件合并为一个渲染网格，内部由 99 个封闭几何壳组成，共 14,624 个三角面和一个红色塑料材质。确定性逐面投影 UV 图集覆盖全部面；本类无需贴图，因此没有 PNG 纹理依赖，也没有生成图片代替网格。

源坐标 X 宽、Y 深、Z 高，米制；底面中心为枢轴。外包围盒 `24 × 24 × 5 mm`、中央名义孔径 `18.6 mm` 均为估算，未实测或取得用户尺寸确认。照片被插入件遮挡的内壁、底面和后部采用简化重复结构；八座塔和墙垛数量是可见轮廓的近似布局推断，不能称为实物完整复原。塑料颜色近似原照曝光下的红色，照明与白平衡影响仍然保留为未知。

## 复现与实际验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type city-wall
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type city-wall -OutputRoot .local/art-pipeline/rebuild/city-wall -SkipPreviews -CompareRoot art
& 'D:\Development\Blender\blender-4.5.14-windows-x64\blender.exe' --background --python-exit-code 1 --python art/recipes/entity-conversion-v001/city-wall/audit_insert_clearance.py
```

已使用 Blender `4.5.14 LTS` 执行正式生成、显式 FBX 导出及独立 FBX 回读。`art/exports/entity-conversion-v001/city-wall/verification.json` 记录通过：一个实际网格、一套全部使用的材质槽、14,624 三角面、所有边使用两次、无退化面、UV 完整、对象原点归零、底面 Z=0、X/Y 包围盒居中、尺寸约 `[0.024, 0.024, 0.005] m`、三张原始照片 hash 未改变。

已从干净目录 `.local/art-pipeline/rebuild/city-wall` 重建并回读，几何、UV、材质、参考来源、估算尺寸、种子和导出参数与正式输出严格一致。wrapper 将比较证据保存到正式导出目录 `clean-rebuild-verification.json`。正式版本不覆盖；再次重建时需要新的输出目录。

另执行只读 `audit_insert_clearance.py`：最窄孔径约 `18.5944 mm`（128 边内环弦的净空），既有城市在墙顶以下的最大直径约 `18.0000 mm`，居中摆放的最小径向余量约 `0.2972 mm`；99 个独立几何壳与设计数量一致，结果保存到 `insert-clearance.json`。此项只核对估算源几何，不表示真实插装间隙或 Unity 运行验证；大都会适配尚未在本子任务检查。

已实际查看 `art/previews/entity-conversion-v001/city-wall` 中俯视、侧视、游戏相机和 `crowded-vertex` 四张 `1600 × 1000` 预览。可辨认环形开孔、低矮墙体、圆塔、墙垛和浮雕台阶。因为本类只有一件城墙，`crowded-vertex` 是单资产斜视，没有相邻道路、城市或地块，不能当作真实节点拥挤摆放/拾取验收。

本子任务未启动 Unity，未验证正式 Unity 灯光、拾取、运行表现或城市/大都会组合摆放。用户外观确认、带尺实测尺寸和独立城墙多视图仍待补齐；源照片参考的参数化近似不等于摄影测量或扫描模型。未改动规则与路线图，Unity 集成由主任务顺序验收。
