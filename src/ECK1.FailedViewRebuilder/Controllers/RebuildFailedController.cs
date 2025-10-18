using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.FailedViewRebuilder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ECK1.FailedViewRebuilder.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RebuildFailedController(
    IRebuildRequestService<SampleEventFailure, Guid> sampleService,
    IOptionsSnapshot<KafkaSettings> config) : ControllerBase
{

    [HttpPost("samples")]
    [ProducesResponseType(typeof(string), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartRebuildSample([FromQuery] int? count)
    {
        var result = await sampleService.StartJob(
            config.Value.SampleEventsRebuildRequestTopic,
            e => e.FailureOccurredAt,
            true,
            count, 
            e => e.SampleId);

        return Accepted(result);
    }

    [HttpDelete("samples")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> StopRebuildSample()
    {
        var result = await sampleService.StopJob(config.Value.SampleEventsRebuildRequestTopic);

        return Ok(result);
    }

    [HttpGet("samples")]
    [ProducesResponseType(typeof(FailedViewsResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSampleOverview() => 
        Ok(await sampleService.GetFailedViewsOverview(x => x.SampleId, x => x.FailureOccurredAt, true));
}