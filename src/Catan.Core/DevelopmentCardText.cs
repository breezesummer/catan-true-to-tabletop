using System;

namespace Catan.Core
{
    /// <summary>实体发展卡的名称与牌面说明；只提供静态文字，不参与规则裁决或手牌投影。</summary>
    public static class DevelopmentCardText
    {
        // 顺序对应 M4/M5 ProgressCardKind，英文 ID、枚举和存档保持稳定。
        public static readonly string[] Ids = {
            "Alchemy",
            "Crane",
            "Engineering",
            "Invention",
            "Irrigation",
            "Medicine",
            "Mining",
            "Printing",
            "RoadBuilding",
            "Smithing",
            "CommercialHarbor",
            "GuildDues",
            "Merchant",
            "MerchantFleet",
            "ResourceMonopoly",
            "TradeMonopoly",
            "Diplomacy",
            "Espionage",
            "Encouragement",
            "Intrigue",
            "Taxation",
            "Constitution",
            "Treason",
            "Wedding",
            "Sabotage"
        };
        public static readonly string[] Names = {
            "炼金术士",
            "起重机",
            "工程师",
            "发明家",
            "灌溉",
            "药物",
            "采矿",
            "印刷厂",
            "兴建道路",
            "锻冶匠",
            "商港",
            "大商家",
            "商人",
            "贸易船队",
            "垄断资源",
            "垄断商品",
            "外交官",
            "间谍",
            "指挥官",
            "权谋",
            "主教",
            "石碑",
            "叛徒",
            "婚礼",
            "破坏者"
        };
        public static readonly string[] Descriptions = {
            "在你掷骰前打出，你操控两颗数字骰子的结果，然后掷出事件骰并如常结算。",
            "本回合中你有一次建设城市设施时可少支付1张商品牌。",
            "免费兴建1道城墙。",
            "交换2枚数字圆片，但不得选择2、6、8、12号。",
            "点算版图上有你的村落或城市相邻的田野板块，每有一块你便获得2张麦。",
            "支付2张铁和1张麦，把你的一个村落升级为城市。",
            "点算版图上有你的村落或城市相邻的高山板块，每有一块你便获得2张铁。",
            "1分（必须即时打出）。",
            "免费兴建2条道路（若游玩使用船只的扩充则可选择兴建2艘船或道路和船只各1）。",
            "免费升级你的2个骑士，但依然需要遵守城市设施的规则才可将骑士升级到三级。",
            "你可向每位其它玩家给予1张资源牌，收到的玩家必须给你1张商品牌；若他没有任何商品牌，则把该资源牌归还给你。",
            "选择一名比你高分的玩家，查看他的全部资源及商品牌，从中偷取2张。",
            "把商人模型放到你的村落或城市旁1块板块上。只要模型还在上面，你便可用该地形的资源以2:1的优惠比率贸易。",
            "你指定一款资源或商品，本回合中你可用它以2:1的优惠比率贸易。",
            "宣告1种资源。所有其他玩家必须给你2张该种资源牌。",
            "宣告1种商品。所有其他玩家必须给你1张该种商品牌。",
            "你可移除任何一条开放道路（即任何一端没有连接同颜色配件的道路）。若你选择移除自己的道路，可立即重新兴建它。",
            "查看另一位玩家的全部发展卡，从中偷取1张。",
            "立即免费激活你的全部骑士。",
            "驱逐其它玩家1个连接到你的道路（或船只）的骑士，把它移动到另一个符合规则的位置。",
            "移动强盗。然后按照强盗所在的新板块，从相邻村落或城市的每位主人手中随机偷取1张资源或商品牌。",
            "1分（必须即时打出）。",
            "指定另一位玩家，他选择移除自己1个骑士，然后你获得1个同等级的骑士（它保持被移除前激活与否的状态）。",
            "每名比你高分的玩家都要选择给你资源牌及商品牌共2张。",
            "每名比你高分或同分的玩家选择并弃掉一半手牌（向下取整）。"
        };
        public static string Name(string id)
        {
            int index = Array.IndexOf(Ids, id);
            return index >= 0 ? Names[index] : "发展卡";
        }
    }
}
