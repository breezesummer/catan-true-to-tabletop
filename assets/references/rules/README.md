# 本地规则索引

沿用用户已确认的官方 2025 英文版开发基线。规则版本、适用范围和中文工作译名见 [规则来源基线](../../../docs/RULES_BASELINE.md)。原始文件保持下载字节不变。

## 规则书

三份 PDF 保存在项目根目录下的 `.local/reference-cache/official-2025/v001/`。

| 内容 | 本地原文 | PDF 页数 |
| --- | --- | --- |
| 基础版 | [catan-base-2025-en.pdf](../../../.local/reference-cache/official-2025/v001/catan-base-2025-en.pdf) | 12 |
| 航海家 | [catan-seafarers-2025-en.pdf](../../../.local/reference-cache/official-2025/v001/catan-seafarers-2025-en.pdf) | 20 |
| 城市与骑士 | [catan-cities-knights-2025-en.pdf](../../../.local/reference-cache/official-2025/v001/catan-cities-knights-2025-en.pdf) | 16 |

官方下载入口：[CATAN 官方规则目录](https://www.catan.com/understand-catan/game-rules)。各 PDF 的固定下载地址、字节数和 SHA-256 见 [来源清单](v001/sources.json)。

## 双扩展组合补充

组合条款首先查城市与骑士规则书第 12 页；项目支持范围和覆盖顺序见 [M5 组合规则](../../../docs/M5_COMBINATION_RULES.md)。以下官方网页快照用于与已锁定规则书一致的补充说明。

| 内容 | 本地原文快照 |
| --- | --- |
| 城市与骑士组合说明 | [cities-knights-combinations.html](m5-v001/originals/cities-knights-combinations.html) |
| 城市与骑士常见问题 | [cities-knights-faq.html](m5-v001/originals/cities-knights-faq.html) |
| 航海家常见问题 | [seafarers-faq.html](m5-v001/originals/seafarers-faq.html) |

快照下载地址、日期、字节数和 SHA-256 见 [组合来源清单](m5-v001/sources.json)。

## 引用与本地恢复

- 引用规则时注明来源文件或来源 ID，以及从 1 起计的 PDF 页码；引用网页快照时注明文件名和对应问题或小节。
- PDF 缓存不纳入 Git；缺失时在项目根目录运行以下命令下载固定版本。脚本校验字节数和 SHA-256，发现不匹配时保留文件并停止。
- 官方资料发生变化时另存新版本，并核对差异后再决定是否更改开发基线。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Fetch-RuleReferences.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Fetch-RuleReferences.ps1 -VerifyOnly
```

2026-10-14（Asia/Shanghai）本地校验：三份 PDF 与三份 HTML 快照的字节数和 SHA-256 均与各自来源清单一致。
