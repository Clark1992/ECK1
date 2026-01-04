using ECK1.QueriesAPI.Queries;
using ECK1.QueriesAPI.Queries.Search.Sample2s;
using ECK1.QueriesAPI.Views;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.QueriesAPI.Controllers;

[ApiController]
[Route("search/sample2s")]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
public class SearchSample2sController : ControllerBase
{
    private readonly IMediator _mediator;

    public SearchSample2sController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<Sample2View>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] SearchSample2sQuery request)
    {
        var result = await _mediator.Send(request);
        return this.ToResult(result);
    }
}
