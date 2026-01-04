using ECK1.TestPlatform.Operations;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.TestPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LoadMixedController(
    IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Scenario: create a mixed stream of Sample and Sample2 entities (50/50).
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<CreateMixedSamplesResponse>> CreateMixed(
        [FromQuery] int count = 200,
        [FromQuery] int concurrency = 10,
        [FromQuery(Name = "min_rate")] double? minRate = null,
        [FromQuery(Name = "max_rate")] double? maxRate = null,
        [FromQuery(Name = "rate_change_sec")] int? rateChangeSec = null,
        [FromQuery] bool withAddress = true,
        [FromQuery(Name = "sample2_ratio")] double sample2Ratio = 0.5,
        CancellationToken ct = default)
    {
        if (sample2Ratio < 0 || sample2Ratio > 1)
            return BadRequest("sample2_ratio must be in range [0..1]");

        var res = await mediator.Send(
            new CreateMixedSamplesOperation(count, concurrency, minRate, maxRate, rateChangeSec, withAddress, sample2Ratio),
            ct);

        return Ok(res);
    }

    /// <summary>
    /// Scenario: create a mixed stream of Sample and Sample2 entities (50/50) then update each created entity M times.
    /// </summary>
    [HttpPost("create-and-update")]
    public async Task<ActionResult<CreateAndUpdateMixedResponse>> CreateAndUpdateMixed(
        [FromQuery] int count = 200,
        [FromQuery] int updatesPerEntity = 2,
        [FromQuery] int concurrency = 10,
        [FromQuery(Name = "min_rate")] double? minRate = null,
        [FromQuery(Name = "max_rate")] double? maxRate = null,
        [FromQuery(Name = "rate_change_sec")] int? rateChangeSec = null,
        [FromQuery] bool withAddress = true,
        [FromQuery(Name = "sample2_ratio")] double sample2Ratio = 0.5,
        CancellationToken ct = default)
    {
        if (sample2Ratio < 0 || sample2Ratio > 1)
            return BadRequest("sample2_ratio must be in range [0..1]");

        var res = await mediator.Send(
            new CreateAndUpdateMixedOperation(count, updatesPerEntity, concurrency, minRate, maxRate, rateChangeSec, withAddress, sample2Ratio),
            ct);

        return Ok(res);
    }
}
