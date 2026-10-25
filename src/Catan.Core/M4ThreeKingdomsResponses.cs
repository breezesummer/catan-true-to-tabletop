using System;
using System.Linq;
using System.Runtime.Serialization;
using Catan.Core.ThreeKingdoms;

namespace Catan.Core.M4
{
    [DataContract] public sealed class B4ProgressResolution
    {
        [DataMember(Order=1)] public string EventId { get; set; }
        [DataMember(Order=2)] public string PlayerId { get; set; }
        [DataMember(Order=3)] public B2ProgressCard Card { get; set; }
        [DataMember(Order=4)] public bool Queued { get; set; }
        [DataMember(Order=5)] public bool Captured { get; set; }
        [DataMember(Order=6)] public bool Virtual { get; set; }
    }
    public sealed partial class GameState
    {
        [DataMember(Order=318,EmitDefaultValue=false)] public B4ProgressResolution[] ThreeKingdomsProgressResolutions { get; set; }
        [DataMember(Order=319,EmitDefaultValue=false)] public bool ThreeKingdomsEndPlayRequested { get; set; }
    }
    public sealed partial class CitiesKnightsGameSession
    {
        private sealed partial class B2Host : IThreeKingdomsB4ResponseHost
        {
            public B4TokenMove[] GetTokenMoves(string playerId) => session.B4TokenMoves(game,playerId);
            public void MoveToken(string playerId,B4TokenMove move) => session.B4MoveToken(game,playerId,move);
            public void ReceiveExtraHarvest(string playerId,int finalTotal,string eventId) => session.B4ExtraHarvest(game,playerId,finalTotal,eventId);
            public void RequestEndPlay(string playerId)
            { Require(game.ActivePlayerId==playerId,"NotActivePlayer","只能结束当前自己的出牌阶段。");game.ThreeKingdomsEndPlayRequested=true; }
            public bool TryCaptureProgressCard(string fromPlayerId,string toPlayerId,B2ProgressCard card,string eventId)
            {
                var entry=(game.ThreeKingdomsProgressResolutions??Array.Empty<B4ProgressResolution>()).SingleOrDefault(x=>x.EventId==eventId);
                if(entry==null||entry.Virtual||entry.Captured||entry.PlayerId!=fromPlayerId||entry.Card?.Id!=card.Id) return false;
                var value=(ProgressCardKind)Enum.Parse(typeof(ProgressCardKind),card.Id);
                var target=game.Players.Single(p=>p.Id==toPlayerId);
                if(card.PublicPoint)
                {
                    var source=game.Players.Single(p=>p.Id==fromPlayerId);var i=Array.IndexOf(source.ProgressVictoryCards,value);
                    if(i<0)return false;source.ProgressVictoryCards=source.ProgressVictoryCards.Where((x,n)=>n!=i).ToArray();
                    target.ProgressVictoryCards=Append(target.ProgressVictoryCards,value);
                }
                else target.ProgressCards=Append(target.ProgressCards,value);
                entry.Captured=true;return true;
            }
        }
        private static void QueueB4Progress(GameState s,string player,ProgressCardKind? card,string eventId,bool virtualCard=false)
        {
            if(!B4(s))return;
            s.ThreeKingdomsProgressResolutions=(s.ThreeKingdomsProgressResolutions??Array.Empty<B4ProgressResolution>()).Concat(new[]{new B4ProgressResolution {
                EventId=eventId??"progress-reveal-"+s.ThreeKingdoms.NextId++,PlayerId=player,Virtual=virtualCard,
                Card=card.HasValue?new B2ProgressCard {Id=card.Value.ToString(),Color=(int)ProgressCatalog.Track(card.Value),PublicPoint=ProgressCatalog.IsVictoryPoint(card.Value)}:null}}).ToArray();
        }
        private void FinishB4Progress(GameState s)
        {
            if(!B4(s)||B2Busy(s)||s.ThreeKingdoms.PendingDecision!=null||s.PendingDecision!=null||s.DecisionQueue.Length>0||s.ThreeKingdomsDeferredCommand!=null)return;
            while(s.ThreeKingdomsProgressResolutions?.Length>0)
            {
                var entry=s.ThreeKingdomsProgressResolutions[0];
                if(!entry.Queued)
                { entry.Queued=true;B2SkillEngine.OnB4ProgressCompleted(CardHost(s),entry.EventId,entry.PlayerId,entry.Card,entry.Virtual);if(B2Busy(s))return; }
                if(!entry.Virtual&&!entry.Captured&&entry.Card?.PublicPoint==false) CardHost(s).ReturnProgress(entry.Card);
                s.ThreeKingdomsProgressResolutions=s.ThreeKingdomsProgressResolutions.Skip(1).ToArray();
            }
        }
        private B4TokenMove[] B4TokenMoves(GameState s,string player)
        {
            bool huoshou=B5(s)&&s.ThreeKingdoms.Skills.Frames.Any(f=>f.Kind=="b5m-huoshou-move"&&f.PlayerId==player);
            if(!s.FirstBarbarianAttack||s.ActivePlayerId!=player&&!huoshou||s.PendingDecision!=null)return Array.Empty<B4TokenMove>();
            return s.Tiles.Where(t=>t.Resource!="sea"&&t.Id!=s.RobberTileId).Select(t=>new B4TokenMove {OptionId="robber:"+t.Id,
                Label="移动强盗至 "+t.Id,TokenKind="robber",TileId=t.Id}).ToArray();
        }
        private void B4MoveToken(GameState s,string player,B4TokenMove move)
        {
            Require(B4TokenMoves(s,player).Any(x=>x.OptionId==move.OptionId&&x.TileId==move.TileId),"InvalidRobberTile","该移动目标不合法。");
            if(B5(s)&&s.ThreeKingdoms.Skills.Frames.Any(f=>f.Kind=="b5m-huoshou-move"&&f.PlayerId==player))B5Board(s).TokenController=player;
            s.PendingDecision=new PendingDecision {Kind="Robber",PlayerId=player,ReturnPhase=GamePhase.Action};s.Phase=GamePhase.RobberMove;
            ApplyB2Native(s,new Command {Id="liuli-"+s.ThreeKingdoms.NextId++,PlayerId=player,Kind=CommandKind.MoveRobber,TargetId=move.TileId});
        }
        private void B4ExtraHarvest(GameState s,string player,int total,string eventId)
        {
            if(total==7||B2DrawBlocked(s,player))return;
            var host=CardHost(s);var before=host.GetHand(player);var cards=new int[8];
            foreach(var tile in s.Tiles.Where(t=>t.Number==total&&(t.Id!=s.RobberTileId||B2SkillEngine.B5DoubleHarvest(s.ThreeKingdoms,player,t.Id))&&t.Resource!="sea"&&t.Resource!="desert"))
            {
                int settlements=s.Settlements.Count(b=>b.PlayerId==player&&B5Working(s,"Settlement",b.LocationId)&&tile.Vertices.Contains(b.LocationId));
                int cities=s.Cities.Count(b=>b.PlayerId==player&&tile.Vertices.Contains(b.LocationId));
                if(B2SkillEngine.B5DoubleHarvest(s.ThreeKingdoms,player,tile.Id)){settlements*=2;cities*=2;}
                int resource=Array.FindIndex(Resources,r=>ResourceName(r)==tile.Resource);if(resource<0)continue;
                cards[resource]+=settlements+(resource==1||resource==3?2:1)*cities;
                int commodity=resource==0?7:resource==2?5:resource==4?6:-1;if(commodity>=0)cards[commodity]+=cities;
            }
            var bank=host.GetBank();for(int i=0;i<8;i++)cards[i]=Math.Min(cards[i],bank[i]);
            host.RemoveBank(cards);host.AddHand(player,cards);
            B2SkillEngine.OnCardsMoved(host,eventId,new[]{new B2HandChange {PlayerId=player,BeforeCount=before.Sum(),AfterCount=before.Sum()+cards.Sum(),GainedCards=cards,SourceSkillId="tk-g08-s2"}});
        }
    }
}
