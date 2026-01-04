using ECK1.CommandsAPI.Commands;
using ECK1.CommandsAPI.Domain.Sample2s;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.CommandsAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(typeof(Success), StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
public class Sample2Controller(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSample2Command command)
    {
        var result = await mediator.Send(command);
        return this.ToResult(result);
    }

    [HttpPut("{id}/customer-email")]
    public async Task<IActionResult> ChangeCustomerEmail(Guid id, [FromBody] string newEmail)
    {
        var result = await mediator.Send(new ChangeSample2CustomerEmailCommand(id, newEmail));
        return this.ToResult(result);
    }

    [HttpPut("{id}/shipping-address")]
    public async Task<IActionResult> ChangeShippingAddress(Guid id, [FromBody] Sample2Address newAddress)
    {
        var result = await mediator.Send(new ChangeSample2ShippingAddressCommand(id, newAddress));
        return this.ToResult(result);
    }

    [HttpPost("{id}/line-items")]
    public async Task<IActionResult> AddLineItem(Guid id, [FromBody] Sample2LineItem item)
    {
        var result = await mediator.Send(new AddSample2LineItemCommand(id, item));
        return this.ToResult(result);
    }

    [HttpDelete("{id}/line-items/{itemId}")]
    public async Task<IActionResult> RemoveLineItem(Guid id, Guid itemId)
    {
        var result = await mediator.Send(new RemoveSample2LineItemCommand(id, itemId));
        return this.ToResult(result);
    }

    public record ChangeStatusRequest(Sample2Status NewStatus, string Reason);

    [HttpPut("{id}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest request)
    {
        var result = await mediator.Send(new ChangeSample2StatusCommand(id, request.NewStatus, request.Reason));
        return this.ToResult(result);
    }

    [HttpPost("{id}/tags")]
    public async Task<IActionResult> AddTag(Guid id, [FromBody] string tag)
    {
        var result = await mediator.Send(new AddSample2TagCommand(id, tag));
        return this.ToResult(result);
    }

    [HttpDelete("{id}/tags")]
    public async Task<IActionResult> RemoveTag(Guid id, [FromQuery] string tag)
    {
        var result = await mediator.Send(new RemoveSample2TagCommand(id, tag));
        return this.ToResult(result);
    }
}
