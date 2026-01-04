using ECK1.QueriesAPI.Data;
using ECK1.QueriesAPI.Views;
using MediatR;

namespace ECK1.QueriesAPI.Queries;

public record PagedQuery<T>: IRequest<PagedResponse<T>>, IPageRequest
{
    public int Top { get; set; } = 10;
    public int Skip { get; set; }
    public string Order { get; set; } = string.Empty;
}

public record PagedResponse<T>
{
    public IReadOnlyCollection<T> Items { get; set; }
    public long Total { get; set; }
}

public record GetSampleByIdQuery(Guid Id) : IRequest<SampleView>;

public record GetSamplesPagedQuery : PagedQuery<SampleView>;

public record GetSample2ByIdQuery(Guid Id) : IRequest<Sample2View>;

public record GetSample2sPagedQuery : PagedQuery<Sample2View>;
