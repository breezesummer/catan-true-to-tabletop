using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.Core;
using Catan.Core.M5;
using Catan.Core.ThreeKingdoms;
using UnityEngine;
using Command = Catan.Core.M5.Command;
using CommandKind = Catan.Core.M5.CommandKind;
using GameState = Catan.Core.M5.GameState;
using GamePhase = Catan.Core.M5.GamePhase;
using PlayerState = Catan.Core.M5.PlayerState;

public sealed partial class TabletopUiVerification
{
    string auditSave;
    M5App auditGame;
    CombinedGameSession auditSession;
    GameState auditStart;
    string auditItem,auditStatus,auditFixtures;
    T Fixture<T>(string id,string ext) where T:class
    {
        var path=Path.Combine(auditFixtures,id+"."+ext+".json");
        if(!File.Exists(path))return null;
        using var stream=File.OpenRead(path);
        return (T)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T)).ReadObject(stream);
    }
    string auditLog => Path.Combine(output,"逐项结果.tsv");
    System.Reflection.FieldInfo stateField=>typeof(CombinedGameSession).GetField("state",PrivateInstance);
    IEnumerator VerifyWholeTable(string[] args)
    {
        mode="combined";
        auditFixtures=args[Array.IndexOf(args,"--audit-all")+1];
        auditSave=args[Array.IndexOf(args,"--direct-input")+1];app.ReadSave(auditSave);
        yield return null;yield return null;
        Require(app.ThreeKingdoms&&app.Fishing&&app.Harbormaster&&app.PlayerCount==4&&app.ScenarioId=="through-the-desert","对局配置错误");
        auditGame=GetComponent<M5App>();
        auditSession=(CombinedGameSession)typeof(M5LocalGameHost).GetField("session",PrivateInstance).GetValue(auditGame.VerificationHost);
        auditStart=auditSession.GetAuthoritativeStateForTesting();
        File.WriteAllText(auditLog,"编号\t项目\t结果\t实际操作\n");
        foreach(var kind in (ProgressCardKind[])Enum.GetValues(typeof(ProgressCardKind)))
            foreach(var frame in AuditItem(kind.ToString(),DevelopmentCardText.Name(kind.ToString()),AuditCard(kind)))yield return frame;
        foreach(var general in GeneralCatalog.B6Definitions)
            foreach(var skill in general.Skills)
                foreach(var frame in AuditItem(skill.Id,general.Name+"·"+skill.Name,AuditSkill(general,skill)))yield return frame;
        configurations++;
    }
    IEnumerable AuditItem(string id,string name,IEnumerator run)
    {
        auditItem=id;auditStatus="未完成";
        var filter=Environment.GetCommandLineArgs();int flag=Array.IndexOf(filter,"--audit-items");
        if(flag>=0&&!filter[flag+1].Split(',').Contains(id))yield break;
        app.ReadSave(auditSave);yield return null;yield return null;
        auditGame=GetComponent<M5App>();
        auditSession=(CombinedGameSession)typeof(M5LocalGameHost).GetField("session",PrivateInstance).GetValue(auditGame.VerificationHost);
        int start=directClickLog.Count;
        while(true)
        {
            object next;
            try{if(!run.MoveNext())break;next=run.Current;}
            catch(Exception ex){auditStatus="待复核："+ex.Message;break;}
            yield return next;
        }
        File.AppendAllText(auditLog,id+"\t"+name+"\t"+auditStatus.Replace("\n"," ")+"\t"+string.Join("；",directClickLog.Skip(start))+"\n");
        Debug.Log("逐项验收 "+id+" "+auditStatus);
        yield return new WaitForEndOfFrame();SaveOceanFrame(id+"-结果.png");
    }
    GameState PrepareAudit()
    {
        typeof(M6App).GetProperty("HumanSeat").SetValue(app,"P1");
        stateField.SetValue(auditSession,auditStart);
        var state=auditSession.GetAuthoritativeStateForTesting();
        var p=state.Players.First(x=>x.Id=="P1");
        foreach(Resource r in Enum.GetValues(typeof(Resource)))
        {
            int add=Math.Min(Math.Max(0,3-p.Resources[r]),state.Bank[r]);
            p.Resources[r]+=add;state.Bank[r]-=add;
        }
        return state;
    }
    void ActivateAudit(GameState state)
    {
        typeof(M6App).GetMethod("ResetThreeKingdomsPanel",PrivateInstance).Invoke(app,null);
        stateField.SetValue(auditSession,state);
        auditSession.AssertInvariants();
        auditGame.M6Refresh();
    }
    Vector2 Avatar(string seat)
    {
        var seats=M6App.OrderedPlayers(app.HistoryPlayers);
        return TabletopUI.PlayerAvatarRect(Array.IndexOf(seats,seat),seats.Length).center;
    }
    IEnumerable MapClick(string target)
    {
        var board=auditGame.VerificationBoard;
        var point=board.ScreenPoint(target);
        var center=MobileTabletop.ScreenPoint(new Vector2(730,420));
        Table.PanFromScreen(new Vector2(point.x,Screen.height-point.y),new Vector2(center.x,Screen.height-center.y));
        Table.EndPan();
        yield return null;
        foreach(var wait in DirectClick(MobileTabletop.Logical(board.ScreenPoint(target)),"点棋盘 "+target))yield return wait;
    }
    void GiveCard(GameState state,PlayerState p,ProgressCardKind kind)
    {
        if(p.ProgressCards.Contains(kind))return;
        var deck=state.ProgressDecks.First(d=>d.Cards.Contains(kind));
        var cards=deck.Cards.ToList();cards.Remove(kind);deck.Cards=cards.ToArray();
        p.ProgressCards=p.ProgressCards.Concat(new[]{kind}).ToArray();
    }
    IEnumerator AuditCard(ProgressCardKind kind)
    {
        var state=PrepareAudit();var own=state.Players.First(p=>p.Id=="P1");
        if(ProgressCatalog.IsVictoryPoint(kind))
        {
            var track=ProgressCatalog.Track(kind);var deck=state.ProgressDecks.Single(d=>d.Track==track);
            int i=Array.IndexOf(deck.Cards,kind);(deck.Cards[i],deck.Cards[0])=(deck.Cards[0],deck.Cards[i]);
            state.Phase=GamePhase.PendingChoice;state.DecisionReturnPhase=GamePhase.Action;
            state.PendingDecision=new Catan.Core.M5.PendingDecision{Kind="DefenderProgress",PlayerId="P1",ReturnPhase=GamePhase.Action};
            ActivateAudit(state);yield return new WaitForSecondsRealtime(.6f);
            int points=auditGame.M6View.OwnVictoryPoints;
            foreach(var f in DirectClick(ProgressTableUI.Item((int)track,3).center,"点奖励牌堆"))yield return f;
            yield return new WaitForSecondsRealtime(3);
            var result=auditGame.VerificationHost.View("P1");
            Require(result.Players.First(p=>p.Id=="P1").ProgressVictoryCards.Contains(kind)&&result.OwnVictoryPoints==points+1,"自动分值牌未亮出或分数错误");
            auditStatus="已自动公开并加一分";yield break;
        }
        if(kind==ProgressCardKind.Taxation)state.FirstBarbarianAttack=true;
        if(kind==ProgressCardKind.Encouragement)foreach(var knight in state.Knights.Where(k=>k.PlayerId=="P1"))knight.Active=false;
        if(kind==ProgressCardKind.TradeMonopoly&&state.Players[1].Commodities[(Catan.Core.M5.Commodity)0]==0){state.CommodityBank[(Catan.Core.M5.Commodity)0]--;state.Players[1].Commodities[(Catan.Core.M5.Commodity)0]++;}
        if(kind==ProgressCardKind.GuildDues||kind==ProgressCardKind.Wedding)state.Players[1].DefenderPoints+=2;
        if(kind==ProgressCardKind.Intrigue||kind==ProgressCardKind.Treason)
        {
            var board=auditGame.M6View.Board;
            var vertex=board.Edges.Where(e=>state.Roads.Any(p=>p.PlayerId=="P1"&&p.LocationId==e.Id)).SelectMany(e=>e.Vertices).First(id=>!state.Settlements.Concat(state.Cities).Any(p=>p.LocationId==id));
            state.Knights=state.Knights.Where(k=>k.LocationId!=vertex).Concat(new[]{new Knight{PlayerId="P2",LocationId=vertex,Level=1,Active=true}}).ToArray();
        }
        if(kind==ProgressCardKind.Irrigation)
        {
            var grain=state.Tiles.Where(t=>t.Resource=="wheat").SelectMany(t=>t.Vertices).ToArray();
            if(!state.Cities.Concat(state.Settlements).Any(p=>p.PlayerId=="P1"&&grain.Contains(p.LocationId))){
                var first=state.Settlements.First(p=>p.PlayerId=="P1");
                var other=state.Settlements.First(p=>grain.Contains(p.LocationId));
                (first.PlayerId,other.PlayerId)=(other.PlayerId,first.PlayerId);
            }
        }
        GiveCard(state,own,kind);
        if(kind==ProgressCardKind.CommercialHarbor){state.CommodityBank[Catan.Core.M5.Commodity.Paper]--;state.Players[1].Commodities[Catan.Core.M5.Commodity.Paper]++;}
        if(kind==ProgressCardKind.Espionage)GiveCard(state,state.Players[1],ProgressCardKind.Invention);
        if(kind==ProgressCardKind.Alchemy){state.Phase=GamePhase.ProductionAwaitRoll;state.ThreeKingdoms.Clock.Phase=ThreeKingdomsPhase.Roll;}
        ActivateAudit(state);yield return new WaitForSecondsRealtime(.65f);
        double until=Time.realtimeSinceStartupAsDouble+12;
        while((!app.IsHumanTurn||!Snapshot.CanInteract)&&Time.realtimeSinceStartupAsDouble<until)yield return null;
        Require(app.IsHumanTurn&&Snapshot.CanInteract,"发展牌支付操作尚未就绪");
        var legal=auditGame.M6View.LegalActions.FirstOrDefault(c=>c.Kind==CommandKind.PlayProgressCard&&c.ProgressCard==kind);
        if(legal==null){auditStatus="待触发：当前局面无合法出牌目标";yield break;}
        foreach(var f in DirectClick(new Vector2(1226,748),"展开发展牌"))yield return f;
        var index=Snapshot.Cards.FindIndex(c=>c.ArtIndex==(int)kind);
        var faceId=Snapshot.Cards[index].Id;
        var menuDeadline=Time.realtimeSinceStartupAsDouble+2;
        while(!Dock.VerificationSpecialVisible(faceId)&&Time.realtimeSinceStartupAsDouble<menuDeadline)yield return null;
        if(!Dock.VerificationSpecialVisible(faceId)){
            foreach(var f in DirectClick(new Vector2(1226,748),"展开发展牌"))yield return f;
            yield return new WaitForSecondsRealtime(.35f);
        }
        var rect=Dock.VerificationSpecialRect(faceId);
        var handPoint=index==Snapshot.Cards.Count-1?rect.center:new Vector2(rect.x+12,rect.center.y);
        for(int n=0;n<3&&Dock.VerificationRaisedCard!=faceId;n++){
            foreach(var f in DirectClick(handPoint,"点"+DevelopmentCardText.Name(kind.ToString())))yield return f;
            yield return new WaitForSecondsRealtime(.2f);
        }
        Require(Dock.VerificationRaisedCard==faceId,"目标发展牌尚未进入选中态");
        yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();SaveOceanFrame(auditItem+"-选中.png");
        if(Snapshot.Cards[index].Immediate)
        {foreach(var f in DirectClick(handPoint,"打出手牌"))yield return f;}
        else if(kind==ProgressCardKind.Alchemy)
        {foreach(var i in new[]{2,9})foreach(var f in DirectClick(ProgressTableUI.Item(i,12).center,"选骰子"))yield return f;}
        else if(kind==ProgressCardKind.Crane)
        {foreach(var f in DirectClick(new Vector2(54+(2+(int)legal.Track)*82,50),"点城市设施"))yield return f;}
        else if(kind==ProgressCardKind.MerchantFleet||kind==ProgressCardKind.ResourceMonopoly||kind==ProgressCardKind.TradeMonopoly)
        {int count=kind==ProgressCardKind.MerchantFleet?8:kind==ProgressCardKind.TradeMonopoly?3:5;foreach(var f in DirectClick(ProgressTableUI.Item(0,count).center,"选牌种"))yield return f;}
        else if(!string.IsNullOrEmpty(legal.OtherPlayerId))
        {foreach(var f in DirectClick(Avatar(legal.OtherPlayerId),"点玩家头像"))yield return f;}
        else
        {
            if(kind==ProgressCardKind.Invention)foreach(var f in MapClick(legal.SourceId))yield return f;
            string target=legal.TargetId??legal.TargetIds?.FirstOrDefault();
            if(target!=null)foreach(var f in MapClick(target))yield return f;
        }
        yield return new WaitForSecondsRealtime(1f);
        var after=auditSession.GetAuthoritativeStateForTesting();
        if(after.ProcessedCommands.Length<=state.ProcessedCommands.Length||!after.ProcessedCommands.Skip(state.ProcessedCommands.Length).Any(c=>c.Command.Kind==CommandKind.PlayProgressCard&&c.Command.ProgressCard==kind))
            throw new InvalidOperationException("手牌未提交");
        if(kind==ProgressCardKind.CommercialHarbor)
        {
            var offer=auditGame.M6View.LegalActions.First(c=>c.Kind==CommandKind.CommercialHarborOffer&&state.Players.First(p=>p.Id==c.OtherPlayerId).Commodities.Total>0);
            yield return new WaitForSecondsRealtime(.65f);
            var face=Dock.HandCardRect((int)offer.Resource,0).Value;
            foreach(var f in DirectClick(new Vector2(face.x+8,face.center.y),"选交出的资源"))yield return f;
            foreach(var f in DirectClick(Avatar(offer.OtherPlayerId),"选交易对象"))yield return f;
            after=auditSession.GetAuthoritativeStateForTesting();
            Require(after.PendingDecision?.Kind=="ProgressHarbor","商港未开始换货");
        }
        if(after.PendingDecision!=null)
        {
            for(int step=0;step<12&&after.PendingDecision!=null;step++)
            {
                var d=after.PendingDecision;string seat=d.PlayerId;
                if(app.HumanSeat!=seat)
                {
                    typeof(M6App).GetProperty("HumanSeat").SetValue(app,seat);auditGame.M6Refresh();yield return new WaitForSecondsRealtime(.65f);
                }
                var view=auditGame.M6View;
                var options=view.LegalActions.Where(a=>a.Kind==CommandKind.ResolveProgressChoice&&!a.Decline).ToArray();
                int previous=after.ProcessedCommands.Length;
                if(d.Kind=="ProgressRoads"||d.Kind=="ProgressTreasonRemove"||d.Kind=="ProgressTreasonPlace"||d.Kind=="DisplacedKnight"){
                    var choice=view.LegalActions.First(c=>!c.Decline&&c.TargetId!=null);foreach(var f in MapClick(choice.TargetId))yield return f;
                    if(d.Kind=="ProgressRoads"&&auditSession.GetAuthoritativeStateForTesting().ProcessedCommands.Length==previous){
                        foreach(var f in DirectClick(new Vector2(730,416),"选择道路或船"))yield return f;
                    }
                }
                else if(d.Kind=="ProgressEspionage"){
                    var faces=view.PrivateTargetProgressCards.Distinct().Count();
                    foreach(var f in DirectClick(ProgressTableUI.Item(0,faces+1).center,"取授权查看的发展牌"))yield return f;
                }
                else if(d.Kind=="ProgressGuildDues"){
                    var order=Enumerable.Range(0,8).Where(t=>(t<5?view.PrivateTargetResources[(Resource)t]:view.PrivateTargetCommodities[(Catan.Core.M5.Commodity)(t-5)])>0&&view.ThreeKingdoms.BlockedGainCardTypes?.Contains(t)!=true).ToArray();
                    var option=options.First();
                    for(int i=0;i<order.Length;i++){
                        int t=order[i],count=t<5?option.Resources[(Resource)t]:option.Commodities[(Catan.Core.M5.Commodity)(t-5)];
                        for(int j=0;j<count;j++)foreach(var f in DirectClick(ProgressTableUI.Item(i,order.Length+1).center,"取授权查看的手牌"))yield return f;
                    }
                }
                else if(d.Kind=="ProgressHarbor"||d.Kind=="ProgressWedding"||d.Kind=="ProgressSabotage"){
                    if(d.Kind!="ProgressHarbor")foreach(var f in DirectClick(new Vector2(1226,704),"切到响应席手牌"))yield return f;
                    var option=options.First();
                    for(int t=0;t<8;t++){
                        int count=d.Kind=="ProgressHarbor"?(t==5+(int)option.Commodity?1:0):(t<5?option.Resources[(Resource)t]:option.Commodities[(Catan.Core.M5.Commodity)(t-5)]);
                        for(int n=0;n<count;n++){
                            var face=Dock.HandCardRect(t,n).Value;
                            foreach(var f in DirectClick(new Vector2(face.x+8,face.center.y),"点响应手牌"))yield return f;
                        }
                    }
                    if(d.Kind!="ProgressHarbor")foreach(var f in DirectClick(TabletopUI.EndTurnRect.center,"完成选牌"))yield return f;
                }
                else break;
                after=auditSession.GetAuthoritativeStateForTesting();if(after.ProcessedCommands.Length==previous)break;
            }
        }
        string effectResult=VerifyCardResult(kind,state,after);
        if(after.PendingDecision==null&&effectResult!=null){auditStatus="已结算："+effectResult;yield break;}
        bool follow=kind==ProgressCardKind.Wedding||kind==ProgressCardKind.Treason||kind==ProgressCardKind.Diplomacy||kind==ProgressCardKind.RoadBuilding||kind==ProgressCardKind.CommercialHarbor||kind==ProgressCardKind.GuildDues||kind==ProgressCardKind.Espionage||kind==ProgressCardKind.Sabotage||kind==ProgressCardKind.Intrigue;
        if(follow&&after.PendingDecision==null&&after.ProcessedCommands.Length>state.ProcessedCommands.Length+1){auditStatus="已出牌并完成后续选择";yield break;}
        auditStatus=after.PendingDecision==null?(kind==ProgressCardKind.CommercialHarbor?"已出牌，后续待验：商港交换":"已出牌，首段结算通过"):"已出牌，后续待验："+after.PendingDecision.Kind;
    }
    string VerifyCardResult(ProgressCardKind kind,GameState before,GameState after)
    {
        var source=before.Players.First(p=>p.Id=="P1");var own=after.Players.First(p=>p.Id=="P1");
        var command=after.ProcessedCommands.Skip(before.ProcessedCommands.Length).First(c=>c.Command.Kind==CommandKind.PlayProgressCard).Command;
        Require(own.ProgressCards.Count(c=>c==kind)<source.ProgressCards.Count(c=>c==kind),"牌未消耗");
        switch(kind){
        case ProgressCardKind.Alchemy:Require(after.AlchemyDice1==3&&after.AlchemyDice2==4,"骰面未记住");return "所选骰面为 3、4";
        case ProgressCardKind.Crane:Require(own.Improvements[(int)command.Track]==source.Improvements[(int)command.Track]+1,"设施未升级");return "点选的设施升一级";
        case ProgressCardKind.Engineering:Require(after.Walls.Any(w=>w.PlayerId=="P1"&&w.LocationId==command.TargetId),"城墙未落定");return "目标城市增加城墙";
        case ProgressCardKind.Invention:Require(after.Tiles.Single(t=>t.Id==command.TargetId).Number==before.Tiles.Single(t=>t.Id==command.SourceId).Number&&after.Tiles.Single(t=>t.Id==command.SourceId).Number==before.Tiles.Single(t=>t.Id==command.TargetId).Number,"两枚数字未互换");return "点中的两枚数字互换";
        case ProgressCardKind.Medicine:Require(after.Cities.Any(c=>c.LocationId==command.TargetId)&&!after.Settlements.Any(c=>c.LocationId==command.TargetId),"村庄未升级");return "目标村庄升级为城市";
        case ProgressCardKind.Irrigation:case ProgressCardKind.Mining:
            var r=kind==ProgressCardKind.Irrigation?Resource.Wheat:Resource.Ore;var sites=before.Cities.Concat(before.Settlements).Where(p=>p.PlayerId=="P1").Select(p=>p.LocationId).ToArray();
            int expected=Math.Min(before.Bank[r],2*before.Tiles.Count(t=>t.Resource==(r==Resource.Wheat?"wheat":"ore")&&t.Vertices.Any(sites.Contains)));
            Require(own.Resources[r]==source.Resources[r]+expected,"资源增量错误");return "按相邻地块领取 "+expected+" 张资源";
        case ProgressCardKind.RoadBuilding:Require(after.Roads.Length+after.Ships.Length==before.Roads.Length+before.Ships.Length+2,"免费建设数量不符");return "连续放置两段免费道路或船";
        case ProgressCardKind.Smithing:Require(command.TargetIds.All(id=>after.Knights.First(k=>k.LocationId==id).Level==1+before.Knights.First(k=>k.LocationId==id).Level),"骑士未晋升");return "所选骑士各升一级";
        case ProgressCardKind.Encouragement:Require(before.Knights.Any(k=>k.PlayerId=="P1"&&!k.Active)&&after.Knights.Where(k=>k.PlayerId=="P1").All(k=>k.Active),"骑士未激活");return "己方待命骑士已激活";
        case ProgressCardKind.Taxation:Require(after.RobberTileId==command.TargetId,"强盗落点错误");return "强盗移至所点地块";
        case ProgressCardKind.Merchant:Require(after.MerchantPlayerId=="P1"&&after.MerchantTileId==command.TargetId,"商人落点错误");return "商人落在所点地块";
        case ProgressCardKind.MerchantFleet:Require(after.MerchantFleetResources.Contains(command.Resource),"船队兑换品类错误");return "所选资源获得船队汇率";
        case ProgressCardKind.ResourceMonopoly:Require(own.Resources[command.Resource]>source.Resources[command.Resource],"未收到垄断资源");return "所选资源增加 "+(own.Resources[command.Resource]-source.Resources[command.Resource])+" 张";
        case ProgressCardKind.TradeMonopoly:Require(own.Commodities[command.Commodity]>source.Commodities[command.Commodity],"未收到垄断商品");return "所选商品增加 "+(own.Commodities[command.Commodity]-source.Commodities[command.Commodity])+" 张";
        case ProgressCardKind.Intrigue:Require(!after.Knights.Any(k=>k.LocationId==command.TargetId&&k.PlayerId!="P1"),"对方骑士仍在原位");return "目标骑士已离开原位";
        case ProgressCardKind.CommercialHarbor:
            var offer=after.ProcessedCommands.Skip(before.ProcessedCommands.Length).Select(c=>c.Command).Single(c=>c.Kind==CommandKind.CommercialHarborOffer);
            var delivery=after.ProcessedCommands.Last(c=>c.Command.Kind==CommandKind.ResolveProgressChoice).Command;
            var partner=after.Players.First(p=>p.Id==offer.OtherPlayerId);var prior=before.Players.First(p=>p.Id==offer.OtherPlayerId);
            Require(own.Resources[offer.Resource]==source.Resources[offer.Resource]-1&&partner.Resources[offer.Resource]==prior.Resources[offer.Resource]+1&&own.Commodities[delivery.Commodity]==source.Commodities[delivery.Commodity]+1&&partner.Commodities[delivery.Commodity]==prior.Commodities[delivery.Commodity]-1,"商港双方换牌数量不符");
            return "双方各自点手牌完成资源与商品交换";
        default:return null;
        }
    }
    IEnumerable SwitchAuditSeat(string seat){
        if(app.HumanSeat!=seat){typeof(M6App).GetProperty("HumanSeat").SetValue(app,seat);auditGame.M6Refresh();}
        yield return new WaitForSecondsRealtime(.7f);
    }
    IEnumerable PlayTriggerCard(GameState state,Command trigger){
        foreach(var f in SwitchAuditSeat(trigger.PlayerId))yield return f;
        foreach(var f in DirectClick(new Vector2(1226,748),"打开实际手牌"))yield return f;
        var card=Snapshot.Cards.First(c=>c.ArtIndex==(int)trigger.ProgressCard);
        var r=Dock.VerificationSpecialRect(card.Id);
        foreach(var f in DirectClick(new Vector2(r.x+8,r.center.y),"打出"+card.Name))yield return f;
        if(card.Immediate){foreach(var f in DirectClick(new Vector2(r.x+8,r.center.y),"确认出牌"))yield return f;}
        else if(!string.IsNullOrEmpty(trigger.OtherPlayerId)){foreach(var f in DirectClick(Avatar(trigger.OtherPlayerId),"点目标头像"))yield return f;}
        else if(trigger.ProgressCard==ProgressCardKind.ResourceMonopoly){foreach(var f in DirectClick(ProgressTableUI.Item((int)trigger.Resource,5).center,"选资源"))yield return f;}
        else if(trigger.TargetId!=null){foreach(var f in MapClick(trigger.TargetId))yield return f;}
        yield return new WaitForSecondsRealtime(1);
    }
    IEnumerator AuditSkill(GeneralDefinition general,SkillDefinition skill)
    {
        var state=PrepareAudit();var active=state.ThreeKingdoms.Generals.First(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P1");
        var wanted=state.ThreeKingdoms.Generals.First(g=>g.GeneralId==general.Id);
        (wanted.GeneralId,active.GeneralId)=(active.GeneralId,wanted.GeneralId);
        (wanted.InstanceId,active.InstanceId)=(active.InstanceId,wanted.InstanceId);
        if(skill.Id=="tk-g05-s1")state.FirstBarbarianAttack=true;
        if(skill.Id=="tk-g54-s1"){
            state.ThreeKingdoms.Board=state.ThreeKingdoms.Board??new B5BoardState();
            state.ThreeKingdoms.Board.Damages=new[]{new B5Damage{Kind="Road",PlayerId="P1",LocationId=state.Roads.First(r=>r.PlayerId=="P1").LocationId}};
        }
        if(skill.Id=="tk-g53-s2"){
            active.Counters=active.Counters.Concat(new[]{new GeneralCounter{Key="xianghai-tax",Value=2}}).ToArray();
        }
        if(skill.Id=="tk-g11-s1"||skill.Id=="tk-g19-s2")
        {
            var target=state.ThreeKingdoms.Generals.Single(g=>g.Region==GeneralRegion.Active&&g.OwnerPlayerId=="P2");
            (target.GeneralId,active.GeneralId)=(active.GeneralId,target.GeneralId);
            (target.InstanceId,active.InstanceId)=(active.InstanceId,target.InstanceId);
            var protectedPlayer=state.Players.First(p=>p.Id=="P2");
            GiveCard(state,protectedPlayer,ProgressCardKind.Invention);
            GiveCard(state,state.Players[0],ProgressCardKind.Espionage);
            if(skill.Id=="tk-g19-s2"){
                foreach(Resource r in Enum.GetValues(typeof(Resource))){state.Bank[r]+=protectedPlayer.Resources[r];protectedPlayer.Resources[r]=0;}
                foreach(Catan.Core.M5.Commodity c in Enum.GetValues(typeof(Catan.Core.M5.Commodity))){state.CommodityBank[c]+=protectedPlayer.Commodities[c];protectedPlayer.Commodities[c]=0;}
            }
            ActivateAudit(state);yield return new WaitForSecondsRealtime(.65f);
            foreach(var f in PlayTriggerCard(state,new Command{Kind=CommandKind.PlayProgressCard,PlayerId="P1",ProgressCard=ProgressCardKind.Espionage,OtherPlayerId="P2"}))yield return f;
            var tested=auditSession.GetAuthoritativeStateForTesting();
            Require(tested.ProcessedCommands.Length>state.ProcessedCommands.Length&&tested.PendingDecision?.Kind!="ProgressEspionage"&&auditGame.VerificationHost.View("P1").PrivateTargetProgressCards.Length==0&&tested.Players[1].ProgressCards.Contains(ProgressCardKind.Invention),"间谍免疫未结算");
            auditStatus="已结算：点对手发动间谍，卡已消耗且未暴露对方发展牌";yield break;
        }
        if(skill.Id=="tk-g45-s2"){
            active.Counters=active.Counters.Concat(new[]{new GeneralCounter{Key="wenji:0",Value=1,TurnInstanceId=state.ThreeKingdoms.Clock.TurnInstanceId}}).ToArray();
            state.ThreeKingdoms.Skills.LossLedger=Array.Empty<B2PlayerLedger>();
            ActivateAudit(state);yield return new WaitForSecondsRealtime(.65f);
            foreach(var f in DirectClick(TabletopUI.EndTurnRect.center,"结束未失牌回合"))yield return f;
            var end=auditSession.GetAuthoritativeStateForTesting();
            int count=state.Players[0].Resources.Wood;int gain=end.Players[0].Resources.Total-state.Players[0].Resources.Total;
            Require(gain==count&&count>0,"屯江取牌数量不符");auditStatus="已结算：结束回合领取"+gain+"张资源";yield break;
        }
        if(skill.Id=="tk-g38-s1"){
            var blocked=Fixture<GameState>(skill.Id,"blocked");
            var control=Fixture<GameState>(skill.Id,"control");
            var road=Fixture<Command>(skill.Id,"road");
            Require(blocked!=null&&control!=null,"缺少对照局面");
            foreach(var scenario in new[]{blocked,control}){
                ActivateAudit(scenario);yield return new WaitForSecondsRealtime(.65f);
                Require(Dock.VerificationTryBuildPoint("BuildRoad",out var piece),"道路库存未展开");
                foreach(var f in DirectClick(piece,"拿起道路"))yield return f;
                foreach(var f in MapClick(road.TargetId))yield return f;
                var placed=auditSession.GetAuthoritativeStateForTesting().Roads.Any(r=>r.PlayerId=="P1"&&r.LocationId==road.TargetId);
                Require(placed==(scenario==control),"咆哮阻断或对照落子不符");
                yield return new WaitForEndOfFrame();SaveOceanFrame(auditItem+(placed?"-对照.png":"-受阻.png"));
            }
            auditStatus="已结算：同一边，张飞在场阻止连接，对照局正常落路";yield break;
        }
        if(skill.Id=="tk-g43-s1"){
            state=Fixture<GameState>(skill.Id,"dice")??throw new InvalidOperationException("缺少自然掷骰局面");
            ActivateAudit(state);yield return new WaitForSecondsRealtime(.65f);
            double deadline=Time.realtimeSinceStartupAsDouble+12;
            while(!app.IsHumanTurn&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Require(app.IsHumanTurn,"投掷前客户端未开放行动");
            foreach(var f in DirectClick(new Vector2(80,208),"点骰盘自然掷骰"))yield return f;
            yield return new WaitForSecondsRealtime(2);
            var end=auditSession.GetAuthoritativeStateForTesting();
            for(int n=0;n<10&&!end.ThreeKingdoms.Generals.Single(g=>g.GeneralId==general.Id).Counters.Any(c=>c.Key=="qinqin"&&c.Value==1);n++){
                var d=end.ThreeKingdoms.Dice?.PendingDecision??end.ThreeKingdoms.Skills.PendingDecision;
                if(d==null)break;
                foreach(var f in SwitchAuditSeat(d.PlayerId))yield return f;
                var choices=auditGame.M6View.ThreeKingdoms.SkillActions;
                if(choices.Any(a=>B45SelectionContract.IsSkip(a.Command))){
                    var skills=B45SkillCatalog.AvailableEntries(auditGame.M6View.ThreeKingdoms,app.HumanSeat);
                    var row=Array.FindIndex(skills,entry=>entry.SkillId==choices[0].Command.SkillId);
                    foreach(var f in DirectClick(new Vector2(293,738+Math.Max(0,row)*Mathf.Min(49,171f/(1+skills.Length))),"完成骰前响应"))yield return f;
                }else break;
                end=auditSession.GetAuthoritativeStateForTesting();
            }
            Debug.Log("勤政核对 "+end.Phase+" "+end.ThreeKingdoms.Clock.Phase+" "+end.LastDice1+"+"+end.LastDice2+" "+end.ThreeKingdoms.Skills.PendingDecision?.Kind+" "+end.ThreeKingdoms.Dice?.PendingDecision?.Kind);
            Require(end.ThreeKingdoms.Generals.Single(g=>g.GeneralId==general.Id).Counters.Any(c=>c.Key=="qinqin"&&c.Value==1),"勤政计数未推进");
            auditStatus="已结算：五点触发，勤政奖励前进一格";yield break;
        }
        if(skill.Id=="tk-g24-s2"){
            var k=state.Knights.First(n=>n.PlayerId=="P1");k.Active=false;
            ActivateAudit(state);yield return new WaitForSecondsRealtime(.65f);
            foreach(var f in MapClick(k.LocationId))yield return f;
            Require(auditGame.M6View.ThreeKingdoms.BoardAssets.Any(a=>a.Kind=="Knight"&&a.LocationId==k.LocationId&&a.Active),"骑士战力未按激活投影");
            auditStatus="已验证常驻战力：未支付行动许可的骑士计作激活";yield break;
        }
        if(skill.Id=="tk-g16-s1"||skill.Id=="tk-g42-s1")
        {
            bool longdan=skill.Id=="tk-g42-s1";
            var owner=state.Players.First(p=>p.Id=="P1");
            if(longdan){state.Bank[Resource.Wood]+=owner.Resources.Wood;owner.Resources.Wood=0;}
            ActivateAudit(state);yield return new WaitForSecondsRealtime(.7f);
            double readyAt=Time.realtimeSinceStartupAsDouble+15;
            while(!app.IsHumanTurn&&Time.realtimeSinceStartupAsDouble<readyAt)yield return null;
            Require(app.IsHumanTurn,"验收开始前尚未轮到玩家");
            if(longdan){
                foreach(var step in DirectClick(new Vector2(211,738),"点赵云旁的龙胆"))yield return step;
                Require(app.LongdanSelected,"龙胆按钮未开启手牌替代");
            }
            string edge;
            if(longdan){
                edge=auditGame.M6View.LegalEdgeIds.First();
                Require(Dock.VerificationTryBuildPoint("BuildRoad",out var piece),"道路筹码不可用");
                foreach(var step in DirectClick(piece,"拿起道路"))yield return step;
                foreach(var step in MapClick(edge))yield return step;
                for(int i=0;i<2;i++){
                    var face=Dock.HandCardRect((int)Resource.Brick,i).Value;
                    foreach(var step in DirectClick(new Vector2(face.x+8,face.center.y),i==0?"点第一张砖作木材":"点第二张砖作砖"))yield return step;
                }
                foreach(var step in DirectClick(new Vector2(1106,682),"确认建路支付"))yield return step;
                var placed=auditSession.GetAuthoritativeStateForTesting();
                Require(placed.Roads.Any(p=>p.LocationId==edge&&p.PlayerId=="P1"),"实际付费道路没有落地");
                Require(placed.Players[0].Resources.Wood==0&&placed.Players[0].Resources.Brick==owner.Resources.Brick-2
                    &&placed.Bank[Resource.Brick]==state.Bank[Resource.Brick]+2,"龙胆扣牌或银行收牌不符");
                auditStatus="已结算：点龙胆，零木材、两张砖造路；银行收到两张砖";yield break;
            }
            var built=auditSession.GetAuthoritativeStateForTesting();
            edge=built.Roads.First(p=>p.PlayerId=="P1"&&built.ProcessedCommands.Any(c=>c.Command.Kind==CommandKind.SetupRoad&&c.Command.TargetId==p.LocationId&&c.Command.PlayerId=="P1")
                &&!(built.ThreeKingdoms.Board?.Receipts??Array.Empty<B5ConstructionReceipt>()).Any(r=>r.AssetId=="Road:"+p.LocationId&&r.PaidCount>0)).LocationId;
            var offer=auditGame.M6View.ThreeKingdoms.SkillActions.First(a=>a.Command.SkillId==skill.Id&&B45SelectionContract.Board(a).Contains("Road:"+edge));
            Require(offer.RefundCardCount==2,"起始免费道路未展示两张返牌");
            foreach(var step in DirectClick(new Vector2(211,738),"点黄盖旁的苦肉"))yield return step;
            app.VerificationMarkerId="Road:"+edge;app.VerificationMarkerArea=null;
            yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
            Require(app.VerificationMarkerArea.HasValue,"开局道路返牌标记不可见");
            foreach(var step in DirectClick(app.VerificationMarkerArea.Value.center,"点开局道路上的路+2"))yield return step;
            app.VerificationMarkerId=null;app.VerificationMarkerArea=null;
            var pending=auditSession.GetAuthoritativeStateForTesting();
            Require(!pending.Roads.Any(p=>p.LocationId==edge)&&pending.ThreeKingdoms.Skills.PendingDecision?.PlayerId=="P1","苦肉拆除后未开放取牌");
            for(int n=0;n<2;n++){
                var pick=(B45SelectionContext)typeof(M6App).GetField("generalSelection",PrivateInstance).GetValue(app);
                Require(pick.Active,"取牌选择未呈现");
                foreach(var step in DirectClick(new Vector2(402,542),"从银行点木材"))yield return step;
            }
            var paid=auditSession.GetAuthoritativeStateForTesting();
            Require(paid.Players[0].Resources.Wood==built.Players[0].Resources.Wood+2&&paid.Bank[Resource.Wood]==built.Bank[Resource.Wood]-2,"苦肉两张木材未实际入手");
            auditStatus="已结算：点苦肉拆开局免费道路，银行减两木、黄盖手牌增两木";yield break;
        }
        if(skill.Id=="tk-g07-s1"||skill.Id=="tk-g44-s2"){
            ActivateAudit(state);yield return new WaitForSecondsRealtime(.65f);
            var build=Snapshot.Builds.First(b=>b.Id=="BuildRoad");
            Require(Dock.VerificationTryBuildPoint(build.Id,out var start),"未找到道路筹码");
            foreach(var f in DirectClick(start,"拿起道路"))yield return f;
            var dest=auditGame.M6View.LegalEdgeIds.First();
            foreach(var f in MapClick(dest))yield return f;
            foreach(var f in DirectClick(new Vector2(1226,704),"打开支付手牌"))yield return f;
            for(int i=0;i<2;i++){
                int type=i==1?1:skill.Id=="tk-g44-s2"?7:0;
                var face=Dock.HandCardRect(type,0).Value;
                foreach(var f in DirectClick(new Vector2(face.x+8,face.center.y),"选实际支付牌"))yield return f;
                var focused=Dock.HandCardRect(type,0).Value;
                float x=Mathf.Clamp(focused.center.x-268,342,630),y=Mathf.Max(480,Mathf.Min(592,focused.y-98));
                if(i==0&&skill.Id=="tk-g07-s1")foreach(var f in DirectClick(new Vector2(x+454,y+16),"点蛊惑声明"))yield return f;
                foreach(var f in DirectClick(new Vector2(x+36+i*65.5f,y+44),"选支付用途"))yield return f;
            }
            foreach(var f in DirectClick(new Vector2(1106,682),"提交支付"))yield return f;
            for(int j=0;j<10;j++){
                var st=auditSession.GetAuthoritativeStateForTesting();
                var dec=st.ThreeKingdoms.Skills.PendingDecision;
                if(dec==null)break;
                foreach(var f in SwitchAuditSeat(dec.PlayerId))yield return f;
                var choices=auditGame.M6View.ThreeKingdoms.SkillActions.Where(a=>!B45SelectionContract.IsSkip(a.Command)).ToArray();
                int trust=Array.FindIndex(choices,a=>a.Command.Option=="response-trust");
                if(trust<0)break;
                foreach(var f in DirectClick(new Vector2(404+113*trust,555),"响应蛊惑：不质疑"))yield return f;
            }
            var afterPay=auditSession.GetAuthoritativeStateForTesting();
            Require(afterPay.Roads.Any(p=>p.LocationId==dest&&p.PlayerId=="P1")&&afterPay.Roads.Length==state.Roads.Length+1,"技能支付未造路");
            auditStatus=skill.Id=="tk-g44-s2"?"已结算：纸张作木材，支付后建路":"已结算：逐席响应蛊惑后建路";yield break;
        }
        if(skill.Id=="tk-g46-s2"){
            ActivateAudit(state);yield return new WaitForSecondsRealtime(.65f);
            Require(auditGame.M6View.ThreeKingdoms.ConstructionCostAdjustment==1,"唯一最多的造价未增加");
            var first=state.Cities.First(c=>c.PlayerId=="P1").LocationId;
            foreach(var f in MapClick(first))yield return f;
            foreach(var f in DirectClick(new Vector2(730,428),"造城墙"))yield return f;
            var d=auditSession.GetAuthoritativeStateForTesting().ThreeKingdoms.Skills.PendingDecision;
            Require(d!=null,"骄姿额外支付未触发");auditStatus="已触发额外支付，最终造价待验";yield break;
        }
        var input=Fixture<GameState>(skill.Id,"input");
        var prior=Fixture<GameState>(skill.Id,"before");
        if(input!=null)state=input;
        if(skill.Id=="tk-g01-s1"){
            ActivateAudit(state);yield return new WaitForSecondsRealtime(.8f);
            var viewRaid=auditGame.M6View.ThreeKingdoms;
            var raidAction=viewRaid.SkillActions.First(a=>a.Command.Option=="raid"&&a.Command.Counts.Sum()==2);
            var selected=raidAction.Command;var kinds=Enumerable.Range(0,8).Where(t=>viewRaid.RaidHarvestCards[t]>0).ToArray();
            var picker=(B45SelectionContext)typeof(M6App).GetField("generalSelection",PrivateInstance).GetValue(app);
            Require(picker.Raid&&!picker.CanSelectPlayer(selected.TargetPlayerIds[0]),"少收前已经能选玩家");
            for(int i=0;i<kinds.Length;i++)for(int n=0;n<selected.Counts[kinds[i]];n++)
                foreach(var step in DirectClick(M6App.RaidCardRect(i,kinds.Length).center,"少收"+B45PaymentContext.Names[kinds[i]]))yield return step;
            Require(picker.RaidCount==2,"少收份数不符");
            yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();SaveOceanFrame(auditItem+"-选中.png");
            foreach(var id in selected.TargetPlayerIds){
                Require(picker.CanSelectPlayer(id),"合法玩家信息框不可选");
                var pos=Avatar(id)+new Vector2(30,122);
                foreach(var step in DirectClick(pos,"点"+id+"信息框下半部"))yield return step;
            }
            var final=auditSession.GetAuthoritativeStateForTesting();
            var executed=final.ProcessedCommands.Skip(state.ProcessedCommands.Length).Select(c=>c.Command)
                .First(c=>c.Kind==CommandKind.UseGeneralSkill&&c.SkillCommand?.Option=="raid").SkillCommand;
            Require(executed.Counts.SequenceEqual(selected.Counts)&&executed.TargetPlayerIds.OrderBy(t=>t).SequenceEqual(selected.TargetPlayerIds.OrderBy(t=>t)),"少收或目标不符");
            Require(final.ThreeKingdoms.SkillHistory.Any(h=>h.SkillId==skill.Id&&h.Complete&&h.PublicResult.Contains("实际获得 2 张")),"没有实际取得两张手牌");
            auditStatus="已结算：少收羊毛、布匹各一张，点两名不同玩家的信息框，各取一张手牌";yield break;
        }
        if(prior!=null)
        {
            var trigger=Fixture<Command>(skill.Id,"cause");
            ActivateAudit(prior);yield return new WaitForSecondsRealtime(.7f);
            if(trigger.Kind==CommandKind.PlayProgressCard){
                foreach(var f in PlayTriggerCard(prior,trigger))yield return f;
            }
            else if(trigger.Kind==CommandKind.RollDice||trigger.Kind==CommandKind.EndTurn)
            {foreach(var f in SwitchAuditSeat(trigger.PlayerId))yield return f;foreach(var f in DirectClick(TabletopUI.EndTurnRect.center,"触发："+trigger.Kind))yield return f;}
            else
            {
                var ai=new Catan.AI.CombinedAi();
                var next=ai.Decide(auditGame.VerificationHost.AiView(trigger.PlayerId));
                Require(next.Kind==trigger.Kind,"原对局触发动作不同");
                next.Id=Guid.NewGuid().ToString("N");
                Require(auditGame.VerificationHost.Submit(next).Success,"触发行动被拒绝");
                directClickLog.Add("由对局AI执行 "+next.Kind);
                auditGame.M6Refresh();
            }
            yield return new WaitForSecondsRealtime(.7f);
            var hist=auditSession.GetAuthoritativeStateForTesting().ThreeKingdoms.SkillHistory;
            var ev=hist?.FirstOrDefault(e=>e.PlayerId=="P1"&&e.SkillId==skill.Id&&e.Complete&&!(prior.ThreeKingdoms.SkillHistory??Array.Empty<B4SkillHistoryEvent>()).Any(p=>p.EventId==e.EventId&&p.Complete));
            Require(ev!=null,"未匹配到本次技能结算记录");auditStatus="被动效果已触发："+ev.PublicResult;yield break;
        }
        ActivateAudit(state);yield return new WaitForSecondsRealtime(.6f);
        var view=auditGame.M6View.ThreeKingdoms;
        var entries=B45SkillCatalog.AvailableEntries(view,"P1");int idx=Array.FindIndex(entries,e=>e.SkillId==skill.Id);
        if(idx<0)throw new InvalidOperationException("武将旁没有技能入口");
        var skillPoint=new Vector2(211,738+idx*Mathf.Min(49,171f/(1+entries.Length)));
        var action=view.SkillActions.FirstOrDefault(a=>a.Command.SkillId==skill.Id&&!B45SelectionContract.IsSkip(a.Command));
        if(action==null&&(skill.Id=="tk-g10-s2"||skill.Id=="tk-g33-s2"||skill.Id=="tk-g53-s1"))
        {
            if(skill.Id=="tk-g33-s2")
            {
                GiveCard(state,state.Players[0],ProgressCardKind.Irrigation);GiveCard(state,state.Players[0],ProgressCardKind.Mining);GiveCard(state,state.Players[0],ProgressCardKind.Crane);
                ActivateAudit(state);yield return new WaitForSecondsRealtime(.55f);
                Require(Snapshot.SpecialHandLimit==0&&state.Players[0].ProgressCards.Length>4,"持牌上限显示错误");
                foreach(var f in DirectClick(TabletopUI.EndTurnRect.center,"持有五张发展牌结束回合"))yield return f;
                var afterTurn=auditSession.GetAuthoritativeStateForTesting();
                Require(afterTurn.ProcessedCommands.Length>state.ProcessedCommands.Length&&afterTurn.Players[0].ProgressCards.Length==state.Players[0].ProgressCards.Length,"奇才仍被强制弃牌");
                auditStatus="已结算：五张发展牌直接结束回合";yield break;
            }
            var target=skill.Id=="tk-g10-s2"?"P1":"P2";
            var player=auditGame.M6View.Players.First(p=>p.Id==target);
            int wall=state.Walls.Count(w=>w.PlayerId==target);
            int predicted=7+wall*2+(skill.Id=="tk-g10-s2"?4:-1);
            Require(player.ResourceDiscardThreshold==predicted,"手牌阈值错误");
            auditStatus="已验证常驻效果：席位"+target+"资源阈值为"+predicted+(skill.Id=="tk-g53-s1"?"，掷骰和交牌分支待验":"");yield break;
        }
        if(action==null)
        {
            foreach(var f in DirectClick(skillPoint,"查看"+skill.Name,1))yield return f;
            var description=(string)typeof(M6App).GetField("inspectionText",PrivateInstance).GetValue(app);
            auditStatus=description!=null&&description.Contains(skill.Name)?"已查看技能，待触发实际效果":"待复核：说明未打开";
            yield break;
        }
        if(view.SkillDecision?.PlayerId!="P1")foreach(var f in DirectClick(skillPoint,"点"+skill.Name))yield return f;
        yield return new WaitForEndOfFrame();SaveOceanFrame(auditItem+"-发动.png");
        var selection=(B45SelectionContext)typeof(M6App).GetField("generalSelection",PrivateInstance).GetValue(app);
        if(selection.Active&&selection.DiceIndices.Length>0)
        {
            var i=selection.DiceIndices[0];
            foreach(var f in DirectClick(new[]{new Vector2(62,223),new Vector2(132,224),new Vector2(98,184)}[i],"点骰子"))yield return f;
            foreach(var f in DirectClick(new Vector2(257,255),"选择骰面"))yield return f;
        }
        if(selection.Active&&selection.RequiresBankCards)
        {
            for(int i=0;i<selection.RequiredBankCount&&selection.Active;i++)
                foreach(var f in DirectClick(new Vector2(404+(i%Math.Max(1,selection.BankOptions.Select(a=>a.Command.CardType).Distinct().Count()))*112,542),"选银行牌"))yield return f;
        }
        if(selection.Active&&selection.ShowOptionChoices)
        {
            var local=selection.Options;
            float w=Mathf.Min(240,880f/Math.Max(1,Math.Min(3,local.Length)));
            bool small=local.Length<=6&&local.All(a=>a.ResultCardType==null&&a.RealCardType==null&&a.VirtualCardId==null&&a.Command.Option!="choose-card"&&a.Command.Option!="b4-guess"&&a.Command.Option!="claim");
            var pos=small?new Vector2(808-Math.Min(3,local.Length)*(w+16)/2+w/2,642-68-(local.Length>3?66:0)+29):new Vector2(405,552);
            if(selection.Contract.DecisionKind=="b4-pile"||local.All(a=>new[]{"card","color","claim","luoying","b4-daoshu","response-trust","response-challenge"}.Contains(a.Command.Option)))pos=new Vector2(406,555);
            foreach(var f in DirectClick(pos,"选择分支"))yield return f;
        }
        if(selection.Active)
        {
            var a=selection.CandidateActions.FirstOrDefault(v=>!B45SelectionContract.IsSkip(v.Command));
            if(a!=null)
            {
                var cost=B45SelectionContract.Cost(a);
                for(int t=0;t<cost.Length;t++)for(int n=0;n<cost[t]&&selection.Active;n++)
                {
                    var face=Dock.HandCardRect(t,n);if(face.HasValue)foreach(var f in DirectClick(new Vector2(face.Value.x+10,face.Value.center.y),"点支付手牌"))yield return f;
                }
                foreach(var id in B45SelectionContract.Players(a))if(selection.Active&&selection.CanSelectPlayer(id))foreach(var f in DirectClick(Avatar(id),"点玩家头像"))yield return f;
                foreach(var id in B45SelectionContract.Board(a))if(selection.Active&&selection.CanSelectTile(id)&&!id.StartsWith("Improvement:"))
                    foreach(var f in MapClick(B45SelectionContract.BoardLocationId(id)))yield return f;
                foreach(var id in B45SelectionContract.Progress(a))if(selection.Active)
                {
                    foreach(var f in DirectClick(new Vector2(1226,748),"展开发展牌"))yield return f;
                    var card=Snapshot.Cards.FirstOrDefault(c=>((ProgressCardKind)c.ArtIndex).ToString()==id);
                    if(card!=null){var r=Dock.VerificationSpecialRect(card.Id);foreach(var f in DirectClick(new Vector2(r.x+10,r.center.y),"选发展牌"))yield return f;}
                }
                if(selection.CanConfirm)foreach(var f in DirectClick(TabletopUI.EndTurnRect.center,"完成已选对象"))yield return f;
            }
        }
        for(int k=0;k<32;k++)
        {
            var live=auditSession.GetAuthoritativeStateForTesting();var pending=live.ThreeKingdoms.Skills.PendingDecision??live.ThreeKingdoms.Dice?.PendingDecision;
            if(pending==null)break;
            if(pending.PlayerId!=app.HumanSeat){
                typeof(M6App).GetProperty("HumanSeat").SetValue(app,pending.PlayerId);auditGame.M6Refresh();
            }
            yield return new WaitForSecondsRealtime(.45f);
            if(!selection.Active)break;
            var cmd=selection.CandidateActions.FirstOrDefault(a=>!B45SelectionContract.IsSkip(a.Command));
            if(cmd==null)break;
            if(pending.Kind=="civil-niepan"){
                var option=selection.CandidateActions.FirstOrDefault(a=>a.Command.Option=="civil-niepan-track");
                if(option==null)break;
                foreach(var f in DirectClick(new Vector2(54+(2+option.Command.CardType)*82,50),"点要降级的设施"))yield return f;
            }
            else if(pending.Kind=="civil-fencheng"||pending.Kind=="alliance-b6-join"||pending.Kind=="b4-order"){
                var options=selection.Options;
                int which=Array.FindIndex(options,a=>pending.Kind=="civil-fencheng"?a.Command.Option=="civil-fencheng-burn":pending.Kind=="alliance-b6-join"?a.Command.Option=="reject"||a.Command.Option?.Contains("reject")==true:a.Command.SkillId==skill.Id);
                int i=Math.Max(0,which),columns=Math.Min(3,options.Length);
                float width=Mathf.Min(240,880f/Math.Max(1,columns));
                var pos=new Vector2(808-columns*(width+16)/2+(i%Math.Max(1,columns))*(width+16)+width/2,642-68-(options.Length>3?66:0)+(i/Math.Max(1,columns))*66+29);
                foreach(var f in DirectClick(pos,"选"+pending.Title))yield return f;
            }
            else if(selection.CanConfirm){
                foreach(var f in DirectClick(TabletopUI.EndTurnRect.center,"点技能处理"+pending.Title))yield return f;
            }
            else if(selection.RequiresBankCards){
                foreach(var f in DirectClick(new Vector2(402,542),"选银行牌"))yield return f;
            }
            else if(selection.DiceIndices.Length>0){
                if(selection.SelectedDie<0){int di=selection.DiceIndices[0];foreach(var f in DirectClick(new[]{new Vector2(62,223),new Vector2(132,224),new Vector2(98,184)}[di],"选骰子"))yield return f;}
                bool fixedDice=selection.Options.Any(a=>a.Command.Option=="fixed");
                foreach(var f in DirectClick(new Vector2(248,fixedDice?236:255),"选数字"))yield return f;
                if(fixedDice)foreach(var f in DirectClick(new Vector2(248,280),"选事件骰"))yield return f;
            }
            else if(selection.CandidateActions.SelectMany(B45SelectionContract.Players).FirstOrDefault(id=>!selection.SelectedPlayers.Contains(id)&&selection.CanSelectPlayer(id)) is string who){
                foreach(var f in DirectClick(Avatar(who),"点响应目标头像"))yield return f;
            }
            else if(Enumerable.Range(0,8).FirstOrDefault(t=>selection.CanSelectHand(t,0)&&!selection.IsHandSelected(t,0)) is int good&&selection.CanSelectHand(good,0)&&!selection.IsHandSelected(good,0)&&Dock.HandCardRect(good,0) is Rect hr){
                foreach(var f in DirectClick(new Vector2(hr.x+8,hr.center.y),"选响应手牌"))yield return f;
            }
            else if(selection.CandidateActions.SelectMany(B45SelectionContract.Board).FirstOrDefault(id=>!selection.SelectedTiles.Contains(id)&&selection.CanSelectTile(id)) is string tile&&!tile.StartsWith("Improvement:")){
                foreach(var f in MapClick(B45SelectionContract.BoardLocationId(tile)))yield return f;
            }
            else if(selection.Options.Length>0||selection.PrivateOptions.Length>0){
                var opts=selection.Options;int cols=Math.Max(1,Math.Min(3,opts.Length));float w=Mathf.Min(240,880f/cols);
                var point=opts.Length>0&&opts.Length<=6&&opts.All(a=>a.ResultCardType==null&&a.RealCardType==null&&a.VirtualCardId==null&&a.Command.Option!="claim")?new Vector2(808-cols*(w+16)/2+w/2,642-68-(opts.Length>3?66:0)+29):new Vector2(405,557);
                if(selection.Contract.DecisionKind=="b4-pile"||selection.Options.All(a=>new[]{"card","color","claim","luoying","b4-daoshu","response-trust","response-challenge"}.Contains(a.Command.Option)))point=new Vector2(405,557);
                foreach(var f in DirectClick(point,"点响应牌或分支"))yield return f;
            }
            else break;
        }
        var after=auditSession.GetAuthoritativeStateForTesting();
        Debug.Log("AUDIT_DETAIL "+skill.Id+" delta="+(after.ProcessedCommands.Length-state.ProcessedCommands.Length)+" submitStatus="+typeof(M5App).GetField("status",PrivateInstance).GetValue(auditGame)+" actor="+app.CurrentActor+" window="+selection.Contract?.DecisionKind+" ready="+selection.CanConfirm+" input="+typeof(M6App).GetProperty("GeneralCanInteract",PrivateInstance).GetValue(app));
        bool submitted=after.ProcessedCommands.Skip(state.ProcessedCommands.Length).Any(p=>p.Command.Kind==CommandKind.UseGeneralSkill&&p.Command.SkillCommand?.SkillId==skill.Id);
        var effect=after.ThreeKingdoms.SkillHistory?.LastOrDefault(e=>e.SkillId==skill.Id&&e.Complete&&!(state.ThreeKingdoms.SkillHistory??Array.Empty<B4SkillHistoryEvent>()).Any(before=>before.EventId==e.EventId&&before.Complete));
        auditStatus=effect!=null?"已结算："+effect.PublicResult:submitted?(after.ThreeKingdoms.Skills.PendingDecision==null?"技能指令已提交":"已发动，响应待验："+after.ThreeKingdoms.Skills.PendingDecision.Kind):"待复核：未提交技能 · "+selection.Contract?.DecisionKind;
    }
}

