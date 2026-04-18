using ECK1.QueriesAPI.Queries.History;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.QueriesAPI.Controllers;

[ApiController]
[Route("api/history")]
public class HistoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public HistoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("samples/{id}")]
    [ProducesResponseType(typeof(EntityHistoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSampleHistory(Guid id)
    {
        var result = await _mediator.Send(new GetEntityHistoryQuery("ECK1.Sample", id));
        return Ok(result);
    }

    [HttpGet("sample2s/{id}")]
    [ProducesResponseType(typeof(EntityHistoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSample2History(Guid id)
    {
        var result = await _mediator.Send(new GetEntityHistoryQuery("ECK1.Sample2", id));
        return Ok(result);
    }
}
