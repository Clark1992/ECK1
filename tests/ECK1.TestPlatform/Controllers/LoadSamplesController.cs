using ECK1.TestPlatform.Operations;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.TestPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LoadSamplesController(
    IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Scenario: create N samples.
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<CreateSamplesResponse>> CreateSamples(
        [FromQuery] int count = 100,
        [FromQuery] int concurrency = 10,
        [FromQuery(Name = "min_rate")] double? minRate = null,
        [FromQuery(Name = "max_rate")] double? maxRate = null,
        [FromQuery(Name = "rate_change_sec")] int? rateChangeSec = null,
        [FromQuery] bool withAddress = true,
        CancellationToken ct = default)
    {
        var res = await mediator.Send(
            new CreateSamplesOperation(count, concurrency, minRate, maxRate, rateChangeSec, withAddress),
            ct);

        return Ok(res);
    }

    /// <summary>
    /// Scenario: create N samples then apply M name updates to each sample.
    /// Useful for driving event volume and checking snapshot cadence.
    /// </summary>
    [HttpPost("create-and-update-names")]
    public async Task<ActionResult<CreateAndUpdateNamesResponse>> CreateAndUpdateNames(
        [FromQuery] int count = 50,
        [FromQuery] int updatesPerSample = 5,
        [FromQuery] int concurrency = 10,
        [FromQuery(Name = "min_rate")] double? minRate = null,
        [FromQuery(Name = "max_rate")] double? maxRate = null,
        [FromQuery(Name = "rate_change_sec")] int? rateChangeSec = null,
        [FromQuery] bool withAddress = true,
        CancellationToken ct = default)
    {
        var res = await mediator.Send(
            new CreateAndUpdateNamesOperation(count, updatesPerSample, concurrency, minRate, maxRate, rateChangeSec, withAddress),
            ct);

        return Ok(res);
    }

    /// <summary>
    /// Scenario: repeatedly update a single sample ("hot key").
    /// Helps reproduce contention/versioning issues.
    /// </summary>
    [HttpPost("hotspot/update-name")]
    public async Task<ActionResult<HotspotUpdateResponse>> HotspotUpdateName(
        [FromQuery] Guid? id = null,
        [FromQuery] int updates = 500,
        [FromQuery] int concurrency = 10,
        [FromQuery(Name = "min_rate")] double? minRate = null,
        [FromQuery(Name = "max_rate")] double? maxRate = null,
        [FromQuery(Name = "rate_change_sec")] int? rateChangeSec = null,
        [FromQuery] bool createIfMissing = true,
        CancellationToken ct = default)
    {
        try
        {
            var res = await mediator.Send(
                new HotspotUpdateNameOperation(id, updates, concurrency, minRate, maxRate, rateChangeSec, createIfMissing),
                ct);

            return Ok(res);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }
        catch (InvalidOperationException e)
        {
            return StatusCode(502, e.Message);
        }
    }
}
