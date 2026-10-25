using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Catan.Core;
using Catan.Network.Contracts;
using Stopwatch=System.Diagnostics.Stopwatch;

// 后台产物只包含脱离 Unity 的数据；弱引用表随响应释放，不保留旧连接或历史正文。
internal sealed class M7PreparedResponse
{
    internal readonly object View;
    internal readonly GameHistoryEntry[] Presentation,History;
    internal readonly bool ReliableEvents;
    internal M7PreparedResponse(object view,GameHistoryEntry[] presentation,GameHistoryEntry[] history,bool reliableEvents=false){View=view;Presentation=presentation;History=history;ReliableEvents=reliableEvents;}
}
internal sealed class M7ResponseDecoder
{
    readonly SemaphoreSlim workers=new SemaphoreSlim(2,2);
    readonly object gate=new object();
    readonly ConditionalWeakTable<NetworkResponse,M7PreparedResponse> prepared=new ConditionalWeakTable<NetworkResponse,M7PreparedResponse>();
    int pending;
    long bytes;
    internal bool Legacy;
    internal bool TryGet(NetworkResponse response,out M7PreparedResponse value)=>prepared.TryGetValue(response,out value);
    internal async Task<NetworkResponse> Decode(byte[] payload,string rules,string operation,string requestId,CancellationToken cancel,SeatStateReceiver state=null)
    {
        long queued=Stopwatch.GetTimestamp();int depth;
        lock(gate)
        {
            if(pending>=8||bytes+payload.Length>8*1024*1024)throw new M7NetworkFailure("DECODE_OVERLOAD","同步处理暂时繁忙，正在恢复连接。");
            depth=++pending;bytes+=payload.Length;
        }
        bool entered=false;
        try
        {
            await workers.WaitAsync(cancel).ConfigureAwait(false);entered=true;
            return await Task.Run(()=>
            {
                cancel.ThrowIfCancellationRequested();
                long started=Stopwatch.GetTimestamp(),allocated=Allocated();
                var response=Read<NetworkResponse>(Encoding.UTF8.GetString(payload),FrameCost.Stage.ResponseDecode);
                if(response==null||response.ProtocolVersion!=NetworkProtocol.Version)throw new M7NetworkFailure("PROTOCOL","联机协议不兼容。");
                SeatStateApplication sync=null;
                if(response.Code==NetworkProtocol.SeatStateCode)
                {
                    if(state==null)throw new M7NetworkFailure("STATE_GAP","同步基准需要恢复。");
                    sync=state.Prepare(response);response=sync.Response;
                }
                string rule=response.RuleSet??response.Room?.RuleSet??rules;
                object view=null;
                using(FrameCost.Measure(FrameCost.Stage.ViewDecode))
                if(response.ViewJson!=null)
                {
                    Type type;
                    switch(rule)
                    {
                        case "base":type=typeof(Catan.Core.M2.PlayerView);break;
                        case "seafarers":type=typeof(Catan.Core.M3.PlayerView);break;
                        case "cities":case "cities-knights":type=typeof(Catan.Core.M4.PlayerView);break;
                        case "combined":type=typeof(Catan.Core.M5.PlayerView);break;
                        default:throw new M7NetworkFailure("RESPONSE_FORMAT","可见局面的规则包无效。");
                    }
                    view=Legacy?WireJson.DeserializeLegacy(response.ViewJson,type):WireJson.Deserialize(response.ViewJson,type);
                    if(view==null)throw new M7NetworkFailure("RESPONSE_FORMAT","服务返回空局面。");
                }
                var presentation=response.PresentationHistoryJson==null?Array.Empty<GameHistoryEntry>():Read<GameHistoryEntry[]>(response.PresentationHistoryJson,FrameCost.Stage.HistoryDecode);
                var history=response.HistoryJson==null?Array.Empty<GameHistoryEntry>():Read<GameHistoryEntry[]>(response.HistoryJson,FrameCost.Stage.HistoryDecode);
                cancel.ThrowIfCancellationRequested();
                sync?.Commit();
                prepared.Add(response,new M7PreparedResponse(view,presentation??Array.Empty<GameHistoryEntry>(),history??Array.Empty<GameHistoryEntry>(),sync!=null||response.Success&&response.Code==NetworkProtocol.FullEventsCode&&response.PresentationHistoryAfterSequence==0));
                M7CommunicationLog.Record(new M7CommunicationEvent{Event="response_decode",Operation=operation,RequestId=response.RequestId??requestId,RoomId=response.RoomId,Revision=response.Revision,Bytes=payload.Length,QueueDepth=depth,QueueWaitMs=Ms(queued,started),DurationMs=Ms(started,Stopwatch.GetTimestamp()),AllocatedBytes=Math.Max(0,Allocated()-allocated),WorkerThreads=ActiveWorkers(),MonoTicks=started});
                return response;
            },cancel).ConfigureAwait(false);
        }
        finally
        {
            if(entered)workers.Release();
            lock(gate){pending--;bytes-=payload.Length;}
        }
    }
    T Read<T>(string json,FrameCost.Stage stage){using(FrameCost.Measure(stage))return Legacy?WireJson.DeserializeLegacy<T>(json):WireJson.Deserialize<T>(json);}
    static long Allocated(){try{return GC.GetAllocatedBytesForCurrentThread();}catch(NotSupportedException){return 0;}}
    static int ActiveWorkers(){try{ThreadPool.GetMaxThreads(out int maximum,out _);ThreadPool.GetAvailableThreads(out int available,out _);return maximum-available;}catch(NotSupportedException){return 0;}}
    static double Ms(long start,long end)=>(end-start)*1000d/Stopwatch.Frequency;
}
