using System;
using System.Linq;
using UnityEngine;
using Catan.Core.M5;
using Command = Catan.Core.M5.Command;
using CommandKind = Catan.Core.M5.CommandKind;
using GamePhase = Catan.Core.M5.GamePhase;

public sealed partial class M5App
{
    bool specialOpen;
    Vector2 specialScroll;
    static bool SpecialCommand(Command c)=>new[]{CommandKind.SetupPort,CommandKind.PlacePort,CommandKind.ClaimWonder,CommandKind.BuildWonder,CommandKind.ConvertWarship,CommandKind.AttackFortress,CommandKind.ChooseInvasionRoute,CommandKind.StealCloth}.Contains(c.Kind);
    bool SpecialPanelVisible=>view!=null&&(specialOpen||view.Phase==GamePhase.SetupPort||view.Phase==GamePhase.PortPlacement);
    bool SpecialRelevant=>view!=null&&(view.Wonders.Length>0||view.Board.Fortresses.Length>0||view.Board.Villages.Length>0||view.ScenarioId=="forgotten-tribe"||SpecialPanelVisible);
    static readonly Rect FramePirateButton=new Rect(15,283,178,32);
    bool HasDirectTokenMoves=>view.Phase==GamePhase.RobberMove&&view.LegalActions.Any(c=>c.Kind==CommandKind.MoveRobber||c.Kind==CommandKind.MovePirate);
    bool CanSendPirateToFrame=>view.Phase==GamePhase.RobberMove&&view.LegalActions.Any(c=>c.Kind==CommandKind.MovePirate&&c.TargetId=="frame");
    bool SpecialBlocksMouse(Vector2 p)=>view!=null&&(CanSendPirateToFrame&&FramePirateButton.Contains(p)||SpecialRelevant&&new Rect(1040,82,220,34).Contains(p)||specialOpen&&new Rect(1032,120,380,410).Contains(p));
    bool PickPendingMapAction(Vector2 mouse,bool click)
    {
        bool port=view.Phase==GamePhase.SetupPort||view.Phase==GamePhase.PortPlacement;
        if(!port&&!HasDirectTokenMoves)return false;
        string target=Pick(port?"E":"T",mouse);
        var move=view.LegalActions.FirstOrDefault(c=>c.TargetId==target&&(port?
            c.Kind==CommandKind.SetupPort||c.Kind==CommandKind.PlacePort:
            c.Kind==CommandKind.MovePirate||c.Kind==CommandKind.MoveRobber));
        if(move==null){board.ClearPreview();return true;}
        board.PreviewPiece(target,TabletopPointerPreview.ForCommand(move.Kind.ToString()),seat,true);
        if(!click)return true;
        if(port&&view.LegalActions.Count(c=>c.Kind==move.Kind&&c.TargetId==target)>1)
        {
            var ports=view.LegalActions.Where(c=>c.Kind==move.Kind&&c.TargetId==target).ToArray();
            var labels=ports.Select(c=>{
                var item=view.HeldPorts.FirstOrDefault(h=>h.Id==c.SourceId);
                return item?.Resource==null?"通用港 3:1":Goods[(int)item.Resource.Value]+"港 2:1";
            }).ToArray();
            mapChoice.Open(mouse,"放置港口",labels,i=>PlaceMapAction(ports[i]));
        }
        else PlaceMapAction(move);
        return true;
    }
    void PlaceMapAction(Command candidate)
    {
        var command=New(candidate.Kind,candidate.TargetId);
        command.SourceId=candidate.SourceId;
        Submit(command);
    }
    void DrawSpecialPanel()
    {
        if(view==null||ai==null||ai.TablePresentationBlocked||tabletop.HistoryOpen)return;
        if(ai.IsHumanTurn&&CanSendPirateToFrame&&ModernOperationUI.Button(FramePirateButton,"海盗移至外海"))Submit(New(CommandKind.MovePirate,"frame"));
        if(!SpecialRelevant)return;
        if(GUI.Button(new Rect(1040,82,220,34),"海洋剧本 · 专属行动",button))specialOpen=!specialOpen;
        if(!specialOpen)return;
        GUI.Box(new Rect(1032,120,380,410),GUIContent.none);
        GUILayout.BeginArea(new Rect(1042,130,360,390));specialScroll=GUILayout.BeginScrollView(specialScroll);
        DrawSpecialActions();GUILayout.EndScrollView();GUILayout.EndArea();
    }
    void DrawSpecialActions()
    {
        if(view==null)return;
        var own=view.Players.First(p=>p.Id==seat);
        if(view.ScenarioId=="forgotten-tribe")Text("项目变体：部落礼物改为自选颜色抽取进步卡。",true);
        if(view.ScenarioId=="pirate-islands")Text("项目变体：可行动的激活骑士失活，将一艘普通进攻船升级为战舰。",true);
        if(view.Board.Villages.Length>0)Text("布匹标记 "+own.Cloth+" · 每两块计一分（与商品布牌分开）",true);
        if(own.WonderId!=null)Text("奇观 "+SpecialWonderName(own.WonderId)+" · "+own.WonderLevel+" / 4 层",true);
        if(ai!=null&&!ai.IsHumanTurn)return;
        int option=0;
        foreach(var c in view.LegalActions.Where(SpecialCommand))if(Btn("选项 "+(++option)+" · "+SpecialLabel(c))){Submit(c);GUIUtility.ExitGUI();return;}
    }
    static string SpecialWonderName(string id)=>id=="great-wall"?"长城":id=="great-bridge"?"巨桥":id=="grand-monument"?"纪念碑":id=="grand-theater"?"大剧院":id=="grand-castle"?"大城堡":id;
    string SpecialLabel(Command c)
    {
        switch(c.Kind){
            case CommandKind.ClaimWonder:return "认领"+SpecialWonderName(c.TargetId);
            case CommandKind.BuildWonder:return "建造"+SpecialWonderName(c.TargetId)+"一层";
            case CommandKind.ConvertWarship:return "失活骑士并升级战舰";
            case CommandKind.AttackFortress:return "攻击要塞并结束回合";
            case CommandKind.ChooseInvasionRoute:return "选择西行起点";
            case CommandKind.StealCloth:return "偷取 "+c.OtherPlayerId+" 一块布匹";
            default:return "放置港口";
        }
    }
    void DrawSpecialMapLabels()
    {
        void Mark(string id,string caption,MapSymbol symbol=MapSymbol.Flag)
        {
            if(!string.IsNullOrEmpty(id))
                PaintedMapSymbols.Draw(MobileTabletop.Logical(board.ScreenPoint(id)),symbol,caption,small);
        }
        if(SpecialPanelVisible&&ai!=null&&ai.IsHumanTurn)
        {
            var actions=view.LegalActions.Where(SpecialCommand).ToArray();
            foreach(var group in actions.Select((c,i)=>new{Command=c,Number=i+1}).Where(x=>x.Command.Kind==CommandKind.SetupPort||x.Command.Kind==CommandKind.PlacePort||x.Command.Kind==CommandKind.ConvertWarship||x.Command.Kind==CommandKind.ChooseInvasionRoute).GroupBy(x=>x.Command.TargetId))
                Mark(group.Key,group.First().Command.Kind==CommandKind.SetupPort||group.First().Command.Kind==CommandKind.PlacePort?"港口":"选项 "+string.Join("/",group.Select(x=>x.Number)));
            foreach(var group in actions.Select((c,i)=>new{Command=c,Number=i+1}).Where(x=>x.Command.Kind==CommandKind.ConvertWarship).GroupBy(x=>x.Command.SourceId))
                Mark(group.Key,"骑士 "+string.Join("/",group.Select(x=>x.Number)));
        }
        foreach(var ship in view.Board.Ships.Where(s=>s.IsWarship))Mark(ship.LocationId,"战舰",MapSymbol.Warship);
        foreach(var v in view.Board.Villages)Mark(v.VertexId,"村 "+v.Number+" · 布 "+v.Cloth,MapSymbol.Village);
        foreach(var f in view.Board.Fortresses){if(f.Strength>0)Mark(f.VertexId,f.PlayerId+" 堡垒 "+f.Strength,MapSymbol.Fortress);Mark(f.BeachheadVertexId,f.PlayerId+" 滩头",MapSymbol.Beachhead);}
        foreach(var e in view.Board.BonusEdgeIds)if(!board.ReferenceMarkers.HasBonusMarker(e))Mark(e,"奖励 1 分",MapSymbol.Gift);
        foreach(var e in view.Board.GiftCardEdgeIds)Mark(e,"进步卡礼物",MapSymbol.Gift);
        foreach(var p in view.Board.GiftPorts)if(!board.ReferenceMarkers.HasGiftPort(p.Id))Mark(p.EdgeId,"港口礼物",MapSymbol.Gift);
        foreach(var w in view.Wonders)foreach(var id in w.VertexIds)Mark(id,SpecialWonderName(w.Id),MapSymbol.Wonder);
    }
}
