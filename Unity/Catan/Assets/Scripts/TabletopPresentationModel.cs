#nullable disable
using System;
using System.Linq;

// 纯显示数据与时间轴；不读取权威状态，不生成或裁决骰点。
public static class TabletopHandLayout
{
    public const float CardWidth = 108, CardHeight = 151, Baseline = 714;
    public const float HoverSpread = 24;
    // 左侧状态区止于 238，右侧操作区始于 1290；包含展开后的手牌也必须留在两者之间。
    const float RestLeft = 268, RestRight = 1260, SpecialRight = 550, MixedResourceLeft = 578;
    public static TabletopCardPose Card(int index, int count, float center, float width, bool selected)
    {
        // 卡牌始终使用屏幕逻辑像素；空间不足只压缩间距，不缩小卡面或分页。
        float step = count <= 1 ? 0 : Math.Min(CardWidth + 10, Math.Max(0, width - CardWidth) / (count - 1));
        return new TabletopCardPose {
            X = center - (CardWidth + (count - 1) * step) / 2 + index * step,
            Y = Baseline - (selected ? 22 : 0), Angle = 0
        };
    }
    public static int ResourceCapacity(bool hasSpecialCards) => int.MaxValue;
    public static int SpecialCapacity(bool hasResources) => int.MaxValue;
    // 资源和发展卡共用完整区域；页签选择决定当前显示哪副手牌。
    public static float ResourceCenter(bool hasSpecialCards) => (RestLeft + RestRight) / 2;
    public static float ResourceWidth(bool hasSpecialCards) => RestRight - RestLeft;
    public static float SpecialCenter(bool hasResources) => (RestLeft + RestRight) / 2;
    public static float SpecialWidth(bool hasResources) => RestRight - RestLeft;
    public static bool IsObscured(float nextX,float x,float width) => nextX < x+width-.5f;
    public static TabletopCardPose Resource(TabletopResourceSlot[] slots,int index,float center,float width,bool selected)
    {
        // 飞牌落下前才逐渐撑开插槽，已有手牌随之让位；同一插槽同时供飞牌和手牌绘制使用。
        double count=slots.Sum(slot=>slot.Weight),before=slots.Take(index).Sum(slot=>slot.Weight);
        double step=count<=1?0:Math.Min(CardWidth+10,Math.Max(0,width-CardWidth)/(count-1));
        return new TabletopCardPose {
            X=(float)(center-CardWidth/2+(before+slots[index].Weight/2-count/2)*step),
            Y=Baseline-(selected?22:0),Angle=0
        };
    }
}
public sealed class TabletopResourceSlot
{
    public int Good,Ordinal;
    public OperationHandCard Card;
    public double Weight=1;
    public TabletopCollectionFlight Flight;
}
public sealed class TabletopCardPose
{
    public float X, Y, Angle;
    public float HalfWidth => (float)(Math.Abs(Math.Cos(Angle * Math.PI / 180)) * TabletopHandLayout.CardWidth
        + Math.Abs(Math.Sin(Angle * Math.PI / 180)) * TabletopHandLayout.CardHeight) / 2;
    public float HalfHeight => (float)(Math.Abs(Math.Sin(Angle * Math.PI / 180)) * TabletopHandLayout.CardWidth
        + Math.Abs(Math.Cos(Angle * Math.PI / 180)) * TabletopHandLayout.CardHeight) / 2;
}
public sealed class TabletopCostToken
{
    public string Name;
    public bool Missing;
    public static TabletopCostToken[] For(int[] cost, int[] owned)
    {
        string[] names = { "木头", "砖块", "羊毛", "小麦", "铁矿", "布匹", "钱币", "纸张" };
        return Enumerable.Range(0, 8).SelectMany(good => Enumerable.Range(0, Math.Max(0, cost[good]))
            .Select(ordinal => new TabletopCostToken { Name = names[good], Missing = ordinal >= owned[good] })).ToArray();
    }
}
public sealed class TabletopPromptSequence
{
    public const double Duration = 1.5;
    string[] messages = Array.Empty<string>();
    double started;
    public string RequiredSelection { get; private set; } = "";
    // 选择提示由当前交互状态决定；定时通知、重绘和收起窗口均不能让未完成的选择消失。
    public void RequireSelection(string instruction) => RequiredSelection = instruction ?? "";
    public void Show(double now, params string[] values)
    { started = now; messages = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(); }
    public string At(double now)
    {
        if (RequiredSelection.Length > 0) return RequiredSelection;
        int index = (int)Math.Floor((now - started) / Duration);
        return index < 0 || index >= messages.Length ? "" : messages[index];
    }
}
public static class TabletopSelectionPrompt
{
    public static bool IsDirectMapDecision(string kind)
    {
        switch(kind)
        {
            case "PillageCity": case "Metropolis": case "B5MetropolisRelocate":
            case "DisplacedKnight": case "ProgressTreasonRemove": case "ProgressTreasonPlace": return true;
            default: return false;
        }
    }
    // 四种规则包共用的权威选择阶段；普通行动和等待掷骰仍使用短暂提示。
    public static bool RequiredPhase(string phase)
    {
        switch (phase)
        {
            case "SetupSettlement": case "SetupRoad": case "Discard": case "RobberMove":
            case "RobberSteal": case "RoadBuilding": case "GoldChoice": case "PortPlacement":
            case "SetupPort": case "PendingChoice": return true;
            default: return false;
        }
    }
    public static string Decision(string kind)
    {
        switch (kind)
        {
            case "PillageCity": return "请选择要降级为村庄的城市";
            case "Metropolis": return "请选择要安置大都会的城市";
            case "B5MetropolisRelocate": return "请由城市所有者点选另一座合法己方城市，迁移大都会后继续结算";
            case "DisplacedKnight": return "请选择合法位置安置被驱逐的骑士";
            case "ProgressTreasonRemove": return "你受到「叛徒」影响，请选择要移除的己方骑士";
            case "ProgressTreasonPlace": return "请选择合法位置安置替补骑士";
            case "Gold": case "Aqueduct": return "请选择要获得的资源";
            case "ProgressDiscard": return "请选择要弃掉的发展卡";
            case "ProgressSabotage": return "你受到「破坏者」影响，请选择要弃掉的手牌";
            case "ProgressWedding": return "请选择要交出的婚礼赠礼";
            case "ProgressHarbor": return "请选择要交出的商品";
            case "ProgressEspionage": return "请选择要获得的发展卡";
            case "ProgressGuildDues": return "请选择要获得的手牌";
            case "DefenderProgress": return "请选择防御奖励的发展卡类别";
            default: return "请完成当前选择";
        }
    }
}
public sealed class TabletopCollectionFlight
{
    public string Player;
    public int Good;
    public string Category,CardName,CardId,SourceTileId,SourcePlayerId;
    public int ProgressIndex=-1,Track=-1;
    public bool IsSpecial => Category=="development"||Category=="progress";
    public double Started;
    public const double Duration = 1.05;
    public double Progress(double now) => Math.Max(0,Math.Min(1,(now-Started)/Duration));
    public double Insertion(double now)
    {
        double t=Math.Max(0,Math.Min(1,(Progress(now)-.60)/.40));
        return t*t*(3-2*t);
    }
}

