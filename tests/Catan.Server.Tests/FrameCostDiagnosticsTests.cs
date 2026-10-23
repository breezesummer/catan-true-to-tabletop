using System.Threading;
using Xunit;

namespace Catan.Server.Tests;

[CollectionDefinition("帧计时全局状态",DisableParallelization=true)]
public sealed class FrameCostDiagnosticsCollection { }

[Collection("帧计时全局状态")]
public sealed class FrameCostDiagnosticsTests : IDisposable
{
    public FrameCostDiagnosticsTests()
    {
        FrameCost.Enabled=FrameCost.DiagnosticsEnabled=false;
        FrameCost.MainThread=FrameCost.DiagnosticsMainThread=Thread.CurrentThread.ManagedThreadId;
        Array.Clear(FrameCost.Ticks);
        FrameCost.ReadDiagnostics(new long[FrameCost.Ticks.Length]);
    }

    [Fact]
    public void RoutineDiagnosticsAndExplicitProfileHaveIndependentCounters()
    {
        FrameCost.Enabled=FrameCost.DiagnosticsEnabled=true;
        using(FrameCost.Measure(FrameCost.Stage.BoardRefresh))Thread.Sleep(1);
        long profileTicks=FrameCost.Ticks[(int)FrameCost.Stage.BoardRefresh];
        var routine=new long[FrameCost.Ticks.Length];FrameCost.ReadDiagnostics(routine);
        Assert.True(profileTicks>0);
        Assert.Equal(profileTicks,routine[(int)FrameCost.Stage.BoardRefresh]);
        Assert.Equal(profileTicks,FrameCost.Ticks[(int)FrameCost.Stage.BoardRefresh]);
        FrameCost.ReadDiagnostics(routine);
        Assert.All(routine,value=>Assert.Equal(0,value));
        Assert.Equal(profileTicks,Interlocked.Exchange(ref FrameCost.Ticks[(int)FrameCost.Stage.BoardRefresh],0));
    }

    [Fact]
    public void RoutineDiagnosticsDoNotEnableOrAccumulateExplicitFrameCapture()
    {
        FrameCost.DiagnosticsEnabled=true;
        using(FrameCost.Measure(FrameCost.Stage.PresentationApply))Thread.Sleep(1);
        var routine=new long[FrameCost.Ticks.Length];FrameCost.ReadDiagnostics(routine);
        Assert.True(routine[(int)FrameCost.Stage.PresentationApply]>0);
        Assert.All(FrameCost.Ticks,value=>Assert.Equal(0,value));
        Assert.False(FrameCost.Enabled);
    }

    [Fact]
    public void BackgroundDecodeIsSeparatedFromMainThreadBudget()
    {
        FrameCost.DiagnosticsEnabled=true;
        var worker=new Thread(()=>
        {
            using(FrameCost.Measure(FrameCost.Stage.ResponseDecode))Thread.Sleep(1);
            using(FrameCost.Measure(FrameCost.Stage.BoardRefresh))Thread.Sleep(1);
        });
        worker.Start();worker.Join();
        var routine=new long[FrameCost.Ticks.Length];FrameCost.ReadDiagnostics(routine);
        Assert.True(routine[(int)FrameCost.Stage.ResponseDecodeWorker]>0);
        Assert.Equal(0,routine[(int)FrameCost.Stage.ResponseDecode]);
        Assert.Equal(0,routine[(int)FrameCost.Stage.BoardRefresh]);
    }

    public void Dispose()
    {
        FrameCost.Enabled=FrameCost.DiagnosticsEnabled=false;
        Array.Clear(FrameCost.Ticks);FrameCost.ReadDiagnostics(new long[FrameCost.Ticks.Length]);
    }
}
