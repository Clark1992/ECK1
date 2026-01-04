using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed class CreateMixedSamplesOperationHandler(
    CommandsApiClient commands,
    FakeSampleDataFactory fake,
    FakeSample2DataFactory fake2,
    LoadRunner runner) : IRequestHandler<CreateMixedSamplesOperation, CreateMixedSamplesResponse>
{
    public async Task<CreateMixedSamplesResponse> Handle(CreateMixedSamplesOperation request, CancellationToken ct)
    {
        var count = Math.Max(0, request.Count);
        var sample2Ratio = Math.Clamp(request.Sample2Ratio, 0, 1);

        var sampleCreated = 0;
        var sample2Created = 0;

        var sampleIds = new List<Guid>(200);
        var sample2Ids = new List<Guid>(200);

        var summary = await runner.RunAsync(
            count,
            request.Concurrency,
            request.MinRate,
            request.MaxRate,
            request.RateChangeSec,
            async (i, token) =>
            {
                var createSample2 = Random.Shared.NextDouble() < sample2Ratio;

                if (!createSample2)
                {
                    var req = fake.CreateSample(request.WithAddress);
                    var accepted = await commands.CreateSampleAsync(req, token);
                    if (accepted is null) return false;

                    Interlocked.Increment(ref sampleCreated);
                    lock (sampleIds)
                    {
                        if (sampleIds.Count < 200)
                            sampleIds.Add(accepted.Id);
                    }

                    return true;
                }

                var req2 = fake2.CreateSample2();
                var accepted2 = await commands.CreateSample2Async(req2, token);
                if (accepted2 is null) return false;

                Interlocked.Increment(ref sample2Created);
                lock (sample2Ids)
                {
                    if (sample2Ids.Count < 200)
                        sample2Ids.Add(accepted2.Id);
                }

                return true;
            },
            ct);

        return new CreateMixedSamplesResponse(
            summary,
            sampleCreated,
            sample2Created,
            sampleIds,
            sample2Ids);
    }
}
