using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

// 仅允许固定事件名、错误码、关联编号及数值；禁止传入正文、口令、凭据、手牌或异常原文。
internal sealed class M7CommunicationEvent
{
    public string Component="client",Event,Transport,Operation,RequestId,CommandId,RoomId,Code,Status,ClientVersion,Platform,BuildId;
    public long Revision=-1,Bytes,QueueDepth,Count,SlowFrames,GcCollections,ProtocolVersion;
    public double DurationMs,QueueWaitMs,DecodeMs,FrameMs,NetworkApplyMs,BoardRefreshMs,ViewDecodeMs,GuiMs,GameUpdateMs,BuildAnimationMs,PresentationApplyMs,ResponseDecodeMs,HistoryDecodeMs,WorkerDecodeMs;
    internal DateTime TimestampUtc;
}

internal static class M7CommunicationLog
{
    static readonly object gate=new object();
    static M7CommunicationWriter writer;
    internal static string DirectoryPath=>writer?.DirectoryPath;
    internal static string SessionId=>writer?.SessionId;
    internal static long DroppedEvents=>writer?.DroppedEvents??0;
    internal static string LastErrorCode=>writer?.LastErrorCode;
    internal static void Start(string directory)
    {
        lock(gate)
        {
            if(writer!=null)return;
            writer=new M7CommunicationWriter(directory);
            Record(new M7CommunicationEvent{Event="session_start",Code="schema_1"});
        }
    }
    internal static void Record(M7CommunicationEvent value)=>writer?.Record(value);
    internal static bool Flush(int milliseconds=500)=>writer==null||writer.Flush(milliseconds);
}

// 专用后台线程批量写入：最多 4096 条待写事件、每文件 4 MiB、最多 8 个文件（含历史会话）。
// 日志失败或磁盘已满只丢弃诊断事件，不进入联机错误与重连路径。
internal sealed class M7CommunicationWriter:IDisposable
{
    readonly Queue<M7CommunicationEvent> queue=new Queue<M7CommunicationEvent>();
    readonly object gate=new object();
    readonly AutoResetEvent ready=new AutoResetEvent(false);
    readonly Thread worker;
    readonly int capacity,maxFiles;
    readonly long maxFileBytes;
    readonly string prefix;
    bool stopping;
    int active;
    long dropped,writeFailures;
    volatile string lastErrorCode;
    internal string DirectoryPath {get;}
    internal string SessionId {get;}=Guid.NewGuid().ToString("N");
    internal long DroppedEvents=>Interlocked.Read(ref dropped);
    internal string LastErrorCode=>lastErrorCode;

