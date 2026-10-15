using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Catan.Network.Contracts;
using Catan.Rooms;

namespace Catan.Server;

public sealed class NetworkTransport(RoomDirectory rooms, ServerOptions options)
{
    private readonly PeerRateLimiter requests = new();
    private readonly SemaphoreSlim socketSlots = new(options.MaxConnections);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task HttpAsync(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!requests.Allow(peer, options.RequestLimitPer10Seconds, 10)) { context.Response.StatusCode = 429; await WriteHttp(context, Error("RateLimited", "请求过快，请稍后重试。")); return; }
        if (context.Request.ContentLength > options.MaxRequestBytes) { context.Response.StatusCode = 413; await WriteHttp(context, Error("MessageTooLarge", "请求超过大小上限。")); return; }
        try
        {
            using var input = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                if (input.Length + count > options.MaxRequestBytes) { context.Response.StatusCode = 413; await WriteHttp(context, Error("MessageTooLarge", "请求超过大小上限。")); return; }
                input.Write(buffer, 0, count);
            }
            var request = WireJson.Deserialize<NetworkRequest>(StrictUtf8.GetString(input.ToArray()));
            var response = Dispatch(request);
            await WriteHttp(context, response);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch { context.Response.StatusCode = 400; await WriteHttp(context, Error("InvalidMessage", "请求格式无效，未执行操作。")); }
    }

    private NetworkResponse Dispatch(NetworkRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Operation)) return Error("InvalidMessage", "缺少请求操作。");
        try { return rooms.Handle(request); }
        catch { return Error("ServiceUnavailable", "服务暂时无法处理，请保留原命令编号后重试。", request.RequestId); }
    }

    public async Task WebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest || context.Request.Headers.ContainsKey("Origin")) { context.Response.StatusCode = 400; return; }
        if (context.Request.QueryString.HasValue) { context.Response.StatusCode = 400; return; }
        var peer = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!requests.Allow(peer, options.RequestLimitPer10Seconds, 10)) { context.Response.StatusCode = 429; return; }
        if (!await socketSlots.WaitAsync(0, context.RequestAborted)) { context.Response.StatusCode = 503; return; }
        try
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            using var sendGate = new SemaphoreSlim(1, 1);
            var output = Channel.CreateBounded<string>(new BoundedChannelOptions(8) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
            var binding = new SocketBinding();
            void Enqueue(NetworkResponse response)
            {
                var message = WireJson.Serialize(response);
                if (Encoding.UTF8.GetByteCount(message) > NetworkProtocol.MaximumResponseBytes || !output.Writer.TryWrite(message)) lifetime.Cancel();
            }
            var sender = SendLoop(socket, output.Reader, sendGate, lifetime.Token);
            var receiver = ReceiveLoop(socket, binding, peer, Enqueue, sendGate, lifetime.Token);
            var updates = PushLoop(binding, Enqueue, lifetime.Token);
            try { await Task.WhenAny(sender, receiver, updates); }
            finally
            {
                lifetime.Cancel();
                output.Writer.TryComplete();
                socket.Abort();
                try { await Task.WhenAll(sender, receiver, updates); } catch { /* 断开原因不包含消息内容。 */ }
                var previous = binding.Read();
                if (previous != null) rooms.Disconnect(previous.ConnectionId);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally { socketSlots.Release(); }
    }

    private async Task ReceiveLoop(WebSocket socket, SocketBinding binding, string peer, Action<NetworkResponse> enqueue, SemaphoreSlim sendGate, CancellationToken cancellation)
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
                part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), receiveDeadline.Token);
                if (part.MessageType == WebSocketMessageType.Close)
                {
                    using var closeDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    closeDeadline.CancelAfter(TimeSpan.FromSeconds(3));
                    await sendGate.WaitAsync(closeDeadline.Token);
                    try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "连接已关闭。", closeDeadline.Token); }
                    finally { sendGate.Release(); }
                    return;
                }
                if (part.MessageType != WebSocketMessageType.Text || message.Length + part.Count > options.MaxRequestBytes) return;
                message.Write(buffer, 0, part.Count);
                if (!part.EndOfMessage) receiveDeadline.CancelAfter(TimeSpan.FromSeconds(10));
            } while (!part.EndOfMessage);
            if (!requests.Allow(peer, options.RequestLimitPer10Seconds, 10)) { enqueue(Error("RateLimited", "请求过快，请稍后重试。")); continue; }
            NetworkRequest request;
            try { request = WireJson.Deserialize<NetworkRequest>(StrictUtf8.GetString(message.ToArray())); }
            catch { enqueue(Error("InvalidMessage", "请求格式无效，未执行操作。")); continue; }
            if (request == null) { enqueue(Error("InvalidMessage", "请求不能为空。")); continue; }
            var current = binding.Read();
            if (current != null && request.Operation != "List" && (request.ConnectionId != current.ConnectionId || request.Credential != current.Credential || request.RoomId != current.RoomId))
            {
                enqueue(Error("ConnectionMismatch", "此连接已绑定原席位，请重新建立连接。", request.RequestId));
                continue;
            }
            var response = Dispatch(request);
            enqueue(response);
            if (response.Success && !string.IsNullOrEmpty(response.ConnectionId) && !string.IsNullOrEmpty(response.RoomId))
            {
                // 登记流程的长期凭据由客户端预先保存，成功回复不会再回传。
                var credential = string.IsNullOrEmpty(response.Credential) ? request.Credential ?? request.EnrollmentCredential : response.Credential;
                binding.Write(new NetworkRequest { Operation = "Get", RoomId = response.RoomId, ConnectionId = response.ConnectionId, Credential = credential });
            }
            if (response.Success && (request.Operation == "Leave" || request.Operation == "Revoke")) binding.Write(null);
        }
    }

    private async Task PushLoop(SocketBinding binding, Action<NetworkResponse> enqueue, CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(750));
        string? previous = null;
        while (await timer.WaitForNextTickAsync(cancellation))
        {
            var request = binding.Read();
            if (request == null) continue;
            var response = Dispatch(request);
            response.RequestId = null;
            // 推送仍经过身份与当前控制权检查，只生成绑定席位的可见视图。
            if (response.Success) response.Code = "Push";
            var encoded = WireJson.Serialize(response);
            if (encoded != previous) { enqueue(response); previous = encoded; }
            if (!response.Success) return;
        }
    }

    private static async Task SendLoop(WebSocket socket, ChannelReader<string> output, SemaphoreSlim sendGate, CancellationToken cancellation)
    {
        await foreach (var message in output.ReadAllAsync(cancellation))
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            await sendGate.WaitAsync(deadline.Token);
            try { await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, deadline.Token); }
            finally { sendGate.Release(); }
        }
    }

    private static async Task WriteHttp(HttpContext context, NetworkResponse response)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        var encoded = WireJson.Serialize(response);
        if (Encoding.UTF8.GetByteCount(encoded) > NetworkProtocol.MaximumResponseBytes) { context.Response.StatusCode = 503; encoded = WireJson.Serialize(Error("ResponseTooLarge", "可见局面超过传输上限，房间已保留，请联系维护者。")); }
        await context.Response.WriteAsync(encoded, context.RequestAborted);
    }

    private static NetworkResponse Error(string code, string message, string? requestId = null) => new() { Success = false, Code = code, Message = message, RequestId = requestId };

    private sealed class SocketBinding
    {
        private NetworkRequest? current;
        public NetworkRequest? Read() => Volatile.Read(ref current);
        public void Write(NetworkRequest? value) => Volatile.Write(ref current, value);
    }
}
