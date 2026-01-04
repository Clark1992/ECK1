using System.Diagnostics;
using System.Threading.Channels;

namespace ECK1.TestPlatform.Services;

/// <summary>
/// Interleaves creates and updates for two ID pools (e.g. Sample + Sample2) under one concurrency + rate schedule.
/// </summary>
public sealed class InterleavedTwoPoolCreateUpdateRunner(LoadRunner runner)
{
    public async Task<InterleavedTwoPoolCreateUpdateResult> RunAsync(
        int createCount,
        int updatesPerEntity,
        double poolBRatio,
        int concurrency,
        double? minRate,
        double? maxRate,
        int? rateChangeSec,
        Func<CancellationToken, Task<Guid?>> createAAsync,
        Func<CancellationToken, Task<Guid?>> createBAsync,
        Func<Guid, CancellationToken, Task<bool>> updateAAsync,
        Func<Guid, CancellationToken, Task<bool>> updateBAsync,
        int previewLimit,
        CancellationToken ct)
    {
        createCount = Math.Max(0, createCount);
        updatesPerEntity = Math.Max(1, updatesPerEntity);
        poolBRatio = Math.Clamp(poolBRatio, 0, 1);
        previewLimit = Math.Clamp(previewLimit, 0, 10_000);

        // Allocate per-pool counts deterministically so we never schedule updates for a pool
        // that has zero planned creates.
        var createCountB = (int)Math.Round(createCount * poolBRatio, MidpointRounding.AwayFromZero);
        createCountB = Math.Clamp(createCountB, 0, createCount);
        var createCountA = createCount - createCountB;

        var updateCountA = createCountA * updatesPerEntity;
        var updateCountB = createCountB * updatesPerEntity;

        var totalOps = createCount + updateCountA + updateCountB;
        if (totalOps <= 0)
        {
            var empty = new LoadRunSummary(0, 0, 0, 0, 0);
            return new InterleavedTwoPoolCreateUpdateResult(empty, empty, 0, 0, [], []);
        }

        var aPreview = new List<Guid>(Math.Min(previewLimit, 200));
        var bPreview = new List<Guid>(Math.Min(previewLimit, 200));

        var aCreated = 0;
        var bCreated = 0;

        var createStarted = 0;
        var createSucceeded = 0;
        var createFailed = 0;
        var updateStarted = 0;
        var updateSucceeded = 0;
        var updateFailed = 0;

        var createLatenciesMs = new List<long>(Math.Min(createCount, 10_000));
        var updateLatenciesMs = new List<long>(Math.Min(updateCountA + updateCountB, 10_000));

        var updateAIds = Channel.CreateUnbounded<Guid>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        var updateBIds = Channel.CreateUnbounded<Guid>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

        var remainingCreatesA = createCountA;
        var remainingCreatesB = createCountB;

        var plan = BuildInterleavedTwoPoolPlan(createCountA, createCountB, updateCountA, updateCountB, concurrency);

        var totalSw = Stopwatch.StartNew();

        await runner.RunAsync(
            totalOps,
            concurrency,
            minRate,
            maxRate,
            rateChangeSec,
            async (_, token) =>
            {
                var op = plan[_];
                if (op == OpKind.CreateA)
                {
                    Interlocked.Increment(ref createStarted);
                    var opSw = Stopwatch.StartNew();
                    try
                    {
                        var id = await createAAsync(token);
                        var ok = id is not null;
                        if (!ok)
                        {
                            Interlocked.Increment(ref createFailed);
                            return false;
                        }

                        Interlocked.Increment(ref createSucceeded);
                        Interlocked.Increment(ref aCreated);

                        lock (aPreview)
                        {
                            if (aPreview.Count < previewLimit)
                                aPreview.Add(id!.Value);
                        }

                        for (var i = 0; i < updatesPerEntity; i++)
                        {
                            await updateAIds.Writer.WriteAsync(id!.Value, token);
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

                        if (Interlocked.Decrement(ref remainingCreatesA) == 0)
                            updateAIds.Writer.TryComplete();
                    }
                }

                if (op == OpKind.CreateB)
                {
                    Interlocked.Increment(ref createStarted);
                    var opSw = Stopwatch.StartNew();
                    try
                    {
                        var id = await createBAsync(token);
                        var ok = id is not null;
                        if (!ok)
                        {
                            Interlocked.Increment(ref createFailed);
                            return false;
                        }

                        Interlocked.Increment(ref createSucceeded);
                        Interlocked.Increment(ref bCreated);

                        lock (bPreview)
                        {
                            if (bPreview.Count < previewLimit)
                                bPreview.Add(id!.Value);
                        }

                        for (var i = 0; i < updatesPerEntity; i++)
                        {
                            await updateBIds.Writer.WriteAsync(id!.Value, token);
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

                        if (Interlocked.Decrement(ref remainingCreatesB) == 0)
                            updateBIds.Writer.TryComplete();
                    }
                }

                // Update (consume from any pool that has IDs available)
                Interlocked.Increment(ref updateStarted);
                var updSw = Stopwatch.StartNew();
                try
                {
                    while (true)
                    {
                        if (updateAIds.Reader.TryRead(out var aId))
                        {
                            var ok = await updateAAsync(aId, token);
                            if (ok) Interlocked.Increment(ref updateSucceeded);
                            else Interlocked.Increment(ref updateFailed);

                            return ok;
                        }

                        if (updateBIds.Reader.TryRead(out var bId))
                        {
                            var ok = await updateBAsync(bId, token);

                            if (ok) Interlocked.Increment(ref updateSucceeded);
                            else Interlocked.Increment(ref updateFailed);

                            return ok;
                        }

                        // If both channels are completed and empty, there's nothing left to update.
                        if (updateAIds.Reader.Completion.IsCompleted && updateBIds.Reader.Completion.IsCompleted)
                            break;

                        // Wait until either pool has something to read (or completes).
                        var waitA = updateAIds.Reader.WaitToReadAsync(token).AsTask();
                        var waitB = updateBIds.Reader.WaitToReadAsync(token).AsTask();
                        await Task.WhenAny(waitA, waitB);
                    }

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

        return new InterleavedTwoPoolCreateUpdateResult(
            createSummary,
            updateSummary,
            aCreated,
            bCreated,
            aPreview,
            bPreview);
    }

    private enum OpKind
    {
        CreateA,
        CreateB,
        Update,
    }

    private static OpKind[] BuildInterleavedTwoPoolPlan(
        int createCountA,
        int createCountB,
        int updateCountA,
        int updateCountB,
        int concurrency)
    {
        // Same deadlock concern as the single-pool runner: updates may block waiting for IDs.
        // Ensure we never schedule too many updates in a row while creates remain.
        concurrency = Math.Clamp(concurrency, 1, 1024);
        var maxConsecutiveUpdates = Math.Max(0, concurrency - 1);

        var totalUpdates = updateCountA + updateCountB;
        var plan = new OpKind[createCountA + createCountB + totalUpdates];

        var remainingCreatesA = createCountA;
        var remainingCreatesB = createCountB;
        var remainingUpdates = totalUpdates;
        var consecutiveUpdates = 0;

        for (var i = 0; i < plan.Length; i++)
        {
            var remainingCreates = remainingCreatesA + remainingCreatesB;
            var canCreate = remainingCreates > 0;
            var canUpdate = remainingUpdates > 0;
            var mustCreate = canCreate && consecutiveUpdates >= maxConsecutiveUpdates;

            OpKind next;
            if (!canUpdate)
            {
                next = PickCreate(ref remainingCreatesA, ref remainingCreatesB);
            }
            else if (!canCreate)
            {
                next = OpKind.Update;
            }
            else if (mustCreate)
            {
                next = PickCreate(ref remainingCreatesA, ref remainingCreatesB);
            }
            else
            {
                var pCreate = remainingCreates / (double)(remainingCreates + remainingUpdates);
                if (Random.Shared.NextDouble() < pCreate)
                {
                    next = PickCreate(ref remainingCreatesA, ref remainingCreatesB);
                }
                else
                {
                    next = OpKind.Update;
                }
            }

            plan[i] = next;
            if (next == OpKind.Update)
            {
                remainingUpdates--;
                consecutiveUpdates++;
            }
            else
            {
                consecutiveUpdates = 0;
            }
        }

        return plan;
    }

    private static OpKind PickCreate(ref int remainingCreatesA, ref int remainingCreatesB)
    {
        if (remainingCreatesA <= 0)
        {
            remainingCreatesB--;
            return OpKind.CreateB;
        }

        if (remainingCreatesB <= 0)
        {
            remainingCreatesA--;
            return OpKind.CreateA;
        }

        var pickB = Random.Shared.Next(remainingCreatesA + remainingCreatesB) >= remainingCreatesA;
        if (pickB)
        {
            remainingCreatesB--;
            return OpKind.CreateB;
        }

        remainingCreatesA--;
        return OpKind.CreateA;
    }
}

public sealed record InterleavedTwoPoolCreateUpdateResult(
    LoadRunSummary CreateSummary,
    LoadRunSummary UpdateSummary,
    int PoolACreated,
    int PoolBCreated,
    List<Guid> PoolACreatedIdsPreview,
    List<Guid> PoolBCreatedIdsPreview);
