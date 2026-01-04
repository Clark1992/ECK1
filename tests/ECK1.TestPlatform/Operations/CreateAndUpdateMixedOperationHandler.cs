using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed class CreateAndUpdateMixedOperationHandler(
    CommandsApiClient commands,
    FakeSampleDataFactory fake,
    FakeSample2DataFactory fake2,
    InterleavedTwoPoolCreateUpdateRunner interleaved) : IRequestHandler<CreateAndUpdateMixedOperation, CreateAndUpdateMixedResponse>
{
    public async Task<CreateAndUpdateMixedResponse> Handle(CreateAndUpdateMixedOperation request, CancellationToken ct)
    {
        var count = Math.Max(0, request.Count);
        var updatesPerEntity = Math.Max(1, request.UpdatesPerEntity);
        var sample2Ratio = Math.Clamp(request.Sample2Ratio, 0, 1);

        var result = await interleaved.RunAsync(
            createCount: count,
            updatesPerEntity: updatesPerEntity,
            poolBRatio: sample2Ratio,
            concurrency: request.Concurrency,
            minRate: request.MinRate,
            maxRate: request.MaxRate,
            rateChangeSec: request.RateChangeSec,
            createAAsync: async token =>
            {
                var accepted = await commands.CreateSampleAsync(fake.CreateSample(request.WithAddress), token);
                return accepted?.Id;
            },
            createBAsync: async token =>
            {
                var accepted = await commands.CreateSample2Async(fake2.CreateSample2(), token);
                return accepted?.Id;
            },
            updateAAsync: async (id, token) =>
            {
                var accepted = await commands.ChangeSampleNameAsync(id, fake.NewName(), token);
                return accepted is not null;
            },
            updateBAsync: async (id, token) =>
            {
                var accepted = await commands.ChangeSample2CustomerEmailAsync(id, fake2.NewEmail(), token);
                return accepted is not null;
            },
            previewLimit: 200,
            ct);

        return new CreateAndUpdateMixedResponse(
            result.CreateSummary,
            result.UpdateSummary,
            result.PoolACreated,
            result.PoolBCreated,
            result.PoolACreatedIdsPreview,
            result.PoolBCreatedIdsPreview);
    }
}
