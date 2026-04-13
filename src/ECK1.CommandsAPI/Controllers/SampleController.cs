using ECK1.CommandsAPI.Commands;
using ECK1.Orleans.Grains;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.CommandsAPI.Controllers;

[ApiController]
[Route("api/sync/[controller]")]
[ProducesResponseType(typeof(Success), StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(VersionConflict), StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
public class SampleController(IGrainRouter<ISampleCommand, NullGrainMetadata, ICommandResult> grainRouter) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSampleCommand command, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(command, ct);
        return this.ToResult(result);
    }

    [HttpPut("{id}/name")]
    public async Task<IActionResult> ChangeName(Guid id, [FromQuery] int version, [FromBody] string newName, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(new ChangeSampleNameCommand(id, newName, version), ct);
        return this.ToResult(result);
    }

    [HttpPut("{id}/description")]
    public async Task<IActionResult> ChangeDescription(Guid id, [FromQuery] int version, [FromBody] string newDescription, CancellationToken ct)
    {
        var result = await grainRouter.RouteToGrain(new ChangeSampleDescriptionCommand(id, newDescription, version), ct);
        return this.ToResult(result);
    }
}