using System.Diagnostics;

namespace CashFlow.LoadTest;

internal sealed class LoadRunner
{
    private readonly SemaphoreSlim _inFlight;

    public LoadRunner(int maxInFlight)
    {
        _inFlight = new(maxInFlight, maxInFlight);
    }

    public async Task RunAsync(int rps, TimeSpan duration, Metrics metrics, Func<long, CancellationToken, Task<(bool Success, string Outcome)>> action, CancellationToken ct)
    {
        if (rps <= 0)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(1.0 / rps);
        var total = (long)(rps * duration.TotalSeconds);
        var pending = new List<Task>((int)Math.Min(total, 1_000_000));
        var clock = Stopwatch.StartNew();

        for (long i = 0; i < total && !ct.IsCancellationRequested; i++)
        {
            var due = interval * i;
            var wait = due - clock.Elapsed;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct);
            }

            if (!_inFlight.Wait(0, CancellationToken.None))
            {
                metrics.Record(0, success: false, "dropped");
                continue;
            }

            var n = i;
            pending.Add(Task.Run(
                async () =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var (success, outcome) = await action(n, ct);
                        metrics.Record(sw.Elapsed.TotalMilliseconds, success, outcome);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                    {
                        metrics.Record(sw.Elapsed.TotalMilliseconds, success: false, ex.GetType().Name);
                    }
                    finally
                    {
                        _inFlight.Release();
                    }
                },
                CancellationToken.None));
        }

        await Task.WhenAll(pending);
    }
}
