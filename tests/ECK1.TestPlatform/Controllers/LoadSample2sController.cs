using ECK1.TestPlatform.Operations;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.TestPlatform.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LoadSample2sController(
    IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Scenario: create N sample2 entities.
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<CreateSample2sResponse>> CreateSample2s(
        [FromQuery] int count = 100,
        [FromQuery] int concurrency = 10,
        [FromQuery(Name = "min_rate")] double? minRate = null,
        [FromQuery(Name = "max_rate")] double? maxRate = null,
        [FromQuery(Name = "rate_change_sec")] int? rateChangeSec = null,
        CancellationToken ct = default)
    {
        var res = await mediator.Send(
            new CreateSample2sOperation(count, concurrency, minRate, maxRate, rateChangeSec),
            ct);

        return Ok(res);
    }

    /// <summary>
    /// Scenario: create N sample2 entities then apply M customer-email updates to each.
    /// </summary>
    [HttpPost("create-and-update-customer-emails")]
    public async Task<ActionResult<CreateAndUpdateSample2CustomerEmailsResponse>> CreateAndUpdateCustomerEmails(
        [FromQuery] int count = 50,
        [FromQuery] int updatesPerSample2 = 5,
        [FromQuery] int concurrency = 10,
        [FromQuery(Name = "min_rate")] double? minRate = null,
        [FromQuery(Name = "max_rate")] double? maxRate = null,
        [FromQuery(Name = "rate_change_sec")] int? rateChangeSec = null,
        CancellationToken ct = default)
    {
        var res = await mediator.Send(
            new CreateAndUpdateSample2CustomerEmailsOperation(count, updatesPerSample2, concurrency, minRate, maxRate, rateChangeSec),
            ct);

        return Ok(res);
    }

    /// <summary>
    /// Scenario: create N sample2 entities then apply M shipping-address updates to each.
    /// </summary>
    [HttpPost("create-and-update-shipping-addresses")]
    public async Task<ActionResult<CreateAndUpdateSample2ShippingAddressesResponse>> CreateAndUpdateShippingAddresses(
        [FromQuery] int count = 50,
        [FromQuery] int updatesPerSample2 = 5,
        [FromQuery] int concurrency = 10,
        [FromQuery(Name = "min_rate")] double? minRate = null,
        [FromQuery(Name = "max_rate")] double? maxRate = null,
        [FromQuery(Name = "rate_change_sec")] int? rateChangeSec = null,
        CancellationToken ct = default)
    {
        var res = await mediator.Send(
            new CreateAndUpdateSample2ShippingAddressesOperation(count, updatesPerSample2, concurrency, minRate, maxRate, rateChangeSec),
            ct);

        return Ok(res);
    }

    /// <summary>
    /// Scenario: repeatedly update a single sample2 entity ("hot key") by changing status.
    /// </summary>
    [HttpPost("hotspot/update-status")]
    public async Task<ActionResult<HotspotUpdateSample2StatusResponse>> HotspotUpdateStatus(
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
                new HotspotUpdateSample2StatusOperation(id, updates, concurrency, minRate, maxRate, rateChangeSec, createIfMissing),
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
