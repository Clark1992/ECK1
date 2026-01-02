using System.Collections.Concurrent;
using System.Diagnostics;

namespace ECK1.TestPlatform.Services;

public sealed class LoadRunner
{
    public async Task<LoadRunSummary> RunAsync(
        int count,
        int concurrency,
        double? rps,
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

        var schedule = CreateScheduler(rps, minRate, maxRate, rateChangeSec);

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

    private static IScheduler CreateScheduler(double? rps, double? minRate, double? maxRate, int? rateChangeSec)
    {
        if (minRate is not null || maxRate is not null)
        {
            if (minRate is null || maxRate is null)
                throw new ArgumentException("Both minRate and maxRate must be provided when using varying rate.");

            if (minRate <= 0 || maxRate <= 0)
                throw new ArgumentException("minRate and maxRate must be > 0.");

            var change = rateChangeSec ?? 10;
            if (change <= 0)
                throw new ArgumentException("rateChangeSec must be > 0.");

            var min = Math.Min(minRate.Value, maxRate.Value);
            var max = Math.Max(minRate.Value, maxRate.Value);

            if (Math.Abs(max - min) < 0.000001)
                return new FixedRateScheduler(min);

            return new StepVaryingRateScheduler(min, max, TimeSpan.FromSeconds(change));
        }

        if (rps is null || rps <= 0)
            return new NoopScheduler();

        return new FixedRateScheduler(rps.Value);
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

    private sealed class StepVaryingRateScheduler : IScheduler
    {
        private readonly double _minRps;
        private readonly double _maxRps;
        private readonly TimeSpan _hold;
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private bool _useMax;
        private TimeSpan _nextSwitchAt;
        private TimeSpan _nextDue;

        public StepVaryingRateScheduler(double minRps, double maxRps, TimeSpan holdDuration)
        {
            _minRps = minRps;
            _maxRps = maxRps;
            _hold = holdDuration;
            _useMax = false;
            _nextSwitchAt = _hold;
            _nextDue = TimeSpan.Zero;
        }

        public async Task WaitTurnAsync(int i, CancellationToken ct)
        {
            var elapsed = _sw.Elapsed;

            while (elapsed >= _nextSwitchAt)
            {
                _useMax = !_useMax;
                _nextSwitchAt += _hold;
            }

            var rps = _useMax ? _maxRps : _minRps;
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
