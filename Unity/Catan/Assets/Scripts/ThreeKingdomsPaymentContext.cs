#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using Catan.Core.ThreeKingdoms;

// 仅持有本人投影与实体牌展示序号；不读取权威手牌，不改写牌种或提前支付。
public sealed class B45PaymentCard
{
    public int RealType, Ordinal, Use = -1;
}
public sealed class B45PaymentContext
{
    readonly List<B45PaymentCard> cards = new List<B45PaymentCard>();
    int[] hand = new int[8], cost;
    int[][] allowed;
    string window, seat;
    bool singleConversion, bankTrade;
    int bankRate, bankReceiveCount, bankEffectiveType=-1;
    public bool Active { get; private set; }
    public bool Declaring { get; private set; }
    public bool CanDeclare { get; private set; }
    public bool IsSubmitting { get; private set; }
    public string Title { get; private set; }
    public string Notice { get; private set; }
    public int ExpectedCount { get; private set; }
    public int SelectedIndex { get; private set; }
    public B45PaymentCard[] Cards => cards.ToArray();
    public B45PaymentCard Current => SelectedIndex >= 0 && SelectedIndex < cards.Count ? cards[SelectedIndex] : null;
    public int[] AllowedUses => Current == null ? Array.Empty<int>() : (Declaring ? Enumerable.Range(0,8) : (allowed?[Current.RealType] ?? new[]{Current.RealType})).Where(t=>bankEffectiveType<0||t==bankEffectiveType).ToArray();
    public int[] SelectedCounts => Enumerable.Range(0,8).Select(t=>cards.Count(c=>c.RealType==t)).ToArray();
    public int[] EffectiveCounts => Enumerable.Range(0,8).Select(t=>cards.Count(c=>c.Use==t)).ToArray();
    public int[] RealTypes => cards.Select(c=>c.RealType).ToArray();
    public int[] EffectiveTypes => cards.Select(c=>c.Use).ToArray();
    public bool Ready => Active && !IsSubmitting && cards.Count == ExpectedCount && cards.Count > 0 && cards.All(c=>c.Use>=0 && c.Use<8 && (Declaring || allowed[c.RealType].Contains(c.Use))) &&
        (cost==null || EffectiveCounts.SequenceEqual(cost)) && BankConstraintReason==null && (Declaring || !singleConversion || cards.Count(c=>c.RealType!=c.Use)<=1);
    public string BankConstraintReason => !bankTrade?null:bankRate>0&&ExpectedCount!=bankRate*bankReceiveCount?"此港口需按 "+bankRate+":1 选择真实底牌总数。":cards.Any(c=>c.Use<0)?null:cards.Select(c=>c.Use).Distinct().Count()>1?"银行交换须使用同一种用途。":bankEffectiveType>=0&&cards.Any(c=>c.Use!=bankEffectiveType)?"此港口只接受"+Names[bankEffectiveType]+"用途。":null;
    public void ConfigureBankTrade(int ratio=0,int receiveCount=0,int effectiveType=-1) {bankTrade=true;bankRate=ratio;bankReceiveCount=receiveCount;bankEffectiveType=effectiveType;}
    public string Requirement => bankEffectiveType>=0?Names[bankEffectiveType]+"用途 · "+bankRate+":1":bankRate>0?"同一用途 · "+bankRate+":1":cost == null ? "逐张选择本次用途" : string.Join("、",Enumerable.Range(0,8).Where(t=>cost[t]>0).Select(t=>Names[t]+cost[t]));
    public static readonly string[] Names={"木材","砖块","羊毛","谷物","矿石","布匹","钱币","纸张"};
    public static bool HasDeclaration(ThreeKingdomsView view,string player) => view?.Players?.Any(p=>p.PlayerId==player&&p.ActiveGeneralId=="tk-g07"&&!p.FaceDown)==true;
    public static bool Applicable(ThreeKingdomsView view,string player) => GeneralCatalog.UsesDirectPackage(view?.PackageVersion) && (HasDeclaration(view,player)||view.EffectiveCardTypes?.Any(a=>a.Length>1)==true);
    public void Begin(ThreeKingdomsView view,string player,int[] owned,int count,int[] requiredCost,string title)
    {
        Reset();if(!Applicable(view,player)||owned?.Length!=8||count<=0)return;
        Active=true;seat=player;window=view.WindowId;hand=(int[])owned.Clone();cost=requiredCost==null?null:(int[])requiredCost.Clone();ExpectedCount=count;Title=title;
        UpdatePermissions(view);Notice="点选真实手牌，再为每张牌选择用途；确认前不消耗。";
    }
    void UpdatePermissions(ThreeKingdomsView view)
    {
        CanDeclare=HasDeclaration(view,seat);allowed=Enumerable.Range(0,8).Select(t=>view.EffectiveCardTypes!=null&&t<view.EffectiveCardTypes.Length?view.EffectiveCardTypes[t]:new[]{t}).ToArray();
        singleConversion=view.Players.Any(p=>p.PlayerId==seat&&p.ActiveGeneralId=="tk-g41");
    }
    public void Refresh(ThreeKingdomsView view,string player,int[] owned)
    {
        if(Active&&player!=seat){Reset();Notice="观看席位已变化，已清除私有支付草稿。";return;}
        Refresh(view,owned);
    }
    public void Refresh(ThreeKingdomsView view,int[] owned)
    {
        if(!Active)return;
        if(view==null||view.WindowId!=window||!Applicable(view,seat)||owned?.Length!=8){Reset();Notice="支付窗口已变化，已清除选择。";return;}
        hand=(int[])owned.Clone();UpdatePermissions(view);
        bool changed=cards.RemoveAll(c=>c.Ordinal>=hand[c.RealType])>0;
        if(Declaring&&!CanDeclare){Declaring=false;changed=true;}
        foreach(var c in cards)if(c.Use>=0&&!Declaring&&!allowed[c.RealType].Contains(c.Use)){c.Use=-1;changed=true;}
        SelectedIndex=Math.Min(SelectedIndex,cards.Count-1);
        if(changed)Notice="手牌或可用能力已变化，请重新核对支付。";
    }
    public bool CanSelect(int type,int ordinal) => Active&&!IsSubmitting&&type>=0&&type<8&&ordinal>=0&&ordinal<hand[type]&&(IsSelected(type,ordinal)||cards.Count<ExpectedCount);
    public bool IsSelected(int type,int ordinal) => cards.Any(c=>c.RealType==type&&c.Ordinal==ordinal);
    public void ToggleCard(int type,int ordinal)
    {
        if(!CanSelect(type,ordinal))return;
        int i=cards.FindIndex(c=>c.RealType==type&&c.Ordinal==ordinal);
        if(i>=0){cards.RemoveAt(i);SelectedIndex=Math.Min(i,cards.Count-1);}else{cards.Add(new B45PaymentCard{RealType=type,Ordinal=ordinal});SelectedIndex=cards.Count-1;}
        Notice=null;
    }
    public void FocusCard(int index){if(!IsSubmitting&&index>=0&&index<cards.Count)SelectedIndex=index;}
    public void ChooseUse(int type){if(!IsSubmitting&&Current!=null&&AllowedUses.Contains(type)){Current.Use=type;Notice=null;}}
    public void SetDeclaring(bool value)
    {
        if(!Active||IsSubmitting||value&&!CanDeclare||Declaring==value)return;
        Declaring=value;foreach(var c in cards)c.Use=-1;Notice="已更换支付方式，请逐张重新确认用途。";
    }
    public void ClearSelections(){if(IsSubmitting)return;cards.Clear();SelectedIndex=-1;Notice="已重选；请点真实手牌。";}
    public bool Lock(){if(!Ready)return false;IsSubmitting=true;return true;}
    public void Unlock(string reason){IsSubmitting=false;Notice=reason;}
    public void Reset(){bankTrade=false;bankRate=bankReceiveCount=0;bankEffectiveType=-1;Active=Declaring=CanDeclare=IsSubmitting=false;cards.Clear();cost=null;SelectedIndex=-1;ExpectedCount=0;Title=Notice=null;}
    public static T Copy<T>(T value)
    {
        using(var stream=new MemoryStream()){var codec=new DataContractJsonSerializer(typeof(T));codec.WriteObject(stream,value);stream.Position=0;return (T)codec.ReadObject(stream);}
    }
}
