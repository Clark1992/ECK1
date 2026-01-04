using System.Diagnostics;
using System.Threading.Channels;

namespace ECK1.TestPlatform.Services;

public sealed class InterleavedCreateUpdateRunner(LoadRunner runner)
{
    public async Task<InterleavedCreateUpdateResult<TId>> RunAsync<TId>(
        int createCount,
        int updatesPerEntity,
        int concurrency,
        double? minRate,
        double? maxRate,
        int? rateChangeSec,
        Func<CancellationToken, Task<TId?>> createAsync,
        Func<TId, CancellationToken, Task<bool>> updateAsync,
        int previewLimit,
        CancellationToken ct)
        where TId : struct
    {
        createCount = Math.Max(0, createCount);
        updatesPerEntity = Math.Max(1, updatesPerEntity);
        previewLimit = Math.Clamp(previewLimit, 0, 10_000);

        var updateCount = createCount * updatesPerEntity;
        var totalOps = createCount + updateCount;
        if (totalOps <= 0)
        {
            var empty = new LoadRunSummary(0, 0, 0, 0, 0);
            return new InterleavedCreateUpdateResult<TId>(empty, empty, []);
        }

        // We enqueue each created ID updatesPerEntity times.
        // Update ops then just consume IDs from the channel.
        var updateIds = Channel.CreateUnbounded<TId>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

        var createsRemaining = createCount;
        var createdPreview = new List<TId>(Math.Min(previewLimit, createCount));

        var createStarted = 0;
        var createSucceeded = 0;
        var createFailed = 0;
        var updateStarted = 0;
        var updateSucceeded = 0;
        var updateFailed = 0;

        var createLatenciesMs = new List<long>(Math.Min(createCount, 10_000));
        var updateLatenciesMs = new List<long>(Math.Min(updateCount, 10_000));

        var plan = BuildInterleavedPlan(createCount, updateCount, concurrency);

        var totalSw = Stopwatch.StartNew();

        await runner.RunAsync(
            totalOps,
            concurrency,
            minRate,
            maxRate,
            rateChangeSec,
            async (op_index, token) =>
            {
                var op = plan[op_index];
                if (op == OpKind.Create)
                {
                    Interlocked.Increment(ref createStarted);
                    var opSw = Stopwatch.StartNew();
                    try
                    {
                        var createdId = await createAsync(token);
                        var ok = createdId is not null;

                        if (!ok)
                        {
                            Interlocked.Increment(ref createFailed);
                            return false;
                        }

                        Interlocked.Increment(ref createSucceeded);

                        lock (createdPreview)
                        {
                            if (createdPreview.Count < previewLimit)
                                createdPreview.Add(createdId!.Value);
                        }

                        // Feed update IDs.
                        for (var i = 0; i < updatesPerEntity; i++)
                        {
                            await updateIds.Writer.WriteAsync(createdId!.Value, token);
                        }

                        return true;
                    }
                    catch
                    {
                        Interlocked.Increment(ref createFailed);
                        return false;
                    }
                    finally
                    {
                        opSw.Stop();
                        lock (createLatenciesMs)
                        {
                            if (createLatenciesMs.Count < 100_000)
                                createLatenciesMs.Add(opSw.ElapsedMilliseconds);
                        }

                        if (Interlocked.Decrement(ref createsRemaining) == 0)
                        {
                            updateIds.Writer.TryComplete();
                        }
                    }
                }

                // Update
                Interlocked.Increment(ref updateStarted);
                var updSw = Stopwatch.StartNew();
                try
                {
                    while (await updateIds.Reader.WaitToReadAsync(token))
                    {
                        if (updateIds.Reader.TryRead(out var id))
                        {
                            var ok = await updateAsync(id, token);
                            if (ok) Interlocked.Increment(ref updateSucceeded);
                            else Interlocked.Increment(ref updateFailed);
                            return ok;
                        }
                    }

                    // No more IDs (e.g. all creates failed)
                    Interlocked.Increment(ref updateFailed);
                    return false;
                }
                catch
                {
                    Interlocked.Increment(ref updateFailed);
                    return false;
                }
                finally
                {
                    updSw.Stop();
                    lock (updateLatenciesMs)
                    {
                        if (updateLatenciesMs.Count < 100_000)
                            updateLatenciesMs.Add(updSw.ElapsedMilliseconds);
                    }
                }
            },
            ct);

        totalSw.Stop();

        var totalMs = totalSw.Elapsed.TotalMilliseconds;
        var denomSeconds = Math.Max(0.001, totalMs / 1000.0);

        double createAvg;
        lock (createLatenciesMs)
        {
            createAvg = createLatenciesMs.Count == 0 ? 0 : createLatenciesMs.Average();
        }

        double updateAvg;
        lock (updateLatenciesMs)
        {
            updateAvg = updateLatenciesMs.Count == 0 ? 0 : updateLatenciesMs.Average();
        }

        var createSummary = new LoadRunSummary(
            Started: createStarted,
            Succeeded: createSucceeded,
            Failed: createFailed,
            AvgLatencyMs: createAvg,
            AchievedRps: createStarted / denomSeconds);

        var updateSummary = new LoadRunSummary(
            Started: updateStarted,
            Succeeded: updateSucceeded,
            Failed: updateFailed,
            AvgLatencyMs: updateAvg,
            AchievedRps: updateStarted / denomSeconds);

        return new InterleavedCreateUpdateResult<TId>(createSummary, updateSummary, createdPreview);
    }

    private enum OpKind
    {
        Create,
        Update,
    }

    private static OpKind[] BuildInterleavedPlan(int createCount, int updateCount, int concurrency)
    {
        // Important: updates may block waiting for IDs. If we schedule too many updates in a row
        // while creates still remain, we can deadlock (all concurrency slots blocked on updates).
        // So we cap the maximum consecutive updates to (concurrency - 1) until all creates are scheduled.
        concurrency = Math.Clamp(concurrency, 1, 1024);
        var maxConsecutiveUpdates = Math.Max(0, concurrency - 1);

        var plan = new OpKind[createCount + updateCount];

        var remainingCreates = createCount;
        var remainingUpdates = updateCount;
        var consecutiveUpdates = 0;

        for (var i = 0; i < plan.Length; i++)
        {
            var mustCreate = remainingCreates > 0 && consecutiveUpdates >= maxConsecutiveUpdates;
            var canCreate = remainingCreates > 0;
            var canUpdate = remainingUpdates > 0;

            OpKind next;
            if (!canUpdate)
            {
                next = OpKind.Create;
            }
            else if (!canCreate)
            {
                next = OpKind.Update;
            }
            else if (mustCreate)
            {
                next = OpKind.Create;
            }
            else
            {
                // Keep the plan roughly proportional while still being safe.
                var pCreate = remainingCreates / (double)(remainingCreates + remainingUpdates);
                next = Random.Shared.NextDouble() < pCreate ? OpKind.Create : OpKind.Update;
            }

            plan[i] = next;
            if (next == OpKind.Create)
            {
                remainingCreates--;
                consecutiveUpdates = 0;
            }
            else
            {
                remainingUpdates--;
                consecutiveUpdates++;
            }
        }

        return plan;
    }
}

public sealed record InterleavedCreateUpdateResult<TId>(
    LoadRunSummary CreateSummary,
    LoadRunSummary UpdateSummary,
    List<TId> CreatedIdsPreview);
