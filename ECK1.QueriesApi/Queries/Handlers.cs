using MediatR;
using ECK1.QueriesApi.Data;
using ECK1.QueriesAPI.Views;
using MongoDB.Driver;

namespace ECK1.QueriesAPI.Queries;

public class Handlers : HandlersBase, 
    IRequestHandler<GetSampleByIdQuery, SampleView>,
    IRequestHandler<GetSamplesPagedQuery, IReadOnlyCollection<SampleView>>
{
    private readonly MongoDbContext _dbContext;
    public Handlers(MongoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SampleView> Handle(GetSampleByIdQuery request, CancellationToken ct)
    {
        return await _dbContext.Samples.Find(s => s.SampleId == request.Id).FirstOrDefaultAsync(ct);
    }

    public Task<IReadOnlyCollection<SampleView>> Handle(GetSamplesPagedQuery request, CancellationToken ct) =>
        GetPagedAsync(_dbContext.Samples, request, ct);
}

public class HandlersBase
{
    private static SortDefinition<T> BuildSort<T>(string order)
    {
        if (string.IsNullOrWhiteSpace(order))
            return Builders<T>.Sort.Ascending("_id");

        var direction = order[0];
        var field = order[1..];

        return direction switch
        {
            '-' => Builders<T>.Sort.Descending(field),
            _ => Builders<T>.Sort.Ascending(field)
        };
    }

    protected async Task<IReadOnlyCollection<T>> GetPagedAsync<T>(
        IMongoCollection<T> collection,
        PagedQuery<T> request,
        CancellationToken cancellationToken)
    {
        var sort = BuildSort<T>(request.Order);

        var query = collection
            .Find(FilterDefinition<T>.Empty)
            .Sort(sort)
            .Skip(request.Skip)
            .Limit(request.Top);

        return await query.ToListAsync(cancellationToken);
    }
}
