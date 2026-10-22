
# 森林场景分解与三视图

日期：2026年10月22日。主方案 **77组、305张主候选图已全部落盘**：76组原角度＋正视＋右侧视＋顶视，以及GT01一张地面平面图。原文42张备选图未启用，不计入本批数量。

入口：[浏览图集](浏览图集.html) · [统一检查摘要](检查摘要.md) · [逐图尺寸与透明度抽样](image-metadata.json) · [任务清单](task-manifest.json)。

参考原件：[reference.png](reference.png)。基础提示词：[原文](../../../../../docs/forest_scene_extraction_prompts.md)；347条拆分提示词保存在 prompts。实际修订提示词、拒用稿及每组检查记录均保留在 assets 对应编号目录。使用内置ImageGen，按每组原角度先检查、再依次生成三视图的顺序执行；不同组分批并行。

**数量齐备不等于全部角度验收通过。** 已对木桶俯视、原木侧视未沿长轴、树冠顶视立面感、树桩前壁等问题多次返工。G01草蕨顶视仍有直立叶立面；ST05侧视与正视过于相近；部分工具顶视、树桩窄边及跨视图结构仍需精修。详细情况以每组记录为准。图像适合建模参考，不能当作同一三维模型的精确投影。

全部305张PNG可读取，总约361MiB。300张为1254×1254，其余5张为横向画布；未强行拉伸为原文1024尺寸。RGBA背景抽样288张角点alpha=0，17张alpha=1，主体常有252/253的近不透明alpha；未伪报全背景严格零或主体全255。用户本次允许纯色或透明背景。D01土层与GT01地表的共边尚未像素级校准。

