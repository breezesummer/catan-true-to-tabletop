using System;
using System.Diagnostics;
using System.Threading;

// 仅采集数值；不记录网络正文、手牌或凭据。嵌套阶段耗时为包含子阶段的时间。
public static class FrameCost
{
    public enum Stage { NetworkUpdate, NetworkApply, ResponseDecode, ViewDecode, HistoryDecode, BoardRefresh, GameUpdate, BuildAnimation, Gui, HarnessDecision, ResponseDecodeWorker }
    public static bool Enabled;
    public static int MainThread;
    public static readonly long[] Ticks=new long[11];
    public static Scope Measure(Stage stage)=>new Scope(stage);
    public struct Scope:IDisposable
    {
        readonly int index;
        readonly long start;
        public Scope(Stage stage)
        {
            index=(int)stage;start=0;
            if(!Enabled)return;
            if(Thread.CurrentThread.ManagedThreadId!=MainThread)
            {
                if(stage!=Stage.ResponseDecode)return;
                index=(int)Stage.ResponseDecodeWorker;
            }
            start=Stopwatch.GetTimestamp();
        }
        public void Dispose(){if(start!=0)Interlocked.Add(ref Ticks[index],Stopwatch.GetTimestamp()-start);}
    }
    public static T Decode<T>(string json,Stage stage)
    {
        using(Measure(stage))return Catan.Network.Contracts.WireJson.Deserialize<T>(json);
    }
}
