using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.FailedViewRebuilder.Models;
using ECK1.FailedViewRebuilder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ECK1.FailedViewRebuilder.Controllers;

[ApiController]
[Route("api/jobs/[controller]")]
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
            new QueryParams<SampleEventFailure, DateTimeOffset> 
            { 
                OrderBy = e => e.FailureOccurredAt,
                IsAsc = true,
                Count = count,
            },
            e => e.SampleId);

        return Accepted((object)result);
    }

    [HttpDelete("samples")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> StopRebuildSample()
    {
        var topic = config.Value.SampleEventsRebuildRequestTopic;
        var result = await sampleService.StopJob(topic);

        return Ok(result == 0 ? $"No {topic} job(s) in progress" : $"{topic}: stopped {result} job(s).");
    }

    [HttpGet("samples")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRebuildSampleStatus()
    {
        var topic = config.Value.SampleEventsRebuildRequestTopic;
        var result = await sampleService.GetStatus(topic);

        return Ok($"{result} job(s) in progress");
    }

    [HttpGet("/api/failed/samples")]
    [ProducesResponseType(typeof(FailedViewsResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSampleOverview() => 
        Ok(await sampleService.GetFailedViewsOverview(
            x => x.SampleId, 
            new QueryParams<SampleEventFailure, DateTimeOffset>
            {
                OrderBy = e => e.FailureOccurredAt,
                IsAsc = true,
            }));
}