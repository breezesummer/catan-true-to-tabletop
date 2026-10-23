using System;
using System.Diagnostics;
using System.Threading;

public sealed partial class M6App
{
    readonly long[] networkDiagnosticFrameTicks=new long[FrameCost.Ticks.Length];
    readonly long[] networkDiagnosticWindowTicks=new long[FrameCost.Ticks.Length];
    long networkDiagnosticPrevious,networkDiagnosticWindowStart,networkDiagnosticLastSpike;
    long networkDiagnosticFrames,networkDiagnosticSlowFrames;
    double networkDiagnosticMaximumFrame;
    int networkDiagnosticGcStart;
    bool networkDiagnosticsActive,networkApplicationFocused=true,networkApplicationPaused;
    string networkInputWait="";
    long networkInputWaitStarted,networkInputWaitReported;

    void InitializeNetworkDiagnostics()
    {
        FrameCost.DiagnosticsMainThread=Thread.CurrentThread.ManagedThreadId;
    }

    // 只累加数字；每五秒提交一次异步日志。帧间隔包含 Unity 主循环、渲染和系统调度，
    // 阶段耗时是窗口内的累计值（嵌套阶段不可相加），不能据此把整段帧间隔归因给网络。
    void TickNetworkDiagnostics()
    {
        long now=Stopwatch.GetTimestamp();
        if(Network==null||!Network.EndpointConfigured)
        {
            if(networkDiagnosticsActive)StopNetworkDiagnostics();
            return;
        }
        if(!networkDiagnosticsActive)
        {
            FrameCost.ReadDiagnostics(networkDiagnosticFrameTicks);
            networkDiagnosticsActive=true;FrameCost.DiagnosticsEnabled=true;
            networkDiagnosticPrevious=networkDiagnosticWindowStart=now;
            networkDiagnosticGcStart=GC.CollectionCount(0);
            return;
        }
        FrameCost.ReadDiagnostics(networkDiagnosticFrameTicks);
        for(int i=0;i<networkDiagnosticFrameTicks.Length;i++)networkDiagnosticWindowTicks[i]+=networkDiagnosticFrameTicks[i];
        double interval=DiagnosticMilliseconds(now-networkDiagnosticPrevious);
        networkDiagnosticPrevious=now;networkDiagnosticFrames++;
        networkDiagnosticMaximumFrame=Math.Max(networkDiagnosticMaximumFrame,interval);
        if(interval>=50)networkDiagnosticSlowFrames++;
        if(interval>=100&&DiagnosticMilliseconds(now-networkDiagnosticLastSpike)>=5000)
        {
            var value=NetworkPresentationEvent("frame_spike");
            value.FrameMs=interval;value.Code=NetworkInputWaitReason();value.Count=buildFlights.Count;
            M7CommunicationLog.Record(value);networkDiagnosticLastSpike=now;
        }
        RecordNetworkInputWait(now,NetworkInputWaitReason());
        if(DiagnosticMilliseconds(now-networkDiagnosticWindowStart)>=5000)FlushNetworkFrameWindow(now);
    }

    static double DiagnosticMilliseconds(long ticks)=>ticks*1000d/Stopwatch.Frequency;
    double DiagnosticStage(FrameCost.Stage stage)=>DiagnosticMilliseconds(networkDiagnosticWindowTicks[(int)stage]);
    M7CommunicationEvent NetworkPresentationEvent(string name)=>new M7CommunicationEvent
    {
        Component="presentation",Event=name,RoomId=Network?.RoomId,Revision=Network?.Revision??-1,
        Transport=Network?.TransportMode,
        Status=networkApplicationPaused?"paused":!networkApplicationFocused?"background":NetworkGame?"playing":"lobby"
    };

