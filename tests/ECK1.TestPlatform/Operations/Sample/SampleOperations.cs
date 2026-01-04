using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed record CreateSamplesOperation(
    int Count,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool WithAddress)
    : LoadOperationBase(Count, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<CreateSamplesResponse>;

public sealed record CreateAndUpdateNamesOperation(
    int Count,
    int UpdatesPerSample,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool WithAddress)
    : LoadOperationBase(Count, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<CreateAndUpdateNamesResponse>;

public sealed record CreateAndUpdateDescriptionsOperation(
    int Count,
    int UpdatesPerSample,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool WithAddress)
    : LoadOperationBase(Count, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<CreateAndUpdateDescriptionsResponse>;

public sealed record HotspotUpdateNameOperation(
    Guid? Id,
    int Updates,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool CreateIfMissing)
    : HotspotOperationBase(Updates, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<HotspotUpdateResponse>;

public sealed record CreateSamplesResponse(LoadRunSummary Summary, List<Guid> CreatedIdsPreview);

public sealed record CreateAndUpdateNamesResponse(LoadRunSummary CreateSummary, LoadRunSummary UpdateSummary, List<Guid> CreatedIdsPreview);

public sealed record CreateAndUpdateDescriptionsResponse(LoadRunSummary CreateSummary, LoadRunSummary UpdateSummary, List<Guid> CreatedIdsPreview);

public sealed record HotspotUpdateResponse(Guid SampleId, LoadRunSummary Summary);
