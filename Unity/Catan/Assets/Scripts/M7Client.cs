using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Catan.Core;
using Catan.Core.ThreeKingdoms;
using Catan.Network.Contracts;
using UnityEngine;
using Stopwatch=System.Diagnostics.Stopwatch;

// Network state is applied only in Update. Neither an authority nor an AI exists in this client.
public sealed class M7Client : MonoBehaviour
{
    public string Endpoint { get; private set; }
    public string Fingerprint { get; private set; }
    // 保留的服务地址不代表传输仍可用；返回主页会释放客户端。
    public bool EndpointConfigured=>http!=null&&lifetime!=null&&!lifetime.IsCancellationRequested;
    public M7LobbyConnectionState LobbyConnectionState { get; private set; }=M7LobbyConnectionState.Unconfigured;
    public string Status { get; private set; }="填写服务地址，或启动本机局域网服务。";
    public string LastError { get; private set; }="";
    public void ClearError(){LastError="";}
    public string SeatId { get; private set; }
    public string RoomId { get; private set; }
    public RoomSummary Room { get; private set; }
    public RoomSummary[] Rooms { get; private set; }=Array.Empty<RoomSummary>();
    public GameHistoryEntry[] History { get; private set; }=Array.Empty<GameHistoryEntry>();
    public int HistoryRevision { get; private set; }
    public long Revision { get; private set; }=-1;
    public bool Playing { get; internal set; }
    public bool Connected { get; private set; }
    public bool Busy { get; private set; }
    public bool CommandPending=>pendingCommand!=null;
    public bool EnrollmentPending=>pendingEnrollment!=null;
    public bool ControlLost=>!automaticReconnect;
    public bool HumanControlAvailable { get; private set; }
    public bool CanInteract=>Connected&&HumanControlAvailable&&!CommandPending&&!Busy;
    public string TransportMode { get; private set; }="未连接";
    public event Action ViewChanged;
    public event Action PreviewChanged;
    public event Action SessionEnded;
    public event Action<string> CommandFeedback;
    readonly ConcurrentQueue<Action> main=new ConcurrentQueue<Action>();
    readonly Dictionary<string,M7Preview> previews=new Dictionary<string,M7Preview>();
    readonly HashSet<string> previewPending=new HashSet<string>();
    readonly Queue<KeyValuePair<string,string>> previewQueue=new Queue<KeyValuePair<string,string>>();
    string credential,connectionId,viewJson,storageDirectory;
    NetworkRequest pendingCommand,pendingEnrollment;
    object decodedView;
    long historyCursor;
    HttpClient http;
    CancellationTokenSource lifetime;
    ClientWebSocket socket;
    int generation,connectionEpoch,matchEpoch;
    bool historyBusy,historyDirty,previewBusy,socketConnected,socketStarting,syncBusy,commandSending;
    long socketRevision;
    double currentQueueWaitMs;
    float nextQueueDiagnosticAt,nextSocketAt,socketRetryDelay=2;
    internal static readonly TimeSpan SocketHeartbeatInterval=TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan SocketResponseTimeout=TimeSpan.FromSeconds(12);
    int commandEpoch,socketEpoch;
    float retryAt,nextPreviewAt,nextSyncAt,nextCommandRetry,nextHistoryAt,nextLobbyPollAt,historyRetryDelay=2;
    string rememberedRuleSet;
    string rememberedGeneralPackage,rememberedGeneralHash;
    bool verification,automaticReconnect=true;
    JoinRoomPayload reconnectJoinFallback;
    string desiredDisplayName;
    float nextRenameAt;
    public void SetDisplayName(string value){desiredDisplayName=PlayerIdentityModel.NormalizeName(value);nextRenameAt=Time.unscaledTime+.6f;}

