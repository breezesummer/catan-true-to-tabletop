using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

[DefaultExecutionOrder(-32000)]
public sealed class TabletopFrameProbe:MonoBehaviour
{
    readonly StringBuilder rows=new StringBuilder(1024*1024);
    string output;
    long previous;
    int frames,gc;
    M6App app;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--frame-profile");
        if(i<0||i+1>=args.Length)return;
        var probe=new GameObject("联机帧耗时采样").AddComponent<TabletopFrameProbe>();
        probe.output=Path.GetFullPath(args[i+1]);DontDestroyOnLoad(probe.gameObject);
        FrameCost.MainThread=Thread.CurrentThread.ManagedThreadId;FrameCost.Enabled=true;
        probe.rows.Append("frame,interval_ms,playing,build_flights,card_busy,dice_busy,gc0");
        foreach(var name in Enum.GetNames(typeof(FrameCost.Stage)))probe.rows.Append(",").Append(name);
        probe.rows.AppendLine();probe.gc=GC.CollectionCount(0);
    }
    void Update()
    {
        long now=Stopwatch.GetTimestamp();
        if(app==null)app=FindFirstObjectByType<M6App>();
        if(previous!=0&&frames<120000)
        {
            rows.Append(frames++).Append(',').Append(((now-previous)*1000d/Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture));
            rows.Append(',').Append(app!=null&&app.NetworkGame&&app.Ready?1:0);
            rows.Append(',').Append(app==null?0:app.VerificationBuildFlightCount);
            rows.Append(',').Append(app!=null&&app.CardPresentationBusy?1:0);
            rows.Append(',').Append(app!=null&&app.TableDiceRolling?1:0);
            rows.Append(',').Append(GC.CollectionCount(0)-gc);
            for(int i=0;i<FrameCost.Ticks.Length;i++)
                rows.Append(',').Append((Interlocked.Exchange(ref FrameCost.Ticks[i],0)*1000d/Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture));
            rows.AppendLine();
        }
        else for(int i=0;i<FrameCost.Ticks.Length;i++)Interlocked.Exchange(ref FrameCost.Ticks[i],0);
        gc=GC.CollectionCount(0);previous=now;
    }
    void OnApplicationQuit()
    {
        FrameCost.Enabled=false;
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"frames.csv"),rows.ToString());
    }
}
