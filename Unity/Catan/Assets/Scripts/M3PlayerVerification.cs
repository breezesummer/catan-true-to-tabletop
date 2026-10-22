using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Command = Catan.Core.M3.Command;
using CommandKind = Catan.Core.M3.CommandKind;
using GamePhase = Catan.Core.M3.GamePhase;
using PlayerView = Catan.Core.M3.PlayerView;

// Explicit opt-in acceptance driver inside the real Windows Mono player.
// Local authority files never enter logs, screenshots or public player views.
public sealed class M3PlayerVerification : MonoBehaviour
{
    private M3App app;
    public void Initialize(M3App value){app=value;StartCoroutine(Run());}
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    Command Cmd(string player,CommandKind kind,string target=null) => new Command{Id=Guid.NewGuid().ToString("N"),PlayerId=player,Kind=kind,TargetId=target};
    void Seat(string player)
    {
        app.VerificationPreparePrivateSelections();
        app.SwitchSeat(player);
        Require(app.VerificationCurtain&&!app.VerificationHasPrivateView&&app.VerificationSelectionsCleared,"Seat switch retained private presentation");
        app.ConfirmSeat();Require(app.VerificationSeat==player,"Seat confirmation failed");
    }
    void Execute(Command command)
    {
        var result=app.Submit(command);Require(result.Success,"UI command rejected: "+command.Kind+" / "+result.ErrorCode);
    }
    void FinishSetup()
    {
        var h=app.VerificationHost;
        for(int count=0;count<40;count++)
        {
            var current=h.View("P1");
            if(current.Phase==GamePhase.SetupPort)
            {
                Seat(current.ActivePlayerId);current=h.View(current.ActivePlayerId);
                Require(current.NextSetupPort!=null&&current.LegalPortEdgeIds.Length>0,"New World setup port has no legal target");
                app.Pickup(CommandKind.SetupPort);Require(app.Drop(current.LegalPortEdgeIds[0]).Success,"UI setup port placement failed");continue;
            }
            if(current.Phase!=GamePhase.SetupSettlement&&current.Phase!=GamePhase.SetupRoad)return;
            Seat(current.ActivePlayerId);current=h.View(current.ActivePlayerId);
            var kind=current.Phase==GamePhase.SetupSettlement?CommandKind.SetupSettlement:CommandKind.SetupRoad;
            var candidates=kind==CommandKind.SetupSettlement?current.LegalVertexIds:current.LegalEdgeIds;
            if(kind==CommandKind.SetupSettlement)candidates=candidates.OrderByDescending(id=>current.Board.Tiles.Any(t=>t.Resource=="sea"&&t.Vertices.Contains(id))).ToArray();
            if(kind==CommandKind.SetupRoad&&current.LegalShipEdgeIds.Length>0){kind=CommandKind.SetupShip;candidates=current.LegalShipEdgeIds;}
            Require(candidates.Length>0,"Setup has no legal target");
            app.Pickup(kind);Require(app.Drop(candidates[0]).Success,"UI setup placement failed");
        }
        throw new InvalidOperationException("Setup did not finish");
    }
    void FinishPending()
    {
        var h=app.VerificationHost;
        for(int i=0;i<12;i++)
        {
            var v=h.View("P1");
            if(v.Phase==GamePhase.Action||v.Phase==GamePhase.ProductionAwaitRoll)return;
            if(v.Phase==GamePhase.Discard)
            {
                var requirement=v.Discards[0];Seat(requirement.PlayerId);var own=h.View(requirement.PlayerId);
                var bag=new ResourceBag();int remaining=requirement.Amount;
                foreach(Resource resource in Enum.GetValues(typeof(Resource))){bag[resource]=Math.Min(remaining,own.OwnResources[resource]);remaining-=bag[resource];}
                var command=Cmd(requirement.PlayerId,CommandKind.DiscardResources);command.Resources=bag;Execute(command);
            }
            else if(v.Phase==GamePhase.RobberMove)
            {
                Seat(v.ActivePlayerId);app.Pickup(CommandKind.MoveRobber);
                var legal=v.Board.Tiles.FirstOrDefault(t=>h.Preview(Cmd(v.ActivePlayerId,CommandKind.MoveRobber,t.Id)).Success);
                if(legal!=null)Require(app.Drop(legal.Id).Success,"UI robber move rejected");
                else
                {
                    app.Pickup(CommandKind.MovePirate);
                    var sea=v.Board.Tiles.First(t=>h.Preview(Cmd(v.ActivePlayerId,CommandKind.MovePirate,t.Id)).Success);
                    Require(app.Drop(sea.Id).Success,"UI pirate move rejected");
                }
            }
            else if(v.Phase==GamePhase.RobberSteal)
            {
                Seat(v.ActivePlayerId);var command=Cmd(v.ActivePlayerId,CommandKind.StealResource);command.OtherPlayerId=v.PendingDecision.EligibleVictimIds[0];Execute(command);
            }
            else if(v.Phase==GamePhase.GoldChoice)
            {
                var claim=v.GoldClaims[0];Seat(claim.PlayerId);var bag=new ResourceBag();int remaining=claim.Amount;
                foreach(Resource resource in Enum.GetValues(typeof(Resource))){bag[resource]=Math.Min(remaining,v.Bank[resource]);remaining-=bag[resource];}
                var command=Cmd(claim.PlayerId,CommandKind.ChooseGoldResources);command.Resources=bag;Execute(command);
            }
            else throw new InvalidOperationException("Unexpected pending phase "+v.Phase);
        }
        throw new InvalidOperationException("Pending sequence did not finish");
    }
    void CheckPicking()
    {
        var board=app.VerificationBoard;var v=app.VerificationHost.View(app.VerificationSeat);
        foreach(bool top in new[]{false,true})
        {
            board.SetCamera(top);
            foreach(float zoom in new[]{-.2f,.2f})
            {
                board.ChangeZoom(zoom);
                foreach(var vertex in v.Board.Vertices)Require(app.VerificationPick(CommandKind.BuildSettlement,board.ScreenPoint(vertex.Id))==vertex.Id,"Vertex picking ambiguous: "+vertex.Id);
                foreach(var edge in v.Board.Edges)Require(app.VerificationPick(CommandKind.BuildRoad,board.ScreenPoint(edge.Id))==edge.Id,"Edge picking ambiguous: "+edge.Id);
                foreach(var tile in v.Board.Tiles)Require(app.VerificationPick(CommandKind.MoveRobber,board.ScreenPoint(tile.Id))==tile.Id,"Tile picking ambiguous: "+tile.Id);
            }
        }
        board.ChangeZoom(-.065f);board.SetCamera(false);
    }
    void CheckTerrain()
    {
        var current=app.VerificationHost.View(app.VerificationSeat);
        foreach(var tile in current.Board.Tiles)Require(app.VerificationBoard.TerrainResource(tile.Id)==tile.Resource,"Rendered terrain differs from projected map: "+tile.Id);
    }
    void CheckScenarioCatalog(string directory)
    {
        var h=app.VerificationHost;
        foreach(var scenario in Catan.Core.M3.SeafarersScenarios.Ids)
        foreach(int players in new[]{3,4})
        {
            h.NewGame(scenario,players,20261009u);var v=h.View("P1");Seat(v.ActivePlayerId);CheckTerrain();
            Require(v.ScenarioId==scenario&&v.Players.Length==players,"Scenario selection created a different game");
            string path=Path.Combine(directory,"catalog-"+scenario+"-"+players+".json");
            h.WriteSave(path);h.ReadSave(path);h.WriteSave(path+".restored");
            Require(File.ReadAllText(path)==File.ReadAllText(path+".restored"),"Scenario save changed authority: "+scenario);
        }
        Debug.Log("CATAN_M3_SCENARIO_CATALOG_OK: 9 scenarios x 3/4 players; projected terrain and authority roundtrip");
        h.NewGame("new-world",3,20261009u);var initial=h.View("P1");Seat(initial.ActivePlayerId);initial=h.View(initial.ActivePlayerId);
        Require(initial.Phase==GamePhase.SetupPort&&initial.NextSetupPort!=null,"New World did not begin with a revealed port");
        Require(Vector2.Distance(app.VerificationBoard.FrameRobberScreenPoint(),app.VerificationBoard.ScreenPoint("frame"))>10,"Frame robber and pirate overlap");
        app.Pickup(CommandKind.SetupPort);Require(app.Drop(initial.LegalPortEdgeIds[0]).Success,"UI setup port rejected");
        Require(h.View("P1").Board.Ports.Length==1,"Setup port did not enter public board");
        Require(app.VerificationCurtain&&!app.VerificationHasPrivateView,"Setup port failed to transfer seat privately");
        string pendingPort=Path.Combine(directory,"new-world-port-pending.json");h.WriteSave(pendingPort);h.ReadSave(pendingPort);h.WriteSave(pendingPort+".restored");
        Require(File.ReadAllText(pendingPort)==File.ReadAllText(pendingPort+".restored"),"Pending port authority changed on restore");
        Debug.Log("CATAN_M3_SETUP_PORT_UI_OK: player-chosen coast, private seat transfer, pending restore, distinct frame tokens");
    }
    void VerifyRandomContinuation(string directory)
    {
        var h=app.VerificationHost;var before=h.View("P1");Seat(before.ActivePlayerId);
        string path=Path.Combine(directory,"before-roll.json");h.WriteSave(path);
        var comparison=new M3LocalGameHost();comparison.ReadSave(path);
        var command=Cmd(before.ActivePlayerId,CommandKind.RollDice);
        var expected=comparison.Submit(command);Require(expected.Success,"Restored comparison roll failed");
        Execute(command);
        var actual=h.View(before.ActivePlayerId);var restored=comparison.View(before.ActivePlayerId);
        Require(actual.LastDice1==restored.LastDice1&&actual.LastDice2==restored.LastDice2&&actual.Phase==restored.Phase,"Random continuation differed");
        foreach(Resource resource in Enum.GetValues(typeof(Resource)))Require(actual.Bank[resource]==restored.Bank[resource]&&actual.OwnResources[resource]==restored.OwnResources[resource],"Production continuation differed");
        h.WriteSave(path+".actual");comparison.WriteSave(path+".comparison");
        Require(File.ReadAllText(path+".actual")==File.ReadAllText(path+".comparison"),"Random continuation authority differed");
        FinishPending();
        var active=h.View("P1").ActivePlayerId;Seat(active);
        Execute(Cmd(active,CommandKind.EndTurn));Require(app.VerificationCurtain&&!app.VerificationHasPrivateView,"End turn failed to hide previous hand");
        Seat(h.View("P1").ActivePlayerId);
    }
    void VerifyShipMove()
    {
        var h=app.VerificationHost;
        for(int turn=0;turn<8;turn++)
        {
            var v=h.View("P1");Seat(v.ActivePlayerId);
            if(v.Phase==GamePhase.ProductionAwaitRoll){Execute(Cmd(v.ActivePlayerId,CommandKind.RollDice));FinishPending();}
            v=h.View(h.View("P1").ActivePlayerId);
            Seat(v.ActivePlayerId);
            foreach(var source in v.MovableShipEdgeIds)
            foreach(var edge in v.Board.Edges)
            {
                var command=Cmd(v.ActivePlayerId,CommandKind.MoveShip,edge.Id);command.SourceId=source;
                if(!h.Preview(command).Success)continue;
                int supply=v.Players.First(p=>p.Id==v.ActivePlayerId).ShipsRemaining;
                app.Pickup(CommandKind.MoveShip);app.Drop(source);
                Require(app.Drop(edge.Id).Success,"UI two-click ship move rejected");
                var moved=h.View(v.ActivePlayerId);
                Require(moved.Board.Ships.Any(s=>s.PlayerId==v.ActivePlayerId&&s.LocationId==edge.Id)&&!moved.Board.Ships.Any(s=>s.LocationId==source),"Ship destination differs");
                Require(moved.Players.First(p=>p.Id==v.ActivePlayerId).ShipsRemaining==supply&&moved.ShipMovedThisTurn,"Ship movement changed supply or lacked turn flag");
                Debug.Log("CATAN_M3_SHIP_UI_OK: coastal setup ships, two-click source/destination move, supply preserved");return;
            }
            Execute(Cmd(v.ActivePlayerId,CommandKind.EndTurn));
        }
        throw new InvalidOperationException("No legal setup ship move found in the representative UI fixture");
    }
    IEnumerator Run()
    {
        string[] args=Environment.GetCommandLineArgs();
        int save=Array.IndexOf(args,"--m3-verify-save"),load=Array.IndexOf(args,"--m3-verify-load"),capture=Array.IndexOf(args,"--m3-capture-ui");
        if(save<0&&load<0&&capture<0)yield break;
        yield return null;
        string directory=null;
        try
        {
            var h=app.VerificationHost;
            if(save>=0)
            {
                directory=args[save+1];Directory.CreateDirectory(directory);
                CheckScenarioCatalog(directory);
                h.NewGame(3,7u);Seat(h.View("P1").ActivePlayerId);CheckTerrain();FinishSetup();
                var three=h.View("P1");Require(three.Players.Length==3&&three.Board.Settlements.Length==6&&three.Board.Roads.Length+three.Board.Ships.Length==6,"Three-player normal setup failed");
                app.VerificationBoard.SetCamera(true);app.VerificationBoard.ChangeZoom(-.01f);float previousZoom=app.VerificationBoard.Zoom;
                h.NewGame(4,20261009u);var initial=h.View("P1");Seat(initial.ActivePlayerId);
                CheckTerrain();Require(app.VerificationBoard.TopView&&Mathf.Abs(app.VerificationBoard.Zoom-previousZoom)<.0001f,"Map recreation lost camera preferences");
                string before=Path.Combine(directory,"invalid-before.json"),after=Path.Combine(directory,"invalid-after.json");
                h.WriteSave(before);app.Pickup(CommandKind.SetupSettlement);Require(!app.Drop("V-not-a-target").Success,"UI accepted unknown vertex");h.WriteSave(after);
                Require(File.ReadAllText(before)==File.ReadAllText(after),"Rejected UI target changed authority");
                var v=h.View(initial.ActivePlayerId);app.Pickup(CommandKind.SetupSettlement);Require(app.Drop(v.LegalVertexIds[0]).Success,"First UI placement rejected");
                Require(h.View(initial.ActivePlayerId).Phase==GamePhase.SetupRoad,"Setup road not pending");
                string authority=Path.Combine(directory,"authority.json");h.WriteSave(authority);h.ReadSave(authority);h.WriteSave(authority+".restored");
                Require(File.ReadAllText(authority)==File.ReadAllText(authority+".restored"),"Pending setup save restore changed authority");
                app.Pickup(CommandKind.SetupRoad);app.CancelPickup();h.WriteSave(authority+".cancelled");
                Require(File.ReadAllText(authority)==File.ReadAllText(authority+".cancelled"),"Pickup cancellation changed authority");
                string damaged=Path.Combine(directory,"damaged.json");File.WriteAllText(damaged,"{not a save}");bool rejected=false;
                try{h.ReadSave(damaged);}catch(Exception){rejected=true;}
                Require(rejected,"Invalid save accepted");h.WriteSave(authority+".after-invalid-load");
                Require(File.ReadAllText(authority)==File.ReadAllText(authority+".after-invalid-load"),"Failed load replaced running session");
                FinishSetup();VerifyRandomContinuation(directory);VerifyShipMove();CheckPicking();
                Debug.Log("CATAN_M3_PLAYER_SAVE_OK: UI placements, rejection atomicity, pending save, bad-load retention, cancellation, private seat transfer, 3/4-player normal setup, terrain recreation and random continuation");
            }
            if(load>=0)
            {
                directory=args[load+1];string authority=Path.Combine(directory,"authority.json");h.ReadSave(authority);h.WriteSave(authority+".new-process");
                Require(File.ReadAllText(authority)==File.ReadAllText(authority+".new-process"),"Cross-process authority restore differs");
                var pending=h.View("P1");Require(pending.Phase==GamePhase.SetupRoad&&pending.PendingDecision!=null,"Cross-process pending decision missing");
                Seat(pending.ActivePlayerId);FinishSetup();VerifyRandomContinuation(directory);VerifyShipMove();CheckPicking();
                Debug.Log("CATAN_M3_PLAYER_LOAD_OK: new Windows process, byte-identical pending authority, normal setup and identical random continuation");
            }
            if(capture>=0)
            {
                directory=args[capture+1];Directory.CreateDirectory(directory);
                if(app.VerificationCurtain)app.ConfirmSeat();
            }
        }
        catch(Exception ex){Debug.LogException(ex);Application.Quit(1);yield break;}
        if(directory!=null)
        {
            for(int i=0;i<12;i++)yield return null;
            foreach(bool top in new[]{false,true})
            {
                app.VerificationBoard.SetCamera(top);yield return null;
                try{CaptureBoard(app.VerificationBoard.BoardCamera,Path.Combine(directory,top?"m3-top.png":"m3-default.png"));}
                catch(Exception ex){Debug.LogException(ex);Application.Quit(1);yield break;}
            }
            app.VerificationBoard.SetCamera(false);
            Debug.Log("CATAN_M3_RENDER_OK: URP camera captures only (no UI evidence); "+SystemInfo.graphicsDeviceName+"; "+Screen.width+"x"+Screen.height);
            if(capture>=0)
            {
                // A hidden window may not receive rendered screen pixels. Require
                // an actual image before claiming any IMGUI capture evidence.
                string uiPath=Path.Combine(directory,"m3-ui.png");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(uiPath);
                for(int i=0;i<12;i++)yield return null;
                try
                {
                    Require(File.Exists(uiPath),"Requested UI screenshot was not written");
                    var ui=new Texture2D(2,2);Require(ui.LoadImage(File.ReadAllBytes(uiPath)),"UI screenshot cannot be decoded");
                    int visible=0;foreach(var pixel in ui.GetPixels32())if(pixel.r>25||pixel.g>25||pixel.b>25)visible++;
                    Destroy(ui);Require(visible>20000,"Requested UI screenshot was black; use a visible player window");
                    Debug.Log("CATAN_M3_UI_CAPTURE_OK: full-window screenshot contains visible pixels; manual visual review still required");
                }
                catch(Exception ex){Debug.LogException(ex);Application.Quit(1);yield break;}
            }
        }
        Application.Quit(0);
    }
    static void CaptureBoard(Camera camera,string path)
    {
        var target=new RenderTexture(1440,1080,24,RenderTextureFormat.ARGB32);target.Create();
        var rect=camera.rect;camera.rect=new Rect(0,0,1,1);
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
        if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("URP capture unsupported");
        RenderPipeline.SubmitRenderRequest(camera,request);camera.rect=rect;
        var previous=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(1440,1080,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,1080),0,0);texture.Apply();
        int colored=0;foreach(var pixel in texture.GetPixels32())if(pixel.r>40&&pixel.g>30&&Math.Abs(pixel.r-pixel.g)>12)colored++;
        Require(colored>5000,"URP capture did not contain colored board");
        File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=previous;Destroy(texture);target.Release();Destroy(target);
    }
}


