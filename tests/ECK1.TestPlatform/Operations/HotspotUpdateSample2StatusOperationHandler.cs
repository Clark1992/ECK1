using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed class HotspotUpdateSample2StatusOperationHandler(
    CommandsApiClient commands,
    FakeSample2DataFactory fake,
    LoadRunner runner) : IRequestHandler<HotspotUpdateSample2StatusOperation, HotspotUpdateSample2StatusResponse>
{
    public async Task<HotspotUpdateSample2StatusResponse> Handle(HotspotUpdateSample2StatusOperation request, CancellationToken ct)
    {
        Guid? sample2Id = request.Id;

        if (sample2Id is null && request.CreateIfMissing)
        {
            var accepted = await commands.CreateSample2Async(fake.CreateSample2(), ct);
            if (accepted is null)
                throw new InvalidOperationException("Failed to create a sample2 via CommandsAPI");

            sample2Id = accepted.Id;
        }

        if (sample2Id is null)
            throw new ArgumentException("Provide id=... or set createIfMissing=true");

        var updates = Math.Max(0, request.Updates);

        var summary = await runner.RunAsync(
            updates,
            request.Concurrency,
            request.MinRate,
            request.MaxRate,
            request.RateChangeSec,
            async (i, token) =>
            {
                var newStatus = i % 5;
                var accepted = await commands.ChangeSample2StatusAsync(sample2Id.Value, newStatus, fake.NewReason(), token);
                return accepted is not null;
            },
            ct);

        return new HotspotUpdateSample2StatusResponse(sample2Id.Value, summary);
    }
}
