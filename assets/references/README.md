# 参考素材

将用户提供的原始照片、扫描件和现成模型放在 inbox 中。保留原始文件名与内容；派生处理另存版本，并在 manifest.json 中登记用途。收到文件后再为每个来源分配稳定编号。

manifest.json 的 assets 当前为空。每项素材建议填写 id、originalPath、kind、edition、referenceRole、measuredDimensionsMm、estimatedDimensionsMm、mustPreserve 和 notes。没有测量的数据使用 null；实测与估算不能混填。

先提供少量样板即可。完整采集规范见 ../../docs/ART_PIPELINE.md；卡牌精确文字需独立核对后进入规则数据。

2026-10-08 用户已确认：M0 先采用官方 2025 规则与明确标注的估算尺寸，实体参考后补。实体版本仍未知，`assets` 为空的含义仍是尚未收到原始素材。官方规则来源另登记在 [rules/v001/sources.json](rules/v001/sources.json)，不冒充用户实物参考。

尺度与补充流程见 [REFERENCE_BASELINE.md](../../docs/REFERENCE_BASELINE.md)，估算配方见 [scale-estimates.json](../../art/recipes/v001/scale-estimates.json)。原始实测字段保持 `null`；后续收到资料时保留 `v001`，新增参数版本并关联真实参考 ID。
