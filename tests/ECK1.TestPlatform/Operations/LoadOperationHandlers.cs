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

public sealed class CreateAndUpdateNamesOperationHandler(
    CommandsApiClient commands,
    FakeSampleDataFactory fake,
    LoadRunner runner) : IRequestHandler<CreateAndUpdateNamesOperation, CreateAndUpdateNamesResponse>
{
    public async Task<CreateAndUpdateNamesResponse> Handle(CreateAndUpdateNamesOperation request, CancellationToken ct)
    {
        var count = Math.Max(0, request.Count);
        var updatesPerSample = Math.Max(1, request.UpdatesPerSample);

        var created = new List<Guid>(count);

        var createSummary = await runner.RunAsync(
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

                lock (created)
                {
                    created.Add(accepted.Id);
                }

                return true;
            },
            ct);

        if (created.Count == 0)
        {
            var empty = new LoadRunSummary(0, 0, 0, 0, 0);
            return new CreateAndUpdateNamesResponse(createSummary, empty, []);
        }

        var totalUpdates = created.Count * updatesPerSample;
        var updateSummary = await runner.RunAsync(
            totalUpdates,
            request.Concurrency,
            request.MinRate,
            request.MaxRate,
            request.RateChangeSec,
            async (i, token) =>
            {
                var id = created[i % created.Count];
                var accepted = await commands.ChangeSampleNameAsync(id, fake.NewName(), token);
                return accepted is not null;
            },
            ct);

        return new CreateAndUpdateNamesResponse(createSummary, updateSummary, created.Take(200).ToList());
    }
}

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
