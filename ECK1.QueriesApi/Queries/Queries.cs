using MediatR;
using ECK1.QueriesAPI.Views;

namespace ECK1.QueriesAPI.Queries;

public record GetSampleByIdQuery(Guid Id) : IRequest<SampleView>;
