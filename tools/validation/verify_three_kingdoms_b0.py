"""检查B0资料完整性、覆盖与链接；不执行游戏规则或声称运行验收。"""

import argparse
import hashlib
import json
import re
from pathlib import Path
from urllib.parse import unquote


ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
FIRST = [1, 6, 9, 12, 15, 22, 23, 28, 37]
MAPS = ["heading-for-new-shores", "four-islands", "fog-islands",
        "through-the-desert", "forgotten-tribe", "cloth-for-catan",
        "pirate-islands", "wonders-of-catan", "new-world"]


def digest(text):
    return hashlib.sha256(text.strip().encode("utf-8")).hexdigest()


def read(path):
    return path.read_text(encoding="utf-8-sig")


def source_inventory(text):
    section = text.split("## 三、武将与技能\n", 1)[1].split("## 四、机制索引", 1)[0]
    entries = list(re.finditer(r"^### (\d{2})．(.+)$", section, re.M))
    generals = []
    for i, entry in enumerate(entries):
        block = section[entry.start():entries[i + 1].start() if i + 1 < len(entries) else len(section)]
        original = block.split("**待确认", 1)[0]
        names = re.findall(r"^\*\*(.+?)\*\*$", original, re.M)
        notes = [name for name in names if name in ("榻谟：板块交换说明", "黄忠专属摧毁规则")]
        generals.append({"编号": int(entry[1]), "标题": entry[2],
                         "技能": [name for name in names if name not in notes],
                         "附注": notes, "原文哈希": digest(original),
                         "含问题建议的条目哈希": digest(block)})
    rows = re.findall(r"^\| (G\d{2})\s*\|.+$", text, re.M)
    questions = {}
    for line in text.splitlines():
        match = re.match(r"^\| (G\d{2})\s*\|", line)
        if match:
            questions[match[1]] = digest(line)
    return {"武将": generals, "全局原问题及答复行哈希": questions,
            "全局表编号": rows}


