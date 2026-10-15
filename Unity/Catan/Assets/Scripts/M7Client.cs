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
using Catan.Network.Contracts;
using UnityEngine;

// Network state is applied only in Update. Neither an authority nor an AI exists in this client.
public sealed class M7Client : MonoBehaviour
{
    public string Endpoint { get; private set; }
    public string Fingerprint { get; private set; }
    public string Status { get; private set; }="填写服务地址，或启动本机局域网服务。";
    public string SeatId { get; private set; }
    public string RoomId { get; private set; }
    public RoomSummary Room { get; private set; }
    public RoomSummary[] Rooms { get; private set; }=Array.Empty<RoomSummary>();
    public GameHistoryEntry[] History { get; private set; }=Array.Empty<GameHistoryEntry>();
    public long Revision { get; private set; }=-1;
    public bool Playing { get; internal set; }
    public bool Connected { get; private set; }
    public bool Busy { get; private set; }
    public bool CommandPending=>pendingCommand!=null;
    public bool EnrollmentPending=>pendingEnrollment!=null;
    public bool ControlLost=>!automaticReconnect;
    public bool CanInteract=>Connected&&!CommandPending&&!Busy;
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
    int generation,connectionEpoch;
    bool historyBusy,historyDirty,previewBusy,socketConnected,syncBusy,commandSending;
    int commandEpoch;
    float retryAt,nextPreviewAt,nextSyncAt,nextCommandRetry,nextHistoryAt,historyRetryDelay=2;
    string rememberedRuleSet;
    bool verification,automaticReconnect=true;

