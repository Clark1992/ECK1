using MediatR;
using ECK1.QueriesAPI.Views;

namespace ECK1.QueriesAPI.Queries;

public record PagedQuery<T>: IRequest<IReadOnlyCollection<T>>
{
    public int Top { get; set; } = 10;
    public int Skip { get; set; }
    public string Order { get; set; } = string.Empty;
}

public record GetSampleByIdQuery(Guid Id) : IRequest<SampleView>;

public record GetSamplesPagedQuery : PagedQuery<SampleView>;
