using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed class CreateAndUpdateNamesOperationHandler(
    CommandsApiClient commands,
    FakeSampleDataFactory fake,
    InterleavedCreateUpdateRunner interleaved) : IRequestHandler<CreateAndUpdateNamesOperation, CreateAndUpdateNamesResponse>
{
    public async Task<CreateAndUpdateNamesResponse> Handle(CreateAndUpdateNamesOperation request, CancellationToken ct)
    {
        var count = Math.Max(0, request.Count);
        var updatesPerSample = Math.Max(1, request.UpdatesPerSample);

        var result = await interleaved.RunAsync(
            createCount: count,
            updatesPerEntity: updatesPerSample,
            concurrency: request.Concurrency,
            minRate: request.MinRate,
            maxRate: request.MaxRate,
            rateChangeSec: request.RateChangeSec,
            createAsync: async token =>
            {
                var req = fake.CreateSample(request.WithAddress);
                var accepted = await commands.CreateSampleAsync(req, token);
                return accepted?.Id;
            },
            updateAsync: async (id, token) =>
            {
                var accepted = await commands.ChangeSampleNameAsync(id, fake.NewName(), token);
                return accepted is not null;
            },
            previewLimit: 200,
            ct);

        return new CreateAndUpdateNamesResponse(result.CreateSummary, result.UpdateSummary, result.CreatedIdsPreview);
    }
}