def verify():
    baseline = json.loads(read(DOCS / "three-kingdoms/B0原始资料指纹.json"))
    encyclopedia = read(DOCS / "三国争霸游戏百科.md")
    inventory = source_inventory(encyclopedia)
    checks = []

    def require(condition, label):
        if not condition:
            raise ValueError(label)
        checks.append(label)

    require(inventory == baseline["原始资料"], "54将原文、附注、逐将问题建议及20项全局原问题答复与本轮前快照一致")
    generals = inventory["武将"]
    require([g["编号"] for g in generals] == list(range(1, 55)), "武将01至54无缺失、重复或换序")
    require(sum(len(g["技能"]) for g in generals) == 97, "97个具名技能原文保留")
    require(sum(len(g["附注"]) for g in generals) == 2, "黄忠与鲁肃两条原附注保留")
    for prefix, count in [("G", 20), ("W", 54)]:
        found = re.findall(r"^\*\*建议答复 (" + prefix + r"\d{2})(?:｜[^*]+)?\*\*$", encyclopedia, re.M)
        require(sorted(found) == [f"{prefix}{i:02}" for i in range(1, count + 1)],
                f"{count}项{prefix}第一轮建议各保留一次")

    main = read(DOCS / "three-kingdoms/B0规则定稿.md")
    first = read(DOCS / "three-kingdoms/B0首批武将规范.md")
    maps = read(DOCS / "three-kingdoms/B0九图兼容与冲突例.md")
    plan = read(DOCS / "三国争霸扩展实现方案.md")
    global_ids = re.findall(r"^\| (G\d{2}) \|", main, re.M)
    require(global_ids == [f"G{i:02}" for i in range(1, 21)], "G01至G20各有一项规范处置")
    deferred = re.findall(r"^\| W(\d{2}) \| (B\d) \|", main, re.M)
    require([int(n) for n, _ in deferred] == [i for i in range(1, 55) if i not in FIRST],
            "45张后批与9张首批恰好覆盖54将，互不重叠")
    phases = {int(n): phase for n, phase in re.findall(r"^\| (\d{2}) [^|]+\|[^|]+\| (B\d) \|", plan, re.M)}
    require(len(phases) == 54 and all(phases[i] == "B2" for i in FIRST), "首批正好覆盖原方案全部B2武将")
    require(all(phases[int(n)] == phase for n, phase in deferred), "后批隔离登记与原方案批次一致")
    headings = re.findall(r"^#{2,4}[^\n]*\bW(\d{2})\b[^\n]*$", first, re.M)
    require(sorted(set(int(n) for n in headings)) == FIRST, "首批逐将规范标题覆盖9将")
    for general in generals:
        if general["编号"] in FIRST:
            require(all(name in first for name in general["技能"]), f"W{general['编号']:02}全部技能出现在首批规范")
    require(sum(len(g["技能"]) for g in generals if g["编号"] in FIRST) == 12, "首批9将共有12个完整技能")
    skill_cases = re.findall(r"^\| ((?:W\d{2}|B2)-[^ |]+) \|", first, re.M)
    require(len(skill_cases) == len(set(skill_cases)), "首批技能及交叉案例ID不重复")
    for prefix in ["W01", "W06", "W09", "W12", "W15", "W22", "W23-L", "W23-Q",
                   "W28-S", "W28-K", "W37-T", "W37-Y"]:
        require(all(any(case.startswith(f"{prefix}-{kind}") for case in skill_cases)
                    for kind in ["N", "I", "Z", "B", "T", "R"]),
                f"{prefix}具备正常、非法、短缺、边界、任次、恢复六类案例入口")
    map_rows = re.findall(r"^\| `([a-z-]+)`／[^\n]+$", maps, re.M)
    require(map_rows == MAPS, "九图稳定ID及顺序与当前支持清单一致")
    require(len(re.findall(r"^\| `[a-z-]+`／[^|]+\| 3／4；", maps, re.M)) == 9,
            "九图各自明确3／4人，共18种组合配置")
    cases = re.findall(r"^### (B0-X\d{2})：", maps, re.M)
    require(cases == [f"B0-X{i:02}" for i in range(1, 11)], "10组冲突案例编号连续且唯一")
    require("非法" in first and "恢复" in first and "隐退" in first and "短缺" in first,
            "首批文档包含非法、恢复、任次与供应边界入口（语义另经交叉审查）")

    # 这里只核对选将合同的数量推导，不模拟游戏抽样或结算。
    for players, minimum in [(3, 9), (4, 11)]:
        remaining = minimum
        for seat in range(1, players + 1):
            require(remaining >= (5 if seat == players else 4),
                    f"{players}人{minimum}张池第{seat}席可发足候选")
            remaining -= 2
        require(minimum - 1 - 2 * (players - 1) < 5, f"{players}人少一张时末席确实不足5候选")

    checked_files = [DOCS / "三国争霸游戏百科.md", DOCS / "三国争霸扩展实现方案.md",
                     *sorted((DOCS / "three-kingdoms").glob("B0*.md")),
                     DOCS / "verification/three-kingdoms-b0/验收记录.md"]
    links = 0
    for path in checked_files:
        content = read(path)
        for target in re.findall(r"\[[^\]\n]*\]\(([^)]+)\)", content):
            if target.startswith(("http:", "https:", "#", "mailto:")):
                continue
            local = unquote(target.split("#", 1)[0].strip("<>"))
            if local:
                if not (path.parent / local).exists():
                    raise ValueError(f"链接不存在：{path.relative_to(ROOT)} → {target}")
                links += 1
    checks.append(f"{len(checked_files)}份文档的{links}个本地链接目标存在")
    return {"状态": "通过", "规范版本": "three-kingdoms-b0-v1", "检查项数": len(checks),
            "逐技及交叉案例数": len(skill_cases), "地图冲突案例数": len(cases),
            "检查": checks, "游戏规则运行测试": "未执行", "Unity构建": "未执行",
            "范围": "资料指纹、编号覆盖、将池数量推导及链接检查；冲突语义另外交叉审阅，不代表游戏实现通过"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="把文档检查结果保存为JSON")
    args = parser.parse_args()
    try:
        result = verify()
    except (ValueError, KeyError, IndexError, OSError) as error:
        result = {"状态": "失败", "原因": str(error)}
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_bytes((json.dumps(result, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["状态"] == "通过" else 1


if __name__ == "__main__":
    raise SystemExit(main())
