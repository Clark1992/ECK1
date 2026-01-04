using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed class CreateAndUpdateDescriptionsOperationHandler(
    CommandsApiClient commands,
    FakeSampleDataFactory fake,
    InterleavedCreateUpdateRunner interleaved) : IRequestHandler<CreateAndUpdateDescriptionsOperation, CreateAndUpdateDescriptionsResponse>
{
    public async Task<CreateAndUpdateDescriptionsResponse> Handle(CreateAndUpdateDescriptionsOperation request, CancellationToken ct)
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
                var accepted = await commands.ChangeSampleDescriptionAsync(id, fake.NewDescription(), token);
                return accepted is not null;
            },
            previewLimit: 200,
            ct);

        return new CreateAndUpdateDescriptionsResponse(result.CreateSummary, result.UpdateSummary, result.CreatedIdsPreview);
    }
}
