using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed record CreateMixedSamplesOperation(
    int Count,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool WithAddress,
    double Sample2Ratio)
    : LoadOperationBase(Count, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<CreateMixedSamplesResponse>;

public sealed record CreateAndUpdateMixedOperation(
    int Count,
    int UpdatesPerEntity,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool WithAddress,
    double Sample2Ratio)
    : LoadOperationBase(Count, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<CreateAndUpdateMixedResponse>;

public sealed record CreateMixedSamplesResponse(
    LoadRunSummary Summary,
    int SampleCreated,
    int Sample2Created,
    List<Guid> SampleCreatedIdsPreview,
    List<Guid> Sample2CreatedIdsPreview);

public sealed record CreateAndUpdateMixedResponse(
    LoadRunSummary CreateSummary,
    LoadRunSummary UpdateSummary,
    int SampleCreated,
    int Sample2Created,
    List<Guid> SampleCreatedIdsPreview,
    List<Guid> Sample2CreatedIdsPreview);
