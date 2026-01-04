using ECK1.TestPlatform.Services;
using MediatR;

namespace ECK1.TestPlatform.Operations;

public sealed record CreateSample2sOperation(
    int Count,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec)
    : LoadOperationBase(Count, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<CreateSample2sResponse>;

public sealed record CreateAndUpdateSample2CustomerEmailsOperation(
    int Count,
    int UpdatesPerSample2,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec)
    : LoadOperationBase(Count, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<CreateAndUpdateSample2CustomerEmailsResponse>;

public sealed record CreateAndUpdateSample2ShippingAddressesOperation(
    int Count,
    int UpdatesPerSample2,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec)
    : LoadOperationBase(Count, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<CreateAndUpdateSample2ShippingAddressesResponse>;

public sealed record HotspotUpdateSample2StatusOperation(
    Guid? Id,
    int Updates,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec,
    bool CreateIfMissing)
    : HotspotOperationBase(Updates, Concurrency, MinRate, MaxRate, RateChangeSec), IRequest<HotspotUpdateSample2StatusResponse>;

public sealed record CreateSample2sResponse(LoadRunSummary Summary, List<Guid> CreatedIdsPreview);

public sealed record CreateAndUpdateSample2CustomerEmailsResponse(
    LoadRunSummary CreateSummary,
    LoadRunSummary UpdateSummary,
    List<Guid> CreatedIdsPreview);

public sealed record CreateAndUpdateSample2ShippingAddressesResponse(
    LoadRunSummary CreateSummary,
    LoadRunSummary UpdateSummary,
    List<Guid> CreatedIdsPreview);

public sealed record HotspotUpdateSample2StatusResponse(Guid Sample2Id, LoadRunSummary Summary);
