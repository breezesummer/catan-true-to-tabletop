using Catan.Core.ThreeKingdoms;
using Xunit;

public sealed class GeneralVoiceTests
{
    static B4SkillHistoryView Event(string id,string skill="tk-g01-s1",bool complete=false)=>new(){EventId=id,SkillId=skill,Complete=complete};
    [Fact] public void InitialHistoryAndCompletionNeverReplay()
    {
        var cursor=new GeneralVoiceCursor();var played=new List<string>();
        cursor.Observe(new[]{Event("old")},false,played.Add);
        cursor.Observe(new[]{Event("old"),Event("new")},false,played.Add);
        cursor.Observe(new[]{Event("old"),Event("new",complete:true)},false,played.Add);
        Assert.Single(played);
    }
    [Fact] public void BatchRetainsDifferentPlayersAndRepeatedSkills()
    {
        var cursor=new GeneralVoiceCursor();var played=new List<string>();
        cursor.Observe(Array.Empty<B4SkillHistoryView>(),false,played.Add);
        cursor.Observe(new[]{Event("a"),Event("b","tk-g02-s1"),Event("c")},false,played.Add);
        Assert.Equal(new[]{"tk-g01-s1","tk-g02-s1","tk-g01-s1"},played);
    }
    [Fact] public void ReconnectLoadAndReplacementEstablishSilentBaseline()
    {
        var cursor=new GeneralVoiceCursor();var played=new List<string>();
        cursor.Observe(new[]{Event("a")},false,played.Add);
        cursor.Observe(new[]{Event("a"),Event("b")},true,played.Add);
        cursor.Observe(new[]{Event("a"),Event("b")},false,played.Add);
        cursor.Reset();cursor.Observe(new[]{Event("loaded")},false,played.Add);
        cursor.Observe(new[]{Event("replacement"),Event("history")},false,played.Add);
        Assert.Empty(played);
    }
}
