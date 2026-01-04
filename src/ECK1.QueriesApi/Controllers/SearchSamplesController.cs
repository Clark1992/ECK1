using ECK1.QueriesAPI.Queries;
using ECK1.QueriesAPI.Queries.Search.Samples;
using ECK1.QueriesAPI.Views;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.QueriesAPI.Controllers;

[ApiController]
[Route("search/samples")]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
public class SearchSamplesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SearchSamplesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<SampleView>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] SearchSamplesQuery request)
    {
        var result = await _mediator.Send(request);
        return this.ToResult(result);
    }
}
