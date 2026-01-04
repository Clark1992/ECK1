using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed class HotspotUpdateNameOperationHandler(
    CommandsApiClient commands,
    FakeSampleDataFactory fake,
    LoadRunner runner) : IRequestHandler<HotspotUpdateNameOperation, HotspotUpdateResponse>
{
    public async Task<HotspotUpdateResponse> Handle(HotspotUpdateNameOperation request, CancellationToken ct)
    {
        Guid? sampleId = request.Id;

        if (sampleId is null && request.CreateIfMissing)
        {
            var accepted = await commands.CreateSampleAsync(fake.CreateSample(withAddress: true), ct);
            if (accepted is null)
                throw new InvalidOperationException("Failed to create a sample via CommandsAPI");

            sampleId = accepted.Id;
        }

        if (sampleId is null)
            throw new ArgumentException("Provide id=... or set createIfMissing=true");

        var updates = Math.Max(0, request.Updates);

        var summary = await runner.RunAsync(
            updates,
            request.Concurrency,
            request.MinRate,
            request.MaxRate,
            request.RateChangeSec,
            async (_, token) =>
            {
                var accepted = await commands.ChangeSampleNameAsync(sampleId.Value, fake.NewName(), token);
                return accepted is not null;
            },
            ct);

        return new HotspotUpdateResponse(sampleId.Value, summary);
    }
}
