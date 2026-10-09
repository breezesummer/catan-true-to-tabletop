using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.Core;
using Catan.Core.M4;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Command = Catan.Core.M4.Command;
using CommandKind = Catan.Core.M4.CommandKind;
using GamePhase = Catan.Core.M4.GamePhase;
using PlayerView = Catan.Core.M4.PlayerView;

// Explicit command-line acceptance driver; never enabled during ordinary play.
public sealed class M4PlayerVerification : MonoBehaviour
{
    M4App app;
    public void Initialize(M4App value){app=value;StartCoroutine(Run());}
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    Command Cmd(string player,CommandKind kind,string target=null)=>new Command{Id=Guid.NewGuid().ToString("N"),PlayerId=player,Kind=kind,TargetId=target};
    void Seat(string player)
    {
        app.VerificationPreparePrivateSelections();app.SwitchSeat(player);
        Require(app.VerificationCurtain&&!app.VerificationHasPrivateView&&app.VerificationSelectionsCleared,"Seat switch retained private presentation");
        app.ConfirmSeat();Require(app.VerificationSeat==player,"Wrong confirmed seat");
    }
    void Execute(Command command){Seat(command.PlayerId);var result=app.Submit(command);Require(result.Success,"Command rejected: "+command.Kind+" / "+result.ErrorCode);}
    void Setup()
    {
        for(int i=0;i<20;i++)
        {
            var v=app.VerificationHost.View("P1");
            if(v.Phase!=GamePhase.SetupSettlement&&v.Phase!=GamePhase.SetupRoad)return;
            v=app.VerificationHost.View(v.ActivePlayerId);
            var kind=v.Phase==GamePhase.SetupRoad?CommandKind.SetupRoad:CommandKind.SetupSettlement;
            Execute(Cmd(v.ActivePlayerId,kind,(kind==CommandKind.SetupRoad?v.LegalEdgeIds:v.LegalVertexIds)[0]));
        }
        throw new InvalidOperationException("Setup did not finish");
    }
    void FinishPending()
    {
        for(int i=0;i<60;i++)
        {
            var initial=app.VerificationHost.View("P1");var actor=M4App.Actor(initial);var v=app.VerificationHost.View(actor);
            if(v.Phase==GamePhase.Action||v.Phase==GamePhase.ProductionAwaitRoll||v.Phase==GamePhase.Finished)return;
            if(v.Phase==GamePhase.Discard)
            {
                var c=Cmd(actor,CommandKind.DiscardResources);c.Resources=new ResourceBag();c.Commodities=new CommodityBag();int remaining=v.Discards.First(x=>x.PlayerId==actor).Amount;
                foreach(Resource r in Enum.GetValues(typeof(Resource))){c.Resources[r]=Math.Min(remaining,v.OwnResources[r]);remaining-=c.Resources[r];}
                foreach(Commodity r in Enum.GetValues(typeof(Commodity))){c.Commodities[r]=Math.Min(remaining,v.OwnCommodities[r]);remaining-=c.Commodities[r];}Execute(c);
            }
            else if(v.Phase==GamePhase.RobberMove)Execute(Cmd(actor,CommandKind.MoveRobber,v.Board.Tiles.First(t=>t.Id!=v.Board.RobberTileId).Id));
            else if(v.Phase==GamePhase.RobberSteal){var c=Cmd(actor,CommandKind.StealResource);c.OtherPlayerId=v.PendingDecision.EligibleVictimIds[0];Execute(c);}
            else if(v.Phase==GamePhase.PendingChoice&&v.PendingDecision.Kind=="ProgressDiscard") {var c=Cmd(actor,CommandKind.DiscardProgressCard);c.ProgressCard=v.OwnProgressCards[0];Execute(c);}
            else if(v.LegalActions.Length>0)Execute(v.LegalActions[0]);
            else throw new InvalidOperationException("Unhandled pending: "+v.Phase+" / "+v.PendingDecision.Kind);
        }
        throw new InvalidOperationException("Pending sequence did not finish");
    }
    void CheckPicking()
    {
        var v=app.VerificationHost.View("P1");Seat(v.ActivePlayerId);var board=app.VerificationBoard;
        foreach(bool top in new[]{false,true})
        {
            board.SetCamera(top);
            foreach(float zoom in new[]{-.10f,.10f})
            {
                board.ChangeZoom(zoom);
                foreach(var x in v.Board.Vertices)Require(app.VerificationPick("V",board.ScreenPoint(x.Id))==x.Id,"Vertex picking: "+x.Id);
                foreach(var x in v.Board.Edges)Require(app.VerificationPick("E",board.ScreenPoint(x.Id))==x.Id,"Edge picking: "+x.Id);
                foreach(var x in v.Board.Tiles)Require(app.VerificationPick("T",board.ScreenPoint(x.Id))==x.Id,"Tile picking: "+x.Id);
            }
        }
        board.SetCamera(false);
    }
    void RandomContinuation(string directory)
    {
        var host=app.VerificationHost;var v=host.View("P1");Require(v.Phase==GamePhase.ProductionAwaitRoll,"Expected pre-roll save");
        var path=Path.Combine(directory,"before-roll.json");host.WriteSave(path);var comparison=new M4LocalGameHost();comparison.ReadSave(path);
        var command=Cmd(v.ActivePlayerId,CommandKind.RollDice);Require(comparison.Submit(command).Success,"Comparison roll failed");Execute(command);
        host.WriteSave(path+".actual");comparison.WriteSave(path+".comparison");Require(File.ReadAllText(path+".actual")==File.ReadAllText(path+".comparison"),"Event/production/random continuation mismatch");FinishPending();
    }
    IEnumerator Run()
    {
        var args=Environment.GetCommandLineArgs();int save=Array.IndexOf(args,"--m4-verify-save"),load=Array.IndexOf(args,"--m4-verify-load");
        if(save<0&&load<0)yield break;yield return null;
        string directory=args[(save>=0?save:load)+1];Directory.CreateDirectory(directory);
        try
        {
            var host=app.VerificationHost;
            if(save>=0)
            {
                foreach(int seats in new[]{3,4})
                {
                    host.NewGame(seats,20261009u);Seat(host.View("P1").ActivePlayerId);Setup();var v=host.View("P1");
                    Require(v.Board.Cities.Length==seats&&v.Board.Settlements.Length==seats&&v.Board.Roads.Length==seats*2,"Normal settlement/city setup failed");
                    Require(v.Board.RobberTileId==null,"Robber must start off-board");
                    Require(v.Players.All(p=>host.View(p.Id).OwnCommodities.Total==0),"Setup incorrectly granted commodities");
                }
                var initial=host.View("P1");string before=Path.Combine(directory,"invalid-before.json"),after=Path.Combine(directory,"invalid-after.json");host.WriteSave(before);
                var bad=host.Submit(Cmd(initial.ActivePlayerId,CommandKind.BuildWall,"V-invalid"));Require(!bad.Success,"Invalid placement accepted");host.WriteSave(after);Require(File.ReadAllText(before)==File.ReadAllText(after),"Invalid command changed authority");
                var damaged=Path.Combine(directory,"damaged.json");File.WriteAllText(damaged,"{bad-save}");bool rejected=false;try{host.ReadSave(damaged);}catch(Exception){rejected=true;}Require(rejected,"Damaged save accepted");host.WriteSave(after);Require(File.ReadAllText(before)==File.ReadAllText(after),"Failed restore changed authority");
                RandomContinuation(directory);
                // Real rolls/actions exercise the event track and barbarian pending decisions in Mono.
                int knightActions=0;
                for(int turn=0;turn<32;turn++)
                {
                    var v=host.View("P1");if(v.Phase==GamePhase.Finished)break;
                    if(v.Phase==GamePhase.Action)Execute(Cmd(v.ActivePlayerId,CommandKind.EndTurn));
                    v=host.View("P1");Execute(Cmd(v.ActivePlayerId,CommandKind.RollDice));FinishPending();
                    v=host.View(v.ActivePlayerId);
                    var knight=v.LegalActions.FirstOrDefault(c=>c.Kind==CommandKind.RecruitKnight||c.Kind==CommandKind.ActivateKnight);if(knight!=null){Execute(knight);knightActions++;}
                }
                var current=host.View("P1");if(current.Phase==GamePhase.Action)Execute(Cmd(current.ActivePlayerId,CommandKind.EndTurn));
                host.WriteSave(Path.Combine(directory,"authority.json"));CheckPicking();
                Debug.Log("CATAN_M4_PLAYER_SAVE_OK: normal 3/4-player setup, event/barbarian commands, privacy curtain, rollback, random continuation, picking, authoritative save; knight commands="+knightActions);
            }
            else
            {
                string path=Path.Combine(directory,"authority.json");host.ReadSave(path);host.WriteSave(path+".restored");Require(File.ReadAllText(path)==File.ReadAllText(path+".restored"),"Independent process save mismatch");Seat(M4App.Actor(host.View("P1")));RandomContinuation(directory);CheckPicking();
                Debug.Log("CATAN_M4_PLAYER_LOAD_OK: independent-process restore and event/production random continuation");
            }
        }
        catch(Exception e){Debug.LogException(e);Application.Quit(1);yield break;}
        for(int i=0;i<5;i++)yield return null;
        try
        {
            app.VerificationBoard.SetCamera(false);Capture(app.VerificationBoard.BoardCamera,Path.Combine(directory,"m4-default.png"));
            app.VerificationBoard.SetCamera(true);Capture(app.VerificationBoard.BoardCamera,Path.Combine(directory,"m4-top.png"));
            Debug.Log("CATAN_M4_RENDER_OK: URP board-only captures; "+SystemInfo.graphicsDeviceName);
        }
        catch(Exception e){Debug.LogException(e);Application.Quit(1);yield break;}
        app.VerificationBoard.ShowComponentSamples();
        for(int i=0;i<3;i++)yield return null;
        try
        {
            var board=app.VerificationBoard;
            board.SetCamera(false);Capture(board.BoardCamera,Path.Combine(directory,"m4-components-default.png"));
            board.SetCamera(true);Capture(board.BoardCamera,Path.Combine(directory,"m4-components-top.png"));
            board.SetCamera(false);board.ChangeZoom(-.15f);Capture(board.BoardCamera,Path.Combine(directory,"m4-components-crowded.png"));
            board.BoardCamera.transform.position=new Vector3(.25f,.065f,-.25f);board.BoardCamera.transform.LookAt(new Vector3(0,.02f,0));Capture(board.BoardCamera,Path.Combine(directory,"m4-components-side.png"));
            Debug.Log("CATAN_M4_COMPONENT_PREVIEWS_OK: four public-only sample renders; no game-authority edits");
        }
        catch(Exception e){Debug.LogException(e);Application.Quit(1);yield break;}
        Application.Quit(0);
    }
    static void Capture(Camera camera,string path)
    {
        var target=new RenderTexture(1440,1080,24,RenderTextureFormat.ARGB32);target.Create();var rect=camera.rect;camera.rect=new Rect(0,0,1,1);
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};Require(RenderPipeline.SupportsRenderRequest(camera,request),"URP capture unsupported");RenderPipeline.SubmitRenderRequest(camera,request);camera.rect=rect;
        var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1440,1080,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,1080),0,0);texture.Apply();
        Require(texture.GetPixels32().Count(p=>p.r>40&&p.g>30&&Math.Abs(p.r-p.g)>12)>5000,"Blank capture");File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=previous;Destroy(texture);target.Release();Destroy(target);
    }
}

