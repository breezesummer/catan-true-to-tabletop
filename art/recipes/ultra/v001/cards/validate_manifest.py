"""校验 U0 牌面盘点、源码映射和本地来源；不修改游戏或生成任何牌面。"""

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import sys


ROOT = Path(__file__).resolve().parents[5]


def text(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def digest(path):
    return hashlib.sha256((ROOT / path).read_bytes()).hexdigest()


def require(value, message):
    if not value:
        raise ValueError(message)


def enum_members(source, name):
    match = re.search(r"\benum\s+" + name + r"\s*\{([^}]+)\}", source, re.S)
    require(match is not None, "无法找到规则枚举：" + name)
    return [item.strip() for item in match.group(1).split(",") if item.strip()]


def progress_counts(source, members):
    victory = re.search(r"bool IsVictoryPoint.*?\{(.*?)\}", source, re.S).group(1)
    victory_names = re.findall(r"ProgressCardKind\.(\w+)", victory)
    body = re.search(r"int Count.*?\{(.*?)\}", source, re.S).group(1)
    default = int(re.findall(r"return (\d+);", body)[-1])
    result = {name: default for name in members}
    for condition, amount in re.findall(r"if \((.*?)\) return (\d+);", body):
        targets = re.findall(r"ProgressCardKind\.(\w+)", condition)
        if "IsVictoryPoint(card)" in condition:
            targets += victory_names
        for target in targets:
            result[target] = int(amount)
    return result


def verify(manifest, check_rule_cache=False):
    cards = manifest["cards"]
    by_id = {card["id"]: card for card in cards}
    require(len(by_id) == len(cards), "牌种 ID 重复")
    backs = {back["id"]: back for back in manifest["sharedBacks"]}
    require(len(backs) == len(manifest["sharedBacks"]), "牌背 ID 重复")
    categories = Counter(card["category"] for card in cards)
    require(categories == {
        "resource": 5, "commodity": 3, "development": 5, "progress": 25,
        "award": 3, "improvement": 15, "guidance": 2, "wonder": 5, "player-aid": 3,
    }, "牌类覆盖数与 U0 核对结果不符")
    texture_paths = set()
    for card in cards:
        for field in ("nameZh", "ruleNameEn", "rulePacks", "representation", "quantity",
                      "rules", "effectZh", "chineseTextSources", "privacyPolicy",
                      "displayEntrypoints", "productionStatus", "runtimeStatus"):
            require(bool(card.get(field)), card["id"] + " 缺少字段：" + field)
        require("victoryPoints" in card and "cost" in card, card["id"] + " 缺少分值/费用")
        require(card["privacyPolicy"] in manifest["privacyPolicies"], "未定义的信息权限")
        require(all(key in manifest["displayEntrypoints"]
                    for key in card["displayEntrypoints"]), card["id"] + " 展示入口未登记")
        require(card["existingAssets"]["hiddenBackId"] in backs
                or card["existingAssets"]["hiddenBackId"] is None, "牌背映射缺失")
        for asset in (card["existingAssets"]["front"], card["existingAssets"]["reverse"]):
            if asset:
                require((ROOT / asset).is_file(), "运行资产不存在：" + asset)
                texture_paths.add(asset)
        for reference in card["referencePaths"]:
            require(reference in manifest["referenceRegistry"], "照片来源未登记：" + reference)
        if card["category"] == "player-aid":
            require(card["runtimeStatus"] == "reference-only/missing-in-runtime",
                    "官方辅助板不得冒充当前运行卡牌")
            require(card["ultraAssets"]["frontId"] is None
                    and card["ultraAssets"]["reverseId"] is None,
                    "仅参考辅助板不得创建新的运行牌面 ID")
        if card["category"] in ("resource", "commodity"):
            require(card["existingAssets"]["hiddenBackId"] == "hand-hidden",
                    "资源商品隐藏背面必须一致")
        if card["category"] == "development":
            require(card["existingAssets"]["hiddenBackId"] == "development-hidden",
                    "基础发展牌隐藏背面必须一致")
    for back in backs.values():
        for path in back["existingPaths"]:
            require((ROOT / path).is_file(), "牌背运行资产不存在：" + path)
            texture_paths.add(path)
    for entry in manifest["displayEntrypoints"].values():
        require((ROOT / entry["path"]).is_file(), "展示入口文件不存在：" + entry["path"])
    for path, expected in manifest["sourceSnapshots"].items():
        require(digest(path) == expected, "盘点后源码发生变化，须重新核对：" + path)
    for path, reference in manifest["referenceRegistry"].items():
        require(digest(path) == reference["sha256"], "原始照片校验值不符：" + path)

    def check_rules(value):
        if isinstance(value, dict):
            if "pdfPages" in value:
                source = manifest["ruleSources"].get(value["source"])
                require(source is not None, "未登记规则来源")
                require(all(isinstance(page, int) and 1 <= page <= source["pdfPageCount"]
                            for page in value["pdfPages"]), "PDF 页码越界")
            for child in value.values():
                check_rules(child)
        elif isinstance(value, list):
            for child in value:
                check_rules(child)

    check_rules(manifest)
    locks = {source["id"]: source for source in json.loads(
        text("assets/references/rules/v001/sources.json"))["sources"]}
    for source_id, source in manifest["ruleSources"].items():
        require(source["sha256"] == locks[source_id]["sha256"], "规则锁与本地索引不一致")
        if check_rule_cache:
            require(digest(source["path"]) == source["sha256"], "本地 PDF 原件校验失败")

    progress = [card for card in cards if card["category"] == "progress"]
    development = [card for card in cards if card["category"] == "development"]
    for milestone in (4, 5):
        source = text(f"src/Catan.Core/M{milestone}ProgressModels.cs")
        members = enum_members(source, "ProgressCardKind")
        require([card["ruleEnum"] for card in progress] == members, "进步牌枚举覆盖或顺序不符")
        counts = progress_counts(source, members)
        for index, card in enumerate(progress):
            require(card["ruleOrdinal"] == index, "进步牌稳定序号不符")
            require(card["quantity"]["perGame"] == counts[card["ruleEnum"]],
                    card["id"] + " 张数与源码不符")
            expected_track = "science" if index < 10 else "trade" if index < 16 else "politics"
            require(card["quantity"]["track"] == expected_track, "进步牌颜色分类不符")
        ui = text(f"Unity/Catan/Assets/Scripts/M{milestone}App.cs")
        help_match = re.search(r"\bCardHelp\s*=\s*\{(.*?)\};", ui, re.S)
        help_text = re.findall(r'"((?:[^"\\]|\\.)*)"', help_match.group(1))
        pack = "cities-knights" if milestone == 4 else "combined"
        require([card["effectByRulePack"][pack] for card in progress] == help_text,
                "进步牌中文效果与当前展示源不符")
    for milestone in (2, 3):
        members = enum_members(text(f"src/Catan.Core/M{milestone}Models.cs"),
                               "DevelopmentCardKind")
        require([card["ruleEnum"] for card in development] == members, "基础发展牌枚举覆盖不符")

    summary = manifest["summary"]
    require(summary["frontDefinitions"] == len(cards) == 66, "总定义数不符")
    runtime_count = sum(card["runtimeStatus"] != "reference-only/missing-in-runtime" for card in cards)
    require(summary["currentRuntimeRedrawDefinitions"] == runtime_count == 63, "运行展示重绘数不符")
    require(summary["referenceOnlyDefinitions"] == 3, "仅参考辅助板计数不符")
    require(summary["handCardKinds"] == 38, "实际手牌种类数不符")
    require(summary["publicReverseDefinitions"] == sum(
        bool(card["ultraAssets"]["reverseId"]) for card in cards) == 15, "翻板状态面计数不符")
    require(summary["existingUniqueTexturePaths"] == len(texture_paths), "已有贴图去重数不符")
    require(summary["sharedHiddenBackDefinitions"] == len(backs) == 6, "共享牌背定义数不符")
    for category, field, expected in [
        ("resource", "resourceSupply", 95), ("commodity", "commoditySupply", 36),
        ("development", "baseDevelopmentDeck", 25), ("progress", "progressDeck", 54),
    ]:
        require(sum(card["quantity"]["perGame"] for card in cards
                    if card["category"] == category) == summary[field] == expected,
                category + " 牌库/供应总数不符")
    track_counts = Counter()
    for card in progress:
        track_counts[card["quantity"]["track"]] += card["quantity"]["perGame"]
    require(dict(track_counts) == summary["progressDeckByTrack"]
            == {"science": 18, "trade": 18, "politics": 18}, "三色进步牌库不符")
    for players, expected in [(3, 20), (4, 25)]:
        total = sum(card["quantity"]["scenarioOverrides"][f"pirate-islands-{players}"]
                    for card in development)
        require(total == expected, "海盗群岛发展牌数量不符")
    require(summary["forgottenTribeGiftCards"] == {
        "count": 4, "includedInBaseDevelopment25": True, "initialRemainingDeck": 21,
    }, "部落奖励重复计入牌库")
    require(len(manifest["scenarioAudit"]) == 9, "航海剧本核对遗漏")
    require(summary["ultraProducedFaces"] == 0
            and all(card["ultraAssets"]["frontPath"] is None for card in cards),
            "U0 清单不能声称 ultra 牌面已制作")
    return {
        "结果": "通过",
        "清单定义": len(cards),
        "当前运行展示重绘项": runtime_count,
        "仅参考辅助板": 3,
        "实际手牌种类": 38,
        "运行贴图去重数": len(texture_paths),
        "原始照片": len(manifest["referenceRegistry"]),
        "源码快照": len(manifest["sourceSnapshots"]),
        "展示入口": len(manifest["displayEntrypoints"]),
        "本地PDF原件": "已校验" if check_rule_cache else "未要求校验缓存",
        "说明": "仅验证清单完整性与静态一致性，不表示主题、美术、双端可读性或性能通过。",
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify-rule-cache", action="store_true", help="同时校验三份本地 PDF 原件")
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    try:
        manifest = json.loads(Path(__file__).with_name("card-manifest.json").read_text(encoding="utf-8"))
        print(json.dumps(verify(manifest, args.verify_rule_cache), ensure_ascii=False, indent=2))
    except (ValueError, KeyError, OSError, AttributeError) as error:
        print("牌面清单校验失败：" + str(error), file=sys.stderr)
        sys.exit(1)
