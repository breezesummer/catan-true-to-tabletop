using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.AI;
using UnityEngine;

public sealed partial class M7PlayerVerification
{
    IEnumerator VerifySharedSkill(string shared)
    {
        var client=app.Network;
        var game=GetComponent<M5App>();
        var ai=new CombinedAi();
        string seat=client.SeatId;
        Require(AuthorityAbsent()&&app.PlayerCount==4&&app.ThreeKingdoms&&app.Fishing&&app.Harbormaster&&app.ScenarioId=="through-the-desert","四人组合模式");
        Signal(Path.Combine(shared,"joined-"+seat),"OK");
        while(Waiting(Directory.GetFiles(shared,"joined-P*").Length==4))yield return null;
        report.Stage="四席等待同一武将技能演出";
        double nextMove=0;
        string seen=null;
        while(Waiting(seen!=null))
        {
            yield return new WaitForEndOfFrame();
            var v=game.M6View;
            if(app.PlayingSkillEvent!=null&&app.CentralSkillPhase>=.42f&&app.CentralSkillOpacity>.85f)
            {
                var e=v.ThreeKingdoms.SkillHistory.First(item=>item.EventId==app.PlayingSkillEvent);
                if(GeneralSkinArt.Get(e.GeneralId)!=null)
                {
                    seen=e.EventId;
                    var frame=ScreenCapture.CaptureScreenshotAsTexture();
                    try{File.WriteAllBytes(Path.Combine(config.OutputDirectory,"中央技能.png"),frame.EncodeToPNG());}
                    finally{Destroy(frame);}
                    Signal(Path.Combine(shared,"skill-"+seat),e.EventId+" "+e.GeneralId+" "+e.SkillId+" "+e.PlayerId);
                    continue;
                }
            }
            if(!client.CanInteract||app.TablePresentationBlocked||Time.unscaledTimeAsDouble<nextMove)continue;
            string actor=v.ThreeKingdoms.PendingDecision?.PlayerId??v.ThreeKingdoms.SkillDecision?.PlayerId??
                (v.TradeOffer!=null?v.TradeOffer.NextResponder(seat):v.PendingDecision?.PlayerId??v.ActivePlayerId);
            bool act=v.Phase==Catan.Core.M5.GamePhase.Discard?v.Discards.Any(d=>d.PlayerId==seat):actor==seat;
            if(!act)continue;
            var command=ai.Decide(v);
            command.Id=Guid.NewGuid().ToString("N");game.Submit(command);
            report.HumanCommands++;nextMove=Time.unscaledTimeAsDouble+.2;
        }
        while(Waiting(Directory.GetFiles(shared,"skill-P*").Length==4))yield return null;
        var observations=Directory.GetFiles(shared,"skill-P*").Select(File.ReadAllText).Distinct().ToArray();
        Require(observations.Length==1,"四席同一公开事件");
        report.Transport=client.TransportMode;report.Revision=client.Revision;report.Turn=Turn();
        Signal(Path.Combine(shared,"done-"+seat),"OK");
        while(Waiting(Directory.GetFiles(shared,"done-P*").Length==4))yield return null;
        client.Leave();while(Waiting(client.RoomId==null&&!client.Busy))yield return null;
    }
}
