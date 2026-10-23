using System;
using System.Diagnostics;
using System.Threading;

// 仅采集数值；不记录网络正文、手牌或凭据。嵌套阶段耗时为包含子阶段的时间。
public static class FrameCost
{
    public enum Stage { NetworkUpdate, NetworkApply, ResponseDecode, ViewDecode, HistoryDecode, BoardRefresh, GameUpdate, BuildAnimation, Gui, HarnessDecision, ResponseDecodeWorker, PresentationApply }
    public static bool Enabled;
    public static int MainThread;
    public static readonly long[] Ticks=new long[12];
    // 常驻联机诊断与显式逐帧采样使用不同计数器，任何一方读取都不会清掉另一方数据。
    public static bool DiagnosticsEnabled;
    public static int DiagnosticsMainThread;
    static readonly long[] diagnosticsTicks=new long[12];
    public static void ReadDiagnostics(long[] destination)
    {
        if(destination==null||destination.Length!=diagnosticsTicks.Length)throw new ArgumentException("诊断计数器长度不正确。",nameof(destination));
        for(int i=0;i<destination.Length;i++)destination[i]=Interlocked.Exchange(ref diagnosticsTicks[i],0);
    }
    public static Scope Measure(Stage stage)=>new Scope(stage);
    public struct Scope:IDisposable
    {
        readonly int index,diagnosticsIndex;
        readonly long start;
        readonly bool profile,diagnostics;
        public Scope(Stage stage)
        {
            index=diagnosticsIndex=(int)stage;start=0;
            profile=Enabled;diagnostics=DiagnosticsEnabled;
            if(!profile&&!diagnostics)return;
            int thread=Thread.CurrentThread.ManagedThreadId;
            if(profile&&thread!=MainThread)
            {
                if(stage==Stage.ResponseDecode)index=(int)Stage.ResponseDecodeWorker;else profile=false;
            }
            if(diagnostics&&thread!=DiagnosticsMainThread)
            {
                if(stage==Stage.ResponseDecode)diagnosticsIndex=(int)Stage.ResponseDecodeWorker;else diagnostics=false;
            }
            if(profile||diagnostics)start=Stopwatch.GetTimestamp();
        }
        public void Dispose()
        {
            if(start==0)return;
            long elapsed=Stopwatch.GetTimestamp()-start;
            if(profile)Interlocked.Add(ref Ticks[index],elapsed);
            if(diagnostics)Interlocked.Add(ref diagnosticsTicks[diagnosticsIndex],elapsed);
        }
    }
    public static T Decode<T>(string json,Stage stage)
    {
        using(Measure(stage))return Catan.Network.Contracts.WireJson.Deserialize<T>(json);
    }
}
