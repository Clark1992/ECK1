using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed record CreateSamplesOperation(
    int Count,
    int Concurrency,
    double? Rps,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool WithAddress) : IRequest<CreateSamplesResponse>;

public sealed record CreateAndUpdateNamesOperation(
    int Count,
    int UpdatesPerSample,
    int Concurrency,
    double? Rps,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool WithAddress) : IRequest<CreateAndUpdateNamesResponse>;

public sealed record HotspotUpdateNameOperation(
    Guid? Id,
    int Updates,
    int Concurrency,
    double? Rps,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool CreateIfMissing) : IRequest<HotspotUpdateResponse>;

public sealed record CreateSamplesResponse(LoadRunSummary Summary, List<Guid> CreatedIdsPreview);

public sealed record CreateAndUpdateNamesResponse(LoadRunSummary CreateSummary, LoadRunSummary UpdateSummary, List<Guid> CreatedIdsPreview);

public sealed record HotspotUpdateResponse(Guid SampleId, LoadRunSummary Summary);