    void Awake()
    {
        storageDirectory=Path.Combine(Application.persistentDataPath,"m7-client");verification=Environment.GetCommandLineArgs().Contains("--m7-verify");
        M7CommunicationLog.Start(Path.Combine(Application.persistentDataPath,"m7-logs"));
        M7CommunicationLog.Record(new M7CommunicationEvent{Event="client_start",ClientVersion=Application.version,Platform=Application.platform.ToString(),ProtocolVersion=NetworkProtocol.Version,BuildId=typeof(M7Client).Assembly.ManifestModule.ModuleVersionId.ToString("N")});
        Debug.Log("CATAN_M7_LOG_DIRECTORY "+M7CommunicationLog.DirectoryPath);
    }
    void Diagnose(string operation,Exception error)
    {
        string code=M7NetworkErrors.Code(error);
        M7CommunicationLog.Record(new M7CommunicationEvent{Event="transport_error",Operation=operation,RoomId=RoomId,Revision=Revision,Code=code});
        if(verification)Debug.LogWarning("CATAN_M7_TRANSPORT_DIAGNOSTIC "+operation+" "+code);
    }
    void EnqueueMain(Action action)
    {
        long queued=Stopwatch.GetTimestamp();
        main.Enqueue(()=>
        {
            currentQueueWaitMs=ElapsedMs(queued);
            try{action();}finally{currentQueueWaitMs=0;}
        });
    }
    static double ElapsedMs(long start)=>(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;
    static void AbortSocket(ClientWebSocket value){try{value?.Abort();}catch(ObjectDisposedException){}}
    internal void UseCredentialDirectory(string path){if(http!=null)throw new InvalidOperationException("连接后不可切换凭据目录。");storageDirectory=path;}
    internal void EnableVerification(){verification=true;}
    void Update()
    {
        using var frameCost=FrameCost.Measure(FrameCost.Stage.NetworkUpdate);
        // 连接恢复时仍按原顺序应用，但不把积压的全部回调挤在一个渲染帧。
        long started=Stopwatch.GetTimestamp();int applied=0;
        while(applied<32&&main.TryDequeue(out var action)){action();applied++;if(ElapsedMs(started)>=4)break;}
        if(!main.IsEmpty&&Time.unscaledTime>=nextQueueDiagnosticAt)
        {
            nextQueueDiagnosticAt=Time.unscaledTime+1;
            M7CommunicationLog.Record(new M7CommunicationEvent{Event="main_queue_backlog",RoomId=RoomId,Revision=Revision,QueueDepth=main.Count,Count=applied,DurationMs=ElapsedMs(started)});
        }
        if(Connected&&!Busy&&!CommandPending&&Room!=null&&!string.IsNullOrEmpty(desiredDisplayName)&&Time.unscaledTime>=nextRenameAt)
        {
            nextRenameAt=Time.unscaledTime+2;
            if(Room.Seats.Any(s=>s.SeatId==SeatId&&s.DisplayName!=desiredDisplayName))Request("Rename",new NamePayload{DisplayName=desiredDisplayName});
        }
        if(socketConnected&&(socket==null||socket.State!=WebSocketState.Open))socketConnected=false;
        if(Connected&&!previewBusy&&previewQueue.Count>0&&Time.unscaledTime>=nextPreviewAt){nextPreviewAt=Time.unscaledTime+.12f;SendPreview(previewQueue.Dequeue());}
        if(automaticReconnect&&!socketConnected&&!syncBusy&&!Busy&&http!=null&&connectionId!=null&&Time.unscaledTime>=nextSyncAt){nextSyncAt=Time.unscaledTime+1;PollState();}
        if(automaticReconnect&&Connected&&!socketConnected&&!socketStarting&&!Busy&&http!=null&&connectionId!=null&&Time.unscaledTime>=nextSocketAt)StartSocket();
        if(automaticReconnect&&Connected&&!Busy&&pendingCommand!=null&&(!commandSending||commandEpoch!=connectionEpoch)&&Time.unscaledTime>=nextCommandRetry)SendPending();
        if(automaticReconnect&&Connected&&historyDirty&&!historyBusy&&Time.unscaledTime>=nextHistoryAt)FetchHistory();
        if(EndpointConfigured&&!Playing&&!Busy&&!EnrollmentPending&&Time.unscaledTime>=nextLobbyPollAt)ListRooms();
        if(automaticReconnect&&!Connected&&!Busy&&Time.unscaledTime>=retryAt)
        {
            retryAt=Time.unscaledTime+5;
            if(EnrollmentPending)RetryEnrollment();else if(RoomId!=null)Reconnect();
        }
    }
    public T View<T>() where T:class
    {
        if(decodedView==null||!(decodedView is T))decodedView=FrameCost.Decode<T>(viewJson,FrameCost.Stage.ViewDecode);
        return (T)decodedView;
    }
    public static string NormalizeFingerprint(string value)=>new string((value??"").Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
    public void ConfigureEndpoint(string address,string pin,bool restoreIdentity=false)
    {
        string normalized=NormalizeFingerprint(pin);
        if(normalized.Length!=0&&normalized.Length!=64)throw new M7NetworkFailure("PIN_FORMAT","主机证书标识无效，请重新搜索。");
        var uri=new Uri(normalized.Length==0?M7OnlineEndpoint.Normalize(address):address.Trim().TrimEnd('/'));
        if(normalized.Length>0&&(uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||uri.AbsolutePath!="/"||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)))throw new M7NetworkFailure("ADDRESS","局域网主机须使用发现结果中的 HTTPS 地址和证书标识，请重新搜索。");
        EndSession();Endpoint=uri.GetLeftPart(UriPartial.Authority);Fingerprint=normalized;
        lifetime=new CancellationTokenSource();
        var handler=new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false};
        if(normalized.Length>0)handler.ServerCertificateCustomValidationCallback=(request,certificate,chain,errors)=>
        {
            // Unity Mono supplies a relative URI in this callback. Resolve it against this
            // client instance's fixed endpoint; redirects remain disabled and the pin is mandatory.
            var actual=request.RequestUri.IsAbsoluteUri?request.RequestUri:new Uri(uri,request.RequestUri);
            return actual.Scheme=="https"&&actual.Authority==uri.Authority&&certificate!=null&&CertificateMatches(certificate.GetRawCertData(),normalized);
        };
        http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(15)};
        M7CommunicationLog.Record(new M7CommunicationEvent{Event="endpoint_configured",Transport=normalized.Length>0?"HTTPS":"HTTP"});
        LobbyConnectionState=M7LobbyConnectionState.Connecting;nextLobbyPollAt=Time.unscaledTime+5;
        LastError="";Status=normalized.Length>0?"已配置局域网主机，将在请求时建立加密连接。":"正在连接在线大厅。";
        if(restoreIdentity)LoadIdentity();
    }
    internal static bool CertificateMatches(byte[] certificate,string expected)
    {
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(certificate)).Replace("-","").ToLowerInvariant()==expected;
    }
    public void ListRooms()
    {
        if(!Busy&&EndpointConfigured){nextLobbyPollAt=Time.unscaledTime+5;if(LobbyConnectionState!=M7LobbyConnectionState.Online)LobbyConnectionState=M7LobbyConnectionState.Connecting;}
        Request("List");
    }
    public async void DescribeRoom(string id,Action<RoomSummary> found)
    {
        id=(id??"").Trim();if(id.Length==0){LastError="请输入房间号。";return;}
        if(Busy||http==null){LastError="请先连接服务，并等待当前请求完成。";return;}
        Busy=true;LastError="";int current=generation;
        try
        {
            var response=await Send(new NetworkRequest{Operation="Describe",RoomId=id,RequestId=Guid.NewGuid().ToString("N")},lifetime.Token).ConfigureAwait(false);
            EnqueueMain(()=>{if(current!=generation)return;Busy=false;MarkLobbyResponse(response);if(!response.Success||response.Room==null){LastError="读取房间信息失败 ["+response.Code+"]："+(response.Message??"没有找到该房间。");return;}found(response.Room);});
        }
        catch(Exception ex){EnqueueMain(()=>{if(current==generation){Busy=false;MarkLobbyOffline();LastError=M7NetworkErrors.Describe("读取房间信息",ex);}});}
    }
    public void Create(RoomSettings settings,string name,string password){SetDisplayName(name);ChangeRoom(null,()=>{rememberedRuleSet=settings.RuleSet;rememberedGeneralPackage=settings.ThreeKingdomsPackageVersion;rememberedGeneralHash=settings.ThreeKingdomsContentHash;Request("Create",new CreateRoomPayload{Settings=settings,DisplayName=name,Password=password});});}
    // 离开旧桌的回执或网络故障不能阻止玩家加入另一桌；各桌恢复身份单独保留。
    async void ChangeRoom(string target,Action next)
    {
        if(http==null){Status="请先连接服务。";return;}
        if(target!=null&&RoomId==target)
        {
            if(Room!=null&&Connected){Status="你已在这个房间。";return;}
            next();return;
        }
        if(RoomId==null&&!EnrollmentPending&&!Busy){next();return;}
        var leave=connectionId!=null&&credential!=null?Envelope("Leave"):null;
        string address=Endpoint,pin=Fingerprint;
        ClearActiveIdentity();
        // 先撤销旧连接及回调，再发送退出；旧局面推送不能把玩家拉回牌桌。
        ConfigureEndpoint(address,pin,false);
        Busy=true;automaticReconnect=false;int current=generation;
        if(leave!=null)
        {
            try{using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)){timeout.CancelAfter(TimeSpan.FromSeconds(2));await Send(leave,timeout.Token).ConfigureAwait(false);}}
            catch(Exception){/* 旧服务不可达时，连接超时后由服务端清理空房。 */}
        }
        EnqueueMain(()=>{if(current!=generation)return;Busy=false;automaticReconnect=true;next();});
    }
    public void Join(string id,string name,string password)
    {
        SetDisplayName(name);
        id=(id??"").Trim();if(id.Length==0){Status="请输入房间号。";return;}
        string target=id;ChangeRoom(target,()=>JoinRoom(target,name,password));
    }
    async void JoinRoom(string id,string name,string password)
    {
        if(Busy||http==null){Status="请先连接服务，等待当前请求完成。";return;}
        if(EnrollmentPending&&pendingEnrollment.RoomId==id){RetryEnrollment();return;}
        int current=generation;Busy=true;
        try
        {
            var response=await Send(new NetworkRequest{Operation="Describe",RoomId=id,RequestId=Guid.NewGuid().ToString("N")},lifetime.Token).ConfigureAwait(false);
            EnqueueMain(()=>
            {
                if(current!=generation)return;Busy=false;
                MarkLobbyResponse(response);
                var info=response.Room;if(!response.Success||info==null){Status=response.Message??"没有找到该房间。";return;}
                if(info.RulesVersion!=NetworkProtocol.RulesVersion(info.RuleSet)||info.ScenarioVersion!=NetworkProtocol.ScenarioVersion(info.RuleSet)){Status="房间规则或场景版本不兼容，请更新客户端。";return;}
                if(info.ThreeKingdomsPackageVersion!=null&&(info.ThreeKingdomsPackageVersion!=GeneralCatalog.B4PackageVersion||info.ThreeKingdomsContentHash!=GeneralCatalog.B4ContentHash)){Status="房间武将包版本或内容不一致，请更新客户端。";return;}
                rememberedRuleSet=info.RuleSet;rememberedGeneralPackage=info.ThreeKingdomsPackageVersion;rememberedGeneralHash=info.ThreeKingdomsContentHash;RoomId=id;
                var payload=new JoinRoomPayload{DisplayName=name,Password=password};
                if(TryLoadRoomIdentity(id)){reconnectJoinFallback=payload;Reconnect();}
                else Request("Join",payload);
            });
        }
        catch(Exception ex){EnqueueMain(()=>{if(current==generation){Busy=false;MarkLobbyOffline();LastError=Status=M7NetworkErrors.Describe("读取房间信息",ex);}});}
    }
    public void SelectSeat(string seat)=>Request("Seat",new SeatPayload{SeatId=seat});
    public void RetryEnrollment(){if(pendingEnrollment==null){Status="没有待确认的创建或加入请求。";return;}Request(pendingEnrollment.Operation);}
    public void AbandonEnrollment(){if(Busy){Status="当前请求仍在传输，请等待结果后再放弃。";return;}ClearEnrollment();if(string.IsNullOrEmpty(credential))RoomId=null;Status="已放弃本设备的待确认申请，可重新创建或加入。";}
    void ClearEnrollment(){try{if(File.Exists(IdentityPath+".enrollment"))File.Delete(IdentityPath+".enrollment");pendingEnrollment=null;}catch(IOException){Status="无法移除待确认申请，请检查本机目录权限。";}}
    static bool EnrollmentRejected(string code)
    {
        switch(code)
        {
            case "InvalidPassword":case "SeatOccupied":case "InvalidRequest":case "VersionMismatch":case "ProtocolMismatch":case "GameStarted":case "RoomNotFound":case "RoomLimit":case "EnrollmentLimit":case "MessageTooLarge":return true;
            default:return false;
        }
    }
    public void Ready(bool value)=>Request("Ready",new ReadyPayload{Ready=value});
    public void ConfigureRoom(RoomSettings settings)=>Request("Configure",settings);
    public void StartRoom()=>Request("Start");
    public bool CanRestartRoom=>Connected&&!Busy&&!CommandPending&&Room?.Started==true&&Room.ManagerSeatId==SeatId;
    public void RestartRoom(){if(CanRestartRoom)Request("Restart");}
    public void Reconnect(){if(string.IsNullOrEmpty(credential)||string.IsNullOrEmpty(RoomId)){Status="此设备没有该房间的恢复凭据，请从大厅加入。";return;}automaticReconnect=true;Request("Reconnect");}
    public void ForgetIdentity(){try{if(File.Exists(IdentityPath))File.Delete(IdentityPath);if(RoomId!=null&&File.Exists(RoomIdentityPath(RoomId)))File.Delete(RoomIdentityPath(RoomId));}catch(IOException){}ClearEnrollment();EndSession();Status="已忘记本设备的恢复身份和待确认申请；可重新加入可用席位。";}
    public void Leave()=>ChangeRoom(null,()=>{ListRooms();Status="已退出房间，可创建或加入其他房间。";});
    public void Transfer(string seat)=>Request("Transfer",new SeatPayload{SeatId=seat});
    NetworkRequest Envelope(string operation,string payload=null)
    {
        string rules=Room?.RuleSet??rememberedRuleSet;
        return new NetworkRequest{RequestId=Guid.NewGuid().ToString("N"),Operation=operation,RoomId=RoomId,Credential=credential,ConnectionId=connectionId,ExpectedRevision=Revision,RuleSet=rules,ExpectedRulesVersion=rules==null?null:NetworkProtocol.RulesVersion(rules),ExpectedScenarioVersion=rules==null?null:NetworkProtocol.ScenarioVersion(rules),ExpectedThreeKingdomsPackageVersion=Room?.ThreeKingdomsPackageVersion??rememberedGeneralPackage,ExpectedThreeKingdomsContentHash=Room?.ThreeKingdomsContentHash??rememberedGeneralHash,Payload=payload};
    }
    public void Submit(object command)
    {
        if(!CanInteract){Status=CommandPending?"操作正在等待确认，请勿重复提交。":"连接恢复后才能操作。";return;}
        pendingCommand=Envelope("Command",WireJson.SerializeObject(command));pendingCommand.CommandId=Guid.NewGuid().ToString("N");
        Status="操作已发送，等待服务端确认。";SendPending();
    }
    async void SendPending()
    {
        if(pendingCommand==null||http==null||commandSending&&commandEpoch==connectionEpoch)return;
        commandSending=true;commandEpoch=connectionEpoch;nextCommandRetry=Time.unscaledTime+2;
        int current=generation,epoch=connectionEpoch;var request=pendingCommand;request.Credential=credential;request.ConnectionId=connectionId;
        try
        {
            var response=await Send(request,lifetime.Token).ConfigureAwait(false);
            EnqueueMain(()=>
            {
                if(current!=generation||epoch!=connectionEpoch||!ReferenceEquals(pendingCommand,request))return;
                commandSending=false;
                bool unknown=response.Code=="ServiceUnavailable"||response.Code=="ConnectionSuperseded"||response.Code=="Unauthorized";
                if(!unknown)pendingCommand=null;
                Apply(response);
                if(response.Code=="ServiceUnavailable")Disconnected("服务暂时无法确认操作，保留原命令编号，重连后核对结果。");
                CommandFeedback?.Invoke(Status);
            });
        }
        catch(Exception ex){EnqueueMain(()=>{if(current==generation&&epoch==connectionEpoch&&ReferenceEquals(pendingCommand,request)){commandSending=false;Diagnose("Command",ex);LastError=M7NetworkErrors.Describe("确认操作",ex);Disconnected(LastError+" 操作结果待确认，重连后核对原命令。");}});}
    }
    public M7Preview Preview(object command)
    {
        if(!CanInteract)return new M7Preview(false,"等待服务端连接或操作确认。");
        if(M7VisiblePreview.Try(command,decodedView,out var visible))return visible;
        // Command IDs are transport identities and change while hovering; exclude them from the cache key.
        var property=command.GetType().GetProperty("Id");var original=property?.GetValue(command);property?.SetValue(command,null);
        string payload;try{payload=WireJson.SerializeObject(command);}finally{property?.SetValue(command,original);}
        string key=Revision+":"+payload;
        if(previews.TryGetValue(key,out var result))return result;
        if(previewQueue.Count<64&&previewPending.Add(key))previewQueue.Enqueue(new KeyValuePair<string,string>(key,payload));
        return new M7Preview(false,"正在核对合法目标…");
    }
    async void SendPreview(KeyValuePair<string,string> item)
    {
        previewBusy=true;int current=generation;long revision=Revision;
        try
        {
            var request=Envelope("Preview",item.Value);request.CommandId=Guid.NewGuid().ToString("N");
            var response=await Send(request,lifetime.Token).ConfigureAwait(false);
            EnqueueMain(()=>{if(current!=generation)return;previewBusy=false;previewPending.Remove(item.Key);if(revision==Revision){previews[item.Key]=new M7Preview(response.Success,response.Message);PreviewChanged?.Invoke();}});
        }
        catch(Exception){EnqueueMain(()=>{if(current==generation){previewBusy=false;previewPending.Remove(item.Key);}});}
    }
    async void Request(string operation,object payload=null)
    {
        if(Busy||http==null){Status=http==null?"请先连接服务地址。":"上一个请求仍在处理。";return;}
        Busy=true;LastError="";int current=generation,epoch=connectionEpoch;var request=Envelope(operation,payload==null?null:WireJson.SerializeObject(payload));
        try
        {
            bool enrollment=operation=="Create"||operation=="Join";
            if(enrollment)
            {
                if(pendingEnrollment!=null)request=pendingEnrollment;
                else
                {
                    request.EnrollmentId=Guid.NewGuid().ToString("N");
                    var bytes=new byte[32];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);
                    request.EnrollmentCredential=BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
                    Directory.CreateDirectory(storageDirectory);
                    M7CredentialStore.Write(IdentityPath+".enrollment",Fingerprint+"\n"+WireJson.Serialize(request));
                    pendingEnrollment=request;
                }
            }
            var response=await Send(request,lifetime.Token).ConfigureAwait(false);
            EnqueueMain(()=>
            {
                if(current!=generation)return;Busy=false;
                if(epoch!=connectionEpoch&&operation!="List")return;
                if(operation=="Reconnect"&&!response.Success&&reconnectJoinFallback!=null&&(response.Code=="Unauthorized"||response.Code=="RoomNotFound"))
                {
                    var fallback=reconnectJoinFallback;reconnectJoinFallback=null;credential=connectionId=null;automaticReconnect=true;
                    Request("Join",fallback);return;
                }
                if(operation=="Leave"&&response.Success){ClearActiveIdentity();EndSession();Status="已退出房间。";return;}
                bool handshake=response.Success&&(operation=="Join"||operation=="Create"||operation=="Reconnect");
                if(handshake&&request.EnrollmentCredential!=null)credential=request.EnrollmentCredential;
                Apply(response,handshake,operation=="List");
                if(enrollment&&!response.Success&&EnrollmentRejected(response.Code))ClearEnrollment();
                if(handshake)
                {
                    reconnectJoinFallback=null;
                    if(SaveIdentity()&&enrollment){try{File.Delete(IdentityPath+".enrollment");pendingEnrollment=null;}catch(IOException){}}
                    StartSocket();if(pendingCommand!=null)SendPending();
                }
                if(!response.Success&&operation=="Join"&&string.IsNullOrEmpty(credential))RoomId=null;
            });
        }
        catch(Exception ex){EnqueueMain(()=>{if(current!=generation)return;Busy=false;if(epoch!=connectionEpoch&&operation!="List")return;Diagnose(operation,ex);LastError=M7NetworkErrors.Describe(OperationLabel(operation),ex);Disconnected(LastError+(RoomId!=null||EnrollmentPending?" 正在等待重连。":" 请重新搜索或重试。"));});}
    }
    static string OperationLabel(string operation)
    {
        switch(operation){case "Join":return "加入房间";case "Create":return "创建房间";case "Reconnect":return "恢复连接";case "List":return "读取房间列表";default:return "房间请求";}
    }
    async Task<NetworkResponse> Send(NetworkRequest request,CancellationToken cancel)
    {
        long started=Stopwatch.GetTimestamp();
        var client=http;string endpoint=Endpoint;
        string transport=string.IsNullOrEmpty(Fingerprint)?"HTTP":"HTTPS";
        string body=WireJson.Serialize(request);
        int requestBytes=Encoding.UTF8.GetByteCount(body);
        if(requestBytes>NetworkProtocol.MaximumRequestBytes)throw new InvalidOperationException("请求过大。");
        M7CommunicationLog.Record(new M7CommunicationEvent{Event="request_send",Transport=transport,Operation=request.Operation,RequestId=request.RequestId,CommandId=request.CommandId,RoomId=request.RoomId,Revision=request.ExpectedRevision,Bytes=requestBytes});
        // ResponseHeadersRead 避免 HttpClient 先把超大正文无界缓冲；后续收包/解码不占用 Unity 帧。
        // 响应正文仍有同一 15 秒截止时间，不能只给等待响应头设置超时。
        using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel))
        try
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using(var message=new HttpRequestMessage(HttpMethod.Post,endpoint+"/api/request"))
            {
                message.Content=new StringContent(body,Encoding.UTF8,"application/json");
                using(var response=await client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false))
                {
                    M7NetworkErrors.RequireHttp(response.StatusCode);
                    if(response.Content.Headers.ContentLength>NetworkProtocol.MaximumResponseBytes)throw new M7NetworkFailure("RESPONSE_SIZE","服务响应超过大小限制。");
                    using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))using(var bytes=new MemoryStream())
                    {
                        var buffer=new byte[8192];int count;
                        while((count=await stream.ReadAsync(buffer,0,buffer.Length,timeout.Token).ConfigureAwait(false))>0)
                        {if(bytes.Length+count>NetworkProtocol.MaximumResponseBytes)throw new M7NetworkFailure("RESPONSE_SIZE","服务响应超过大小限制。");bytes.Write(buffer,0,count);}
                        NetworkResponse result;var payload=bytes.ToArray();long decode=Stopwatch.GetTimestamp();
                        try{result=await Task.Run(()=>FrameCost.Decode<NetworkResponse>(Encoding.UTF8.GetString(payload),FrameCost.Stage.ResponseDecode),timeout.Token).ConfigureAwait(false);}
                        catch(OperationCanceledException){throw;}
                        catch(Exception){throw new M7NetworkFailure("RESPONSE_FORMAT","服务响应格式无效，请确认双方游戏版本一致。");}
                        if(result==null)throw new M7NetworkFailure("RESPONSE_FORMAT","服务返回空响应。");
                        if(result.ProtocolVersion!=NetworkProtocol.Version)throw new M7NetworkFailure("PROTOCOL","联机协议不兼容，请所有玩家更新至同一版本。");
                        M7CommunicationLog.Record(new M7CommunicationEvent{Event="response_received",Transport=transport,Operation=request.Operation,RequestId=request.RequestId,CommandId=request.CommandId,RoomId=result.RoomId??request.RoomId,Revision=result.Revision,Code=result.Code,Status=result.Success?"ok":"rejected",DurationMs=ElapsedMs(started),DecodeMs=ElapsedMs(decode),Bytes=payload.Length});
                        return result;
                    }
                }
            }
        }
        catch(Exception ex)
        {
            M7CommunicationLog.Record(new M7CommunicationEvent{Event="request_failed",Transport=transport,Operation=request.Operation,RequestId=request.RequestId,CommandId=request.CommandId,RoomId=request.RoomId,Revision=request.ExpectedRevision,DurationMs=ElapsedMs(started),Code=cancel.IsCancellationRequested?"SESSION_CANCELLED":M7NetworkErrors.Code(ex)});throw;
        }
    }
    void Apply(NetworkResponse response,bool handshake=false,bool roomList=false)
    {
        using var frameCost=FrameCost.Measure(FrameCost.Stage.NetworkApply);
        using var diagnostic=new ResponseApplication(this,response);
        if(response.ProtocolVersion!=NetworkProtocol.Version){Disconnected("联机协议不兼容，请更新客户端。");return;}
        MarkLobbyResponse(response);
        Status=response.Message??(response.Success?"已连接。":"请求被拒绝。");
        if(!response.Success)LastError="房间请求失败 ["+response.Code+"]："+Status;
        else if(handshake)LastError="";
        // 大厅列表只证明服务可达，不确认当前席位连接，也不受席位重连代次影响。
        if(roomList)
        {
            if(response.Success&&LobbyConnectionState==M7LobbyConnectionState.Online)Rooms=response.Rooms??Array.Empty<RoomSummary>();
            return;
        }
        if(response.Code=="RoomClosed"||response.Code=="RoomNotFound")
        {
            ClearActiveIdentity();ConfigureEndpoint(Endpoint,Fingerprint,false);
            Status="房间已销毁，可创建或加入其他房间。";ListRooms();return;
        }
        bool stale=response.Room!=null&&response.Revision<Revision;
        if(stale)return;
        if(Room!=null&&response.Room!=null&&Room.GameNumber!=response.Room.GameNumber)
        {
            matchEpoch++;History=Array.Empty<GameHistoryEntry>();historyCursor=0;
            historyBusy=false;historyDirty=true;nextHistoryAt=0;historyRetryDelay=2;
            pendingCommand=null;commandSending=false;decodedView=null;viewJson=null;
            previews.Clear();previewQueue.Clear();previewPending.Clear();previewBusy=false;
        }
        if(handshake)
        {
            connectionEpoch++;
            if(response.Credential!=null)credential=response.Credential;
            if(response.ConnectionId!=null)connectionId=response.ConnectionId;
        }
        if(!stale)
        {
            if(response.RoomId!=null)RoomId=response.RoomId;
            if(response.SeatId!=null)SeatId=response.SeatId;
        }
        if(response.Room!=null&&!stale){Room=response.Room;rememberedRuleSet=Room.RuleSet;rememberedGeneralPackage=Room.ThreeKingdomsPackageVersion;rememberedGeneralHash=Room.ThreeKingdomsContentHash;}
        Connected=connectionId!=null&&(response.Success||Connected);
        if(response.Room!=null&&!stale)HumanControlAvailable=response.HumanControlAvailable;
        if(response.ViewJson!=null&&response.Revision>=Revision)
        {
            bool changed=response.Revision!=Revision||viewJson==null;
            // 只有近期记录与已接收记录连续，或首次入桌时才直接采用；长时间掉线仍分页补齐，避免漏播卡牌。
            if(changed&&response.PresentationHistoryJson!=null&&(viewJson==null||(History.LastOrDefault()?.Sequence??0)>=response.PresentationHistoryAfterSequence))
                MergeHistory(FrameCost.Decode<GameHistoryEntry[]>(response.PresentationHistoryJson,FrameCost.Stage.HistoryDecode));
            Revision=response.Revision;viewJson=response.ViewJson;
            if(changed){decodedView=null;previews.Clear();previewQueue.Clear();previewPending.Clear();ViewChanged?.Invoke();historyDirty=true;FetchHistory();}
        }
        else if(response.Room!=null&&!stale)Revision=Math.Max(Revision,response.Revision);
        Interlocked.Exchange(ref socketRevision,Revision);
        if(response.Code=="ConnectionSuperseded"||response.Code=="Unauthorized")
        {
            automaticReconnect=false;Connected=false;
            Status=response.Code=="ConnectionSuperseded"?"该席位已在另一连接接管。自动重连已停止；需要取回控制时请手动重连。":"恢复凭据无效或已到期，自动重连已停止。可忘记本设备身份后重新加入。";
        }
        // A push may precede its command acknowledgement. Refresh display-only availability
        // even when the acknowledgement carries the same revision as the push.
        PreviewChanged?.Invoke();
    }
    readonly struct ResponseApplication:IDisposable
    {
        readonly M7Client client;readonly NetworkResponse response;readonly long start;
        internal ResponseApplication(M7Client client,NetworkResponse response){this.client=client;this.response=response;start=Stopwatch.GetTimestamp();}
        public void Dispose()=>M7CommunicationLog.Record(new M7CommunicationEvent{Event="response_apply",RequestId=response.RequestId,RoomId=response.RoomId??client.RoomId,Revision=response.Revision,DurationMs=ElapsedMs(start),QueueWaitMs=client.currentQueueWaitMs,QueueDepth=client.main.Count,Code=response.Code});
    }
    async void FetchHistory()
    {
        if(historyBusy||!historyDirty||!automaticReconnect||!Connected||http==null||RoomId==null||Time.unscaledTime<nextHistoryAt)return;
        historyBusy=true;historyDirty=false;int current=generation,epoch=connectionEpoch,match=matchEpoch;long requestedRevision=Revision;
        try
        {
            var response=await Send(Envelope("History",WireJson.Serialize(new HistoryPayload{AfterSequence=historyCursor,Limit=100})),lifetime.Token).ConfigureAwait(false);
            var entries=string.IsNullOrEmpty(response.HistoryJson)?Array.Empty<GameHistoryEntry>():await Task.Run(()=>FrameCost.Decode<GameHistoryEntry[]>(response.HistoryJson,FrameCost.Stage.HistoryDecode)).ConfigureAwait(false);
            EnqueueMain(()=>
            {
                if(current!=generation||match!=matchEpoch)return;historyBusy=false;
                if(epoch!=connectionEpoch||!response.Success){RetryHistory();return;}
                if(response.Room?.GameNumber!=Room?.GameNumber){historyDirty=true;nextHistoryAt=0;return;}
                MergeHistory(entries);historyCursor=Math.Max(historyCursor,response.HistoryCursor);
                historyRetryDelay=2;nextHistoryAt=0;historyDirty|=response.HasMoreHistory||Revision>requestedRevision;
                if(historyDirty)FetchHistory();
            });
        }
        catch(Exception){EnqueueMain(()=>{if(current==generation&&match==matchEpoch){historyBusy=false;RetryHistory();}});}
    }
    void MergeHistory(GameHistoryEntry[] entries)
    {
        if(entries==null||entries.Length==0)return;
        // 一个宿主命令可产生多条发动；多步结果按稳定发动ID替换，不能只按命令序号丢弃。
        string Key(GameHistoryEntry e)=>e.SkillEventId??("command:"+e.Sequence);
        var merged=History.ToDictionary(Key,e=>e);
        foreach(var entry in entries)merged[Key(entry)]=entry;
        History=merged.Values.OrderBy(e=>e.Sequence).ToArray();
        HistoryRevision++;
    }
    void RetryHistory(){historyDirty=true;nextHistoryAt=Time.unscaledTime+historyRetryDelay;historyRetryDelay=Math.Min(30,historyRetryDelay*2);}
    async void PollState()
    {
        syncBusy=true;int current=generation,epoch=connectionEpoch;
        try
        {
            var response=await Send(Envelope("Get"),lifetime.Token).ConfigureAwait(false);
            EnqueueMain(()=>{if(current!=generation)return;syncBusy=false;if(epoch!=connectionEpoch)return;if(!socketConnected)TransportMode=PollTransportLabel;Apply(response);});
        }
        catch(Exception ex){EnqueueMain(()=>{if(current!=generation)return;syncBusy=false;if(epoch==connectionEpoch){Diagnose("HttpsSync",ex);LastError=M7NetworkErrors.Describe("同步对局",ex);Disconnected(LastError+" 原席位与待决保留，正在重连。");}});}
    }
    async void StartSocket()
    {
        // 同一身份只保留一次建连；HTTP 重连已轮换身份时必须立即废弃旧建连。
        if(lifetime==null||socketStarting&&socketEpoch==connectionEpoch)return;
        socketStarting=true;socketConnected=false;AbortSocket(socket);socket?.Dispose();var ws=new ClientWebSocket();socket=ws;
        int current=generation,epoch=connectionEpoch;socketEpoch=epoch;var sessionCancel=lifetime.Token;
        // 在 Unity 线程捕获本次连接身份，后台心跳不等待 Update，即使帧卡顿也不会停止保活。
        var identity=Envelope("Get");var endpoint=M7OnlineEndpoint.WebSocketUri(Endpoint);
        string transport=string.IsNullOrEmpty(Fingerprint)?"WS":"WSS";
        var receipts=new ConcurrentDictionary<string,SocketReceipt>();
        var socketLifetime=CancellationTokenSource.CreateLinkedTokenSource(sessionCancel);var cancel=socketLifetime.Token;
        Task heartbeat=null;long started=Stopwatch.GetTimestamp();
        if(!string.IsNullOrEmpty(Fingerprint)){string pin=Fingerprint;ws.Options.RemoteCertificateValidationCallback=(sender,certificate,chain,errors)=>certificate!=null&&CertificateMatches(certificate.GetRawCertData(),pin);}
        ws.Options.KeepAliveInterval=TimeSpan.FromSeconds(15);
        M7CommunicationLog.Record(new M7CommunicationEvent{Event="socket_connect_start",Transport=transport,RoomId=identity.RoomId});
        try
        {
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {deadline.CancelAfter(SocketResponseTimeout);await ws.ConnectAsync(endpoint,deadline.Token).ConfigureAwait(false);}
            M7CommunicationLog.Record(new M7CommunicationEvent{Event="socket_connected",Transport=transport,RoomId=identity.RoomId,DurationMs=ElapsedMs(started)});
            EnqueueMain(()=>{if(current==generation&&epoch==connectionEpoch&&socket==ws){socketStarting=false;socketConnected=true;TransportMode=transport=="WSS"?"WSS 局域网加密同步":"WS 在线同步";}});
            heartbeat=SocketHeartbeat(ws,identity,receipts,transport,cancel);
            var buffer=new byte[16384];
            while(!cancel.IsCancellationRequested&&ws.State==WebSocketState.Open)
            {
                using(var message=new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do{result=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),cancel).ConfigureAwait(false);if(result.MessageType==WebSocketMessageType.Close)throw new IOException("连接关闭。");if(result.MessageType!=WebSocketMessageType.Text||message.Length+result.Count>NetworkProtocol.MaximumResponseBytes)throw new IOException("无效消息。");message.Write(buffer,0,result.Count);}while(!result.EndOfMessage);
                    var payload=message.ToArray();
                    // 等待当前消息解析完成才继续接收，避免同一连接的快照被并行解析打乱。
                    long decode=Stopwatch.GetTimestamp();
                    var response=await Task.Run(()=>FrameCost.Decode<NetworkResponse>(Encoding.UTF8.GetString(payload),FrameCost.Stage.ResponseDecode),cancel).ConfigureAwait(false);
                    if(response==null||response.ProtocolVersion!=NetworkProtocol.Version)throw new M7NetworkFailure("PROTOCOL","联机协议不兼容。");
                    double duration=0;
                    if(response.RequestId!=null&&receipts.TryRemove(response.RequestId,out var receipt)){duration=ElapsedMs(receipt.Started);receipt.Received.TrySetResult(true);}
                    M7CommunicationLog.Record(new M7CommunicationEvent{Event="response_received",Transport=transport,Operation=duration>0?"Get":"Push",RequestId=response.RequestId,RoomId=response.RoomId??identity.RoomId,Revision=response.Revision,Code=response.Code,Status=response.Success?"ok":"rejected",Bytes=payload.Length,DecodeMs=ElapsedMs(decode),DurationMs=duration});
                    EnqueueMain(()=>{if(current==generation&&epoch==connectionEpoch&&socket==ws){socketRetryDelay=2;Apply(response);}});
                }
            }
        }
        catch(Exception ex)
        {
            M7CommunicationLog.Record(new M7CommunicationEvent{Event="socket_closed",Transport=transport,RoomId=identity.RoomId,DurationMs=ElapsedMs(started),Code=sessionCancel.IsCancellationRequested?"SESSION_CANCELLED":M7NetworkErrors.Code(ex)});
            EnqueueMain(()=>{if(current==generation&&epoch==connectionEpoch&&socket==ws){socketConnected=false;TransportMode=PollTransportLabel;Status=transport=="WSS"?"局域网实时同步暂不可用，正在使用同一证书校验的 HTTPS 持续同步。":"实时同步暂不可用，正在通过 HTTP 持续同步。";}});
        }
        finally
        {
            socketLifetime.Cancel();AbortSocket(ws);
            if(heartbeat!=null)try{await heartbeat.ConfigureAwait(false);}catch(Exception){}
            socketLifetime.Dispose();
            EnqueueMain(()=>{if(current==generation&&epoch==connectionEpoch&&socket==ws){socketStarting=false;socketConnected=false;nextSyncAt=0;nextSocketAt=Time.unscaledTime+socketRetryDelay;socketRetryDelay=Math.Min(30,socketRetryDelay*2);}});
        }
    }
    sealed class SocketReceipt
    {
        internal readonly long Started=Stopwatch.GetTimestamp();
        internal readonly TaskCompletionSource<bool> Received=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    async Task SocketHeartbeat(ClientWebSocket ws,NetworkRequest identity,ConcurrentDictionary<string,SocketReceipt> receipts,string transport,CancellationToken cancel)
    {
        try
        {
            while(!cancel.IsCancellationRequested&&ws.State==WebSocketState.Open)
            {
                var request=new NetworkRequest{Operation="Get",RequestId=Guid.NewGuid().ToString("N"),RoomId=identity.RoomId,Credential=identity.Credential,ConnectionId=identity.ConnectionId,RuleSet=identity.RuleSet,ExpectedRulesVersion=identity.ExpectedRulesVersion,ExpectedScenarioVersion=identity.ExpectedScenarioVersion,ExpectedThreeKingdomsPackageVersion=identity.ExpectedThreeKingdomsPackageVersion,ExpectedThreeKingdomsContentHash=identity.ExpectedThreeKingdomsContentHash,ExpectedRevision=Interlocked.Read(ref socketRevision)};
                var receipt=new SocketReceipt();receipts[request.RequestId]=receipt;
                var bytes=Encoding.UTF8.GetBytes(WireJson.Serialize(request));
                M7CommunicationLog.Record(new M7CommunicationEvent{Event="request_send",Transport=transport,Operation="Get",RequestId=request.RequestId,RoomId=request.RoomId,Revision=request.ExpectedRevision,Bytes=bytes.Length});
                using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel))
                {
                    deadline.CancelAfter(SocketResponseTimeout);
                    await ws.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,deadline.Token).ConfigureAwait(false);
                    // 只把本次请求编号的回执视为成功；其它推送不能掩盖单向半断连接。
                    using(deadline.Token.Register(()=>receipt.Received.TrySetCanceled()))await receipt.Received.Task.ConfigureAwait(false);
                }
                await Task.Delay(SocketHeartbeatInterval,cancel).ConfigureAwait(false);
            }
        }
        catch(Exception ex)
        {
            if(!cancel.IsCancellationRequested)M7CommunicationLog.Record(new M7CommunicationEvent{Event="heartbeat_failed",Transport=transport,Operation="Get",RoomId=identity.RoomId,RequestId=receipts.Keys.FirstOrDefault(),Code=M7NetworkErrors.Code(ex)});
            AbortSocket(ws);
        }
    }
    string PollTransportLabel=>string.IsNullOrEmpty(Fingerprint)?"HTTP 在线同步（每秒）":"HTTPS 局域网加密同步（每秒）";
    void MarkLobbyResponse(NetworkResponse response)
    {
        if(response.Code=="ServiceUnavailable"){MarkLobbyOffline();return;}
        LobbyConnectionState=M7LobbyConnectionState.Online;
    }
    void MarkLobbyOffline(){LobbyConnectionState=M7LobbyConnectionState.Offline;Rooms=Array.Empty<RoomSummary>();nextLobbyPollAt=Time.unscaledTime+5;}
    void Disconnected(string message)
    {
        M7CommunicationLog.Record(new M7CommunicationEvent{Event="connection_lost",RoomId=RoomId,Revision=Revision,CommandId=pendingCommand?.CommandId,Code=automaticReconnect?"reconnect_scheduled":"control_lost"});
        Connected=false;MarkLobbyOffline();if(automaticReconnect)Status=message;retryAt=Time.unscaledTime+3;PreviewChanged?.Invoke();
    }
    string IdentityPath
    {
        get{return CredentialPath(Endpoint??"");}
    }
    string CredentialPath(string key){using(var sha=SHA256.Create())return Path.Combine(storageDirectory,BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-","")+".credential");}
    string RoomIdentityPath(string room)=>CredentialPath((string.IsNullOrEmpty(Fingerprint)?Endpoint:"lan:"+Fingerprint)+"|"+room);
    internal string[] RememberedRoomIds(string fingerprint)
    {
        var rooms=new List<string>();if(!Directory.Exists(storageDirectory))return rooms.ToArray();
        foreach(var path in Directory.GetFiles(storageDirectory,"*.credential").OrderByDescending(File.GetLastWriteTimeUtc).Take(128))
        {
            try
            {
                var lines=M7CredentialStore.Read(path).Split('\n');
                if(lines.Length==4&&lines[2]==NormalizeFingerprint(fingerprint)&&lines[0].Length>0&&lines[0].Length<=64&&!string.IsNullOrEmpty(lines[1]))rooms.Add(lines[0]);
            }
            catch(Exception){/* 无法读取的旧恢复材料不阻止发现和加入新房。 */}
        }
        return rooms.Distinct().Take(16).ToArray();
    }
    void ClearActiveIdentity()
    {
        try{File.Delete(IdentityPath);File.Delete(IdentityPath+".enrollment");}
        catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException){Status="自动恢复记录无法清理；大厅仍不会自动加入房间。";}
        pendingEnrollment=null;
    }
    bool SaveIdentity()
    {
        try{Directory.CreateDirectory(storageDirectory);string value=string.Join("\n",RoomId??"",credential??"",Fingerprint??"",rememberedRuleSet??"",rememberedGeneralPackage??"",rememberedGeneralHash??"");M7CredentialStore.Write(RoomIdentityPath(RoomId),value);M7CredentialStore.Write(IdentityPath,value);return true;}
        catch(Exception){Status+=" 此设备无法安全保存恢复凭据，请保留本次待确认请求。";return false;}
    }
    bool TryLoadRoomIdentity(string room)
    {
        try
        {
            string path=RoomIdentityPath(room);if(!File.Exists(path))path=CredentialPath((Endpoint??"")+"|"+room);
            if(!File.Exists(path))path=IdentityPath;
            if(!File.Exists(path))return RoomId==room&&!string.IsNullOrEmpty(credential);
            var lines=M7CredentialStore.Read(path).Split('\n');
            if((lines.Length!=4&&lines.Length!=6)||lines[0]!=room||lines[2]!=Fingerprint||string.IsNullOrEmpty(lines[1]))return false;
            RoomId=room;credential=lines[1];rememberedRuleSet=lines[3];LoadGeneralIdentity(lines);return true;
        }
        catch(Exception){return false;}
    }
    internal bool HasRoomIdentity(string room)
    {
        try
        {
            foreach(string path in new[]{RoomIdentityPath(room),CredentialPath((Endpoint??"")+"|"+room),IdentityPath})
            {
                if(!File.Exists(path))continue;
                var lines=M7CredentialStore.Read(path).Split('\n');
                if((lines.Length==4||lines.Length==6)&&lines[0]==room&&lines[2]==Fingerprint&&!string.IsNullOrEmpty(lines[1]))return true;
            }
        }
        catch(Exception){/* 无法读取身份时仍可通过密码正常申请加入。 */}
        return false;
    }
    void LoadIdentity()
    {
        try{if(File.Exists(IdentityPath)){var lines=M7CredentialStore.Read(IdentityPath).Split('\n');if((lines.Length==4||lines.Length==6)&&lines[2]==Fingerprint){RoomId=lines[0];credential=lines[1];rememberedRuleSet=lines[3];LoadGeneralIdentity(lines);Status+=" 已找到原席位凭据，可重连或退出原房间。";}}}catch(Exception){Status+=" 原恢复凭据不可读取。";}
        try
        {
            if(File.Exists(IdentityPath+".enrollment"))
            {
                string saved=M7CredentialStore.Read(IdentityPath+".enrollment");int newline=saved.IndexOf('\n');
                if(newline>=0&&saved.Substring(0,newline)==Fingerprint){pendingEnrollment=WireJson.Deserialize<NetworkRequest>(saved.Substring(newline+1));rememberedRuleSet=pendingEnrollment.RuleSet;Status+=" 有待确认的创建或加入请求，请点击恢复请求。";}
            }
        }
        catch(Exception){Status+=" 待确认请求不可读取。";}
    }
    void LoadGeneralIdentity(string[] lines)
    {
        rememberedGeneralPackage=lines.Length==6&&!string.IsNullOrEmpty(lines[4])?lines[4]:null;
        rememberedGeneralHash=lines.Length==6&&!string.IsNullOrEmpty(lines[5])?lines[5]:null;
    }
    public void EndSession()
    {
        bool hadGame=Playing;LastError="";
        if(http!=null)M7CommunicationLog.Record(new M7CommunicationEvent{Event="endpoint_session_end",RoomId=RoomId,Revision=Revision});
        generation++;connectionEpoch++;lifetime?.Cancel();AbortSocket(socket);socket?.Dispose();socket=null;http?.Dispose();http=null;lifetime?.Dispose();lifetime=null;
        socketStarting=false;nextSocketAt=0;socketRetryDelay=2;Interlocked.Exchange(ref socketRevision,-1);
        LobbyConnectionState=M7LobbyConnectionState.Unconfigured;nextLobbyPollAt=0;
        Playing=false;Connected=false;Busy=false;HumanControlAvailable=false;automaticReconnect=true;socketConnected=syncBusy=commandSending=false;TransportMode="未连接";Room=null;Rooms=Array.Empty<RoomSummary>();RoomId=SeatId=credential=connectionId=viewJson=rememberedRuleSet=null;decodedView=null;Revision=-1;History=Array.Empty<GameHistoryEntry>();historyCursor=0;pendingCommand=pendingEnrollment=null;reconnectJoinFallback=null;historyBusy=historyDirty=previewBusy=false;nextHistoryAt=0;historyRetryDelay=2;previews.Clear();previewPending.Clear();previewQueue.Clear();
        if(hadGame)SessionEnded?.Invoke();
        rememberedGeneralPackage=rememberedGeneralHash=null;
    }
    void OnApplicationPause(bool paused)=>M7CommunicationLog.Record(new M7CommunicationEvent{Event="application_pause",RoomId=RoomId,Revision=Revision,Status=paused?"paused":"resumed"});
    void OnApplicationFocus(bool focused)=>M7CommunicationLog.Record(new M7CommunicationEvent{Event="application_focus",RoomId=RoomId,Revision=Revision,Status=focused?"focused":"background"});
    void OnApplicationQuit(){M7CommunicationLog.Record(new M7CommunicationEvent{Event="application_quit",RoomId=RoomId,Revision=Revision});M7CommunicationLog.Flush();}
    void OnDestroy(){EndSession();}
}
