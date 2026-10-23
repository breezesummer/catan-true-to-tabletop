using System.Net.WebSockets;
using System.Diagnostics;
using System.Text;
using Catan.Network.Contracts;
using Catan.Rooms;

namespace Catan.Server;

public sealed class NetworkTransport(RoomDirectory rooms, ServerOptions options, CommunicationDiagnostics? diagnostics = null)
{
    private readonly PeerRateLimiter requests = new();
    private readonly SemaphoreSlim socketSlots = new(options.MaxConnections);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task HttpAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        NetworkRequest? request = null;
        NetworkResponse? response = null;
        long sent = 0;
        string? exceptionType = null;
        var peer = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!requests.Allow(peer, options.RequestLimitPer10Seconds, 10)) { context.Response.StatusCode = 429; diagnostics?.Record(new() { Event = "request.rejected", Transport = "http", Code = "RateLimited" }); await WriteHttp(context, Error("RateLimited", "请求过快，请稍后重试。")); return; }
        if (context.Request.ContentLength > options.MaxRequestBytes) { context.Response.StatusCode = 413; diagnostics?.Record(new() { Event = "request.rejected", Transport = "http", Code = "MessageTooLarge", Bytes = context.Request.ContentLength }); await WriteHttp(context, Error("MessageTooLarge", "请求超过大小上限。")); return; }
        try
        {
            using var input = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                if (input.Length + count > options.MaxRequestBytes) { context.Response.StatusCode = 413; response = Error("MessageTooLarge", "请求超过大小上限。"); sent = await WriteHttp(context, response); return; }
                input.Write(buffer, 0, count);
            }
            request = WireJson.Deserialize<NetworkRequest>(StrictUtf8.GetString(input.ToArray()));
            diagnostics?.Record(CommunicationDiagnostics.Request("request.received", "http", null, request) with { Bytes = input.Length });
            response = Dispatch(request, "http");
            sent = await WriteHttp(context, response);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { exceptionType = "RequestAborted"; }
        catch (Exception error)
        {
            exceptionType = error.GetType().Name;
            if (!context.Response.HasStarted)
            { context.Response.StatusCode = 400; response = Error("InvalidMessage", "请求格式无效，未执行操作。", request?.RequestId); sent = await WriteHttp(context, response); }
        }
        finally
        {
            diagnostics?.Record(CommunicationDiagnostics.Request("http.completed", "http", null, request, response) with
            { Bytes = sent, ElapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds, ExceptionType = exceptionType });
        }
    }

    private NetworkResponse Dispatch(NetworkRequest? request, string transport, string? socketId = null)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Operation)) return Error("InvalidMessage", "缺少请求操作。");
        var started = Stopwatch.GetTimestamp();
        NetworkResponse response;
        string? exceptionType = null;
        try { response = rooms.Handle(request, observedClientActivity: transport != "push"); }
        catch (Exception error) { exceptionType = error.GetType().Name; response = Error("ServiceUnavailable", "服务暂时无法处理，请保留原命令编号后重试。", request.RequestId); }
        diagnostics?.Record(CommunicationDiagnostics.Request("request.dispatched", transport, socketId, request, response) with
        { ElapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds, ExceptionType = exceptionType });
        return response;
    }

    public async Task WebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest || context.Request.Headers.ContainsKey("Origin")) { context.Response.StatusCode = 400; return; }
        if (context.Request.QueryString.HasValue) { context.Response.StatusCode = 400; return; }
        var peer = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!requests.Allow(peer, options.RequestLimitPer10Seconds, 10)) { context.Response.StatusCode = 429; diagnostics?.Record(new() { Event = "socket.rejected", Transport = "ws", Code = "RateLimited" }); return; }
        if (!await socketSlots.WaitAsync(0, context.RequestAborted)) { context.Response.StatusCode = 503; diagnostics?.Record(new() { Event = "socket.rejected", Transport = "ws", Code = "ConnectionLimit" }); return; }
        var socketId = Guid.NewGuid().ToString("N");
        var started = Stopwatch.GetTimestamp();
        var closure = new CloseReason();
        var binding = new SocketBinding();
        try
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            using var sendGate = new SemaphoreSlim(1, 1);
            using var output = new SocketOutbox();
            diagnostics?.Record(new() { Event = "socket.opened", Transport = "ws", SocketId = socketId });
            void Enqueue(NetworkResponse response, NetworkRequest? request, bool push, byte[]? encoded)
            {
                var bytes = encoded ?? Encoding.UTF8.GetBytes(WireJson.Serialize(response));
                var metadata = CommunicationDiagnostics.Request("response.queued", "ws", socketId, request, response);
                if (bytes.Length > NetworkProtocol.MaximumResponseBytes)
                { closure.Set("ResponseTooLarge"); lifetime.Cancel(); return; }
                if (!output.TryWrite(new(bytes, response, metadata, push), out var coalesced))
                {
                    closure.Set("ResponseQueueFull");
                    diagnostics?.Record(metadata with { Event = "response.queue_full", Bytes = bytes.Length, QueueDepth = output.Count });
                    lifetime.Cancel(); return;
                }
                diagnostics?.Record(metadata with { Event = coalesced ? "push.coalesced" : "response.queued", Bytes = bytes.Length, QueueDepth = output.Count });
            }
            var sender = SendLoop(socket, binding, output, sendGate, closure, lifetime.Token);
            var receiver = ReceiveLoop(socket, binding, peer, socketId, Enqueue, sendGate, closure, lifetime.Token);
            var updates = PushLoop(binding, socketId, Enqueue, closure, lifetime.Token);
            try
            {
                var completed = await Task.WhenAny(sender, receiver, updates);
                if (completed.Exception?.GetBaseException() is Exception error)
                    closure.Set(error is WebSocketException ws ? "WebSocket:" + ws.WebSocketErrorCode : "TransportFailure");
            }
            finally
            {
                lifetime.Cancel();
                socket.Abort();
                try { await Task.WhenAll(sender, receiver, updates); }
                catch (Exception error)
                {
                    closure.Set(error is OperationCanceledException ? "Cancelled" : error is WebSocketException ws ? "WebSocket:" + ws.WebSocketErrorCode : "TransportFailure");
                    // 关闭握手后取消其余循环属于正常清理；关闭原因保留在 socket.closed。
                    if (error is not OperationCanceledException && !(closure.Value?.StartsWith("PeerClose:", StringComparison.Ordinal) ?? false))
                        diagnostics?.Record(new() { Event = "socket.failure", Transport = "ws", SocketId = socketId, Code = closure.Value, ExceptionType = error.GetType().Name });
                }
                // 仅传输关闭时保留 45 秒在线宽限。HTTP 或新 WS 可继续同一席位，
                // 避免旧 WS 清理把已恢复的连接错误转交 AI；主动离开由 Leave/Revoke 处理。
            }
        }
        catch (OperationCanceledException) { closure.Set("RequestAborted"); }
        catch (WebSocketException) { closure.Set("WebSocketAcceptFailure"); }
        finally
        {
            diagnostics?.Record(CommunicationDiagnostics.Request("socket.closed", "ws", socketId, binding.Read()) with
            { Code = closure.Value ?? "TransportEnded", ElapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds });
            socketSlots.Release();
        }
    }

    private async Task ReceiveLoop(WebSocket socket, SocketBinding binding, string peer, string socketId, Action<NetworkResponse, NetworkRequest?, bool, byte[]?> enqueue, SemaphoreSlim sendGate, CloseReason closure, CancellationToken cancellation)
    {
        var buffer = new byte[8192];
        while (!cancellation.IsCancellationRequested)
        {
            using var message = new MemoryStream();
            using var receiveDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            if (binding.Read() == null) receiveDeadline.CancelAfter(TimeSpan.FromSeconds(10));
            WebSocketReceiveResult part;
            do
            {
                try { part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), receiveDeadline.Token); }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { closure.Set("ReceiveDeadline"); throw; }
                if (part.MessageType == WebSocketMessageType.Close)
                {
                    closure.Set("PeerClose:" + ((int?)part.CloseStatus)?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    using var closeDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    closeDeadline.CancelAfter(TimeSpan.FromSeconds(3));
                    await sendGate.WaitAsync(closeDeadline.Token);
                    try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "连接已关闭。", closeDeadline.Token); }
                    finally { sendGate.Release(); }
                    return;
                }
                if (part.MessageType != WebSocketMessageType.Text || message.Length + part.Count > options.MaxRequestBytes)
                { closure.Set(part.MessageType != WebSocketMessageType.Text ? "InvalidMessageType" : "MessageTooLarge"); return; }
                message.Write(buffer, 0, part.Count);
                if (!part.EndOfMessage) receiveDeadline.CancelAfter(TimeSpan.FromSeconds(10));
            } while (!part.EndOfMessage);
            if (!requests.Allow(peer, options.RequestLimitPer10Seconds, 10)) { enqueue(Error("RateLimited", "请求过快，请稍后重试。"), null, false, null); continue; }
            NetworkRequest request;
            try { request = WireJson.Deserialize<NetworkRequest>(StrictUtf8.GetString(message.ToArray())); }
            catch { enqueue(Error("InvalidMessage", "请求格式无效，未执行操作。"), null, false, null); continue; }
            if (request == null) { enqueue(Error("InvalidMessage", "请求不能为空。"), null, false, null); continue; }
            diagnostics?.Record(CommunicationDiagnostics.Request("request.received", "ws", socketId, request) with { Bytes = message.Length });
            var current = binding.Read();
            bool mayChangeRoom = request.Operation == "Create" || request.Operation == "Join" || request.Operation == "Reconnect";
            bool publicQuery = request.Operation == "List" || request.Operation == "Describe";
            if (current != null && !mayChangeRoom && !publicQuery && (request.ConnectionId != current.ConnectionId || request.Credential != current.Credential || request.RoomId != current.RoomId))
            {
                enqueue(Error("ConnectionMismatch", "此连接已绑定原席位，请重新建立连接。", request.RequestId), request, false, null);
                continue;
            }
            var response = Dispatch(request, "ws", socketId);
            if (response.Success && !string.IsNullOrEmpty(response.ConnectionId) && !string.IsNullOrEmpty(response.RoomId))
            {
                // 登记流程的长期凭据由客户端预先保存，成功回复不会再回传。
                var credential = string.IsNullOrEmpty(response.Credential) ? mayChangeRoom && request.EnrollmentCredential != null ? request.EnrollmentCredential : request.Credential : response.Credential;
                if (current?.ConnectionId != response.ConnectionId || current.RoomId != response.RoomId)
                    binding.Write(new NetworkRequest { Operation = "Get", RoomId = response.RoomId, ConnectionId = response.ConnectionId, Credential = credential });
                if (current != null && current.ConnectionId != response.ConnectionId) rooms.Disconnect(current.ConnectionId);
            }
            if (response.Success && (request.Operation == "Leave" || request.Operation == "Revoke")) binding.Write(null);
            // 先完成席位绑定与旧房连接清理，再发送成功回执，避免客户端已切房而服务端仍显示旧席位在线。
            enqueue(response, request, false, null);
        }
    }

    private async Task PushLoop(SocketBinding binding, string socketId, Action<NetworkResponse, NetworkRequest?, bool, byte[]?> enqueue, CloseReason closure, CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(750));
        byte[]? previous = null;
        while (await timer.WaitForNextTickAsync(cancellation))
        {
            var request = binding.Read();
            if (request == null) { previous = null; continue; }
            var response = Dispatch(request, "push", socketId);
            // 切房与推送可同时发生，旧房快照不能覆盖已经切换的新房界面。
            if (!ReferenceEquals(request, binding.Read())) continue;
            response.RequestId = null;
            // 推送仍经过身份与当前控制权检查，只生成绑定席位的可见视图。
            if (response.Success) response.Code = "Push";
            var encoded = Encoding.UTF8.GetBytes(WireJson.Serialize(response));
            if (previous == null || !encoded.AsSpan().SequenceEqual(previous)) { enqueue(response, request, true, encoded); previous = encoded; }
            if (!response.Success) { closure.Set("PushRejected:" + response.Code); return; }
        }
    }

    private async Task SendLoop(WebSocket socket, SocketBinding binding, SocketOutbox output, SemaphoreSlim sendGate, CloseReason closure, CancellationToken cancellation)
    {
        string? sentRoom = null;
        long sentRevision = 0;
        while (!cancellation.IsCancellationRequested)
        {
            var message = await output.ReadAsync(cancellation);
            if (message.IsPush && (message.Response.ConnectionId != binding.Read()?.ConnectionId ||
                message.Response.RoomId == sentRoom && message.Response.Revision < sentRevision))
            {
                diagnostics?.Record(message.Diagnostic with { Event = "push.superseded", Bytes = message.Bytes.Length });
                continue;
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            var started = Stopwatch.GetTimestamp();
            var waited = Stopwatch.GetElapsedTime(message.QueuedAt).TotalMilliseconds;
            try
            {
                await sendGate.WaitAsync(deadline.Token);
                try { await socket.SendAsync(message.Bytes, WebSocketMessageType.Text, true, deadline.Token); }
                finally { sendGate.Release(); }
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            { closure.Set("SendDeadline"); diagnostics?.Record(message.Diagnostic with { Event = "response.send_timeout", Bytes = message.Bytes.Length, WaitMs = waited }); throw; }
            if (message.Response.RoomId != null) { sentRoom = message.Response.RoomId; sentRevision = message.Response.Revision; }
            diagnostics?.Record(message.Diagnostic with
            { Event = "response.sent", Bytes = message.Bytes.Length, QueueDepth = output.Count, WaitMs = waited, ElapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds });
        }
    }

    private static async Task<long> WriteHttp(HttpContext context, NetworkResponse response)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        var encoded = WireJson.Serialize(response);
        if (Encoding.UTF8.GetByteCount(encoded) > NetworkProtocol.MaximumResponseBytes) { context.Response.StatusCode = 503; encoded = WireJson.Serialize(Error("ResponseTooLarge", "可见局面超过传输上限，房间已保留，请联系维护者。")); }
        await context.Response.WriteAsync(encoded, context.RequestAborted);
        return Encoding.UTF8.GetByteCount(encoded);
    }

    private static NetworkResponse Error(string code, string message, string? requestId = null) => new() { Success = false, Code = code, Message = message, RequestId = requestId };

    private sealed class SocketBinding
    {
        private NetworkRequest? current;
        public NetworkRequest? Read() => Volatile.Read(ref current);
        public void Write(NetworkRequest? value) => Volatile.Write(ref current, value);
    }
    private sealed class CloseReason
    {
        private string? value;
        public string? Value => Volatile.Read(ref value);
        public void Set(string reason) => Interlocked.CompareExchange(ref value, reason, null);
    }
}
