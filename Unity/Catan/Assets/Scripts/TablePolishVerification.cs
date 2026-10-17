using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Catan.Core;
using UnityEngine;

// 显式参数启用的隔离夹具，覆盖正式地图落子与 IMGUI 单张手牌事件，不触碰玩家存档。
public sealed partial class InteractionPresentationVerification
{
    Action polishGuiAction;
    Exception polishGuiError;
    bool polishPalette;
    IEnumerator VerifyTablePolish()
    {
        var font=MobileTabletop.LoadFont();Require(font.name=="NotoSerifCJKsc-Regular","没有使用随包宋体");
        foreach(char character in "村庄道路船银行库存为0你的回合0123456789")Require(font.HasCharacter(character),"字体缺字："+character);
        foreach(string rules in new[]{"base","seafarers","cities-knights","combined"})
        {
            stage=rules+" 地图直接建设";
            app.StartGame(rules,"heading-for-new-shores",4,"P1",true);yield return null;yield return null;
            object game=rules=="base"?(object)GetComponent<M2App>():rules=="seafarers"?GetComponent<M3App>():rules=="cities-knights"?GetComponent<M4App>():GetComponent<M5App>();
            var host=PolishGet(game,"VerificationHost");
            var session=host.GetType().GetField("session",Private).GetValue(host);
            var state=session.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(session,null);
            var initialBoard=PolishGet(PolishGet(game,"M6View"),"Board");
            var tiles=(Tile[])PolishGet(initialBoard,"Tiles");var edges=(Edge[])PolishGet(initialBoard,"Edges");
            var coast=edges.FirstOrDefault(e=>tiles.Any(t=>t.Resource=="sea"&&e.Vertices.All(t.Vertices.Contains))&&tiles.Any(t=>t.Resource!="sea"&&t.Resource!="fog"&&e.Vertices.All(t.Vertices.Contains)))??edges[0];
            PolishSet(state,"ActivePlayerId","P1");PolishSet(state,"Turn",9);PolishSet(state,"SetupPlacementIndex",8);PolishSetEnum(state,"Phase",rules=="cities-knights"||rules=="combined"?"PendingChoice":"RoadBuilding");if(rules=="cities-knights"||rules=="combined")PolishSetEnum(state,"DecisionReturnPhase","Action");
            var pending=Activator.CreateInstance(state.GetType().GetProperty("PendingDecision").PropertyType);
            PolishSet(pending,"Kind",rules=="cities-knights"||rules=="combined"?"ProgressRoads":"RoadBuilding");if(rules=="cities-knights"||rules=="combined")PolishSet(pending,"Amount",2);PolishSet(pending,"PlayerId","P1");PolishSet(pending,"RemainingRoads",2);PolishSetEnum(pending,"ReturnPhase","Action");PolishSet(state,"PendingDecision",pending);
            PolishSet(state,"Settlements",new[]{new PiecePlacement{PlayerId="P1",LocationId=coast.Vertices[0]}});
            PolishSet(state,"Roads",Array.Empty<PiecePlacement>());
            var players=(Array)PolishGet(state,"Players");
            foreach(var player in players)
            {
                bool own=(string)PolishGet(player,"Id")=="P1";
                var resources=(ResourceBag)PolishGet(player,"Resources");foreach(Resource resource in Enum.GetValues(typeof(Resource)))resources[resource]=own?(resource==Resource.Wood?3:2):0;
                var pieces=PolishGet(player,"Pieces");PolishSet(pieces,"Settlements",own?4:5);PolishSet(pieces,"Roads",15);
            }
            var bank=(ResourceBag)PolishGet(state,"Bank");foreach(Resource resource in Enum.GetValues(typeof(Resource)))bank[resource]=19-players.Cast<object>().Sum(p=>((ResourceBag)PolishGet(p,"Resources"))[resource]);
            session.GetType().GetField("state",Private).SetValue(session,state);session.GetType().GetMethod("AssertInvariants").Invoke(session,null);
            game.GetType().GetMethod("M6Refresh",Private).Invoke(game,null);yield return null;yield return Settle();
            var board=PolishGet(game,"VerificationBoard");table=(TabletopScene)PolishGet(board,"TableScene");
            var snapshot=(OperationSnapshot)PolishGet(game,"VerificationOperationSnapshot");var dock=(OperationDock)PolishGet(game,"VerificationOperationDock");
            Require(ReferenceEquals(game.GetType().GetField("font",Private).GetValue(game),font),"规则界面字体不统一");
            Require(!dock.HasOverlay&&!app.TableOverlayOpen,"道路建设被旧面板遮挡");
            Require(app.RoadPlacementPrompt.EndsWith("0/2"),"初始进度不是0/2");
            Func<string> save=()=> (string)host.GetType().GetMethod("ExportSave").Invoke(host,null);
            var input=game.GetType().GetMethod("UpdateFreeRoadPlacement",Private);
            string before=save();input.Invoke(game,new object[]{new Vector2(-100,-100),true});Require(save()==before,"非法位置改变了局面");
            var view=PolishGet(game,"M6View");var legal=rules=="cities-knights"||rules=="combined"?(string[])PolishGet(game,"FreeRoadIds"):(string[])PolishGet(view,"LegalEdgeIds");Require(legal.Length>0,"夹具没有可建道路");
            var screen=board.GetType().GetMethod("ScreenPoint");string target=legal.First(id=>TabletopUI.ContainsMapScreenPoint((Vector2)screen.Invoke(board,new object[]{id})));
            var point=(Vector2)screen.Invoke(board,new object[]{target});
            var adjacent=tiles.Where(t=>t.Resource!="sea"&&t.Resource!="fog"&&edges.First(e=>e.Id==target).Vertices.All(t.Vertices.Contains)).First();
            point=Vector2.Lerp(point,(Vector2)screen.Invoke(board,new object[]{adjacent.Id}),.08f);
            input.Invoke(game,new object[]{point,false});Require(save()==before,"悬停预览提前执行建设");
            Require(board.GetType().GetField("ghost",Private).GetValue(board)!=null,"合法位置没有自动预览");
            yield return Capture(rules+"-道路预览0-2");
            input.Invoke(game,new object[]{point,true});yield return null;yield return Settle();
            view=PolishGet(game,"M6View");Require(((PiecePlacement[])PolishGet(PolishGet(view,"Board"),"Roads")).Length==1,"地图点击没有建成道路");
            Require(app.RoadPlacementPrompt.EndsWith("1/2"),"第一条道路后进度没有更新");
            var shipIds=rules=="combined"?(string[])PolishGet(game,"FreeShipIds"):rules=="seafarers"?(string[])PolishGet(view,"LegalShipEdgeIds"):Array.Empty<string>();
            bool useShip=shipIds.Length>0;target=(useShip?shipIds:rules=="cities-knights"?(string[])PolishGet(game,"FreeRoadIds"):(string[])PolishGet(view,"LegalEdgeIds")).First(id=>TabletopUI.ContainsMapScreenPoint((Vector2)screen.Invoke(board,new object[]{id})));
            point=(Vector2)screen.Invoke(board,new object[]{target});
            if(useShip)
            {
                var edge=edges.First(e=>e.Id==target);var sea=tiles.FirstOrDefault(t=>t.Resource=="sea"&&edge.Vertices.All(t.Vertices.Contains));
                if(sea!=null)point=Vector2.Lerp(point,(Vector2)screen.Invoke(board,new object[]{sea.Id}),.08f);
                else{var land=tiles.First(t=>edge.Vertices.All(t.Vertices.Contains));point=point+(point-(Vector2)screen.Invoke(board,new object[]{land.Id}))*.08f;}
            }
            input.Invoke(game,new object[]{point,false});yield return Capture(rules+"-第二段预览1-2");input.Invoke(game,new object[]{point,true});yield return null;yield return Settle();
            view=PolishGet(game,"M6View");Require(PolishGet(view,"Phase").ToString()=="Action","第二段建设后未回到行动阶段");
            var finalBoard=PolishGet(view,"Board");Require(((PiecePlacement[])PolishGet(finalBoard,"Roads")).Length== (useShip?1:2),"第二段道路数量不正确");
            if(useShip)Require(((Array)PolishGet(finalBoard,"Ships")).Length==1,"海面一侧没有自动建船");
            Require(((ResourceBag)PolishGet(view,"OwnResources")).Wood==3,"免费建设错误扣费");session.GetType().GetMethod("AssertInvariants").Invoke(session,null);
            if(rules=="base")
            {
                stage="同类手牌逐张收放";yield return new WaitForEndOfFrame();
                snapshot=(OperationSnapshot)PolishGet(game,"VerificationOperationSnapshot");before=save();
                var cardPoint=PolishCardPoint(dock,0,2);
                yield return PolishGui(()=>PolishClick(dock,snapshot,font,cardPoint));Require(dock.Selection.IsCardSelected(0,2)&&dock.Selection.Selected.Sum()==1,"没有抽出所点的单张手牌："+string.Join(",",dock.Selection.Selected));
                yield return PolishGui(()=>PolishClick(dock,snapshot,font,cardPoint));Require(dock.Selection.Selected.Sum()==0,"再次点击未放回同一张牌");
                yield return PolishGui(()=>PolishClick(dock,snapshot,font,PolishCardPoint(dock,0,0)));
                yield return PolishGui(()=>PolishClick(dock,snapshot,font,PolishCardPoint(dock,0,2)));Require(dock.Selection.IsCardSelected(0,0)&&dock.Selection.IsCardSelected(0,2)&&!dock.Selection.IsCardSelected(0,1),"点击另一张同类牌改变了中间那张牌");
                yield return Capture("单张手牌选择");yield return PolishGui(()=>PolishClick(dock,snapshot,font,PolishCardPoint(dock,0,2)));Require(dock.Selection.IsCardSelected(0,0)&&!dock.Selection.IsCardSelected(0,2),"放回一张牌影响了另一张同类牌");Require(save()==before,"收放手牌改变权威状态");
                dock.Reset();yield return null;dock.OpenTrade();yield return Capture("银行交易隐藏非零库存");dock.Reset();
            }
        }
        stage="四套棋子同色与新字体";
        foreach(string seat in new[]{"P1","P2","P3","P4"})
        {
            var plastic=PlayerComponentAssets.BuildingPlastic(seat);Require(plastic!=null,"缺少建筑基准材质");
            var root=new GameObject("配色检查 "+seat);root.transform.position=new Vector3(100,0,100);
            var models=new[]{PlayerComponentAssets.Building("village",seat,"村庄",root.transform.position,root.transform),PlayerComponentAssets.Building("city",seat,"城市",root.transform.position,root.transform),PlayerComponentAssets.Building("city-wall",seat,"城墙",root.transform.position,root.transform),PlayerComponentAssets.Knight(seat,1,false,"骑士",root.transform.position,root.transform),PhotoSampleAssets.Piece(true,"道路",root.transform.position,Quaternion.identity,BoardRenderer.SeatColor(seat),root.transform),EntityBoardAssets.Ship("船",BoardRenderer.SeatColor(seat),root.transform.position,Quaternion.identity,root.transform)};
            foreach(var model in models)
            {
                Require(model!=null,"棋子模型缺失");var plastics=model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.name.Contains("Plastic")).ToArray();
                Require(plastics.Length>0&&plastics.All(m=>m==plastic),"棋子塑料与建筑颜色不一致："+model.name);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>()){var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);Require(block.isEmpty,"残留亮色覆盖建筑标准材质");}
            }
            foreach(string kind in new[]{"road","settlement","city","ship","wall","knight-1","knight-2","knight-3"})Require(OperationPieceAssets.Texture(kind,seat,kind.StartsWith("knight-")?int.Parse(kind.Substring(7)):1)!=null,"新版棋子图标缺失");
            Destroy(root);
        }
        PolishSetField(app,"openingOrderUntil",0d);app.ShowTablePrompt("");polishPalette=true;yield return Capture("四套棋子图标与字体");polishPalette=false;
        foreach(string rules in new[]{"cities-knights","combined"})
        {
            stage=rules+" 引水渠五资源面板";app.StartGame(rules,"heading-for-new-shores",4,"P1",true);yield return null;yield return null;
            object game=rules=="cities-knights"?(object)GetComponent<M4App>():GetComponent<M5App>();var host=PolishGet(game,"VerificationHost");var session=host.GetType().GetField("session",Private).GetValue(host);
            var state=session.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(session,null);PolishSet(state,"ActivePlayerId","P1");PolishSet(state,"Turn",9);PolishSet(state,"SetupPlacementIndex",8);PolishSetEnum(state,"Phase","PendingChoice");PolishSetEnum(state,"DecisionReturnPhase","Action");
            var pending=Activator.CreateInstance(state.GetType().GetProperty("PendingDecision").PropertyType);PolishSet(pending,"Kind","Aqueduct");PolishSet(pending,"PlayerId","P1");PolishSetEnum(pending,"ReturnPhase","Action");PolishSet(state,"PendingDecision",pending);
            var players=(Array)PolishGet(state,"Players");((ResourceBag)PolishGet(players.GetValue(1),"Resources")).Wood=19;((ResourceBag)PolishGet(state,"Bank")).Wood=0;
            session.GetType().GetField("state",Private).SetValue(session,state);session.GetType().GetMethod("AssertInvariants").Invoke(session,null);game.GetType().GetMethod("M6Refresh",Private).Invoke(game,null);table=(TabletopScene)PolishGet(PolishGet(game,"VerificationBoard"),"TableScene");yield return null;
            var selected=(ResourceBag)game.GetType().GetField("resourceChoiceSelection",Private).GetValue(game);Require(selected.Total==0,"引水渠没有从空选择开始");yield return Capture(rules+"-引水渠五资源与库存为0");
            var bank=(ResourceBag)PolishGet(state,"Bank");Require(OperationResourceChoice.Change(0,1,1,selected,bank)!=null&&selected.Total==0,"引水渠可以选择空库存资源");
            Require(OperationResourceChoice.Change(3,1,1,selected,bank)==null&&selected.Wheat==1&&selected.Total==1,"引水渠没有选中小麦");
            Require(OperationResourceChoice.Change(2,1,1,selected,bank)!=null&&selected.Wool==0&&selected.Total==1,"引水渠允许领取多张资源");
            game.GetType().GetMethod("ConfirmPendingResources",Private).Invoke(game,null);yield return null;yield return Settle();var own=(ResourceBag)PolishGet(PolishGet(game,"M6View"),"OwnResources");Require(own.Wheat==1,"引水渠没有经规则指令领取一张小麦");Require(PolishGet(PolishGet(game,"M6View"),"PendingDecision")==null,"引水渠确认后仍停留在待决面板");session.GetType().GetMethod("AssertInvariants").Invoke(session,null);
        }
        stage="五种金矿资源";app.StartGame("seafarers","heading-for-new-shores",4,"P1",true);yield return null;yield return null;
        var goldGame=GetComponent<M3App>();var goldSession=typeof(M3LocalGameHost).GetField("session",Private).GetValue(goldGame.VerificationHost);var goldState=(Catan.Core.M3.GameState)goldSession.GetType().GetMethod("GetAuthoritativeStateForTesting").Invoke(goldSession,null);
        goldState.ActivePlayerId="P1";goldState.Phase=Catan.Core.M3.GamePhase.GoldChoice;goldState.SetupPlacementIndex=8;goldState.Turn=9;goldState.GoldClaims=new[]{new Catan.Core.M3.GoldClaim{PlayerId="P1",Amount=2}};goldState.GoldReturnPhase=Catan.Core.M3.GamePhase.Action;goldState.PendingDecision=new Catan.Core.M3.PendingDecision{Kind="Gold",PlayerId="P1",ReturnPhase=Catan.Core.M3.GamePhase.Action};
        goldState.Players[1].Resources.Wood=19;goldState.Bank.Wood=0;goldSession.GetType().GetField("state",Private).SetValue(goldSession,goldState);goldSession.GetType().GetMethod("AssertInvariants").Invoke(goldSession,null);goldGame.M6Refresh();table=goldGame.VerificationBoard.TableScene;yield return null;yield return Capture("金矿五资源与库存为0");
        stage="局域网搜索单一提示";app.ReturnToMainMenu();PolishSetField(app,"networkMenu",true);PolishSetField(app,"networkLan",true);PolishSetField(app,"networkWorking",true);PolishSetField(app,"networkTab",0);PolishSetField(app,"networkMessage","正在搜索（此状态应隐藏）");yield return Capture("局域网搜索仅按钮提示");PolishSetField(app,"networkWorking",false);
    }
    static object PolishGet(object value,string name)=>value.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
    static void PolishSet(object value,string name,object data)=>value.GetType().GetProperty(name).SetValue(value,data);
    static void PolishSetEnum(object value,string name,string data){var property=value.GetType().GetProperty(name);property.SetValue(value,Enum.Parse(property.PropertyType,data));}
    static void PolishSetField(object value,string name,object data)=>value.GetType().GetField(name,Private).SetValue(value,data);
    static Vector2 PolishCardPoint(OperationDock dock,int good,int ordinal)
    {
        var faces=(IEnumerable)typeof(OperationDock).GetField("faces",Private).GetValue(dock);
        foreach(var face in faces)if((int)face.GetType().GetField("Good").GetValue(face)==good&&(int)face.GetType().GetField("Ordinal").GetValue(face)==ordinal)
        {var rect=(Rect)face.GetType().GetField("RestRect").GetValue(face);return new Vector2(rect.x+15,rect.y+70);}
        throw new InvalidOperationException("没有找到指定单张手牌");
    }
    static void PolishClick(OperationDock dock,OperationSnapshot snapshot,Font font,Vector2 point)
    {
        var previous=Event.current;var matrix=GUI.matrix;bool enabled=GUI.enabled;GUI.enabled=true;GUI.matrix=MobileTabletop.GuiMatrix;
        try
        {
            dock.ProcessHandInput(snapshot,new Event{type=EventType.MouseDown,button=0,mousePosition=point});
            dock.ProcessHandInput(snapshot,new Event{type=EventType.MouseUp,button=0,mousePosition=point});
        }
        finally{Event.current=previous;GUI.matrix=matrix;GUI.enabled=enabled;}
    }
    IEnumerator PolishGui(Action action)
    {
        polishGuiError=null;polishGuiAction=action;double deadline=Time.unscaledTimeAsDouble+3;
        while(polishGuiAction!=null&&Time.unscaledTimeAsDouble<deadline)yield return null;
        Require(polishGuiAction==null,"没有收到界面事件");if(polishGuiError!=null)throw polishGuiError;yield return null;
    }
    void OnGUI()
    {
        if(Event.current.type!=EventType.Repaint)return;
        if(polishGuiAction!=null){var action=polishGuiAction;polishGuiAction=null;try{action();}catch(Exception ex){polishGuiError=ex;}}
        if(!polishPalette)return;
        var matrix=GUI.matrix;GUI.matrix=MobileTabletop.GuiMatrix;ModernOperationUI.Styles(MobileTabletop.LoadFont());
        ModernOperationUI.Panel(new Rect(285,216,896,430));var kinds=new[]{"road","settlement","city","ship","wall","knight-1","knight-2","knight-3"};
        ModernOperationUI.Label(new Rect(310,229,845,36),"棋子配色 · 以村庄材质为基准",ModernOperationUI.Title);
        for(int row=0;row<4;row++)
        {string seat="P"+(row+1);ModernOperationUI.Label(new Rect(310,282+row*83,76,60),M6App.ColorLabel(seat));for(int col=0;col<8;col++)GUI.DrawTexture(new Rect(393+col*94,278+row*83,82,72),OperationPieceAssets.Texture(kinds[col],seat,col>4?col-4:1),ScaleMode.ScaleToFit);}
        GUI.matrix=matrix;
    }
}
