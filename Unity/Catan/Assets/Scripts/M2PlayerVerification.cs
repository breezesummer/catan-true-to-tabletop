using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Command = Catan.Core.M2.Command;
using CommandKind = Catan.Core.M2.CommandKind;
using GamePhase = Catan.Core.M2.GamePhase;
using PlayerView = Catan.Core.M2.PlayerView;

// Explicit opt-in acceptance driver inside the real Windows Mono player.
// Local authority files never enter logs, screenshots or public player views.
public sealed class M2PlayerVerification : MonoBehaviour
{
    private M2App app;
    public void Initialize(M2App value){app=value;StartCoroutine(Run());}
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
        for(int count=0;count<20;count++)
        {
            var current=h.View("P1");
            if(current.Phase!=GamePhase.SetupSettlement&&current.Phase!=GamePhase.SetupRoad)return;
            Seat(current.ActivePlayerId);current=h.View(current.ActivePlayerId);
            var kind=current.Phase==GamePhase.SetupSettlement?CommandKind.SetupSettlement:CommandKind.SetupRoad;
            var candidates=kind==CommandKind.SetupSettlement?current.LegalVertexIds:current.LegalEdgeIds;
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
                Require(app.Drop(v.Board.Tiles.First(t=>t.Id!=v.Board.RobberTileId).Id).Success,"UI robber move rejected");
            }
            else if(v.Phase==GamePhase.RobberSteal)
            {
                Seat(v.ActivePlayerId);var command=Cmd(v.ActivePlayerId,CommandKind.StealResource);command.OtherPlayerId=v.PendingDecision.EligibleVictimIds[0];Execute(command);
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
    void VerifyRandomContinuation(string directory)
    {
        var h=app.VerificationHost;var before=h.View("P1");Seat(before.ActivePlayerId);
        string path=Path.Combine(directory,"before-roll.json");h.WriteSave(path);
        var comparison=new M2LocalGameHost();comparison.ReadSave(path);
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
    IEnumerator Run()
    {
        string[] args=Environment.GetCommandLineArgs();
        int save=Array.IndexOf(args,"--m2-verify-save"),load=Array.IndexOf(args,"--m2-verify-load"),capture=Array.IndexOf(args,"--m2-capture-ui");
        if(save<0&&load<0&&capture<0)yield break;
        yield return null;
        string directory=null;
        try
        {
            var h=app.VerificationHost;
            if(save>=0)
            {
                directory=args[save+1];Directory.CreateDirectory(directory);
                h.NewGame(3,7u);Seat(h.View("P1").ActivePlayerId);CheckTerrain();FinishSetup();
                var three=h.View("P1");Require(three.Players.Length==3&&three.Board.Settlements.Length==6&&three.Board.Roads.Length==6,"Three-player normal setup failed");
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
                FinishSetup();VerifyRandomContinuation(directory);CheckPicking();
                Debug.Log("CATAN_M2_PLAYER_SAVE_OK: UI placements, rejection atomicity, pending save, bad-load retention, cancellation, private seat transfer, 3/4-player normal setup, terrain recreation and random continuation");
            }
            if(load>=0)
            {
                directory=args[load+1];string authority=Path.Combine(directory,"authority.json");h.ReadSave(authority);h.WriteSave(authority+".new-process");
                Require(File.ReadAllText(authority)==File.ReadAllText(authority+".new-process"),"Cross-process authority restore differs");
                var pending=h.View("P1");Require(pending.Phase==GamePhase.SetupRoad&&pending.PendingDecision!=null,"Cross-process pending decision missing");
                Seat(pending.ActivePlayerId);FinishSetup();VerifyRandomContinuation(directory);CheckPicking();
                Debug.Log("CATAN_M2_PLAYER_LOAD_OK: new Windows process, byte-identical pending authority, normal setup and identical random continuation");
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
                try{CaptureBoard(app.VerificationBoard.BoardCamera,Path.Combine(directory,top?"m2-top.png":"m2-default.png"));}
                catch(Exception ex){Debug.LogException(ex);Application.Quit(1);yield break;}
            }
            app.VerificationBoard.SetCamera(false);
            Debug.Log("CATAN_M2_RENDER_OK: URP camera captures only (no UI evidence); "+SystemInfo.graphicsDeviceName+"; "+Screen.width+"x"+Screen.height);
            if(capture>=0)
            {
                // Screen capture requires a visible, rendered window. Hidden-window
                // verification intentionally makes no claim about IMGUI pixels.
                string uiPath=Path.Combine(directory,"m2-ui.png");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(uiPath);
                for(int i=0;i<12;i++)yield return null;
                try
                {
                    Require(File.Exists(uiPath),"Requested UI screenshot was not written");
                    var ui=new Texture2D(2,2);Require(ui.LoadImage(File.ReadAllBytes(uiPath)),"UI screenshot cannot be decoded");
                    int visible=0;foreach(var pixel in ui.GetPixels32())if(pixel.r>25||pixel.g>25||pixel.b>25)visible++;
                    Destroy(ui);Require(visible>20000,"Requested UI screenshot was black; use a visible player window");
                    Debug.Log("CATAN_M2_UI_CAPTURE_OK: full-window screenshot contains visible pixels; manual visual review still required");
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
