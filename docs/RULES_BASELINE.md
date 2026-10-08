# 规则来源基线 v001

2026-10-08，用户确认：先使用官方 2025 规则和明确标注的估算尺寸完成 M0，实体参考后补。这是开发基线的确认；用户拥有的实体版本、中文印刷名称、照片和实测尺寸仍未知。尺寸及美术替代基线见 [REFERENCE_BASELINE.md](REFERENCE_BASELINE.md)。

## 已锁定来源

来源入口为 [CATAN 官方规则目录](https://www.catan.com/understand-catan/game-rules)。本次选择目录当前区的英文基础版、航海家及城市与骑士；该目录将 2025 年以前的规则另列于 Archive。三份原文的版权页均标示 2025 和第六版编辑信息。

| 来源 ID | 官方原文 | 页数 | 版本证据位置 | 用途 |
| --- | --- | --- | --- | --- |
| `catan-base-2025-en` | [CATAN - The Game](https://www.catan.com/sites/default/files/2025-03/CN3081%20CATAN%E2%80%93The%20Game%20Rulebook%20secure%20%281%29.pdf) | 12 | PDF 第 1 页标题、第 12 页版权/第六版 | M1 子集来源、M2 完整规则来源 |
| `catan-seafarers-2025-en` | [CATAN - Seafarers](https://www.catan.com/sites/default/files/2025-03/CN3083%20CATAN%E2%80%93Seafarers%20Rulebook%202025%20secured%20reduced.pdf) | 20 | PDF 第 1 页标题、第 20 页版权/第六版 | M3 来源已锁；逐条规则与剧本核验待 M3 |
| `catan-cities-knights-2025-en` | [CATAN - Cities & Knights](https://www.catan.com/sites/default/files/2025-03/CN3087%20CATAN%E2%80%93Cities%26Knights_%20Rulebook.pdf) | 16 | PDF 第 1 页标题、第 16 页版权/第六版 | M4 来源已锁；逐条规则与卡牌核验待 M4 |

访问日期为 **2026-10-08（Asia/Shanghai）**。准确 URL、字节数、SHA-256、页数、已核验范围集中登记于 [sources.json](../assets/references/rules/v001/sources.json)。页码均为从 1 起计的 PDF 页码；此处基础版规则所引用页码与印刷页码一致。

原始 PDF 保存在 `.local/reference-cache/official-2025/v001/`，保持下载字节不变，不将大二进制加入 Git。可从项目根目录恢复或离线校验：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Fetch-RuleReferences.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Fetch-RuleReferences.ps1 -VerifyOnly
```

脚本使用登记的固定 URL，核对字节数与 SHA-256 后才接纳文件；已存在的文件不重新下载。原件或新下载的哈希不符时停止并保留文件，不自动改写来源记录。官方文件更新时另建 `v002` 来源目录、差异记录和缓存，旧版本继续可识别。

## 规则版本与 M1 边界

完整基础版规则 ID 固定为 `catan-base-2025-en-v1`；M1 使用独立子集 ID `catan-m1-slice-v1`。来源锁定不表示规则已经实现。存档同时保存规则 ID 与场景版本，不能把 M1 存档无条件解释成完整基础版存档。

M1 验收采用自编固定地图与固定 4 席位行动序列，**不是**原文第 4 页的新手固定布局。受控骰点、固定先手与预定落点是可复核的测试输入；不声称这些测试安排属于正式游戏规则。

| M1 行为 | 基础版来源定位 | 切片界限 |
| --- | --- | --- |
| 有限资源与棋子供应 | 第 3 页组件 | 使用真实有限数量，检查资源/棋子守恒 |
| 开局摆放与第二处定居点资源 | 第 12 页步骤 7、8 | 固定先手，按正序再逆序摆放；距离限制生效 |
| 掷骰产出与回合推进 | 第 6 页回合/产出 | 验收输入首轮骰点 2 + 3；不进入 7 的分支 |
| 一次银行 4:1 交易 | 第 7 页交易 | 有限银行供应；玩家间交易、港口优惠留至 M2 |
| 一次道路建造、占用和连通检查 | 第 8 页建造 | 扣除 1 木材和 1 砖，空边与道路连接条件生效 |
| 保存恢复、命令去重与信息权限 | 项目工程约定 | 工程要求，不伪称规则书条文 |

完整行动和预期状态由 [M1 固定场景数据](acceptance/v0.1/scenario.json)及对应验收文档固定。M1 不支持完整胜利流程、7/弃牌/强盗行动、发展卡、港口优惠、玩家间交易、最长道路、最大骑士军队及扩展规则；未实现行动必须明确拒绝，不得悄悄套用自创规则。定居点后续建造、城市升级和其他 M2 边界案例在 M2 清单中补齐。

## 中文工作译名

这些是开发文档和界面的工作译名，**不是用户实体中文版本已确认的印刷名称**。结构化数据使用稳定英文 ID；收到中文实体参考后可以改显示名而不改变存档中的资源/组件 ID。

| 英文词/稳定 ID | 中文工作译名 |
| --- | --- |
| Lumber / `wood` | 木材 |
| Brick / `brick` | 砖 |
| Wool / `wool` | 羊毛 |
| Grain / `wheat` | 谷物 |
| Ore / `ore` | 矿石 |
| Settlement / `settlement` | 定居点（文档可简称村） |
| City / `city` | 城市 |
| Road / `road` | 道路 |
| Robber / `robber` | 强盗 |
| Development card / `development-card` | 发展卡 |
| Seafarers | 航海家 |
| Cities & Knights | 城市与骑士 |

## 后续差异与验收登记

| 阶段 | 开始实现前需要登记的内容 | 通过后才可声称 |
| --- | --- | --- |
| M2 | 基础版完整条款清单；逐项来源页码；边界案例和预期状态；M1 未覆盖项 | 基础版从开局到胜利完整可玩 |
| M3 | 航海新增条款和逐个剧本版本/初始布局/计分；与基础版冲突的明确覆盖顺序 | 通过清单内的单扩展剧本 |
| M4 | 城市与骑士条款、卡牌数量/效果、结算顺序与待决选择；独立案例 | 通过清单内的卡牌和流程 |
| M5 | 适用于当前版本的官方组合来源及快照；兼容剧本表、跨扩展冲突与专项案例 | 明确标为支持的组合剧本 |

每条差异使用稳定 `diffId`，记录旧来源/新来源及页码、差异摘要、适用版本和剧本、用户取舍（如需要）、实施状态、验收案例 ID、存档迁移策略。尚未查清的条款标为待核验，不能用其他版本惯例静默填空。规则变更只有在独立案例、非法操作检查和不变量检查通过后才更新实现支持状态。

实体资料到位后保留原件并新增参考版本，比较其与本基线的差异；不覆盖这次确认，也不假定实物一定是 2025 版。收藏版/3D 收藏版在本阶段仅提供外观方向，相关额外变体不会自动进入规则包。5–6 人、旧版规则和双扩展组合细则也不因下载或提及而启用。

## M0 核验记录

已完成：三份官方 PDF 下载、SHA-256/字节数核对及解析页数；基础版第 8、12 页图文核对由验收场景任务完成；两个扩展的标题与版权页已渲染检查。原始规则数据仅作为资料读取。

`Fetch-RuleReferences.ps1` 已在 Windows PowerShell 5.1 下实际运行：已有缓存的常规模式、`-VerifyOnly` 模式、隔离空缓存的三份原文重新下载均通过；向隔离目录放入错误文件后脚本以非零状态退出，输入文件哈希保持不变。测试脚本副本和缓存位于 `.local/development/temp/m0-rule-fetch-smoke-v001/` 和 `.local/development/temp/m0-rule-hash-rejection-v001/`，不改变正式原件。

尚未验证：两个扩展的逐条规则、全部场景/卡牌及组合兼容；实物对应版本和中文印刷名称。此记录不代表完成 M1 实现或游戏试玩。
