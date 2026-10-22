using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Catan.Core;
using UnityEngine;

// 显式交互验收中的隔离局面；通过真实菜单回调和真实模型检查升级与鼠标预览。
public sealed partial class InteractionPresentationVerification
{
    static object PointerGet(object owner,string name)
    {
        var type=owner.GetType();var property=type.GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        return property!=null?property.GetValue(owner):type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(owner);
    }
    static void PointerSet(object owner,string name,object value)=>owner.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(owner,value);
    static object PointerCall(object owner,string name,params object[] values)=>owner.GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single(method=>method.Name==name&&method.GetParameters().Length==values.Length).Invoke(owner,values);
    IEnumerator VerifyPointerMenus()
    {
        foreach(var mode in new[]{"base","seafarers","cities-knights","combined"})
        {
            stage=mode+" 右键建筑升级与鼠标效果";
            app.StartGame(mode,"heading-for-new-shores",4,"P1",true);yield return null;yield return null;
            Component game=mode=="base"?(Component)GetComponent<M2App>():mode=="seafarers"?GetComponent<M3App>():mode=="cities-knights"?GetComponent<M4App>():GetComponent<M5App>();
            var host=PointerGet(game,"VerificationHost");var session=PointerGet(host,"session");
            var state=PointerCall(session,"GetAuthoritativeStateForTesting");var view=PointerGet(game,"M6View");var publicBoard=PointerGet(view,"Board");
            var tiles=(Tile[])PointerGet(publicBoard,"Tiles");
            string own=tiles.First(t=>t.Resource!="sea"&&t.Resource!="fog").Vertices[0];
            string foe=tiles.Last(t=>t.Resource!="sea"&&t.Resource!="fog"&&!t.Vertices.Contains(own)).Vertices.Last();
            PointerSet(state,"ActivePlayerId","P1");PointerSet(state,"Turn",9);PointerSet(state,"SetupPlacementIndex",8);PointerSet(state,"PendingDecision",null);
            var phase=state.GetType().GetProperty("Phase");phase.SetValue(state,Enum.Parse(phase.PropertyType,"Action"));
            PointerSet(state,"Settlements",new[]{Piece(own),new PiecePlacement{PlayerId="P2",LocationId=foe}});
            PointerSet(state,"Cities",Array.Empty<PiecePlacement>());PointerSet(state,"Roads",Array.Empty<PiecePlacement>());
            var players=((IEnumerable)PointerGet(state,"Players")).Cast<object>().ToArray();
            foreach(var player in players)
            {
                PointerSet(player,"Resources",new ResourceBag());
                var pieces=(PieceSupply)PointerGet(player,"Pieces");pieces.Settlements=(string)PointerGet(player,"Id")=="P1"||(string)PointerGet(player,"Id")=="P2"?4:5;pieces.Cities=4;pieces.Roads=15;
            }
            var bank=(ResourceBag)PointerGet(state,"Bank");foreach(Resource resource in Enum.GetValues(typeof(Resource)))bank[resource]=19;
            session.GetType().GetField("state",Private).SetValue(session,state);PointerCall(session,"AssertInvariants");PointerCall(game,"M6Refresh");yield return null;yield return Settle();
            string before=(string)PointerCall(host,"ExportSave");
            Require(!(bool)PointerCall(game,"VerificationOpenBuilding",foe),mode+" 允许升级对手村庄");
            Require((bool)PointerCall(game,"VerificationOpenBuilding",own),mode+" 不能打开村庄升级菜单");
            Require(before==(string)PointerCall(host,"ExportSave"),mode+" 打开菜单改变权威状态");
            PointerCall(game,"VerificationChooseBuildingUpgrade");
            Require(before==(string)PointerCall(host,"ExportSave"),mode+" 资源不足的升级仍改变了权威状态");
            // 补足固定费用后再走完全相同的菜单回调。
            PointerSet(players[0],"Resources",new ResourceBag{Wheat=2,Ore=3,Brick=2});bank.Wheat=17;bank.Ore=16;bank.Brick=17;
            session.GetType().GetField("state",Private).SetValue(session,state);PointerCall(session,"AssertInvariants");PointerCall(game,"M6Refresh");yield return null;
            // 即使权威快照已有足够资源，骰子展示期间也不能提前消费收获。
            before=(string)PointerCall(host,"ExportSave");app.TableDiceRolling=true;
            try
            {
                Require(!app.IsHumanTurn&&app.BlocksTableInput(new Vector2(720,450)),mode+" 骰子展示未阻止牌桌输入");
                Require(!(bool)PointerCall(game,"VerificationOpenBuilding",own),mode+" 骰子展示期间可打开升级菜单");
                var commandType=game.GetType().GetMethod("Submit").GetParameters()[0].ParameterType;
                var command=Activator.CreateInstance(commandType);
                PointerSet(command,"PlayerId","P1");PointerSet(command,"TargetId",own);
                var kindProperty=commandType.GetProperty("Kind");kindProperty.SetValue(command,Enum.Parse(kindProperty.PropertyType,"BuildCity"));
                Require(PointerCall(game,"Submit",command)==null&&!app.StepAi(),mode+" 骰子展示期间仍然提交了操作");
                Require(before==(string)PointerCall(host,"ExportSave"),mode+" 骰子展示期间改变了权威状态");
            }
            finally{app.TableDiceRolling=false;}
            Require((bool)PointerCall(game,"VerificationOpenBuilding",own),mode+" 合法升级菜单未打开");
            table=(TabletopScene)PointerGet(PointerGet(game,"VerificationBoard"),"TableScene");
            if(mode=="cities-knights")yield return Capture("08-村庄右键升级菜单");
            PointerCall(game,"VerificationChooseBuildingUpgrade");yield return null;yield return Settle();
            view=PointerGet(game,"M6View");publicBoard=PointerGet(view,"Board");
            Require(((PiecePlacement[])PointerGet(publicBoard,"Cities")).Any(c=>c.LocationId==own),mode+" 菜单未将村庄升级城市");
            var resources=(ResourceBag)PointerGet(view,"OwnResources");Require(resources.Wheat==0&&resources.Ore==0&&resources.Brick==2,mode+" 城市升级扣费不正确");
            if(mode=="cities-knights"||mode=="combined")
            {
                Require((bool)PointerCall(game,"VerificationOpenBuilding",own),mode+" 城市城墙菜单未打开");
                if(mode=="cities-knights")yield return Capture("09-城市右键城墙菜单");
                PointerCall(game,"VerificationChooseBuildingUpgrade");yield return null;yield return Settle();
                view=PointerGet(game,"M6View");publicBoard=PointerGet(view,"Board");
                Require(((PiecePlacement[])PointerGet(publicBoard,"Walls")).Any(w=>w.LocationId==own),mode+" 城墙菜单没有建墙");
                Require(((ResourceBag)PointerGet(view,"OwnResources")).Brick==0,mode+" 城墙没有正确扣除两砖块");
                before=(string)PointerCall(host,"ExportSave");PointerCall(game,"VerificationOpenBuilding",own);PointerCall(game,"VerificationChooseBuildingUpgrade");
                Require(before==(string)PointerCall(host,"ExportSave"),mode+" 重复城墙改变了状态");
            }
            var renderer=PointerGet(game,"VerificationBoard");var edge=((Edge[])PointerGet(publicBoard,"Edges"))[0].Id;var tile=tiles[0].Id;
            foreach(string command in new[]{"BuildCity","BuildWall","RecruitKnight","PromoteKnight","MoveKnight","MoveRobber","MovePirate","BuildShip","PlacePort","BuildRoad"})
            {
                var preview=TabletopPointerPreview.ForCommand(command,knightLevel:2,knightActive:true);
                string target=preview.Kind=="road"||preview.Kind=="ship"||preview.Kind=="port"?edge:preview.Kind=="robber"||preview.Kind=="pirate"?tile:own;
                before=(string)PointerCall(host,"ExportSave");PointerCall(renderer,"PreviewPiece",target,preview,"P1",true);
                var ghost=(GameObject)PointerGet(renderer,"VerificationPointerPreview");
                Require(ghost!=null&&ghost.name.Contains(preview.Kind),mode+" "+command+" 预览棋子类型不正确");
                Require(ghost.GetComponentsInChildren<Renderer>().Length>0,mode+" "+command+" 缺少实际模型");
                if(command=="PromoteKnight")Require(ghost.name.Contains("level-2"),mode+" 晋升预览未使用二级骑士实体模型");
                Require(before==(string)PointerCall(host,"ExportSave"),mode+" 鼠标效果改变权威状态");
            }
            PointerCall(renderer,"ClearPreview");PointerCall(session,"AssertInvariants");
        }
    }
}
