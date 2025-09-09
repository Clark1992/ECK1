using ECK1.QueriesAPI.Controllers;
using ECK1.QueriesAPI.Queries;
using ECK1.QueriesAPI.Views;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.QueriesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
public class SampleController : ControllerBase
{
    private readonly IMediator _mediator;
    public SampleController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SampleView), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetSampleByIdQuery(id));
        return this.ToResult(result);
    }
}
