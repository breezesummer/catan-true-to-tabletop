using System;
using System.Collections;
using System.IO;
using System.Linq;
using Catan.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Opt-in acceptance driver executed in the actual Windows Mono player.
// Authority files stay local; verification logs contain only public assertions.
public sealed class M1PlayerVerification : MonoBehaviour
{
    private CatanApp app;
    public void Initialize(CatanApp value) {app=value;StartCoroutine(Run());}
    static void Require(bool ok,string message) {if(!ok)throw new InvalidOperationException(message);}
    static Command Cmd(string id,string player,CommandKind kind,string target=null) => new Command {Id=id,PlayerId=player,Kind=kind,TargetId=target};
    static void Execute(LocalGameHost h,Command c) {var r=h.Submit(c);Require(r.Success,"Command rejected: "+c.Id+" / "+r.ErrorCode);}
    IEnumerator Run()
    {
        string[] args=Environment.GetCommandLineArgs();
        int save=Array.IndexOf(args,"--m1-verify-save"),load=Array.IndexOf(args,"--m1-verify-load"),capture=Array.IndexOf(args,"--m1-capture");
        if(save<0&&load<0&&capture<0)yield break;
        yield return null;
        try
        {
            var h=app.VerificationHost;
            if(save>=0)
            {
                string path=args[save+1];
                app.ConfirmSeat();app.Pickup(false);
                Require(app.Drop("V42").Success,"UI could not commit a legal settlement");
                Require(h.View("P1").PendingDecision.AnchorVertexId=="V42","UI placement did not enter pending road");
                app.Pickup(true);Require(app.Drop("E60").Success,"UI could not commit a legal setup road");
                Require(app.VerificationCurtain&&!app.VerificationHasPrivateView,"UI turn change exposed prior hand");
                h.NewGame();app.SwitchSeat("P1");app.ConfirmSeat();
                string[] seats={"P1","P2","P3","P4","P4","P3","P2","P1"};
                string[] vertices={"V42","V15","V29","V40","V32","V18","V49","V09"};
                string[] edges={"E60","E17","E41","E56","E48","E27","E69","E08"};
                for(int i=0;i<8;i++)
                {
                    Execute(h,Cmd("C"+(i*2+1).ToString("00"),seats[i],CommandKind.SetupSettlement,vertices[i]));
                    if(i==3)
                    {
                        app.SwitchSeat("P4");app.ConfirmSeat();app.Pickup(true);
                        h.WriteSave(path+".before-invalid");
                        Require(!app.Drop("E01").Success,"UI accepted a disconnected setup road");
                        h.WriteSave(path+".after-invalid");
                        Require(File.ReadAllText(path+".before-invalid")==File.ReadAllText(path+".after-invalid"),"Invalid UI road changed authority");
                        h.WriteSave(path+".pending");h.ReadSave(path+".pending");h.WriteSave(path+".pending-restored");
                        Require(File.ReadAllText(path+".pending")==File.ReadAllText(path+".pending-restored"),"Pending save changed");
                        Require(h.View("P4").PendingDecision.AnchorVertexId=="V40","Pending anchor missing");
                    }
                    Execute(h,Cmd("C"+(i*2+2).ToString("00"),seats[i],CommandKind.SetupRoad,edges[i]));
                    if(i==0)
                    {
                        app.SwitchSeat("P2");app.ConfirmSeat();app.Pickup(false);
                        h.WriteSave(path+".before-distance");
                        Require(!app.Drop("V46").Success,"UI accepted an adjacent settlement");
                        h.WriteSave(path+".after-distance");
                        Require(File.ReadAllText(path+".before-distance")==File.ReadAllText(path+".after-distance"),"Invalid UI settlement changed authority");
                    }
                }
                var roll=Cmd("C17","P1",CommandKind.RollDice);Execute(h,roll);
                Require(h.View("P1").OwnResources.Wood==5,"Production ledger mismatch");
                h.WriteSave(path+".roll");h.ReadSave(path+".roll");
                var repeated=h.Submit(roll);Require(repeated.Success&&repeated.IsDuplicate&&repeated.NewEvents.Length==0,"Roll retry duplicated effects");
                h.WriteSave(path+".roll-restored");Require(File.ReadAllText(path+".roll")==File.ReadAllText(path+".roll-restored"),"Retry changed authority");
                var trade=Cmd("C18","P1",CommandKind.BankTrade);trade.GiveResource=Resource.Wood;trade.ReceiveResource=Resource.Brick;Execute(h,trade);
                Execute(h,Cmd("C19","P1",CommandKind.BuildRoad,"E65"));
                Execute(h,Cmd("C20","P1",CommandKind.EndTurn));
                Require(h.View("P2").Board.Roads.Length==9&&h.View("P2").Bank.Wood==18,"Final ledger mismatch");
                h.WriteSave(path);
                app.SwitchSeat("P2");Require(app.VerificationCurtain&&!app.VerificationHasPrivateView,"Old seat view retained");app.ConfirmSeat();
                app.Pickup(true);app.CancelPickup();h.WriteSave(path+".cancel");
                Require(File.ReadAllText(path)==File.ReadAllText(path+".cancel"),"Pickup/cancel changed rules");
                app.VerificationBoard.Refresh(h.View("P2"));
                var board=app.VerificationBoard;
                foreach(bool top in new[]{false,true})
                {
                    board.SetCamera(top);
                    foreach(float delta in new[]{-.2f,.2f})
                    {
                        board.ChangeZoom(delta);
                        foreach(var vertex in h.View("P2").Board.Vertices)
                            Require(app.VerificationPick(false,board.ScreenPoint(vertex.Id))==vertex.Id,"Vertex picking ambiguous at "+vertex.Id);
                        foreach(var edge in h.View("P2").Board.Edges)
                            Require(app.VerificationPick(true,board.ScreenPoint(edge.Id))==edge.Id,"Edge picking ambiguous at "+edge.Id);
                    }
                }
                board.ChangeZoom(-.065f);board.SetCamera(false);
                Debug.Log("CATAN_M1_PLAYER_SAVE_OK: setup, ledgers, pending restore, retry, privacy curtain, cancellation");
            }
            if(load>=0)
            {
                string path=args[load+1];h.ReadSave(path);h.WriteSave(path+".restored");
                Require(File.ReadAllText(path)==File.ReadAllText(path+".restored"),"Cross-process restore differs");
                Execute(h,Cmd("C21","P2",CommandKind.RollDice));
                var v=h.View("P2");Require(v.OwnResources.Wheat==2&&v.Bank.Wheat==17&&v.Phase==GamePhase.Action,"Random continuation differs");
                var e=v.Events.Last(x=>x.Kind=="RollDice");Require(e.Dice1==1&&e.Dice2==1,"Continuation dice differs");
                app.VerificationBoard.Refresh(v);app.SwitchSeat("P2");app.ConfirmSeat();
                Debug.Log("CATAN_M1_PLAYER_RESTORE_OK: new process, canonical equality, continuation ledger");
            }
        }
        catch(Exception ex) {Debug.LogException(ex);Application.Quit(1);yield break;}
        if(capture>=0)
        {
            for(int i=0;i<12;i++)yield return null;
            string directory=args[capture+1];Directory.CreateDirectory(directory);
            foreach(bool top in new[]{false,true})
            {
                app.VerificationBoard.SetCamera(top);yield return null;
                try {Capture(app.VerificationBoard.BoardCamera,Path.Combine(directory,top?"m1-top.png":"m1-default.png"));}
                catch(Exception ex) {Debug.LogException(ex);Application.Quit(1);yield break;}
            }
            app.VerificationBoard.SetCamera(false);
            Debug.Log("CATAN_M1_RENDER_OK: URP camera captures only; "+SystemInfo.graphicsDeviceName+"; "+Screen.width+"x"+Screen.height);
        }
        Application.Quit(0);
    }
    static void Capture(Camera camera,string path)
    {
        var target=new RenderTexture(1440,1080,24,RenderTextureFormat.ARGB32);target.Create();
        var rect=camera.rect;camera.rect=new Rect(0,0,1,1);
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
        if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new InvalidOperationException("URP capture unsupported");
        RenderPipeline.SubmitRenderRequest(camera,request);camera.rect=rect;
        var prev=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(1440,1080,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,1080),0,0);texture.Apply();
        int colored=0;foreach(var p in texture.GetPixels32())if(p.r>40&&p.g>30&&Math.Abs(p.r-p.g)>12)colored++;
        if(colored<5000)throw new InvalidOperationException("URP capture did not contain the colored board");
        File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=prev;Destroy(texture);target.Release();Destroy(target);
    }
}
