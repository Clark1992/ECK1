using ECK1.QueriesAPI.Queries.Analytics;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.QueriesAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("overview")]
    [ProducesResponseType(typeof(AnalyticsOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOverview([FromQuery] GetAnalyticsOverviewQuery request)
    {
        try
        {
            return Ok(await _mediator.Send(request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid analytics request", Detail = ex.Message });
        }
    }

    [HttpGet("trend")]
    [ProducesResponseType(typeof(AnalyticsTrendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTrend([FromQuery] GetAnalyticsTrendQuery request)
    {
        try
        {
            return Ok(await _mediator.Send(request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid analytics request", Detail = ex.Message });
        }
    }

    [HttpGet("breakdown")]
    [ProducesResponseType(typeof(AnalyticsBreakdownResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBreakdown([FromQuery] GetAnalyticsBreakdownQuery request)
    {
        try
        {
            return Ok(await _mediator.Send(request));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid analytics request", Detail = ex.Message });
        }
    }
}