| 资产 | 原角度／地面 | 正视 | 右侧 | 顶视 | 检查 |
| --- | --- | --- | --- | --- | --- |
| B01 后方大型红布储物棚 | [查看](assets/B01/B01__original.png) | [查看](assets/B01/B01__front.png) | [查看](assets/B01/B01__side.png) | [查看](assets/B01/B01__top.png) | [记录](assets/B01/检查记录.md) |
| B02 前方小型红布储物棚 | [查看](assets/B02/B02__original.png) | [查看](assets/B02/B02__front.png) | [查看](assets/B02/B02__side.png) | [查看](assets/B02/B02__top.png) | [记录](assets/B02/检查记录.md) |
| C01 左侧锯木工 | [查看](assets/C01/C01__original.png) | [查看](assets/C01/C01__front.png) | [查看](assets/C01/C01__side.png) | [查看](assets/C01/C01__top.png) | [记录](assets/C01/检查记录.md) |
| C02 右侧劈柴工 | [查看](assets/C02/C02__original.png) | [查看](assets/C02/C02__front.png) | [查看](assets/C02/C02__side.png) | [查看](assets/C02/C02__top.png) | [记录](assets/C02/检查记录.md) |
| D01 近六边形厚土层基座 | [查看](assets/D01/D01__original.png) | [查看](assets/D01/D01__front.png) | [查看](assets/D01/D01__side.png) | [查看](assets/D01/D01__top.png) | [记录](assets/D01/检查记录.md) |
| G01 前左细草与蕨状地被模块 | [查看](assets/G01/G01__original.png) | [查看](assets/G01/G01__front.png) | [查看](assets/G01/G01__side.png) | [查看](assets/G01/G01__top.png) | [记录](assets/G01/G01__检查记录.md) |
| G02 前缘宽叶匍匐地被模块 | [查看](assets/G02/G02__original.png) | [查看](assets/G02/G02__front.png) | [查看](assets/G02/G02__side.png) | [查看](assets/G02/G02__top.png) | [记录](assets/G02/检查记录.md) |
| G03 前缘垂沿藤蔓模块 | [查看](assets/G03/G03__original.png) | [查看](assets/G03/G03__front.png) | [查看](assets/G03/G03__side.png) | [查看](assets/G03/G03__top.png) | [记录](assets/G03/检查记录.md) |
| GT01 整块场景的正交俯视平面地面贴图 | [查看](assets/GT01/GT01__ground.png) | 不需要 | 不需要 | 不需要 | [记录](assets/GT01/检查记录.md) |
| K01 大棚下储物组合 | [查看](assets/K01/K01__original.png) | [查看](assets/K01/K01__front.png) | [查看](assets/K01/K01__side.png) | [查看](assets/K01/K01__top.png) | [记录](assets/K01/检查记录.md) |
| K02 小棚下储物组合 | [查看](assets/K02/K02__original.png) | [查看](assets/K02/K02__front.png) | [查看](assets/K02/K02__side.png) | [查看](assets/K02/K02__top.png) | [记录](assets/K02/检查记录.md) |
| P01 红旗与木旗杆 | [查看](assets/P01/P01__original.png) | [查看](assets/P01/P01__front.png) | [查看](assets/P01/P01__side.png) | [查看](assets/P01/P01__top.png) | [记录](assets/P01/检查记录.md) |
| P02 营地左侧前段木围栏 | [查看](assets/P02/P02__original.png) | [查看](assets/P02/P02__front.png) | [查看](assets/P02/P02__side.png) | [查看](assets/P02/P02__top.png) | [记录](assets/P02/检查记录.md) |
| P03 大棚左后段木围栏 | [查看](assets/P03/P03__original.png) | [查看](assets/P03/P03__front.png) | [查看](assets/P03/P03__side.png) | [查看](assets/P03/P03__top.png) | [记录](assets/P03/P03-检查记录.md) |
| P04 左侧外置大木桶 | [查看](assets/P04/P04__original.png) | [查看](assets/P04/P04__front.png) | [查看](assets/P04/P04__side.png) | [查看](assets/P04/P04__top.png) | [记录](assets/P04/检查记录.md) |
| P05 右侧外置大木桶 | [查看](assets/P05/P05__original.png) | [查看](assets/P05/P05__front.png) | [查看](assets/P05/P05__side.png) | [查看](assets/P05/P05__top.png) | [记录](assets/P05/检查记录.md) |
| P06 锯木工手锯 | [查看](assets/P06/P06__original.png) | [查看](assets/P06/P06__front.png) | [查看](assets/P06/P06__side.png) | [查看](assets/P06/P06__top.png) | [记录](assets/P06/检查记录.md) |
| P07 劈柴工长柄斧 | [查看](assets/P07/P07__original.png) | [查看](assets/P07/P07__front.png) | [查看](assets/P07/P07__side.png) | [查看](assets/P07/P07__top.png) | [记录](assets/P07/检查记录.md) |
| P08 树桩上的双端弧形铁工具 | [查看](assets/P08/P08__original.png) | [查看](assets/P08/P08__front.png) | [查看](assets/P08/P08__side.png) | [查看](assets/P08/P08__top.png) | [记录](assets/P08/检查记录.md) |
| P09 锯架上的长原木 | [查看](assets/P09/P09__original.png) | [查看](assets/P09/P09__front.png) | [查看](assets/P09/P09__side.png) | [查看](assets/P09/P09__top.png) | [记录](assets/P09/检查记录.md) |
| P10 左前锯马支架 | [查看](assets/P10/P10__original.png) | [查看](assets/P10/P10__front.png) | [查看](assets/P10/P10__side.png) | [查看](assets/P10/P10__top.png) | [记录](assets/P10/P10__checks.md) |
| P11 右后锯马支架 | [查看](assets/P11/P11__original.png) | [查看](assets/P11/P11__front.png) | [查看](assets/P11/P11__side.png) | [查看](assets/P11/P11__top.png) | [记录](assets/P11/检查记录.md) |
| P12 前景捆扎原木堆 | [查看](assets/P12/P12__original.png) | [查看](assets/P12/P12__front.png) | [查看](assets/P12/P12__side.png) | [查看](assets/P12/P12__top.png) | [记录](assets/P12/P12__检查记录.md) |
| P13 散落原木上方长段 | [查看](assets/P13/P13__original.png) | [查看](assets/P13/P13__front.png) | [查看](assets/P13/P13__side.png) | [查看](assets/P13/P13__top.png) | [记录](assets/P13/检查记录.md) |
| P14 散落原木下方粗段 | [查看](assets/P14/P14__original.png) | [查看](assets/P14/P14__front.png) | [查看](assets/P14/P14__side.png) | [查看](assets/P14/P14__top.png) | [记录](assets/P14/检查记录.md) |
| P15 散落短枝木段 | [查看](assets/P15/P15__original.png) | [查看](assets/P15/P15__front.png) | [查看](assets/P15/P15__side.png) | [查看](assets/P15/P15__top.png) | [记录](assets/P15/检查记录.md) |
| R01 左上中景苔石 | [查看](assets/R01/R01__original.png) | [查看](assets/R01/R01__front.png) | [查看](assets/R01/R01__side.png) | [查看](assets/R01/R01__top.png) | [记录](assets/R01/检查记录.md) |
| R02 上中偏左大苔石 | [查看](assets/R02/R02__original.png) | [查看](assets/R02/R02__front.png) | [查看](assets/R02/R02__side.png) | [查看](assets/R02/R02__top.png) | [记录](assets/R02/检查记录.md) |
| R03 左边缘小灰石 | [查看](assets/R03/R03__original.png) | [查看](assets/R03/R03__front.png) | [查看](assets/R03/R03__side.png) | [查看](assets/R03/R03__top.png) | [记录](assets/R03/检查记录.md) |
| R04 左前大树后的大苔石 | [查看](assets/R04/R04__original.png) | [查看](assets/R04/R04__front.png) | [查看](assets/R04/R04__side.png) | [查看](assets/R04/R04__top.png) | [记录](assets/R04/检查记录.md) |
| R05 左木桶旁营地苔石 | [查看](assets/R05/R05__original.png) | [查看](assets/R05/R05__front.png) | [查看](assets/R05/R05__side.png) | [查看](assets/R05/R05__top.png) | [记录](assets/R05/检查记录.md) |
| R06 红棚右后方苔石 | [查看](assets/R06/R06__original.png) | [查看](assets/R06/R06__front.png) | [查看](assets/R06/R06__side.png) | [查看](assets/R06/R06__top.png) | [记录](assets/R06/R06__检查记录.md) |
| R07 木堆前方小圆苔石 | [查看](assets/R07/R07__original.png) | [查看](assets/R07/R07__front.png) | [查看](assets/R07/R07__side.png) | [查看](assets/R07/R07__top.png) | [记录](assets/R07/R07__检查记录.md) |
| R08 前沿中左苔石 | [查看](assets/R08/R08__original.png) | [查看](assets/R08/R08__front.png) | [查看](assets/R08/R08__side.png) | [查看](assets/R08/R08__top.png) | [记录](assets/R08/R08__检查记录.md) |
| R09 前沿右侧苔石 | [查看](assets/R09/R09__original.png) | [查看](assets/R09/R09__front.png) | [查看](assets/R09/R09__side.png) | [查看](assets/R09/R09__top.png) | [记录](assets/R09/检查记录.md) |
| S01 左前边缘深绿矮灌木丛 | [查看](assets/S01/S01__original.png) | [查看](assets/S01/S01__front.png) | [查看](assets/S01/S01__side.png) | [查看](assets/S01/S01__top.png) | [记录](assets/S01/检查记录.md) |
| S02 中央大树右下亮绿灌木丛 | [查看](assets/S02/S02__original.png) | [查看](assets/S02/S02__front.png) | [查看](assets/S02/S02__side.png) | [查看](assets/S02/S02__top.png) | [记录](assets/S02/检查记录.md) |
| S03 栅栏左侧浓绿灌木丛 | [查看](assets/S03/S03__original.png) | [查看](assets/S03/S03__front.png) | [查看](assets/S03/S03__side.png) | [查看](assets/S03/S03__top.png) | [记录](assets/S03/检查记录.md) |
| S04 前中部深绿卵叶灌木丛 | [查看](assets/S04/S04__original.png) | [查看](assets/S04/S04__front.png) | [查看](assets/S04/S04__side.png) | [查看](assets/S04/S04__top.png) | [记录](assets/S04/检查记录.md) |
| S05 最前方中央蓝绿低灌木丛 | [查看](assets/S05/S05__original.png) | [查看](assets/S05/S05__front.png) | [查看](assets/S05/S05__side.png) | [查看](assets/S05/S05__top.png) | [记录](assets/S05/检查记录.md) |
| S06 左侧树桩前下方亮绿小灌木 | [查看](assets/S06/S06__original.png) | [查看](assets/S06/S06__front.png) | [查看](assets/S06/S06__side.png) | [查看](assets/S06/S06__top.png) | [记录](assets/S06/检查记录.md) |
| S07 木堆右下方深绿矮灌木 | [查看](assets/S07/S07__original.png) | [查看](assets/S07/S07__front.png) | [查看](assets/S07/S07__side.png) | [查看](assets/S07/S07__top.png) | [记录](assets/S07/README.md) |
| ST01 左前大树桩 | [查看](assets/ST01/ST01__original.png) | [查看](assets/ST01/ST01__front.png) | [查看](assets/ST01/ST01__side.png) | [查看](assets/ST01/ST01__top.png) | [记录](assets/ST01/检查记录.md) |
| ST02 营地中部工具树桩 | [查看](assets/ST02/ST02__original.png) | [查看](assets/ST02/ST02__front.png) | [查看](assets/ST02/ST02__side.png) | [查看](assets/ST02/ST02__top.png) | [记录](assets/ST02/ST02__检查记录.md) |
| ST03 右侧劈柴大树桩 | [查看](assets/ST03/ST03__original.png) | [查看](assets/ST03/ST03__front.png) | [查看](assets/ST03/ST03__side.png) | [查看](assets/ST03/ST03__top.png) | [记录](assets/ST03/检查记录.md) |
| ST04 最前景根部外张树桩 | [查看](assets/ST04/ST04__original.png) | [查看](assets/ST04/ST04__front.png) | [查看](assets/ST04/ST04__side.png) | [查看](assets/ST04/ST04__top.png) | [记录](assets/ST04/检查记录.md) |
| ST05 木堆右侧小树桩 | [查看](assets/ST05/ST05__original.png) | [查看](assets/ST05/ST05__front.png) | [查看](assets/ST05/ST05__side.png) | [查看](assets/ST05/ST05__top.png) | [记录](assets/ST05/检查记录.md) |
| T01 后左粗干大阔叶树 | [查看](assets/T01/T01__original.png) | [查看](assets/T01/T01__front.png) | [查看](assets/T01/T01__side.png) | [查看](assets/T01/T01__top.png) | [记录](assets/T01/检查记录.md) |
| T02 后排中左针叶树 | [查看](assets/T02/T02__original.png) | [查看](assets/T02/T02__front.png) | [查看](assets/T02/T02__side.png) | [查看](assets/T02/T02__top.png) | [记录](assets/T02/检查记录.md) |
| T03 后排中央小亮绿阔叶树 | [查看](assets/T03/T03__original.png) | [查看](assets/T03/T03__front.png) | [查看](assets/T03/T03__side.png) | [查看](assets/T03/T03__top.png) | [记录](assets/T03/检查记录.md) |
| T04 后排右中深绿阔叶树 | [查看](assets/T04/T04__original.png) | [查看](assets/T04/T04__front.png) | [查看](assets/T04/T04__side.png) | [查看](assets/T04/T04__top.png) | [记录](assets/T04/检查记录.md) |
| T05 后排右侧针叶树 | [查看](assets/T05/T05__original.png) | [查看](assets/T05/T05__front.png) | [查看](assets/T05/T05__side.png) | [查看](assets/T05/T05__top.png) | [记录](assets/T05/检查记录.md) |
| T06 后部中央亮绿圆冠树 | [查看](assets/T06/T06__original.png) | [查看](assets/T06/T06__front.png) | [查看](assets/T06/T06__side.png) | [查看](assets/T06/T06__top.png) | [记录](assets/T06/检查记录.md) |
| T07 后左大树右前方蓝绿树 | [查看](assets/T07/T07__original.png) | [查看](assets/T07/T07__front.png) | [查看](assets/T07/T07__side.png) | [查看](assets/T07/T07__top.png) | [记录](assets/T07/检查记录.md) |
| T08 左后边缘小绿树 | [查看](assets/T08/T08__original.png) | [查看](assets/T08/T08__front.png) | [查看](assets/T08/T08__side.png) | [查看](assets/T08/T08__top.png) | [记录](assets/T08/检查记录.md) |
| T09 左后亮绿小圆冠树 | [查看](assets/T09/T09__original.png) | [查看](assets/T09/T09__front.png) | [查看](assets/T09/T09__side.png) | [查看](assets/T09/T09__top.png) | [记录](assets/T09/检查记录.md) |
| T10 后右粗干亮绿大树 | [查看](assets/T10/T10__original.png) | [查看](assets/T10/T10__front.png) | [查看](assets/T10/T10__side.png) | [查看](assets/T10/T10__top.png) | [记录](assets/T10/检查记录.md) |
| T11 后中央针叶树 | [查看](assets/T11/T11__original.png) | [查看](assets/T11/T11__front.png) | [查看](assets/T11/T11__side.png) | [查看](assets/T11/T11__top.png) | [记录](assets/T11/检查记录.md) |
| T12 右中后方蓝绿高树 | [查看](assets/T12/T12__original.png) | [查看](assets/T12/T12__front.png) | [查看](assets/T12/T12__side.png) | [查看](assets/T12/T12__top.png) | [记录](assets/T12/检查记录.md) |
| T13 中后部蓝绿圆冠树 | [查看](assets/T13/T13__original.png) | [查看](assets/T13/T13__front.png) | [查看](assets/T13/T13__side.png) | [查看](assets/T13/T13__top.png) | [记录](assets/T13/检查记录.md) |
| T14 左中针叶树 | [查看](assets/T14/T14__original.png) | [查看](assets/T14/T14__front.png) | [查看](assets/T14/T14__side.png) | [查看](assets/T14/T14__top.png) | [记录](assets/T14/检查记录.md) |
| T15 中部亮绿小树 | [查看](assets/T15/T15__original.png) | [查看](assets/T15/T15__front.png) | [查看](assets/T15/T15__side.png) | [查看](assets/T15/T15__top.png) | [记录](assets/T15/检查记录.md) |
| T16 左边缘中部小绿树 | [查看](assets/T16/T16__original.png) | [查看](assets/T16/T16__front.png) | [查看](assets/T16/T16__side.png) | [查看](assets/T16/T16__top.png) | [记录](assets/T16/检查记录.md) |
| T17 中左小阔叶树 | [查看](assets/T17/T17__original.png) | [查看](assets/T17/T17__front.png) | [查看](assets/T17/T17__side.png) | [查看](assets/T17/T17__top.png) | [记录](assets/T17/检查记录.md) |
| T18 中央浓绿圆冠树 | [查看](assets/T18/T18__original.png) | [查看](assets/T18/T18__front.png) | [查看](assets/T18/T18__side.png) | [查看](assets/T18/T18__top.png) | [记录](assets/T18/检查记录.md) |
| T19 中右小蓝绿树 | [查看](assets/T19/T19__original.png) | [查看](assets/T19/T19__front.png) | [查看](assets/T19/T19__side.png) | [查看](assets/T19/T19__top.png) | [记录](assets/T19/检查记录.md) |
| T20 中右绿冠树 | [查看](assets/T20/T20__original.png) | [查看](assets/T20/T20__front.png) | [查看](assets/T20/T20__side.png) | [查看](assets/T20/T20__top.png) | [记录](assets/T20/检查记录.md) |
| T21 左中前景巨大阔叶树 | [查看](assets/T21/T21__original.png) | [查看](assets/T21/T21__front.png) | [查看](assets/T21/T21__side.png) | [查看](assets/T21/T21__top.png) | [记录](assets/T21/检查记录.md) |
| T22 右侧营地后方亮绿树 | [查看](assets/T22/T22__original.png) | [查看](assets/T22/T22__front.png) | [查看](assets/T22/T22__side.png) | [查看](assets/T22/T22__top.png) | [记录](assets/T22/检查记录.md) |
| T23 营地左后蓝绿大树 | [查看](assets/T23/T23__original.png) | [查看](assets/T23/T23__front.png) | [查看](assets/T23/T23__side.png) | [查看](assets/T23/T23__top.png) | [记录](assets/T23/检查记录.md) |
| T24 中央前景亮绿大树 | [查看](assets/T24/T24__original.png) | [查看](assets/T24/T24__front.png) | [查看](assets/T24/T24__side.png) | [查看](assets/T24/T24__top.png) | [记录](assets/T24/检查记录.md) |
| T25 左中下部小针叶树 | [查看](assets/T25/T25__original.png) | [查看](assets/T25/T25__front.png) | [查看](assets/T25/T25__side.png) | [查看](assets/T25/T25__top.png) | [记录](assets/T25/检查记录.md) |
| T26 左前中部深绿窄冠树 | [查看](assets/T26/T26__original.png) | [查看](assets/T26/T26__front.png) | [查看](assets/T26/T26__side.png) | [查看](assets/T26/T26__top.png) | [记录](assets/T26/检查记录.md) |
| T27 前中偏左蓝绿树 | [查看](assets/T27/T27__original.png) | [查看](assets/T27/T27__front.png) | [查看](assets/T27/T27__side.png) | [查看](assets/T27/T27__top.png) | [记录](assets/T27/检查记录.md) |
| T28 伐木区左侧亮绿小树 | [查看](assets/T28/T28__original.png) | [查看](assets/T28/T28__front.png) | [查看](assets/T28/T28__side.png) | [查看](assets/T28/T28__top.png) | [记录](assets/T28/检查记录.md) |
| T29 左前景亮绿大树 | [查看](assets/T29/T29__original.png) | [查看](assets/T29/T29__front.png) | [查看](assets/T29/T29__side.png) | [查看](assets/T29/T29__top.png) | [记录](assets/T29/检查记录.md) |
| T30 右边缘小蓝绿树 | [查看](assets/T30/T30__original.png) | [查看](assets/T30/T30__front.png) | [查看](assets/T30/T30__side.png) | [查看](assets/T30/T30__top.png) | [记录](assets/T30/检查记录.md) |

本批只新增参考图和检查资料，未接入游戏、未制作真实三维模型、未变更游戏版本或构建发布包。

