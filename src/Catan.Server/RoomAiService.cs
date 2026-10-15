using Catan.Rooms;

namespace Catan.Server;

public sealed class RoomAiService(RoomDirectory rooms, ServerOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.AiIntervalMs));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await rooms.TickAiAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch { Console.Error.WriteLine("房间后台行动未完成；等待下一次安全重试。详情未写入公开日志。"); }
        }
    }
}