    internal M7CommunicationWriter(string directory,int capacity=4096,long maxFileBytes=4*1024*1024,int maxFiles=8)
    {
        if(capacity<1||maxFileBytes<1024||maxFiles<1)throw new ArgumentOutOfRangeException();
        DirectoryPath=directory;this.capacity=capacity;this.maxFileBytes=maxFileBytes;this.maxFiles=maxFiles;
        prefix="comm-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff",CultureInfo.InvariantCulture)+"-"+SessionId;
        worker=new Thread(WriteLoop){IsBackground=true,Name="Catan communication log"};worker.Start();
    }
    internal void Record(M7CommunicationEvent value)
    {
        if(value==null)return;
        value.TimestampUtc=DateTime.UtcNow;
        lock(gate)
        {
            if(stopping||queue.Count>=capacity){Interlocked.Increment(ref dropped);return;}
            queue.Enqueue(value);ready.Set();
        }
    }
    internal bool Flush(int milliseconds)
    {
        var clock=Stopwatch.StartNew();
        do
        {
            lock(gate){if(queue.Count==0&&active==0)return true;}
            ready.Set();Thread.Sleep(5);
        }while(clock.ElapsedMilliseconds<milliseconds);
        return false;
    }
    void WriteLoop()
    {
        StreamWriter output=null;long size=0;int file=0;
        try
        {
            while(true)
            {
                ready.WaitOne(1000);
                var batch=new List<M7CommunicationEvent>(128);
                lock(gate)
                {
                    while(batch.Count<128&&queue.Count>0)batch.Add(queue.Dequeue());
                    active=batch.Count;
                    if(batch.Count==0&&stopping)break;
                }
                if(batch.Count==0)continue;
                try
                {
                    foreach(var entry in batch)
                    {
                        string line=Encode(entry,SessionId,DroppedEvents,Interlocked.Read(ref writeFailures));
                        int bytes=Encoding.UTF8.GetByteCount(line)+1;
                        if(output==null||size+bytes>maxFileBytes)
                        {
                            output?.Dispose();output=null;
                            Directory.CreateDirectory(DirectoryPath);
                            string path=Path.Combine(DirectoryPath,prefix+"-"+(++file).ToString("D4",CultureInfo.InvariantCulture)+".jsonl");
                            output=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false)){NewLine="\n"};size=0;
                            foreach(var old in Directory.GetFiles(DirectoryPath,"comm-*.jsonl").Where(old=>old!=path).OrderByDescending(Path.GetFileName,StringComparer.Ordinal).Skip(maxFiles-1))
                                try{File.Delete(old);}catch(IOException){}catch(UnauthorizedAccessException){}
                        }
                        output.WriteLine(line);size+=bytes;
                    }
                    output.Flush();lastErrorCode=null;
                }
                catch(Exception ex)
                {
                    // 刷盘失败时整批是否落盘不可确认，按整批损失计数，避免显示为零丢失。
                    Interlocked.Add(ref dropped,batch.Count);Interlocked.Increment(ref writeFailures);lastErrorCode=ex.GetType().Name;
                    try{output?.Dispose();}catch(Exception){}output=null;
                }
                finally
                {
                    lock(gate){active=0;if(queue.Count>0)ready.Set();}
                }
            }
        }
        finally{try{output?.Dispose();}catch(Exception){}}
    }
    internal static string Encode(M7CommunicationEvent value,string session,long dropped,long failures)
    {
        var text=new StringBuilder(768);text.Append("{\"schema\":1");
        Add(text,"utc",value.TimestampUtc.ToString("O",CultureInfo.InvariantCulture));Add(text,"session",session);
        Add(text,"component",value.Component);Add(text,"event",value.Event);Add(text,"transport",value.Transport);Add(text,"operation",value.Operation);
        Add(text,"requestId",value.RequestId);Add(text,"commandId",value.CommandId);Add(text,"roomId",value.RoomId);Add(text,"code",value.Code);Add(text,"status",value.Status);
        Add(text,"clientVersion",value.ClientVersion);Add(text,"platform",value.Platform);Add(text,"buildId",value.BuildId);Number(text,"protocolVersion",value.ProtocolVersion);
        Number(text,"revision",value.Revision);Number(text,"bytes",value.Bytes);Number(text,"queueDepth",value.QueueDepth);Number(text,"count",value.Count);
        Number(text,"slowFrames",value.SlowFrames);Number(text,"gcCollections",value.GcCollections);Number(text,"durationMs",value.DurationMs);Number(text,"queueWaitMs",value.QueueWaitMs);
        Number(text,"decodeMs",value.DecodeMs);Number(text,"frameMs",value.FrameMs);Number(text,"networkApplyMs",value.NetworkApplyMs);Number(text,"boardRefreshMs",value.BoardRefreshMs);
        Number(text,"viewDecodeMs",value.ViewDecodeMs);Number(text,"guiMs",value.GuiMs);Number(text,"gameUpdateMs",value.GameUpdateMs);Number(text,"buildAnimationMs",value.BuildAnimationMs);
        Number(text,"presentationApplyMs",value.PresentationApplyMs);Number(text,"responseDecodeMs",value.ResponseDecodeMs);Number(text,"historyDecodeMs",value.HistoryDecodeMs);Number(text,"workerDecodeMs",value.WorkerDecodeMs);
        Number(text,"droppedEvents",dropped);Number(text,"writeFailures",failures);return text.Append('}').ToString();
    }
    static void Add(StringBuilder text,string name,string value)
    {
        if(string.IsNullOrEmpty(value))return;
        text.Append(",\"").Append(name).Append("\":\"");
        // 所有字符串都是短标签或编号；禁止换行、JSON 注入和无限长内容。
        foreach(char c in value.Take(128))text.Append(char.IsLetterOrDigit(c)||c=='_'||c=='-'||c=='.'||c==':'||c=='+'?c:'_');
        text.Append('"');
    }
    static void Number(StringBuilder text,string name,double value)
    {
        if(value==0||double.IsNaN(value)||double.IsInfinity(value))return;
        text.Append(",\"").Append(name).Append("\":").Append(value.ToString("0.###",CultureInfo.InvariantCulture));
    }
    public void Dispose()
    {
        lock(gate){stopping=true;ready.Set();}
        // 只用于测试和应用退出；通常运行过程不等待磁盘。
        if(worker.Join(1000))ready.Dispose();
    }
}
