#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

// 私有手牌的显示顺序。仅接受观看者已有的数量及获准历史，不推测隐藏牌的身份。
public sealed class TabletopHandOrder
{
    readonly List<int> goods = new List<int>();
    bool initialized;
    public int[] Goods => goods.ToArray();
    public void Sync(int[] owned, int goodCount, IEnumerable<TabletopHandChange> changes)
    {
        var updates=(changes??Array.Empty<TabletopHandChange>()).Where(c=>c.Good>=0&&c.Good<goodCount).ToArray();
        if(!initialized)
        {
            // 首次打开或读档没有保存过的摆牌顺序；从当前数量反推本次尚未展示变化之前的基线。
            // 资源本身没有实体卡 ID，同资源消费时从队列前方取走一张。
            for(int good=0;good<goodCount;good++)
            {
                int count=Math.Max(0,owned[good]-updates.Where(c=>c.Good==good).Sum(c=>c.Amount));
                for(int i=0;i<count;i++)goods.Add(good);
            }
            initialized=true;
        }
        foreach(var change in updates)
        {
            if(change.Amount>0)for(int i=0;i<change.Amount;i++)goods.Add(change.Good);
            else for(int i=0;i<-change.Amount;i++){int at=goods.IndexOf(change.Good);if(at>=0)goods.RemoveAt(at);}
        }
        // 净持牌视图始终是最终依据；未有详细历史的增量同样追加到最末尾。
        for(int good=0;good<goodCount;good++)
        {
            int count=Math.Max(0,owned[good]),existing=goods.Count(g=>g==good);
            while(existing>count){goods.RemoveAt(goods.IndexOf(good));existing--;}
            while(existing<count){goods.Add(good);existing++;}
        }
        goods.RemoveAll(g=>g>=goodCount);
    }
    public void Sort(){goods.Sort();}
}
public sealed class TabletopHandChange
{
    public int Good, Amount;
}

public sealed class TabletopSpecialHandOrder
{
    readonly List<string> ids = new List<string>();
    public string[] Sync(IEnumerable<string> current)
    {
        var present=(current??Array.Empty<string>()).Where(id=>id!=null).Distinct().ToArray();
        ids.RemoveAll(id=>!present.Contains(id));
        foreach(var id in present)if(!ids.Contains(id))ids.Add(id);
        return ids.ToArray();
    }
    public void Sort(IEnumerable<string> current){ids.Clear();Sync(current);}
}
