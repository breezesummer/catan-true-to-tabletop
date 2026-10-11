# 铁矿板块改为卡片插画 v004

2026-10-11，当前 M6 四类规则的铁矿地块改为**矿石资源卡插画派生的平坦六边形厚板**。真实六边形山地板块照片仍未提供；来源是现有 `资源牌/铁矿石.jpg`，原照片、完整资源手牌卡面和旧山地模型保留。

处理沿用已校正的 `resource-ore-front.png`，裁切像素 `[112,392,912,1192]` 后 bicubic 缩放为 1024×1024，映射到 8 mm 厚六边形顶面。实际为 1 个闭合 Mesh、20 个三角面，底心轴心、米制，68.5892×79.2×8 mm 尺寸仍是估算；没有 AI 重绘，也不把资源卡照片声称为山地实物复刻。[配方、SHA-256 与复现](../art/recipes/entity-conversion-v004/ore-tile/README.md)。

| 验证 | 结果与证据 |
| --- | --- |
| Blender 生成、FBX 回读 | Blender 4.5.14 LTS，1/1 通过；真实闭合网格、UV、非退化三角面、底心、尺寸与来源检查。[回读记录](../art/exports/entity-conversion-v004/ore-tile/verification.json) |
| 干净目录重建 | 几何、UV、材质、纹理、来源一致。[比较记录](../art/exports/entity-conversion-v004/ore-tile/clean-rebuild-verification.json) |
| Unity 与 Windows 构建 | Unity 6000.3.25f1，netstandard2.1 构建 0 警告、0 错误；Unity 核心与保存回读检查通过，新 M6 已构建。[导入记录](verification/ore-tile-v004/unity-import.json) |
| 四类正式显示入口 | 基础版、航海家、城市骑士、组合共 60 个来源实例与 8 张相机图；`oreUsesFlatCardArt=true`，米制、无碰撞、迷雾和合法/非法预览通过。[运行记录](verification/ore-tile-v004/runtime-integration.json) |
| 人工目检 | 主任务查看新纹理、四张 Blender 图和全部八张 Unity 图，平坦铁矿显示正常。[13 张图的目检记录](verification/ore-tile-v004/visual-review.json) |

当前数量为 **115 个唯一资产 ID、124 个保留版本 FBX**；重用现有来源，没有增加原照片，仍为 135 张。本轮只改外观，没有重复运行全部规则测试或全量 M6 保存恢复烟测，也不将此前结果记成本轮重跑。最终用户外观、实测尺寸及 M8 退出条件仍待确认，路线图状态不变。

运行 `tools/development/Play-M6.ps1` 查看。重新构建使用 `tools/development/Build-EntityIntegration.ps1`；仅美术运行检查使用：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-EntityIntegration.ps1 -EvidenceVersion ore-tile-v004 -ArtOnly
```
