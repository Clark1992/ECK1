namespace ECK1.TestPlatform.Operations;

public abstract record LoadOperationBase(
    int Count,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec);

public abstract record HotspotOperationBase(
    int Updates,
    int Concurrency,
    double? MinRate,
    double? MaxRate,
    int? RateChangeSec);
