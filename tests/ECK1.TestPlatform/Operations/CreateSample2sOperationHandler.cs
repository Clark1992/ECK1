using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed class CreateSample2sOperationHandler(
    CommandsApiClient commands,
    FakeSample2DataFactory fake,
    LoadRunner runner) : IRequestHandler<CreateSample2sOperation, CreateSample2sResponse>
{
    public async Task<CreateSample2sResponse> Handle(CreateSample2sOperation request, CancellationToken ct)
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
                var req = fake.CreateSample2();
                var accepted = await commands.CreateSample2Async(req, token);
                if (accepted is null) return false;

                lock (createdIds)
                {
                    if (createdIds.Count < 200)
                        createdIds.Add(accepted.Id);
                }

                return true;
            },
            ct);

        return new CreateSample2sResponse(summary, createdIds);
    }
}
