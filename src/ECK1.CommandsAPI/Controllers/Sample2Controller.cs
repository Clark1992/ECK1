using ECK1.CommandsAPI.Commands;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Dto.Common;
using ECK1.CommandsAPI.Dto.Sample2;
using ECK1.Orleans.Grains;
using Microsoft.AspNetCore.Mvc;
using Orleans;

namespace ECK1.CommandsAPI.Controllers;

[ApiController]
[Route("api/sync/[controller]")]
[ProducesResponseType(typeof(Success), StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ConcurrencyConflict), StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
public class Sample2Controller(IGrainRouter<ISample2Command, NullGrainMetadata, ICommandResult> grainRouter) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSample2Command command, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(command, ct);
        return this.ToResult(result);
    }

    [HttpPut("{id}/customer-email")]
    public async Task<IActionResult> ChangeCustomerEmail(Guid id, [FromBody] string newEmail, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(new ChangeSample2CustomerEmailCommand(id, newEmail), ct);
        return this.ToResult(result);
    }

    [HttpPut("{id}/shipping-address")]
    public async Task<IActionResult> ChangeShippingAddress(Guid id, [FromBody] Address newAddress, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(new ChangeSample2ShippingAddressCommand(id, newAddress), ct);
        return this.ToResult(result);
    }

    [HttpPost("{id}/line-items")]
    public async Task<IActionResult> AddLineItem(Guid id, [FromBody] LineItem item, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(new AddSample2LineItemCommand(id, item), ct);
        return this.ToResult(result);
    }

    [HttpDelete("{id}/line-items/{itemId}")]
    public async Task<IActionResult> RemoveLineItem(Guid id, Guid itemId, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(new RemoveSample2LineItemCommand(id, itemId), ct);
        return this.ToResult(result);
    }

    public record ChangeStatusRequest(Sample2Status NewStatus, string Reason);

    [HttpPut("{id}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest request, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(new ChangeSample2StatusCommand(id, request.NewStatus, request.Reason), ct);
        return this.ToResult(result);
    }

    [HttpPost("{id}/tags")]
    public async Task<IActionResult> AddTag(Guid id, [FromBody] string tag, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(new AddSample2TagCommand(id, tag), ct);
        return this.ToResult(result);
    }

    [HttpDelete("{id}/tags")]
    public async Task<IActionResult> RemoveTag(Guid id, [FromQuery] string tag, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(new RemoveSample2TagCommand(id, tag), ct);
        return this.ToResult(result);
    }
}