// 地块、建筑和强盗均来自 PlayerView 的公开棋盘；不查询权威状态或未揭示地形。
public sealed class TabletopCollectionTile
{
    public string Id,Resource;
    public int Number;
    public string[] Vertices=Array.Empty<string>();
    public Catan.Core.PiecePlacement[] Settlements=Array.Empty<Catan.Core.PiecePlacement>(),Cities=Array.Empty<Catan.Core.PiecePlacement>();
    public bool CitiesKnights,Blocked;
    public int Yield(string player,int good,bool starting=false)
    {
        int resource=Array.IndexOf(new[]{"wood","brick","wool","wheat","ore"},Resource);
        int settlements=Settlements.Count(p=>p.PlayerId==player),cities=Cities.Count(p=>p.PlayerId==player);
        if(starting)return good==resource?1:0;
        if(good==resource)return settlements+cities*(CitiesKnights&&resource!=1&&resource!=3?1:2);
        int commodity=resource==2?5:resource==4?6:resource==0?7:-1;
        return CitiesKnights&&good==commodity?cities:0;
    }
}
public static class TabletopCollectionSources
{
    public static TabletopCollectionTile[] Project(Catan.Core.Tile[] tiles,Catan.Core.PiecePlacement[] settlements,Catan.Core.PiecePlacement[] cities,string robber,bool citiesKnights)
    {
        return (tiles??Array.Empty<Catan.Core.Tile>()).Where(t=>new[]{"wood","brick","wool","wheat","ore","gold"}.Contains(t.Resource))
            .Select(t=>new TabletopCollectionTile{Id=t.Id,Resource=t.Resource,Number=t.Number??0,Vertices=t.Vertices.ToArray(),CitiesKnights=citiesKnights,Blocked=t.Id==robber,
                Settlements=settlements.Where(p=>t.Vertices.Contains(p.LocationId)).ToArray(),Cities=cities.Where(p=>t.Vertices.Contains(p.LocationId)).ToArray()}).ToArray();
    }
}

