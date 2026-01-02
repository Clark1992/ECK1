using System.Collections.Concurrent;
using System.Diagnostics;

namespace ECK1.TestPlatform.Services;

public sealed class LoadRunner
{
    public async Task<LoadRunSummary> RunAsync(
        int count,
        int concurrency,
        double? minRate,
        double? maxRate,
        int? rateChangeSec,
        Func<int, CancellationToken, Task<bool>> operation,
        CancellationToken ct)
    {
        if (count <= 0) return new LoadRunSummary(0, 0, 0, 0, 0);
        concurrency = Math.Clamp(concurrency, 1, 1024);

        var sw = Stopwatch.StartNew();
        var semaphore = new SemaphoreSlim(concurrency, concurrency);

        var started = 0;
        var succeeded = 0;
        var failed = 0;
        var latenciesMs = new ConcurrentBag<long>();

        var tasks = new List<Task>(count);

        var schedule = CreateScheduler(minRate, maxRate, rateChangeSec);

        for (var i = 0; i < count; i++)
        {
            await schedule.WaitTurnAsync(i, ct);

            await semaphore.WaitAsync(ct);
            Interlocked.Increment(ref started);

            tasks.Add(Task.Run(async () =>
            {
                var opSw = Stopwatch.StartNew();
                try
                {
                    var ok = await operation(i, ct);
                    if (ok) Interlocked.Increment(ref succeeded);
                    else Interlocked.Increment(ref failed);
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                }
                finally
                {
                    opSw.Stop();
                    latenciesMs.Add(opSw.ElapsedMilliseconds);
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        var totalMs = sw.Elapsed.TotalMilliseconds;
        var avgLatencyMs = latenciesMs.Count == 0 ? 0 : latenciesMs.Average();
        var achievedRps = totalMs <= 0 ? 0 : (started / (totalMs / 1000.0));

        return new LoadRunSummary(
            Started: started,
            Succeeded: succeeded,
            Failed: failed,
            AvgLatencyMs: avgLatencyMs,
            AchievedRps: achievedRps);
    }

    private static IScheduler CreateScheduler(double? minRate, double? maxRate, int? rateChangeSec)
    {
        if (minRate is null && maxRate is null)
            return new NoopScheduler();

        var min = minRate ?? maxRate;
        var max = maxRate ?? minRate;

        if (min is null || max is null)
            return new NoopScheduler();

        if (min <= 0 || max <= 0)
            throw new ArgumentException("min_rate/max_rate must be > 0.");

        var change = rateChangeSec ?? 10;
        if (change <= 0)
            throw new ArgumentException("rate_change_sec must be > 0.");

        var orderedMin = Math.Min(min.Value, max.Value);
        var orderedMax = Math.Max(min.Value, max.Value);

        if (Math.Abs(orderedMax - orderedMin) < 0.000001)
            return new FixedRateScheduler(orderedMin);

        return new SmoothVaryingRateScheduler(orderedMin, orderedMax, TimeSpan.FromSeconds(change));
    }

    private interface IScheduler
    {
        Task WaitTurnAsync(int i, CancellationToken ct);
    }

    private sealed class NoopScheduler : IScheduler
    {
        public Task WaitTurnAsync(int i, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FixedRateScheduler(double rps) : IScheduler
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly TimeSpan _period = TimeSpan.FromSeconds(1.0 / Math.Max(0.1, rps));

        public async Task WaitTurnAsync(int i, CancellationToken ct)
        {
            var target = TimeSpan.FromTicks(_period.Ticks * (long)i);
            var remaining = target - _sw.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, ct);
            }
        }
    }

    private sealed class SmoothVaryingRateScheduler : IScheduler
    {
        private readonly double _minRps;
        private readonly double _maxRps;
        private readonly TimeSpan _hold;
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private TimeSpan _nextDue;

        public SmoothVaryingRateScheduler(double minRps, double maxRps, TimeSpan holdDuration)
        {
            _minRps = minRps;
            _maxRps = maxRps;
            _hold = holdDuration;
            _nextDue = TimeSpan.Zero;
        }

        public async Task WaitTurnAsync(int i, CancellationToken ct)
        {
            var elapsed = _sw.Elapsed;

            // Smoothly ramp between min and max in a triangle wave.
            var cycle = _hold.TotalSeconds * 2.0;
            var t = elapsed.TotalSeconds % cycle;
            var phase = t <= _hold.TotalSeconds ? (t / _hold.TotalSeconds) : (1.0 - ((t - _hold.TotalSeconds) / _hold.TotalSeconds));
            var rps = _minRps + ((_maxRps - _minRps) * phase);

            var period = TimeSpan.FromSeconds(1.0 / Math.Max(0.1, rps));

            _nextDue += period;

            var remaining = _nextDue - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, ct);
            }
        }
    }
}

public sealed record LoadRunSummary(
    int Started,
    int Succeeded,
    int Failed,
    double AvgLatencyMs,
    double AchievedRps);
