using Catan.Rooms;
using System.Diagnostics;

namespace Catan.Server;

public sealed class RoomAiService(RoomDirectory rooms, ServerOptions options, CommunicationDiagnostics diagnostics) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.AiIntervalMs));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                var advanced = await rooms.TickAiAsync(stoppingToken);
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (advanced > 0 || elapsed >= 100)
                    diagnostics.Record(new() { Event = "rooms.ai_tick", ElapsedMs = elapsed, QueueDepth = advanced, Success = true });
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { diagnostics.Record(new() { Event = "rooms.ai_tick", Success = false, ElapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds, ExceptionType = error.GetType().Name }); }
        }
    }
}