// 只消费授权历史。资源转移不伪装成银行流出；发展/发展卡对手只绘制牌背。
public sealed class TabletopCollectionTimeline
{
    readonly System.Collections.Generic.List<TabletopCollectionFlight> flights = new System.Collections.Generic.List<TabletopCollectionFlight>();
    string scope;
    long sequence;
    bool initialized;
    readonly System.Collections.Generic.Dictionary<string,TabletopHandOrder> orders = new System.Collections.Generic.Dictionary<string,TabletopHandOrder>();
    readonly System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<TabletopHandChange>> handChanges = new System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<TabletopHandChange>>();
    readonly System.Collections.Generic.Dictionary<string,TabletopSpecialHandOrder> specialOrders = new System.Collections.Generic.Dictionary<string,TabletopSpecialHandOrder>();
    TabletopCollectionTile[] observedTiles;
    int lastRoll;
    bool productionPending;
    Catan.Core.GameHistoryEntry[] observedEntries;
    readonly System.Collections.Generic.Dictionary<string,Catan.Core.GameHistoryEntry> consumed=new System.Collections.Generic.Dictionary<string,Catan.Core.GameHistoryEntry>();
    public double RecoveryUntil { get; private set; }=double.NegativeInfinity;
    static string EventKey(Catan.Core.GameHistoryEntry entry)=>entry.SkillEventId==null?"command:"+entry.Sequence:"skill:"+entry.SkillEventId;
    public double OutcomesReadyAt { get; private set; } = double.NegativeInfinity;
    public void Observe(string playerScope,Catan.Core.GameHistoryEntry[] entries,double now,TabletopCollectionTile[] tiles=null,double? outcomesReadyAt=null)
    {
        OutcomesReadyAt=outcomesReadyAt??double.NegativeInfinity;
        entries=entries??Array.Empty<Catan.Core.GameHistoryEntry>();
        if(initialized&&scope==playerScope&&ReferenceEquals(observedEntries,entries))
        {flights.RemoveAll(f=>now-f.Started>=TabletopCollectionFlight.Duration);return;}
        observedEntries=entries;
        long latest=entries.Length==0?0:entries.Max(e=>e.Sequence);
        if(!initialized||scope!=playerScope||latest<sequence)
        {
            // 首次绑定、换局和读档均只接收当前历史基线，不能重播旧的收牌。
            flights.Clear();orders.Clear();specialOrders.Clear();handChanges.Clear();initialized=true;scope=playerScope;sequence=latest;
            consumed.Clear();foreach(var entry in entries)consumed[EventKey(entry)]=entry;RecoveryUntil=double.NegativeInfinity;
            observedTiles=tiles==null?null:CopyTiles(tiles);
            var roll=entries.OrderBy(e=>e.Sequence).LastOrDefault(e=>e.Dice1>0);lastRoll=roll==null?0:roll.Dice1+roll.Dice2;
            productionPending=roll!=null&&lastRoll!=7&&!entries.Where(e=>e.Sequence>=roll.Sequence).Any(e=>e.Kind=="EndTurn"||e.Changes.Any(c=>c.Category=="goods"&&c.Amount>0));return;
        }
        var updates=entries.Where(e=>e.Sequence>sequence||e.SkillEventId!=null&&(!consumed.TryGetValue(EventKey(e),out var old)||!ReferenceEquals(old,e))).OrderBy(e=>e.Sequence).ToArray();
        if(updates.Length==0){flights.RemoveAll(f=>now-f.Started>=TabletopCollectionFlight.Duration);return;}
        var sourceTiles=CopyTiles(observedTiles??tiles??Array.Empty<TabletopCollectionTile>());
        int inventions=updates.Count(e=>e.Action=="使用「发明家」");bool numbersReliable=true;
        foreach(var entry in updates)
        {
            var entryChanges=DeltaChanges(entry);
            // 动画统一对齐真实骰子落定时间；历史迟到时不能再从零重复等待一次掷骰。
            double collectionStart=Math.Max(now,outcomesReadyAt??(entry.Dice1>0?now+TabletopDicePresentation.Duration:now));
            ReplayPublicBoard(entry,sourceTiles,tiles,inventions,ref numbersReliable);
            if(entry.Dice1>0){lastRoll=entry.Dice1+entry.Dice2;productionPending=lastRoll!=7;}
            if(entry.Kind=="EndTurn")productionPending=false;
            // GetHistory 已按观看者权限投影；仅有具体资源索引的变化能改变该手牌显示队列。
            foreach(var change in entryChanges.Where(c=>c.Category=="goods"&&c.GoodIndex>=0&&c.GoodIndex<8&&c.PlayerId!=null))
            {
                if(!handChanges.TryGetValue(change.PlayerId,out var pending))handChanges[change.PlayerId]=pending=new System.Collections.Generic.List<TabletopHandChange>();
                if(pending.Count>=512){pending.Clear();RecoveryUntil=now+3;}
                pending.Add(new TabletopHandChange{Good=change.GoodIndex,Amount=change.Amount});
            }
            var changes=entryChanges.Where(c=>c.Category=="goods"&&!c.IsPrivate&&c.GoodIndex>=0&&c.GoodIndex<8).ToArray();
            bool production=entry.Dice1>0||productionPending&&(entry.Kind=="ResolveChoice"&&entry.Action!="领取水渠资源"||entry.Kind=="DiscardProgressCard"||entry.Kind=="ChooseGoldResources");
            int ordinal=0;
            foreach(var group in changes.GroupBy(c=>c.GoodIndex))
            {
                int fromBank=group.Sum(c=>c.Amount);
                if(fromBank<=0)continue;
                foreach(var gain in group.Where(c=>c.Amount>0))
                {
                    int amount=Math.Min(fromBank,gain.Amount);fromBank-=amount;
                    var origins=Origins(entry,gain.PlayerId,gain.GoodIndex,production,production&&!numbersReliable?Array.Empty<TabletopCollectionTile>():sourceTiles);
                    for(int i=0;i<Math.Min(257,amount);i++)flights.Add(new TabletopCollectionFlight {
                        Player=gain.PlayerId,Good=gain.GoodIndex,SourceTileId=origins.ElementAtOrDefault(i),Started=collectionStart+Math.Min(.55,ordinal++*.06)
                    });
                }
            }
            // 垄断等卡牌的公开转移从失去资源的玩家飞出，绝不冒充银行产出。
            // 私有偷取仍不生成资源正面；只使用投影历史已经公开的种类和数量。
            if(entry.Kind=="PlayMonopoly"||entry.Kind=="PlayProgressCard")foreach(var group in changes.GroupBy(c=>c.GoodIndex))
            {
                var donors=group.Where(c=>c.Amount<0).ToArray();var remaining=donors.Select(c=>-c.Amount).ToArray();
                int bank=Math.Max(0,group.Sum(c=>c.Amount));
                foreach(var gain in group.Where(c=>c.Amount>0))
                {
                    int bankPart=Math.Min(bank,gain.Amount);bank-=bankPart;int needed=gain.Amount-bankPart;
                    for(int donor=0;donor<donors.Length&&needed>0;donor++)
                    {
                        int amount=Math.Min(needed,remaining[donor]);needed-=amount;remaining[donor]-=amount;
                        for(int i=0;i<Math.Min(257,amount);i++)flights.Add(new TabletopCollectionFlight{
                            Player=gain.PlayerId,Good=gain.GoodIndex,SourcePlayerId=donors[donor].PlayerId,Started=collectionStart+Math.Min(.55,ordinal++*.06)});
                    }
                }
            }
            if(production&&changes.Any(c=>c.Amount>0))productionPending=false;
            var cardChanges=entryChanges.Where(c=>c.Category=="development"||c.Category=="progress").ToArray();
            foreach(var gain in cardChanges.Where(c=>c.Amount>0&&!(c.Category=="progress"&&!c.IsPrivate&&TabletopCardReveal.IsVictory(c.Name))))
            {
                int art=gain.Category=="progress"?ProgressIndex(gain.Name):-1;
                var source=cardChanges.FirstOrDefault(c=>c.Category==gain.Category&&c.Amount<0&&c.Name==gain.Name);
                for(int i=0;i<Math.Min(257,gain.Amount);i++)flights.Add(new TabletopCollectionFlight{
                    Player=gain.PlayerId,Good=-1,Category=gain.Category,CardName=gain.Name,ProgressIndex=art,
                    Track=art>=0?(art<10?2:art<16?0:1):entry.EventDie=="Science"?2:entry.EventDie=="Trade"?0:entry.EventDie=="Politics"?1:-1,
                    SourcePlayerId=source?.PlayerId,Started=collectionStart+Math.Min(.55,ordinal++*.06)});
            }
            if(flights.Count>256){flights.Clear();RecoveryUntil=now+3;}
        }
        observedTiles=tiles==null?sourceTiles:CopyTiles(tiles);
        sequence=latest;flights.RemoveAll(f=>now-f.Started>=TabletopCollectionFlight.Duration);
    }
    sealed class CollectionChange
    {
        internal Catan.Core.GameHistoryChange Source;
        internal int Amount;
        internal string PlayerId=>Source.PlayerId;
        internal string Category=>Source.Category;
        internal int GoodIndex=>Source.GoodIndex;
        internal string Name=>Source.Name;
        internal bool IsPrivate=>Source.IsPrivate;
    }
    CollectionChange[] DeltaChanges(Catan.Core.GameHistoryEntry entry)
    {
        consumed.TryGetValue(EventKey(entry),out var previous);consumed[EventKey(entry)]=entry;
        var current=entry.Changes??Array.Empty<Catan.Core.GameHistoryChange>();
        if(previous==null)return current.Select(c=>new CollectionChange{Source=c,Amount=c.Amount}).ToArray();
        var old=previous.Changes??Array.Empty<Catan.Core.GameHistoryChange>();
        // 同一发动补写结果，只消费授权变化的差额；已播放的收牌不能再次入队。
        return current.Concat(old).GroupBy(c=>(c.PlayerId,c.Category,c.GoodIndex,c.Name,c.LocationId,c.CardTrack,c.IsPrivate))
            .Select(g=>new CollectionChange{Source=g.First(),Amount=current.Where(c=>(c.PlayerId,c.Category,c.GoodIndex,c.Name,c.LocationId,c.CardTrack,c.IsPrivate)==g.Key).Sum(c=>c.Amount)-old.Where(c=>(c.PlayerId,c.Category,c.GoodIndex,c.Name,c.LocationId,c.CardTrack,c.IsPrivate)==g.Key).Sum(c=>c.Amount)})
            .Where(c=>c.Amount!=0).ToArray();
    }
    static TabletopCollectionTile[] CopyTiles(TabletopCollectionTile[] tiles)
    {
        return tiles.Select(t=>new TabletopCollectionTile{Id=t.Id,Resource=t.Resource,Number=t.Number,Vertices=t.Vertices.ToArray(),CitiesKnights=t.CitiesKnights,Blocked=t.Blocked,
            Settlements=t.Settlements.Select(CopyPiece).ToArray(),Cities=t.Cities.Select(CopyPiece).ToArray()}).ToArray();
    }
    static Catan.Core.PiecePlacement CopyPiece(Catan.Core.PiecePlacement piece)=>new Catan.Core.PiecePlacement{PlayerId=piece.PlayerId,LocationId=piece.LocationId};
    static void ReplayPublicBoard(Catan.Core.GameHistoryEntry entry,TabletopCollectionTile[] tiles,TabletopCollectionTile[] finalTiles,int inventions,ref bool numbersReliable)
    {
        // 联机一次同步可带多条命令；产出按该条命令发生时已公开的棋盘定位，不能拿最终强盗位置或城市等级反推过去。
        if(entry.Kind=="MoveRobber"&&entry.TargetId!=null)foreach(var tile in tiles)tile.Blocked=tile.Id==entry.TargetId;
        foreach(var change in (entry.Changes??Array.Empty<Catan.Core.GameHistoryChange>()).Where(c=>c.Category=="piece"&&(c.Name=="村庄"||c.Name=="城市")))
        {
            foreach(var tile in tiles.Where(t=>t.Vertices.Contains(change.LocationId)))
            {
                var before=change.Name=="城市"?tile.Cities:tile.Settlements;
                var after=before.Where(p=>p.PlayerId!=change.PlayerId||p.LocationId!=change.LocationId).ToArray();
                if(change.Amount>0)after=after.Concat(new[]{new Catan.Core.PiecePlacement{PlayerId=change.PlayerId,LocationId=change.LocationId}}).ToArray();
                if(change.Name=="城市")tile.Cities=after;else tile.Settlements=after;
            }
        }
        if(entry.Action!="使用「发明家」")return;
        // 现有公开历史未携带发明家的两个目标；单次交换可由前后公开点数唯一恢复，多次合并则不猜测中间位置。
        if(inventions!=1||finalTiles==null){numbersReliable=false;return;}
        var changed=tiles.Where(t=>finalTiles.Any(final=>final.Id==t.Id&&final.Number!=t.Number)).ToArray();
        if(changed.Length!=0&&changed.Length!=2){numbersReliable=false;return;}
        foreach(var tile in changed)tile.Number=finalTiles.First(final=>final.Id==tile.Id).Number;
    }
    public TabletopCollectionFlight[] Active(double now)
    {
        flights.RemoveAll(f=>now-f.Started>=TabletopCollectionFlight.Duration);
        return flights.Where(f=>now>=f.Started).ToArray();
    }
    public TabletopResourceSlot[] HandSlots(string player,int[] owned,int goodCount,double now)
    {
        flights.RemoveAll(f=>now-f.Started>=TabletopCollectionFlight.Duration);
        var result=new System.Collections.Generic.List<TabletopResourceSlot>();
        player=player??"";
        if(!orders.TryGetValue(player,out var order))orders[player]=order=new TabletopHandOrder();
        handChanges.TryGetValue(player,out var pendingChanges);
        order.Sync(owned,goodCount,pendingChanges);
        if(pendingChanges!=null)pendingChanges.Clear();
        var incomingByGood=new System.Collections.Generic.Dictionary<int,TabletopCollectionFlight[]>();
        var visibleByGood=new System.Collections.Generic.Dictionary<int,int>();
        for(int good=0;good<goodCount;good++)
        {
            int count=Math.Max(0,owned[good]);
            var incoming=flights.Where(f=>f.Player==player&&f.Good==good).OrderBy(f=>f.Started).ToArray();
            // 已到账资源可能立即被花费；取消超出当前持有数的展示，绝不能凭动画补回规则资源。
            foreach(var canceled in incoming.Skip(count))flights.Remove(canceled);
            incoming=incoming.Take(count).ToArray();
            incomingByGood[good]=incoming;visibleByGood[good]=count-incoming.Length;
        }
        var ordinals=new int[8];
        foreach(int good in order.Goods)
        {
            int ordinal=ordinals[good]++;
            var flight=ordinal<visibleByGood[good]?null:incomingByGood[good][ordinal-visibleByGood[good]];
            result.Add(new TabletopResourceSlot{Good=good,Ordinal=ordinal,Flight=flight,Weight=flight==null?1:flight.Insertion(now)});
        }
        return result.ToArray();
    }
    public void SortHand(string player){if(player!=null&&orders.TryGetValue(player,out var order))order.Sort();}
    string[] Origins(Catan.Core.GameHistoryEntry entry,string player,int good,bool production,TabletopCollectionTile[] tiles)
    {
        if(entry.Kind=="SetupSettlement")return tiles.Where(t=>t.Vertices.Contains(entry.TargetId)&&t.Yield(player,good,true)>0).OrderBy(t=>t.Id).Select(t=>t.Id).ToArray();
        var productionOrigins=production?tiles.Where(t=>!t.Blocked&&t.Number==lastRoll).OrderBy(t=>t.Id).SelectMany(t=>Enumerable.Repeat(t.Id,t.Yield(player,good))).ToArray():Array.Empty<string>();
        if(entry.Kind=="ChooseGoldResources"||entry.Action=="领取金矿资源")
            return productionOrigins.Concat(tiles.Where(t=>!t.Blocked&&t.Resource=="gold"&&(t.Number==lastRoll||entry.Turn==0)).OrderBy(t=>t.Id)
                .SelectMany(t=>Enumerable.Repeat(t.Id,t.Settlements.Count(p=>p.PlayerId==player)+2*t.Cities.Count(p=>p.PlayerId==player)))).ToArray();
        if(production)return productionOrigins;
        if(entry.Action=="使用「灌溉」"||entry.Action=="使用「采矿」")
            return tiles.Where(t=>t.Resource==(good==3?"wheat":good==4?"ore":"")).OrderBy(t=>t.Id)
                .SelectMany(t=>Enumerable.Repeat(t.Id,2*t.Settlements.Concat(t.Cities).Count(p=>p.PlayerId==player))).ToArray();
        return Array.Empty<string>();
    }
    static int ProgressIndex(string name) => Array.IndexOf(Catan.Core.DevelopmentCardText.Names,name);
    public TabletopResourceSlot[] SpecialSlots(string player,System.Collections.Generic.IEnumerable<OperationHandCard> owned,double now)
    {
        flights.RemoveAll(f=>now-f.Started>=TabletopCollectionFlight.Duration);
        var cards=(owned??Array.Empty<OperationHandCard>()).ToArray();
        if(!specialOrders.TryGetValue(player,out var order))specialOrders[player]=order=new TabletopSpecialHandOrder();
        var ids=order.Sync(cards.Select(c=>c.Id));
        var incoming=flights.Where(f=>f.Player==player&&f.IsSpecial).OrderBy(f=>f.Started).ToArray();
        foreach(var group in incoming.GroupBy(f=>new{f.Category,f.CardName}))
        {
            // 同类牌的显示 ID 会在出牌后按持有数量重编号。规则从最早持有的同类牌中消费，
            // 所以未落地的新牌始终对应当前同类手牌的后缀；重绑插槽不能重置其飞行时间。
            var available=cards.Where(c=>c.Name==group.Key.CardName&&c.Progress==(group.Key.Category=="progress")).Reverse().ToArray();
            var newest=group.Reverse().ToArray();
            for(int index=0;index<newest.Length;index++)
                if(index<available.Length)newest[index].CardId=available[index].Id;
                else flights.Remove(newest[index]);
        }
        return ids.Select(id=>{var card=cards.First(c=>c.Id==id);var flight=flights.FirstOrDefault(f=>f.Player==player&&f.IsSpecial&&f.CardId==id);
            return new TabletopResourceSlot{Good=-1,Card=card,Flight=flight,Weight=flight==null?1:flight.Insertion(now)};}).ToArray();
    }
    public void SortSpecialHand(string player,System.Collections.Generic.IEnumerable<string> ids)
    {if(!specialOrders.TryGetValue(player,out var order))specialOrders[player]=order=new TabletopSpecialHandOrder();order.Sort(ids);}
}
