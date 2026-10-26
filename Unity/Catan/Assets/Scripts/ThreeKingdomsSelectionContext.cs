#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Catan.Core.ThreeKingdoms;

// 只保存客户端草稿；既不持有权威状态，也不提交命令、扣牌或推进随机。
public sealed class B45SelectionContext
{
    readonly HashSet<string> handKeys=new HashSet<string>();
    readonly List<string> players=new List<string>();
    readonly List<string> tiles=new List<string>();
    readonly List<string> progress=new List<string>();
    readonly List<string> progressKeys=new List<string>();
    readonly int[] bankCounts=new int[8];
    readonly int[] privateCounts=new int[8];
    string optionKey;
    int selectedDie=-1;
    bool submitting;
    public B45SelectionContract Contract {get;private set;}
    public bool Active => Contract!=null;
    public string SkillId => Contract?.SkillId;
    public bool IsSubmitting => submitting;
    public int SelectedDie => selectedDie;
    public int[] DiceIndices => (Contract?.Actions??Array.Empty<B2SkillAction>()).Where(a=>a.SelectionDiceIndex.HasValue).Select(a=>a.SelectionDiceIndex.Value).Distinct().ToArray();
    public bool SelectDie(int index)
    {if(!Active||submitting||!DiceIndices.Contains(index))return false;selectedDie=index;optionKey=null;tiles.Clear();if(Options.Length==1)optionKey=B45SelectionContract.OptionKey(Options[0]);return true;}
    public int[] SelectedCounts => Enumerable.Range(0,8).Select(t=>handKeys.Count(k=>k.StartsWith(t+":",StringComparison.Ordinal))).ToArray();
    public int[] SelectedPrivateCounts => (int[])privateCounts.Clone();
    public int[] SelectedBankCounts => (int[])bankCounts.Clone();
    public bool RequiresBankCards => Current().Any(a=>a.RequiredCardCount.HasValue);
    public int RequiredBankCount => Current().Where(a=>a.RequiredCardCount.HasValue).Select(a=>a.RequiredCardCount.Value).DefaultIfEmpty(0).Max();
    public B2SkillAction[] BankOptions => Current().Where(a=>a.RequiredCardCount.HasValue).ToArray();
    public string[] SelectedPlayers => players.ToArray();
    public string[] SelectedTiles => tiles.ToArray();
    public string[] SelectedProgress => progress.ToArray();
    public bool CanCancel => Contract?.CanCancel==true && !submitting;
    public B2SkillCommand SkipCommand => submitting?null:B45SelectionContract.Copy(Contract?.SkipCommand);
    public string Description => Contract?.Description??"";
    public string Notice {get;private set;}
    public string Summary => (RequiresBankCards?"已选银行牌 "+bankCounts.Sum()+" / "+RequiredBankCount+" 张 · ":"")+"已选手牌 "+SelectedCounts.Sum()+" 张"+(progress.Count>0?" · 发展牌 "+progress.Count+" 张":"")+(players.Count>0?" · 目标 "+players.Count+" 人":"")+(tiles.Count>0?" · 棋盘目标 "+tiles.Count+" 项":"")+(privateCounts.Sum()>0?" · 授权目标手牌 "+privateCounts.Sum()+" 张":"");
    public B2SkillAction[] CandidateActions => Current().ToArray();
    public B2SkillAction[] Options => Subset||Poxi?Array.Empty<B2SkillAction>():(Contract?.Actions??Array.Empty<B2SkillAction>()).Where(a=>!B45SelectionContract.IsSkip(a.Command)&&(!a.SelectionDiceIndex.HasValue||selectedDie<0||a.SelectionDiceIndex==selectedDie)).GroupBy(B45SelectionContract.OptionKey).Select(g=>g.First()).ToArray();
    public bool CanConfirm => !submitting && BuildCommand()!=null;
    bool Subset => Contract?.DirectBatch==true&&(Contract.DecisionKind=="b4-reveal"||Contract.DecisionKind=="b4-subset");
    bool Poxi => Contract?.DirectBatch==true&&Contract.DecisionKind=="response-poxi-select";
    static string HandKey(int type,int ordinal)=>type+":"+ordinal;
    static bool SameSet(IEnumerable<string> first,IEnumerable<string> second) => first.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(second.OrderBy(x=>x,StringComparer.Ordinal));
    IEnumerable<B2SkillAction> Current() => (Contract?.Actions??Array.Empty<B2SkillAction>()).Where(a=>!B45SelectionContract.IsSkip(a.Command)&&(optionKey==null||B45SelectionContract.OptionKey(a)==optionKey)&&(!a.SelectionDiceIndex.HasValue||selectedDie<0||a.SelectionDiceIndex==selectedDie));
    public void Begin(ThreeKingdomsView view,string viewerId,string skillId,int[] hand)
    {
        Reset();Contract=B45SelectionContract.Build(view,viewerId,skillId,hand);
        if(Contract.Actions.Length==0){Reset();return;}
        if(DiceIndices.Length==1)selectedDie=DiceIndices[0];
        if(Options.Length==1)optionKey=B45SelectionContract.OptionKey(Options[0]);
    }
    public void Begin(ThreeKingdomsView view,string skillId,int[] hand)
    {Begin(view,view?.SkillDecision?.PlayerId??view?.Clock?.ActivePlayerId,skillId,hand);}
    public void Refresh(ThreeKingdomsView view,int[] hand)
    {
        if(!Active)return;
        var updated=B45SelectionContract.Build(view,Contract.ViewerId,SkillId,hand);
        // 本人独立选将不由此模型管理；技能投影变更必须严格匹配本人窗口与待决。
        if(updated.WindowId!=Contract.WindowId||updated.DecisionId!=Contract.DecisionId||updated.Actions.Length==0)
        {Reset();Notice="操作窗口已经变化，已清除未提交选择。";return;}
        Contract=updated;
        if(optionKey!=null&&!Options.Any(a=>B45SelectionContract.OptionKey(a)==optionKey))optionKey=null;
        if(Options.Length==1)optionKey=B45SelectionContract.OptionKey(Options[0]);
        bool changed=handKeys.RemoveWhere(k=>{var split=k.Split(':');int t=int.Parse(split[0]),n=int.Parse(split[1]);return n>=Contract.Hand[t];})>0;
        changed|=players.RemoveAll(p=>!Current().Any(a=>B45SelectionContract.Players(a).Contains(p)))>0;
        changed|=tiles.RemoveAll(t=>!Current().Any(a=>B45SelectionContract.Board(a).Contains(t)))>0;
        changed|=PruneProgress(p=>!Current().Any(a=>B45SelectionContract.Progress(a).Contains(p)));
        for(int t=0;t<8;t++)if(privateCounts[t]>0&&!PrivateActions().Any(a=>a.Command.CardType==t)){privateCounts[t]=0;changed=true;}
        for(int t=0;t<8;t++){int available=BankOptions.Where(a=>a.Command.CardType==t).Select(a=>a.AvailableCount??0).DefaultIfEmpty(0).Max();if(bankCounts[t]>available){bankCounts[t]=0;changed=true;}}
        if(changed)Notice="牌或目标已变化，失效选择已清除。";
    }
    public void RevalidateProgressCopies(string[] ownedKinds)
    {
        if(!Active)return;var counts=(ownedKinds??Array.Empty<string>()).GroupBy(x=>x).ToDictionary(g=>g.Key,g=>g.Count());bool changed=false;
        for(int i=progress.Count-1;i>=0;i--)
        {
            string key=progressKeys[i];int split=key.LastIndexOf(':');int ordinal=int.Parse(key.Substring(split+1));
            if(!counts.TryGetValue(progress[i],out int count)||ordinal>=count){progress.RemoveAt(i);progressKeys.RemoveAt(i);changed=true;}
        }
        if(changed)Notice="所选发展牌已不在当前手牌中，请重新选择。";
    }
    bool PruneProgress(Func<string,bool> remove)
    {bool changed=false;for(int i=progress.Count-1;i>=0;i--)if(remove(progress[i])){progress.RemoveAt(i);progressKeys.RemoveAt(i);changed=true;}return changed;}
    public void Reset()
    {Contract=null;ClearSelections();optionKey=null;selectedDie=-1;submitting=false;Notice=null;}
    public void ClearSelections()
    {handKeys.Clear();players.Clear();tiles.Clear();progress.Clear();progressKeys.Clear();Array.Clear(privateCounts,0,8);Array.Clear(bankCounts,0,8);Notice=null;}
    public void Reselect(){if(!submitting)ClearSelections();}
    public bool ChooseOption(B2SkillAction action)
    {
        if(!Active||submitting||action==null||!Options.Any(a=>B45SelectionContract.OptionKey(a)==B45SelectionContract.OptionKey(action)))return false;
        string next=B45SelectionContract.OptionKey(action);if(next==optionKey)return true;
        optionKey=next;
        // 分支更换只保留仍可用于新分支的选择，不能将其替换成另一张牌。
        var live=Current().ToArray();
        handKeys.RemoveWhere(k=>{int type=int.Parse(k.Split(':')[0]);return !live.Any(a=>B45SelectionContract.Cost(a)[type]>0);});
        players.RemoveAll(p=>!live.Any(a=>B45SelectionContract.Players(a).Contains(p)));
        tiles.RemoveAll(t=>!live.Any(a=>B45SelectionContract.Board(a).Contains(t)));
        PruneProgress(p=>!live.Any(a=>B45SelectionContract.Progress(a).Contains(p)));
        return true;
    }
    public bool SelectOption(B2SkillAction action)=>ChooseOption(action);
    public bool IsOptionSelected(B2SkillAction action)=>action!=null&&optionKey==B45SelectionContract.OptionKey(action);
    public bool IsHandSelected(int type,int ordinal)=>handKeys.Contains(HandKey(type,ordinal));
    public bool CanSelectHand(int type,int ordinal=0)
    {
        if(!Active||submitting||type<0||type>=8||ordinal<0||ordinal>=Contract.Hand[type])return false;
        if(IsHandSelected(type,ordinal))return true;
        if(Subset)return true;
        if(Poxi)return SelectedCounts[type]+privateCounts[type]==0&&SelectedCounts.Sum()+privateCounts.Sum()<4&&
            Contract.Actions.Any(a=>a.Command.Option=="response-poxi-select"&&a.Command.CardType==type&&a.Command.TargetPlayerIds.FirstOrDefault()==Contract.ViewerId);
        var selected=SelectedCounts;selected[type]++;
        return Current().Any(a=>Compatible(a,false,true,true)&&AllowsCost(a,selected,false));
    }
    public bool ToggleHand(int type,int ordinal)=>ToggleCard(type,ordinal);
    public bool ToggleCard(int type,int ordinal)
    {
        if(!CanSelectHand(type,ordinal))return false;
        string key=HandKey(type,ordinal);if(!handKeys.Remove(key))handKeys.Add(key);
        if(!Subset&&!Poxi)RevalidateTargets();return true;
    }
    public bool CanSelectPlayer(string id)
    {
        if(!Active||submitting||string.IsNullOrEmpty(id)||Poxi)return false;
        if(players.Contains(id))return true;
        int max=Current().Select(a=>B45SelectionContract.Players(a).Length).DefaultIfEmpty(0).Max();
        var next=max==1?new[]{id}:players.Concat(new[]{id}).ToArray();
        return Current().Any(a=>next.All(B45SelectionContract.Players(a).Contains)&&Compatible(a,true,false,true));
    }
    public bool TogglePlayer(string id)
    {
        if(!CanSelectPlayer(id))return false;if(players.Remove(id))return true;
        int max=Current().Select(a=>B45SelectionContract.Players(a).Length).DefaultIfEmpty(0).Max();
        if(max==1)players.Clear();players.Add(id);RevalidateCosts();return true;
    }
    public bool CanSelectTile(string id)
    {
        if(!Active||submitting||string.IsNullOrEmpty(id))return false;if(tiles.Contains(id))return true;
        int max=Current().Select(a=>B45SelectionContract.Board(a).Length).DefaultIfEmpty(0).Max();
        var next=max==1?new[]{id}:tiles.Concat(new[]{id}).ToArray();
        return Current().Any(a=>next.All(B45SelectionContract.Board(a).Contains)&&Compatible(a,true,true,false));
    }
    public bool ToggleTile(string id)
    {
        if(!CanSelectTile(id))return false;if(tiles.Remove(id))return true;
        int max=Current().Select(a=>B45SelectionContract.Board(a).Length).DefaultIfEmpty(0).Max();
        if(max==1)tiles.Clear();tiles.Add(id);RevalidateCosts();return true;
    }
    public bool IsProgressSelected(string id,int ordinal=0)=>progressKeys.Contains(id+":"+ordinal);
    public bool CanSelectProgress(string id,int ordinal=0)
    {
        if(!Active||submitting||id==null||ordinal<0)return false;if(IsProgressSelected(id,ordinal))return true;
        var next=progress.Concat(new[]{id}).ToArray();
        return Current().Any(a=>next.GroupBy(x=>x).All(g=>B45SelectionContract.Progress(a).Count(x=>x==g.Key)>=g.Count())&&Compatible(a,true,true,true));
    }
    public bool ToggleProgress(string id,int ordinal=0)
    {
        if(!CanSelectProgress(id,ordinal))return false;string key=id+":"+ordinal;int index=progressKeys.IndexOf(key);
        if(index>=0){progressKeys.RemoveAt(index);progress.RemoveAt(index);}else {progressKeys.Add(key);progress.Add(id);}return true;
    }
    public bool ToggleBankCard(int type,int delta=1)
    {
        if(!Active||submitting||type<0||type>=8||delta==0)return false;
        int next=bankCounts[type]+Math.Sign(delta);if(next<0)return false;
        int available=BankOptions.Where(a=>a.Command.CardType==type).Select(a=>a.AvailableCount??0).DefaultIfEmpty(0).Max();
        if(next>available||bankCounts.Sum()+Math.Sign(delta)>RequiredBankCount)return false;
        bankCounts[type]=next;return true;
    }
    IEnumerable<B2SkillAction> PrivateActions() => (Contract?.Actions??Array.Empty<B2SkillAction>()).Where(a=>a.Command.Option=="response-poxi-select"&&a.Command.TargetPlayerIds.FirstOrDefault()!=Contract.ViewerId);
    public B2SkillAction[] PrivateOptions => PrivateActions().ToArray();
    public bool TogglePrivateCard(int type)
    {
        if(!Poxi||submitting||type<0||type>=8)return false;
        if(privateCounts[type]>0){privateCounts[type]=0;return true;}
        if(SelectedCounts[type]>0||SelectedCounts.Sum()+privateCounts.Sum()>=4||!PrivateActions().Any(a=>a.Command.CardType==type))return false;
        privateCounts[type]=1;return true;
    }
    bool Compatible(B2SkillAction a,bool cost,bool target,bool board)
    {
        if(cost&&!AllowsCost(a,SelectedCounts,false))return false;
        if(target&&!players.All(B45SelectionContract.Players(a).Contains))return false;
        if(board&&!tiles.All(B45SelectionContract.Board(a).Contains))return false;
        return progress.GroupBy(x=>x).All(g=>B45SelectionContract.Progress(a).Count(x=>x==g.Key)>=g.Count());
    }
    bool AllowsCost(B2SkillAction action,int[] selected,bool complete)
    {
        var cost=B45SelectionContract.Cost(action);int total=selected.Sum();
        if(B45SelectionContract.Batch(action))
        {
            var candidates=Current().Where(a=>a.Command.Option==action.Command.Option&&players.All(B45SelectionContract.Players(a).Contains)).ToArray();
            int maximum=action.Command.Option=="exchange-hand"?4:Contract.Hand.Sum();
            return (!complete||total>=1)&&total<=maximum&&Enumerable.Range(0,8).All(t=>selected[t]>=0&&selected[t]<=Contract.Hand[t]&&(selected[t]==0||candidates.Any(a=>B45SelectionContract.Cost(a)[t]>0)));
        }
        return Enumerable.Range(0,8).All(t=>selected[t]>=0&&(complete?selected[t]==cost[t]:selected[t]<=cost[t]));
    }
    void RevalidateTargets()
    {
        if(Current().Any(a=>Compatible(a,true,true,true)))return;
        players.Clear();tiles.Clear();Notice="费用变化，需重新选择目标。";
    }
    void RevalidateCosts()
    {
        if(Current().Any(a=>Compatible(a,true,true,true)))return;
        handKeys.Clear();progress.Clear();progressKeys.Clear();Notice="目标变化，需重新选择费用。";
    }
    public B2SkillCommand BuildCommand()
    {
        if(!Active||submitting||DiceIndices.Length>0&&selectedDie<0)return null;
        if(RequiresBankCards)
        {
            if(bankCounts.Sum()!=RequiredBankCount||Enumerable.Range(0,8).Any(t=>bankCounts[t]>BankOptions.Where(a=>a.Command.CardType==t).Select(a=>a.AvailableCount??0).DefaultIfEmpty(0).Max()))return null;
            var c=B45SelectionContract.Copy(BankOptions.FirstOrDefault()?.Command);if(c==null)return null;c.CardType=-1;c.Counts=(int[])bankCounts.Clone();return c;
        }
        if(Subset)
        {
            var c=B45SelectionContract.Copy(Contract.Actions.FirstOrDefault()?.Command);if(c==null)return null;
            if(Contract.DecisionKind=="b4-subset"&&SelectedCounts.Sum()==0)return null;
            c.Option="b45-selected";c.CardType=-1;c.Counts=SelectedCounts;return c;
        }
        if(Poxi)
        {
            if(SelectedCounts.Sum()+privateCounts.Sum()!=4||Enumerable.Range(0,8).Any(t=>SelectedCounts[t]+privateCounts[t]>1))return null;
            var c=B45SelectionContract.Copy(Contract.Actions.FirstOrDefault()?.Command);if(c==null)return null;
            c.Option="b45-poxi-confirm";c.CardType=-1;c.Counts=SelectedCounts;c.SecondaryCounts=(int[])privateCounts.Clone();c.TargetPlayerIds=Array.Empty<string>();return c;
        }
        var ready=Current().Where(a=>AllowsCost(a,SelectedCounts,true)&&SameSet(players,B45SelectionContract.Players(a))&&SameSet(tiles,B45SelectionContract.Board(a))&&SameSet(progress,B45SelectionContract.Progress(a))).ToArray();
        // 不替玩家选择尚未选定的用途；同组具体命令只能因同一选择等价。
        if(ready.Length==0||optionKey==null&&ready.Select(B45SelectionContract.OptionKey).Distinct().Count()!=1)return null;
        var result=B45SelectionContract.Copy(ready[0].Command);
        if(B45SelectionContract.Batch(ready[0]))result.Counts=SelectedCounts;
        return result;
    }
    public bool TryBuildCommand(out B2SkillCommand command){command=BuildCommand();return command!=null;}
    public B2SkillCommand BeginSubmit()
    {var command=BuildCommand();if(command!=null)submitting=true;return command;}
    public void AcknowledgeFailure(){submitting=false;}
}
