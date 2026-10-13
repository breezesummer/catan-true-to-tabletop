# 参考素材

将用户提供的原始照片、扫描件和现成模型放在 inbox 中。保留原始文件名与内容；派生处理另存版本，并在 manifest.json 中登记用途。收到文件后再为每个来源分配稳定编号。

manifest.json 已登记第一批五份样板来源。每项素材填写 id、originalPath、kind、edition、referenceRole、measuredDimensionsMm、estimatedDimensionsMm、mustPreserve 和 notes。没有测量的数据使用 null；实测与估算不能混填。其余原始照片先保留在 inbox，后续生产时再分配来源编号。

先提供少量样板即可。完整采集规范见 ../../docs/ART_PIPELINE.md；卡牌精确文字需独立核对后进入规则数据。

2026-10-08 用户已确认：M0 先采用官方 2025 规则与明确标注的估算尺寸，实体参考后补。2026-10-09 已收到原始照片并完成首批样板，见 [PHOTO_SAMPLES_V001.md](../../docs/PHOTO_SAMPLES_V001.md)。实体版本和实测尺寸仍未知。官方规则来源另登记在 [rules/v001/sources.json](rules/v001/sources.json)，不冒充用户实物参考。

尺度与补充流程见 [REFERENCE_BASELINE.md](../../docs/REFERENCE_BASELINE.md)，估算配方见 [scale-estimates.json](../../art/recipes/v001/scale-estimates.json)。原始实测字段保持 `null`；后续收到资料时保留 `v001`，新增参数版本并关联真实参考 ID。

2026-10-10，按用户要求完成橙色、米色、蓝色玩家组件照片分类：每色 6 张骑士照片归入 `inbox/单位`（一级至三级，各含戴头盔与未戴头盔），每色 6 张建筑照片归入 `inbox/建筑`（村庄、城市、大都会、城墙、城市+城墙、大都会+城墙），共 36 张。骑士等级通过旗帜的一、二、三尖轮廓与现有红色参考对照；头盔仅按可见外观命名。橙色新增文件统一使用“橙色”，既有“橘色船”文件名保留。

分类文件按“颜色+组件名称.jpg”命名，未裁切、重绘或重新编码。原文件名与原始内容另存于 `originals/player-components-v001`；[归档清单](catalogs/player-components-v001.json)记录原文件名、原路径、备份路径、新路径及 SHA-256。36 张归档文件和 36 张原件备份均已逐一校验，哈希与移动前一致；本批“未分类”目录已清空。此次仅整理参考照片，不构成美术制作或开发阶段验收。
