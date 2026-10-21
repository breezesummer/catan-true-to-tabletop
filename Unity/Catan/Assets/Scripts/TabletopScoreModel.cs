#nullable disable
using System.Collections.Generic;
using System.Linq;
using Catan.Core;

// 只解释当前 PlayerView 已授权的分数来源；不读取权威状态，也不参与胜负裁决。
public sealed class TabletopScoreRow
{
    public string Name { get; private set; }
    public string Detail { get; private set; }
    public int Points { get; private set; }
    public bool Hidden { get; private set; }
    internal TabletopScoreRow(string name,string detail,int points,bool hidden=false)
    {Name=name;Detail=detail;Points=points;Hidden=hidden;}
}

public sealed class TabletopScoreBreakdown
{
    public string PlayerId { get; private set; }
    public bool IsOwn { get; private set; }
    public TabletopScoreRow[] Rows { get; private set; }
    public int PublicPoints => Rows.Where(r=>!r.Hidden).Sum(r=>r.Points);
    public int HiddenPoints => Rows.Where(r=>r.Hidden).Sum(r=>r.Points);
    public int Total => PublicPoints+HiddenPoints;
    internal TabletopScoreBreakdown(string playerId,bool isOwn,List<TabletopScoreRow> rows)
    {PlayerId=playerId;IsOwn=isOwn;Rows=rows.ToArray();}
}

public static class TabletopScoreModel
{
    public static TabletopScoreBreakdown For(Catan.Core.M2.PlayerView view,string playerId)
    {
        if(view==null||!view.Players.Any(p=>p.Id==playerId))return null;
        var rows=Buildings(view.Board.Settlements,view.Board.Cities,playerId);
        Awards(rows,playerId,view.LongestRoadPlayerId,view.LargestArmyPlayerId,"最长道路");
        if(playerId==view.PlayerId)HiddenCards(rows,view.OwnDevelopmentCards.Count(c=>c.Kind==Catan.Core.M2.DevelopmentCardKind.VictoryPoint));
        return new TabletopScoreBreakdown(playerId,playerId==view.PlayerId,rows);
    }
    public static TabletopScoreBreakdown For(Catan.Core.M3.PlayerView view,string playerId)
    {
        if(view==null||!view.Players.Any(p=>p.Id==playerId))return null;
        var p=view.Players.First(x=>x.Id==playerId);
        var rows=Buildings(view.Board.Settlements,view.Board.Cities,playerId);
        Awards(rows,playerId,view.LongestRoadPlayerId,view.LargestArmyPlayerId,"最长航线");
        Add(rows,"剧本奖励","登岛或航路奖励",p.BonusVictoryPoints);
        Add(rows,"布匹",p.Cloth+" 匹 · 每 2 匹计 1 分",p.Cloth/2);
        if(playerId==view.PlayerId)HiddenCards(rows,view.OwnDevelopmentCards.Count(c=>c.Kind==Catan.Core.M3.DevelopmentCardKind.VictoryPoint));
        return new TabletopScoreBreakdown(playerId,playerId==view.PlayerId,rows);
    }
    public static TabletopScoreBreakdown For(Catan.Core.M4.PlayerView view,string playerId)
    {
        if(view==null||!view.Players.Any(p=>p.Id==playerId))return null;
        var p=view.Players.First(x=>x.Id==playerId);
        var rows=Buildings(view.Board.Settlements,view.Board.Cities,playerId);
        Awards(rows,playerId,view.LongestRoadPlayerId,null,"最长道路");
        CityBonuses(rows,view.Board.Metropolises.Count(m=>m.PlayerId==playerId),p.DefenderPoints,view.MerchantPlayerId==playerId);
        Add(rows,"印刷厂","公开胜利点卡",p.ProgressVictoryCards.Count(c=>c==Catan.Core.M4.ProgressCardKind.Printing));
        Add(rows,"石碑","公开胜利点卡",p.ProgressVictoryCards.Count(c=>c==Catan.Core.M4.ProgressCardKind.Constitution));
        return new TabletopScoreBreakdown(playerId,playerId==view.PlayerId,rows);
    }
    public static TabletopScoreBreakdown For(Catan.Core.M5.PlayerView view,string playerId)
    {
        if(view==null||!view.Players.Any(p=>p.Id==playerId))return null;
        var p=view.Players.First(x=>x.Id==playerId);
        var rows=Buildings(view.Board.Settlements,view.Board.Cities,playerId);
        Awards(rows,playerId,view.LongestRoadPlayerId,null,"最长航线");
        Add(rows,"登岛奖励","在新岛屿建立村庄",p.BonusVictoryPoints);
        CityBonuses(rows,view.Board.Metropolises.Count(m=>m.PlayerId==playerId),p.DefenderPoints,view.MerchantPlayerId==playerId);
        Add(rows,"印刷厂","公开胜利点卡",p.ProgressVictoryCards.Count(c=>c==Catan.Core.M5.ProgressCardKind.Printing));
        Add(rows,"石碑","公开胜利点卡",p.ProgressVictoryCards.Count(c=>c==Catan.Core.M5.ProgressCardKind.Constitution));
        return new TabletopScoreBreakdown(playerId,playerId==view.PlayerId,rows);
    }
    static List<TabletopScoreRow> Buildings(PiecePlacement[] settlements,PiecePlacement[] cities,string playerId)
    {
        var rows=new List<TabletopScoreRow>();
        int villages=settlements.Count(p=>p.PlayerId==playerId),towns=cities.Count(p=>p.PlayerId==playerId);
        Add(rows,"村庄",villages+" 座 × 1 分",villages);
        Add(rows,"城市",towns+" 座 × 2 分",towns*2);
        return rows;
    }
    static void Awards(List<TabletopScoreRow> rows,string playerId,string road,string army,string roadName)
    {
        if(road==playerId)Add(rows,roadName,"持有奖励卡",2);
        if(army==playerId)Add(rows,"最大骑士军队","持有奖励卡",2);
    }
    static void CityBonuses(List<TabletopScoreRow> rows,int metropolises,int defenders,bool merchant)
    {
        Add(rows,"大都会加分",metropolises+" 座 × 额外 2 分",metropolises*2);
        Add(rows,"卡坦保卫者",defenders+" 张 × 1 分",defenders);
        if(merchant)Add(rows,"商人","持有商人",1);
    }
    static void HiddenCards(List<TabletopScoreRow> rows,int count)
    {if(count>0)rows.Add(new TabletopScoreRow("胜利点卡",count+" 张 · 仅自己可见",count,true));}
    static void Add(List<TabletopScoreRow> rows,string name,string detail,int points)
    {if(points>0)rows.Add(new TabletopScoreRow(name,detail,points));}
}
