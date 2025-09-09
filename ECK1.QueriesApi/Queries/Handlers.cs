using MediatR;
using ECK1.QueriesApi.Data;
using ECK1.QueriesAPI.Views;
using MongoDB.Driver;

namespace ECK1.QueriesAPI.Queries;

public class Handlers : IRequestHandler<GetSampleByIdQuery, SampleView>
{
    private readonly MongoDbContext _dbContext;
    public Handlers(MongoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SampleView> Handle(GetSampleByIdQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.Samples.Find(s => s.SampleId == request.Id).FirstOrDefaultAsync(cancellationToken);
    }
}
