using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed class CreateAndUpdateSample2CustomerEmailsOperationHandler(
    CommandsApiClient commands,
    FakeSample2DataFactory fake,
    InterleavedCreateUpdateRunner interleaved) : IRequestHandler<CreateAndUpdateSample2CustomerEmailsOperation, CreateAndUpdateSample2CustomerEmailsResponse>
{
    public async Task<CreateAndUpdateSample2CustomerEmailsResponse> Handle(CreateAndUpdateSample2CustomerEmailsOperation request, CancellationToken ct)
    {
        var count = Math.Max(0, request.Count);
        var updatesPerSample2 = Math.Max(1, request.UpdatesPerSample2);

        var result = await interleaved.RunAsync(
            createCount: count,
            updatesPerEntity: updatesPerSample2,
            concurrency: request.Concurrency,
            minRate: request.MinRate,
            maxRate: request.MaxRate,
            rateChangeSec: request.RateChangeSec,
            createAsync: async token =>
            {
                var req = fake.CreateSample2();
                var accepted = await commands.CreateSample2Async(req, token);
                return accepted?.Id;
            },
            updateAsync: async (id, token) =>
            {
                var accepted = await commands.ChangeSample2CustomerEmailAsync(id, fake.NewEmail(), token);
                return accepted is not null;
            },
            previewLimit: 200,
            ct);

        return new CreateAndUpdateSample2CustomerEmailsResponse(result.CreateSummary, result.UpdateSummary, result.CreatedIdsPreview);
    }
}