    void FlushNetworkFrameWindow(long now)
    {
        if(networkDiagnosticFrames>0)
        {
            var value=NetworkPresentationEvent("frame_window");
            value.DurationMs=DiagnosticMilliseconds(now-networkDiagnosticWindowStart);
            value.FrameMs=networkDiagnosticMaximumFrame;value.Count=networkDiagnosticFrames;
            value.SlowFrames=networkDiagnosticSlowFrames;value.GcCollections=GC.CollectionCount(0)-networkDiagnosticGcStart;
            value.NetworkApplyMs=DiagnosticStage(FrameCost.Stage.NetworkApply);
            value.PresentationApplyMs=DiagnosticStage(FrameCost.Stage.PresentationApply);
            value.BoardRefreshMs=DiagnosticStage(FrameCost.Stage.BoardRefresh);
            value.ViewDecodeMs=DiagnosticStage(FrameCost.Stage.ViewDecode);
            value.ResponseDecodeMs=DiagnosticStage(FrameCost.Stage.ResponseDecode);
            value.HistoryDecodeMs=DiagnosticStage(FrameCost.Stage.HistoryDecode);
            value.WorkerDecodeMs=DiagnosticStage(FrameCost.Stage.ResponseDecodeWorker);
            value.GuiMs=DiagnosticStage(FrameCost.Stage.Gui);
            value.GameUpdateMs=DiagnosticStage(FrameCost.Stage.GameUpdate);
            value.BuildAnimationMs=DiagnosticStage(FrameCost.Stage.BuildAnimation);
            M7CommunicationLog.Record(value);
        }
        Array.Clear(networkDiagnosticWindowTicks,0,networkDiagnosticWindowTicks.Length);
        networkDiagnosticFrames=networkDiagnosticSlowFrames=0;networkDiagnosticMaximumFrame=0;
        networkDiagnosticWindowStart=now;networkDiagnosticGcStart=GC.CollectionCount(0);
    }

    string NetworkInputWaitReason()
    {
        if(!NetworkGame||MenuVisible||tableMenu)return "";
        if(!Network.Connected)return "disconnected";
        if(Network.CommandPending)return "command_pending";
        if(Network.Busy)return "request_pending";
        if(pendingNetworkPresentation!=null)return "presentation_history";
        if(CardPresentationBusy)return "card_animation";
        if(TableDiceRolling)return "dice_animation";
        for(int i=0;i<buildFlights.Count;i++)if(buildFlights[i].Piece.Previous!=null)return "piece_animation";
        if(!Network.HumanControlAvailable)return "ai_control";
        return "";
    }

    void RecordNetworkInputWait(long now,string reason)
    {
        if(reason!=networkInputWait)
        {
            if(networkInputWait.Length>0)
            {
                var value=NetworkPresentationEvent("input_wait_end");value.Code=networkInputWait;
                value.DurationMs=DiagnosticMilliseconds(now-networkInputWaitStarted);M7CommunicationLog.Record(value);
            }
            networkInputWait=reason;networkInputWaitStarted=networkInputWaitReported=now;
            if(reason.Length>0)
            {
                var value=NetworkPresentationEvent("input_wait_begin");value.Code=reason;M7CommunicationLog.Record(value);
            }
        }
        else if(reason.Length>0&&DiagnosticMilliseconds(now-networkInputWaitReported)>=10000)
        {
            var value=NetworkPresentationEvent("input_wait_long");value.Code=reason;
            value.DurationMs=DiagnosticMilliseconds(now-networkInputWaitStarted);
            value.QueueDepth=reason=="card_animation"?playedCards.Count:0;
            M7CommunicationLog.Record(value);networkInputWaitReported=now;
        }
    }

    void NetworkApplicationFocus(bool focused)
    {
        networkApplicationFocused=focused;
        if(Network==null||!Network.EndpointConfigured)return;
        var value=NetworkPresentationEvent("application_focus");value.Code=focused?"focused":"unfocused";M7CommunicationLog.Record(value);
    }
    void NetworkApplicationPause(bool paused)
    {
        networkApplicationPaused=paused;
        if(Network==null||!Network.EndpointConfigured)return;
        var value=NetworkPresentationEvent("application_pause");value.Code=paused?"paused":"resumed";M7CommunicationLog.Record(value);
    }
    void StopNetworkDiagnostics()
    {
        if(!networkDiagnosticsActive)return;
        long now=Stopwatch.GetTimestamp();
        RecordNetworkInputWait(now,"");FlushNetworkFrameWindow(now);
        networkDiagnosticsActive=false;FrameCost.DiagnosticsEnabled=false;
    }
}