    void Awake(){storageDirectory=Path.Combine(Application.persistentDataPath,"m7-client");verification=Environment.GetCommandLineArgs().Contains("--m7-verify");}
    void Diagnose(string operation,Exception error)
    {
        if(!verification)return;
        var details=new List<string>();
        for(var current=error;current!=null;current=current.InnerException)
        {
            string message=current is HttpRequestException||current is AuthenticationException||current is NotSupportedException||current is TypeLoadException||current is InvalidOperationException||current.GetType().FullName=="Mono.Security.Interface.TlsException"?current.Message:"";
            details.Add(current.GetType().FullName+(message.Length==0?"":": "+message));
        }
        Debug.LogWarning("CATAN_M7_TRANSPORT_DIAGNOSTIC "+operation+" "+string.Join(" -> ",details));
    }
    internal void UseCredentialDirectory(string path){if(http!=null)throw new InvalidOperationException("连接后不可切换凭据目录。");storageDirectory=path;}
    void Update()
    {
        while(main.TryDequeue(out var action))action();
        if(socketConnected&&(socket==null||socket.State!=WebSocketState.Open))socketConnected=false;
        if(Connected&&!previewBusy&&previewQueue.Count>0&&Time.unscaledTime>=nextPreviewAt){nextPreviewAt=Time.unscaledTime+.12f;SendPreview(previewQueue.Dequeue());}
        if(automaticReconnect&&!socketConnected&&!syncBusy&&!Busy&&http!=null&&connectionId!=null&&Time.unscaledTime>=nextSyncAt){nextSyncAt=Time.unscaledTime+1;PollState();}
        if(automaticReconnect&&Connected&&!Busy&&pendingCommand!=null&&(!commandSending||commandEpoch!=connectionEpoch)&&Time.unscaledTime>=nextCommandRetry)SendPending();
        if(automaticReconnect&&Connected&&historyDirty&&!historyBusy&&Time.unscaledTime>=nextHistoryAt)FetchHistory();
        if(automaticReconnect&&!Connected&&!Busy&&Time.unscaledTime>=retryAt)
        {
            retryAt=Time.unscaledTime+5;
            if(EnrollmentPending)RetryEnrollment();else if(RoomId!=null)Reconnect();
        }
    }
    public T View<T>() where T:class
    {
        if(decodedView==null||!(decodedView is T))decodedView=WireJson.Deserialize<T>(viewJson);
        return (T)decodedView;
    }
    public static string NormalizeFingerprint(string value)=>new string((value??"").Where(Uri.IsHexDigit).ToArray()).ToLowerInvariant();
    public void ConfigureEndpoint(string address,string pin)
    {
        var uri=new Uri(address.Trim().TrimEnd('/'));
        if(uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||uri.AbsolutePath!="/"||!string.IsNullOrEmpty(uri.Query))throw new InvalidOperationException("请填写 https://主机:端口，不得附带路径或凭据。");
        string normalized=NormalizeFingerprint(pin);
        if(normalized.Length!=0&&normalized.Length!=64)throw new InvalidOperationException("证书指纹必须是 64 位 SHA-256 十六进制值。");
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
        Status=normalized.Length>0?"已固定所核对的局域网证书。":"在线连接使用系统证书校验。";
        LoadIdentity();
    }
    internal static bool CertificateMatches(byte[] certificate,string expected)
    {
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(certificate)).Replace("-","").ToLowerInvariant()==expected;
    }
    public void ListRooms()=>Request("List");
    public void Create(RoomSettings settings,string name,string password){if(EnrollmentPending){Status="上次创建或加入仍待确认，请先恢复该请求。";return;}rememberedRuleSet=settings.RuleSet;Request("Create",new CreateRoomPayload{Settings=settings,DisplayName=name,Password=password});}
    public async void Join(string id,string name,string password)
    {
        if(Busy||http==null){Status="请先连接服务，等待当前请求完成。";return;}
        if(EnrollmentPending){Status="上次创建或加入仍待确认，请先恢复该请求。";return;}
        id=(id??"").Trim();if(RoomId!=null&&RoomId!=id){Status="请先退出当前房间。";return;}
        if(id.Length==0){Status="请输入房间号。";return;}
        int current=generation;Busy=true;
        try
        {
            var response=await Send(new NetworkRequest{Operation="Describe",RoomId=id,RequestId=Guid.NewGuid().ToString("N")},lifetime.Token);
            main.Enqueue(()=>
            {
                if(current!=generation)return;Busy=false;
                var info=response.Room;if(!response.Success||info==null){Status=response.Message??"没有找到该房间。";return;}
                if(info.RulesVersion!=NetworkProtocol.RulesVersion(info.RuleSet)||info.ScenarioVersion!=NetworkProtocol.ScenarioVersion(info.RuleSet)){Status="房间规则或场景版本不兼容，请更新客户端。";return;}
                rememberedRuleSet=info.RuleSet;RoomId=id;Request("Join",new JoinRoomPayload{DisplayName=name,Password=password});
            });
        }
        catch(Exception){main.Enqueue(()=>{if(current==generation){Busy=false;Status="无法读取房间信息，请检查连接。";}});}
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
    public void Reconnect(){if(string.IsNullOrEmpty(credential)||string.IsNullOrEmpty(RoomId)){Status="此设备没有该房间的恢复凭据，请从大厅加入。";return;}automaticReconnect=true;Request("Reconnect");}
    public void ForgetIdentity(){try{if(File.Exists(IdentityPath))File.Delete(IdentityPath);}catch(IOException){}ClearEnrollment();EndSession();Status="已忘记本设备的恢复身份和待确认申请；可重新加入可用席位。";}
    public void Leave()=>Request("Leave");
    public void Transfer(string seat)=>Request("Transfer",new SeatPayload{SeatId=seat});
    NetworkRequest Envelope(string operation,string payload=null)
    {
        string rules=Room?.RuleSet??rememberedRuleSet;
        return new NetworkRequest{RequestId=Guid.NewGuid().ToString("N"),Operation=operation,RoomId=RoomId,Credential=credential,ConnectionId=connectionId,ExpectedRevision=Revision,RuleSet=rules,ExpectedRulesVersion=rules==null?null:NetworkProtocol.RulesVersion(rules),ExpectedScenarioVersion=rules==null?null:NetworkProtocol.ScenarioVersion(rules),Payload=payload};
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
            var response=await Send(request,lifetime.Token);
            main.Enqueue(()=>
            {
                if(current!=generation||epoch!=connectionEpoch)return;
                commandSending=false;
                bool unknown=response.Code=="ServiceUnavailable"||response.Code=="ConnectionSuperseded"||response.Code=="Unauthorized";
                if(!unknown)pendingCommand=null;
                Apply(response);
                if(response.Code=="ServiceUnavailable")Disconnected("服务暂时无法确认操作，保留原命令编号，重连后核对结果。");
                CommandFeedback?.Invoke(Status);
            });
        }
        catch(Exception ex){main.Enqueue(()=>{if(current==generation&&epoch==connectionEpoch){commandSending=false;Diagnose("Command",ex);Disconnected("操作回执未收到，重连后使用同一命令编号核对，不重复执行。");}});}
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
            var response=await Send(request,lifetime.Token);
            main.Enqueue(()=>{if(current!=generation)return;previewBusy=false;previewPending.Remove(item.Key);if(revision==Revision){previews[item.Key]=new M7Preview(response.Success,response.Message);PreviewChanged?.Invoke();}});
        }
        catch(Exception){main.Enqueue(()=>{if(current==generation){previewBusy=false;previewPending.Remove(item.Key);}});}
    }
    async void Request(string operation,object payload=null)
    {
        if(Busy||http==null){Status=http==null?"请先连接服务地址。":"上一个请求仍在处理。";return;}
        Busy=true;int current=generation,epoch=connectionEpoch;var request=Envelope(operation,payload==null?null:WireJson.SerializeObject(payload));
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
            var response=await Send(request,lifetime.Token);
            main.Enqueue(()=>
            {
                if(current!=generation||epoch!=connectionEpoch)return;Busy=false;
                if(operation=="Leave"&&response.Success){if(Room==null||!Room.Started){try{if(File.Exists(IdentityPath))File.Delete(IdentityPath);}catch(IOException){}}EndSession();Status="已退出房间。已开始的房间可用此设备凭据恢复。";return;}
                bool handshake=response.Success&&(operation=="Join"||operation=="Create"||operation=="Reconnect");
                if(handshake&&request.EnrollmentCredential!=null)credential=request.EnrollmentCredential;
                Apply(response,handshake);
                if(enrollment&&!response.Success&&EnrollmentRejected(response.Code))ClearEnrollment();
                if(handshake)
                {
                    if(SaveIdentity()&&enrollment){try{File.Delete(IdentityPath+".enrollment");pendingEnrollment=null;}catch(IOException){}}
                    StartSocket();if(pendingCommand!=null)SendPending();
                }
                if(!response.Success&&operation=="Join"&&string.IsNullOrEmpty(credential))RoomId=null;
            });
        }
        catch(Exception ex){main.Enqueue(()=>{if(current==generation&&epoch==connectionEpoch){Diagnose(operation,ex);Busy=false;Disconnected("连接失败：请检查地址、证书指纹、端口与防火墙。正在等待重连。");}});}
    }
    async Task<NetworkResponse> Send(NetworkRequest request,CancellationToken cancel)
    {
        string body=WireJson.Serialize(request);
        if(Encoding.UTF8.GetByteCount(body)>NetworkProtocol.MaximumRequestBytes)throw new InvalidOperationException("请求过大。");
        using(var content=new StringContent(body,Encoding.UTF8,"application/json"))
        using(var response=await http.PostAsync(Endpoint+"/api/request",content,cancel))
        {
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>NetworkProtocol.MaximumResponseBytes)throw new InvalidOperationException("响应过大。");
            using(var stream=await response.Content.ReadAsStreamAsync())using(var bytes=new MemoryStream())
            {
                var buffer=new byte[8192];int count;
                while((count=await stream.ReadAsync(buffer,0,buffer.Length,cancel))>0){if(bytes.Length+count>NetworkProtocol.MaximumResponseBytes)throw new InvalidOperationException("响应过大。");bytes.Write(buffer,0,count);}
                return WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(bytes.ToArray()));
            }
        }
    }
    void Apply(NetworkResponse response,bool handshake=false)
    {
        if(response.ProtocolVersion!=NetworkProtocol.Version){Disconnected("联机协议不兼容，请更新客户端。");return;}
        Status=response.Message??(response.Success?"已连接。":"请求被拒绝。");
        bool stale=response.Room!=null&&response.Revision<Revision;
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
        if(response.Room!=null&&!stale){Room=response.Room;rememberedRuleSet=Room.RuleSet;}
        if(response.Rooms!=null)Rooms=response.Rooms;
        Connected=true;
        if(response.ViewJson!=null&&response.Revision>=Revision)
        {
            bool changed=response.Revision!=Revision||viewJson==null;
            Revision=response.Revision;viewJson=response.ViewJson;
            if(changed){decodedView=null;previews.Clear();previewQueue.Clear();previewPending.Clear();ViewChanged?.Invoke();historyDirty=true;FetchHistory();}
        }
        else if(response.Room!=null&&!stale)Revision=Math.Max(Revision,response.Revision);
        if(response.Code=="ConnectionSuperseded"||response.Code=="Unauthorized")
        {
            automaticReconnect=false;Connected=false;
            Status=response.Code=="ConnectionSuperseded"?"该席位已在另一连接接管。自动重连已停止；需要取回控制时请手动重连。":"恢复凭据无效或已到期，自动重连已停止。可忘记本设备身份后重新加入。";
        }
        // A push may precede its command acknowledgement. Refresh display-only availability
        // even when the acknowledgement carries the same revision as the push.
        PreviewChanged?.Invoke();
    }
    async void FetchHistory()
    {
        if(historyBusy||!historyDirty||!automaticReconnect||!Connected||http==null||RoomId==null||Time.unscaledTime<nextHistoryAt)return;
        historyBusy=true;historyDirty=false;int current=generation,epoch=connectionEpoch;long requestedRevision=Revision;
        try
        {
            var response=await Send(Envelope("History",WireJson.Serialize(new HistoryPayload{AfterSequence=historyCursor,Limit=100})),lifetime.Token);
            var entries=string.IsNullOrEmpty(response.HistoryJson)?Array.Empty<GameHistoryEntry>():WireJson.Deserialize<GameHistoryEntry[]>(response.HistoryJson);
            main.Enqueue(()=>
            {
                if(current!=generation)return;historyBusy=false;
                if(epoch!=connectionEpoch||!response.Success){RetryHistory();return;}
                History=History.Concat(entries).GroupBy(e=>e.Sequence).Select(g=>g.First()).OrderBy(e=>e.Sequence).ToArray();historyCursor=Math.Max(historyCursor,response.HistoryCursor);
                historyRetryDelay=2;nextHistoryAt=0;historyDirty|=response.HasMoreHistory||Revision>requestedRevision;
                if(historyDirty)FetchHistory();
            });
        }
        catch(Exception){main.Enqueue(()=>{if(current==generation){historyBusy=false;RetryHistory();}});}
    }
    void RetryHistory(){historyDirty=true;nextHistoryAt=Time.unscaledTime+historyRetryDelay;historyRetryDelay=Math.Min(30,historyRetryDelay*2);}
    async void PollState()
    {
        syncBusy=true;int current=generation,epoch=connectionEpoch;
        try
        {
            var response=await Send(Envelope("Get"),lifetime.Token);
            main.Enqueue(()=>{if(current!=generation)return;syncBusy=false;if(epoch!=connectionEpoch)return;TransportMode="HTTPS 安全同步（每秒）";Apply(response);});
        }
        catch(Exception ex){main.Enqueue(()=>{if(current!=generation)return;syncBusy=false;if(epoch==connectionEpoch){Diagnose("HttpsSync",ex);Disconnected("同步连接中断，原席位与待决保留，正在重连。");}});}
    }
    async void StartSocket()
    {
        socketConnected=false;socket?.Abort();socket?.Dispose();var ws=new ClientWebSocket();socket=ws;
        int current=generation;var cancel=lifetime.Token;
        if(!string.IsNullOrEmpty(Fingerprint)){string pin=Fingerprint;ws.Options.RemoteCertificateValidationCallback=(sender,certificate,chain,errors)=>certificate!=null&&CertificateMatches(certificate.GetRawCertData(),pin);}
        ws.Options.KeepAliveInterval=TimeSpan.FromSeconds(15);
        try
        {
            await ws.ConnectAsync(new Uri("wss"+Endpoint.Substring(5)+"/ws"),cancel);
            main.Enqueue(()=>{if(current==generation&&socket==ws){socketConnected=true;TransportMode="WSS 安全同步";}});
            var heartbeat=SocketHeartbeat(ws,current,cancel);
            var buffer=new byte[16384];
            while(!cancel.IsCancellationRequested&&ws.State==WebSocketState.Open)
            {
                using(var message=new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do{result=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),cancel);if(result.MessageType==WebSocketMessageType.Close)throw new IOException("连接关闭。");if(result.MessageType!=WebSocketMessageType.Text||message.Length+result.Count>NetworkProtocol.MaximumResponseBytes)throw new IOException("无效消息。");message.Write(buffer,0,result.Count);}while(!result.EndOfMessage);
                    var response=WireJson.Deserialize<NetworkResponse>(Encoding.UTF8.GetString(message.ToArray()));
                    main.Enqueue(()=>{if(current==generation&&socket==ws)Apply(response);});
                }
            }
            await heartbeat;
        }
        catch(Exception ex){main.Enqueue(()=>{if(current==generation&&socket==ws){Diagnose("WebSocket",ex);socketConnected=false;TransportMode="HTTPS 安全同步（每秒）";Status="此运行环境的 WSS 未连接，已改用同一证书校验的 HTTPS 持续同步。";nextSyncAt=0;}});}
        finally{ws.Abort();main.Enqueue(()=>{if(current==generation&&socket==ws){socketConnected=false;nextSyncAt=0;}});}
    }
    async Task SocketHeartbeat(ClientWebSocket ws,int current,CancellationToken cancel)
    {
        try
        {
            while(current==generation&&!cancel.IsCancellationRequested&&ws.State==WebSocketState.Open)
            {
                // Envelope reads are marshalled through Update so seat credentials never race a reconnect.
                var source=new TaskCompletionSource<string>();main.Enqueue(()=>{if(current==generation)source.TrySetResult(WireJson.Serialize(Envelope("Get")));else source.TrySetCanceled();});
                var text=await source.Task;var bytes=Encoding.UTF8.GetBytes(text);await ws.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,cancel);await Task.Delay(1000,cancel);
            }
        }
        catch(Exception){ws.Abort();}
    }
    void Disconnected(string message){Connected=false;if(automaticReconnect)Status=message;retryAt=Time.unscaledTime+3;PreviewChanged?.Invoke();}
    string IdentityPath
    {
        get{using(var sha=SHA256.Create())return Path.Combine(storageDirectory,BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Endpoint??""))).Replace("-","")+".credential");}
    }
    bool SaveIdentity()
    {
        try{Directory.CreateDirectory(storageDirectory);M7CredentialStore.Write(IdentityPath,string.Join("\n",RoomId??"",credential??"",Fingerprint??"",rememberedRuleSet??""));return true;}
        catch(Exception){Status+=" 此设备无法安全保存恢复凭据，请保留本次待确认请求。";return false;}
    }
    void LoadIdentity()
    {
        try{if(File.Exists(IdentityPath)){var lines=M7CredentialStore.Read(IdentityPath).Split('\n');if(lines.Length==4&&lines[2]==Fingerprint){RoomId=lines[0];credential=lines[1];rememberedRuleSet=lines[3];Status+=" 已找到原席位凭据，可重连或退出原房间。";}}}catch(Exception){Status+=" 原恢复凭据不可读取。";}
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
    public void EndSession()
    {
        bool hadGame=Playing;
        generation++;connectionEpoch++;lifetime?.Cancel();socket?.Abort();socket?.Dispose();socket=null;http?.Dispose();http=null;lifetime?.Dispose();lifetime=null;
        Playing=false;Connected=false;Busy=false;automaticReconnect=true;socketConnected=syncBusy=commandSending=false;TransportMode="未连接";Room=null;Rooms=Array.Empty<RoomSummary>();RoomId=SeatId=credential=connectionId=viewJson=rememberedRuleSet=null;decodedView=null;Revision=-1;History=Array.Empty<GameHistoryEntry>();historyCursor=0;pendingCommand=pendingEnrollment=null;historyBusy=historyDirty=previewBusy=false;nextHistoryAt=0;historyRetryDelay=2;previews.Clear();previewPending.Clear();previewQueue.Clear();
        if(hadGame)SessionEnded?.Invoke();
    }
    void OnDestroy(){EndSession();}
}
