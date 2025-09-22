using MediatR;
using ECK1.QueriesApi.Data;
using ECK1.QueriesAPI.Views;
using MongoDB.Driver;
using ECK1.QueriesAPI.Data;

namespace ECK1.QueriesAPI.Queries;

public class Handlers : HandlersBase, 
    IRequestHandler<GetSampleByIdQuery, SampleView>,
    IRequestHandler<GetSamplesPagedQuery, PagedResponse<SampleView>>
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

    public Task<PagedResponse<SampleView>> Handle(GetSamplesPagedQuery request, CancellationToken ct) =>
        GetPagedAsync(_dbContext.Samples, request, FilterDefinition<SampleView>.Empty, ct);
}

public class HandlersBase
{
    protected async Task<PagedResponse<T>> GetPagedAsync<T>(
        IMongoCollection<T> collection,
        PagedQuery<T> request,
        FilterDefinition<T> filter,
        CancellationToken ct)
    {
        var query = collection
            .Find(filter)
            .ApplyPaging(request);

        return new PagedResponse<T>
        {
            Items = await query.ToListAsync(ct),
            Total = await collection.CountDocumentsAsync(filter, cancellationToken: ct)
        };
    }
}
