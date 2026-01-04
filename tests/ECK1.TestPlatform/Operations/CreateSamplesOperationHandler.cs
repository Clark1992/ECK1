using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed class CreateSamplesOperationHandler(
    CommandsApiClient commands,
    FakeSampleDataFactory fake,
    LoadRunner runner) : IRequestHandler<CreateSamplesOperation, CreateSamplesResponse>
{
    public async Task<CreateSamplesResponse> Handle(CreateSamplesOperation request, CancellationToken ct)
    {
        var count = Math.Max(0, request.Count);
        var createdIds = new List<Guid>(Math.Min(count, 200));

        var summary = await runner.RunAsync(
            count,
            request.Concurrency,
            request.MinRate,
            request.MaxRate,
            request.RateChangeSec,
            async (_, token) =>
            {
                var req = fake.CreateSample(request.WithAddress);
                var accepted = await commands.CreateSampleAsync(req, token);
                if (accepted is null) return false;

                lock (createdIds)
                {
                    if (createdIds.Count < 200)
                        createdIds.Add(accepted.Id);
                }

                return true;
            },
            ct);

        return new CreateSamplesResponse(summary, createdIds);
    }
}
