using System.Text.Json;
using Catan.Core.ThreeKingdoms;
using Xunit;

namespace Catan.Rules.Tests;

internal sealed class ThreeKingdomsB5CivilTestHost : IThreeKingdomsB5Host
{
    public ThreeKingdomsB2TestHost Inner { get; set; } = new();
    public ThreeKingdomsState State => Inner.State;
    public Dictionary<string,int[]> Technology { get; set; } = new();
    public Dictionary<string,int[]> Suppressed { get; set; } = new();
    public Dictionary<string,int> Scores { get; set; } = new();
    public List<B5BoardOption> Options { get; set; } = new();
    public List<B5BoardOption> Applied { get; set; } = new();
    public List<string> Protected { get; set; } = new();
    public List<B4VirtualUse> VirtualUses { get; set; } = new();
    public List<B4VirtualUse> Used { get; set; } = new();
    public static ThreeKingdomsB5CivilTestHost Create(string first,string second="tk-g06",string third="tk-g09")
    {
        var h=new ThreeKingdomsB5CivilTestHost { Inner=ThreeKingdomsB2TestHost.Create(first,second,third) };
        h.State.RulesVersion=GeneralCatalog.B5RulesVersion;h.State.PackageVersion=GeneralCatalog.B5PackageVersion;h.State.PackageContentHash=GeneralCatalog.B5ContentHash;h.State.CivilB5=new B5CivilState();h.State.Board=new B5BoardState();
        foreach(var p in h.State.SeatPlayerIds){h.Technology[p]=new int[3];h.Suppressed[p]=new int[3];h.Scores[p]=2;}
        return h;
    }
    public int[] GetHand(string p)=>Inner.GetHand(p); public int[] GetBank()=>Inner.GetBank();
    public void RemoveHand(string p,int[] c)=>Inner.RemoveHand(p,c);public void AddHand(string p,int[] c)=>Inner.AddHand(p,c);
    public void RemoveBank(int[] c)=>Inner.RemoveBank(c);public void AddBank(int[] c)=>Inner.AddBank(c);
    public int DrawRandom(int max)=>Inner.DrawRandom(max);
    public B2ProgressCard[] GetProgressCards(string p)=>Inner.GetProgressCards(p);public int GetProgressDeckCount(int c)=>Inner.GetProgressDeckCount(c);
    public void RemoveProgress(string p,string id)=>Inner.RemoveProgress(p,id);public B2ProgressCard DrawProgress(string p,int c)=>Inner.DrawProgress(p,c);public void ReturnProgress(B2ProgressCard c)=>Inner.ReturnProgress(c);
    public B2HarvestTile[] GetHarvestTiles(string p)=>Inner.GetHarvestTiles(p);public int FinalDiceTotal=>Inner.FinalDiceTotal;
    public string[] GetProducingTileIds(string p)=>GetHarvestTiles(p).Select(t=>t.TileId).ToArray();
    public int GetHandLimit(string p)=>7;public B4VirtualUse[] GetVirtualProgressUses(string p,string timing)=>VirtualUses.ToArray();
    public void UseVirtualProgress(string p,B4VirtualUse use)=>Used.Add(use);
    public B4TokenMove[] GetTokenMoves(string p)=>Array.Empty<B4TokenMove>();public void MoveToken(string p,B4TokenMove move)=>throw new NotSupportedException();
    public void ReceiveExtraHarvest(string p,int total,string id)=>throw new NotSupportedException();public void RequestEndPlay(string p)=>throw new NotSupportedException();
    public bool TryCaptureProgressCard(string from,string to,B2ProgressCard c,string id)=>false;
    public B5BoardAsset[] GetBoardAssets(string? p=null)=>Array.Empty<B5BoardAsset>();
    public B5BoardOption[] GetBoardOptions(string p,string kind,string? arg=null,int budget=0)=>Options.Where(o=>o.Kind==kind&&o.PlayerId==p&&(arg==null||o.OtherPlayerId==arg)&&(kind!="free-military"||o.Level<=budget)).ToArray();
    public void ApplyBoardOption(string p,B5BoardOption option)
    {Assert.Equal(p,option.PlayerId);Assert.Contains(Options,o=>o.Id==option.Id);Options.RemoveAll(o=>o.Id==option.Id);Applied.Add(option);}
    public B5BoardOption AddOption(string kind,string id,string target="P2",int level=1,int paid=0,string asset="Road",params int[] cost)
    {var o=new B5BoardOption {Id=id,Kind=kind,PlayerId="P1",OtherPlayerId=target,TargetId=id,Level=level,PaidCount=paid,AssetKind=asset,Cost=cost.Concat(new int[8]).Take(8).ToArray(),Label=id};Options.Add(o);return o;}
    public int GetScore(string p)=>Scores[p];public int[] GetImprovements(string p)=>Technology[p].ToArray();public int[] GetActualImprovements(string p)=>Enumerable.Range(0,3).Select(i=>Technology[p][i]+Suppressed[p][i]).ToArray();
    public void ChangeImprovement(string p,int track,int delta,bool temporary=false){Assert.InRange(Technology[p][track]+delta,0,5);Technology[p][track]+=delta;if(temporary)Suppressed[p][track]-=delta;}
    public void RestoreImprovements(string p){for(int i=0;i<3;i++){Technology[p][i]+=Suppressed[p][i];Suppressed[p][i]=0;}}
    public void SetCollapseProtection(string p){if(!Protected.Contains(p))Protected.Add(p);}public void AddPermanentScore(string p,int delta)=>Scores[p]+=delta;
    public void ResolveInvasion(string id,string[] participants,bool virtualInvasion)=>throw new NotSupportedException();public int[] GetAllRedResourceTypes()=>new[]{0,1,3};
    public string Snapshot()=>JsonSerializer.Serialize(this);public ThreeKingdomsB5CivilTestHost Copy()=>JsonSerializer.Deserialize<ThreeKingdomsB5CivilTestHost>(Snapshot())!;
    public void Conserved(){var escrow=B2SkillEngine.EscrowCards(State);for(int t=0;t<8;t++)Assert.Equal(30,Inner.Bank[t]+Inner.Hands.Values.Sum(c=>c[t])+escrow[t]);}
}
