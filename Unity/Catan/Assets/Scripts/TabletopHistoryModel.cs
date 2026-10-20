#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core;

// 只使用已投影的历史变化；合并叙述不能补全未知手牌。
public sealed class TabletopHistoryLine
{
    public string Player, Prefix, Suffix, Partner, PartnerAction, Emphasis;
    public int Track = -1;
    public bool Quote, Trade, Roll;
    public GameHistoryChange[] Cards = Array.Empty<GameHistoryChange>();
    public GameHistoryChange[] ReceivedCards = Array.Empty<GameHistoryChange>();
}

public static class TabletopHistoryModel
{
    static bool Goods(GameHistoryChange c) => c.Category == "goods" || c.Category == "resource-count";
    public static int CardTrack(string name)
    {
        string[] science={"炼金术","起重机","工程学","发明","灌溉","医学","采矿","印刷术","道路建设","锻铁匠","锻造"};
        string[] trade={"商业港口","行会征费","商人","商船队","资源垄断","商品垄断"};
        string[] politics={"外交","间谍","鼓舞","谋略","征税","宪法","叛变","婚礼","破坏"};
        return science.Contains(name)?2:trade.Contains(name)?0:politics.Contains(name)?1:-1;
    }
    // 中文名称对应当前实体建设板；等级和能力仍沿用规则书第 8 页。
    public static string FacilityName(int track,int level)
    {
        string[][] names={new[]{"市场","行会","同业公会","银行","商业中心"},
            new[]{"市政厅","教堂","要塞","大教堂","卡坦议会"},new[]{"修道院","图书馆","引水渠","剧场","大学"}};
        return track>=0&&track<3&&level>=1&&level<=5?names[track][level-1]:"城市设施";
    }
    public static TabletopHistoryLine[] Lines(GameHistoryEntry entry)
    {
        var result=new List<TabletopHistoryLine>();
        var changes=entry.Changes??Array.Empty<GameHistoryChange>();
        var consumed=new HashSet<GameHistoryChange>();
        string action=(entry.Action??"").Replace("打出", "使用").Replace("使用了","使用");
        if(entry.Kind=="ProposeTrade")return new[]{new TabletopHistoryLine{Player=entry.PlayerId,Prefix="提出交易：",Quote=true,
            Cards=changes.Where(c=>c.Category=="offer"&&c.Amount<0).ToArray(),ReceivedCards=changes.Where(c=>c.Category=="offer"&&c.Amount>0).ToArray()}};
        if(entry.Kind=="BankTrade"||entry.Kind=="AcceptTrade"||action=="完成商业港口交换")
        {
            var exchanged=changes.Where(Goods).ToArray();
            var given=exchanged.Where(c=>c.PlayerId==entry.PlayerId&&c.Amount<0).ToArray();
            var received=exchanged.Where(c=>c.PlayerId==entry.PlayerId&&c.Amount>0).ToArray();
            var partners=exchanged.Where(c=>c.PlayerId!=entry.PlayerId&&c.Amount!=0).Select(c=>c.PlayerId).Distinct().ToArray();
            if(given.Length>0&&received.Length>0&&(entry.Kind=="BankTrade"||partners.Length==1))return new[]{new TabletopHistoryLine{
                Player=entry.PlayerId,Prefix="用",Cards=given,ReceivedCards=received,Trade=true,Partner=entry.Kind=="BankTrade"?"银行":partners[0]}};
        }
        bool steal=entry.Kind=="StealResource"||entry.Kind=="StealCloth"||action=="结算间谍";
        bool wedding=action=="交出婚礼礼物";
        bool transfer=steal||wedding||action=="结算行会征费"||entry.Kind=="PlayMonopoly"||entry.Kind=="PlayProgressCard"&&
            new[]{"资源垄断","商品垄断","征税"}.Any(action.Contains);
        if(transfer)
        {
            var moved=changes.Where(c=>Goods(c)||steal&&c.Category=="progress"||entry.Kind=="StealCloth"&&c.Category=="count").ToArray();
            string receiver=moved.FirstOrDefault(c=>c.Amount>0)?.PlayerId;
            if(receiver!=null)
            {
                foreach(var giver in moved.Where(c=>c.Amount<0&&c.PlayerId!=receiver).GroupBy(c=>c.PlayerId))
                {
                    var cards=giver.Select(c=>{consumed.Add(c);return c;}).ToArray();
                    result.Add(new TabletopHistoryLine{Player=receiver,Prefix=wedding?"收取了":steal||action=="结算行会征费"?"偷取了":"收获",Partner=giver.Key,
                        PartnerAction=wedding?"交出的婚礼礼物：":steal?" "+cards.Sum(c=>Math.Abs(c.Amount))+"张"+(cards.Any(c=>c.Category=="progress")?"发展卡":entry.Kind=="StealCloth"?"布匹":"手牌"):"的手牌：",
                        Cards=steal?Array.Empty<GameHistoryChange>():cards});
                }
                foreach(var c in moved.Where(c=>c.Amount>0))consumed.Add(c);
            }
        }
        bool paid=changes.Any(c=>Goods(c)&&c.Amount<0&&c.PlayerId==entry.PlayerId);
        bool medicineUpgrade=entry.Kind=="PlayProgressCard"&&action=="使用「医学」"&&changes.Any(c=>c.Category=="piece"&&c.Name=="城市"&&c.Amount>0&&c.PlayerId==entry.PlayerId);
        bool build=paid&&(medicineUpgrade||entry.Kind.StartsWith("Build")||new[]{"RecruitKnight","PromoteKnight","ActivateKnight","ImproveCity","BuyDevelopmentCard"}.Contains(entry.Kind));
        bool facility=changes.Any(c=>c.Category=="status"&&c.Amount>0&&(c.Name??"").Contains("改良"));
        bool setup=entry.Kind=="SetupSettlement"||entry.Kind=="SetupRoad"||entry.Kind=="SetupShip";
        bool discard=entry.Kind=="DiscardResources"||action=="弃置破坏要求的手牌";
        bool pillage=false;
        foreach(var city in changes.Where(c=>c.Category=="piece"&&c.Name=="城市"&&c.Amount<0))
        {
            var village=changes.FirstOrDefault(c=>c.Category=="piece"&&c.Name=="村庄"&&c.Amount>0&&c.PlayerId==city.PlayerId&&c.LocationId==city.LocationId);
            if(village==null)continue;
            var walls=changes.Where(c=>c.Category=="piece"&&c.Name=="城墙"&&c.Amount<0&&c.PlayerId==city.PlayerId&&c.LocationId==city.LocationId).ToArray();
            result.Add(new TabletopHistoryLine{Player=city.PlayerId,Prefix="的城市被海盗掠夺，已"+(walls.Length>0?"失去城墙并":"")+"降级为村庄"});
            consumed.Add(city);consumed.Add(village);foreach(var wall in walls)consumed.Add(wall);pillage=true;
        }
        if(discard)
        {
            foreach(var group in changes.Where(c=>Goods(c)&&c.Amount<0).GroupBy(c=>c.PlayerId))
            {
                bool sabotage=action=="弃置破坏要求的手牌";
                result.Add(new TabletopHistoryLine{Player=group.Key,Prefix=(sabotage?"因「破坏」":"因掷出7")+"弃置了"+group.Sum(c=>-c.Amount)+"张手牌",Emphasis=sabotage?"破坏":null,Track=sabotage?1:-1});
                foreach(var c in group)consumed.Add(c);
            }
        }
        bool showAction=(!transfer||result.Count==0||entry.Kind.StartsWith("Play"))&&!build&&!setup&&entry.Kind!="ImproveCity"&&!discard&&!(pillage&&action=="选择降级城市");
        if(showAction)
        {
            string card=changes.FirstOrDefault(c=>c.PlayerId==entry.PlayerId&&c.Amount<0&&c.Category=="progress"&&action.Contains(c.Name))?.Name;
            result.Insert(0,new TabletopHistoryLine{Player=entry.PlayerId,Prefix=action,Roll=entry.Dice1>0,Emphasis=card,Track=card==null?-1:CardTrack(card)});
        }
        if(setup)
        {
            string piece=changes.FirstOrDefault(c=>c.Category=="piece"&&c.Amount>0)?.Name??(entry.Kind=="SetupRoad"?"道路":entry.Kind=="SetupShip"?"船":"村庄");
            result.Add(new TabletopHistoryLine{Player=entry.PlayerId,Prefix="建造了开局"+piece});
            foreach(var c in changes.Where(c=>c.Category=="piece"))consumed.Add(c);
        }
        if(facility)
        {
            foreach(var c in changes.Where(c=>c.Category=="status"&&c.Amount>0&&(c.Name??"").Contains("改良")))
            {
                int track=c.Name.StartsWith("贸易")?0:c.Name.StartsWith("政治")?1:2;
                int start=c.Name.IndexOf("现为 ",StringComparison.Ordinal)+3;
                int level=start>=3&&start<c.Name.Length&&char.IsDigit(c.Name[start])?c.Name[start]-'0':0;
                var cost=changes.Where(d=>Goods(d)&&d.Amount<0&&d.PlayerId==c.PlayerId).ToArray();
                result.Add(new TabletopHistoryLine{Player=c.PlayerId,Prefix=cost.Length>0?"使用":"建造了",Cards=cost,
                    Suffix=(cost.Length>0?"建造了":"")+FacilityName(track,level),Emphasis=FacilityName(track,level),Track=track});
                consumed.Add(c);foreach(var d in cost)consumed.Add(d);
            }
        }
        // 激活与休眠是同一棋子的状态替换，按玩家合为一句。
        foreach(var player in changes.Where(c=>c.Category=="piece"&&c.Amount>0&&(c.Name??"").EndsWith("（休眠）")).GroupBy(c=>c.PlayerId))
        {
            bool moved=entry.Kind=="MoveKnight"||entry.Kind=="DisplaceKnight";
            var asleep=player.Where(c=>changes.Any(d=>d.Category=="piece"&&d.Amount<0&&d.PlayerId==c.PlayerId&&(d.LocationId==c.LocationId||moved)&&d.Name==c.Name.Replace("休眠","激活"))).ToArray();
            if(asleep.Length==0)continue;
            result.Add(new TabletopHistoryLine{Player=player.Key,Prefix="的"+string.Join("、",asleep.Select(c=>c.Name.Replace("（休眠）","")))+"已休眠"});
            foreach(var c in asleep){consumed.Add(c);foreach(var d in changes.Where(d=>d.Category=="piece"&&d.PlayerId==c.PlayerId&&(d.LocationId==c.LocationId||moved)&&d.Name==c.Name.Replace("休眠","激活")))consumed.Add(d);}
        }
        foreach(var group in changes.Where(c=>!consumed.Contains(c)&&(c.GoodIndex>=0&&c.GoodIndex<8||c.Category=="resource-count")).GroupBy(c=>new{c.PlayerId,Sign=Math.Sign(c.Amount),c.Category}))
        {
            string suffix=build&&group.Key.PlayerId==entry.PlayerId&&group.Key.Sign<0?(medicineUpgrade?"升级了城市":action.Replace("建造","建造了").Replace("招募骑士","建造了骑士").Replace("升级","升级了").Replace("晋升","晋升了").Replace("激活","激活了").Replace("购买","购买了")):"";
            result.Add(new TabletopHistoryLine{Player=group.Key.PlayerId,Prefix=group.Key.Sign>0?"收获":"使用",Suffix=suffix,Cards=group.ToArray()});
        }
        foreach(var c in changes.Where(c=>!consumed.Contains(c)&&c.GoodIndex<0&&c.Category!="resource-count"))
        {
            if(c.Category=="piece"&&(build||action.Contains(c.Name)))continue;
            if(c.Amount<0&&(c.Category=="progress"||c.Category=="development")&&action.Contains(c.Name))continue;
            bool card=c.Category=="progress"||c.Category=="development";
            bool victory=c.Category=="defender"||c.Category=="progress"&&new[]{"宪法","石碑","印刷术"}.Contains(c.Name);
            string verb=c.Amount>0?(c.Category=="piece"&&c.Name=="城墙"?"建造了":"收获"):"失去了";
            result.Add(new TabletopHistoryLine{Player=c.PlayerId,Prefix=verb+c.Name+(victory?"（1分）":"")+(Math.Abs(c.Amount)>1?" ×"+Math.Abs(c.Amount):""),Emphasis=card?c.Name:null,
                Track=card?(c.CardTrack>=0?c.CardTrack:CardTrack(c.Name)>=0?CardTrack(c.Name):3):-1});
        }
        return result.ToArray();
    }
}
