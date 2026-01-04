using ECK1.QueriesAPI.Queries;
using ECK1.QueriesAPI.Views;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.QueriesAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
public class Sample2sController : ControllerBase
{
    private readonly IMediator _mediator;
    public Sample2sController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Sample2View), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetSample2ByIdQuery(id));
        return this.ToResult(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(Sample2View[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] GetSample2sPagedQuery request)
    {
        var result = await _mediator.Send(request);
        return this.ToResult(result);
    }
}
