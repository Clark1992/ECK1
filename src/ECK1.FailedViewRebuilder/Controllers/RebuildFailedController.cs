using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.FailedViewRebuilder.Models;
using ECK1.FailedViewRebuilder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ECK1.FailedViewRebuilder.Controllers;

[ApiController]
[Route("api/jobs/[controller]")]
public class RebuildFailedController(
    IRebuildRequestService<EventFailure, Guid> service,
    IOptionsSnapshot<KafkaSettings> config) : ControllerBase
{

    [HttpPost("samples")]
    [ProducesResponseType(typeof(string), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartRebuildSample([FromQuery] int? count)
    {
        var result = await service.StartJob(
            config.Value.SampleEventsRebuildRequestTopic,
            new QueryParams<EventFailure, DateTimeOffset> 
            { 
                Filter = e => e.EntityType == EntityType.Sample,
                OrderBy = e => e.FailureOccurredAt,
                IsAsc = true,
                Count = count,
            },
            e => e.EntityId);

        return Accepted((object)result);
    }

    [HttpDelete("samples")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> StopRebuildSample()
    {
        var topic = config.Value.SampleEventsRebuildRequestTopic;
        var result = await service.StopJob(topic);

        return Ok(result == 0 ? $"No {topic} job(s) in progress" : $"{topic}: stopped {result} job(s).");
    }

    [HttpGet("samples")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRebuildSampleStatus()
    {
        var topic = config.Value.SampleEventsRebuildRequestTopic;
        var result = await service.GetStatus(topic);

        return Ok($"{result} job(s) in progress");
    }

    [HttpGet("/api/failed/samples")]
    [ProducesResponseType(typeof(FailedViewsResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSampleOverview() => 
        Ok(await service.GetFailedViewsOverview(
            x => (x.EntityId, x.EntityType), 
            new QueryParams<EventFailure, DateTimeOffset>
            {
                Filter = e => e.EntityType == EntityType.Sample,
                OrderBy = e => e.FailureOccurredAt,
                IsAsc = true,
            }));

    [HttpPost("sample2s")]
    [ProducesResponseType(typeof(string), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartRebuildSample2([FromQuery] int? count)
    {
        var result = await service.StartJob(
            config.Value.Sample2EventsRebuildRequestTopic,
            new QueryParams<EventFailure, DateTimeOffset>
            {
                Filter = e => e.EntityType == EntityType.Sample2,
                OrderBy = e => e.FailureOccurredAt,
                IsAsc = true,
                Count = count,
            },
            e => e.EntityId);

        return Accepted((object)result);
    }

    [HttpDelete("sample2s")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> StopRebuildSample2()
    {
        var topic = config.Value.Sample2EventsRebuildRequestTopic;
        var result = await service.StopJob(topic);

        return Ok(result == 0 ? $"No {topic} job(s) in progress" : $"{topic}: stopped {result} job(s).");
    }

    [HttpGet("sample2s")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRebuildSample2Status()
    {
        var topic = config.Value.Sample2EventsRebuildRequestTopic;
        var result = await service.GetStatus(topic);

        return Ok($"{result} job(s) in progress");
    }

    [HttpGet("/api/failed/sample2s")]
    [ProducesResponseType(typeof(FailedViewsResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSample2Overview() =>
        Ok(await service.GetFailedViewsOverview(
            x => (x.EntityId, x.EntityType),
            new QueryParams<EventFailure, DateTimeOffset>
            {
                Filter = e => e.EntityType == EntityType.Sample2,
                OrderBy = e => e.FailureOccurredAt,
                IsAsc = true,
            }));

    [HttpGet("/api/failed")]
    [ProducesResponseType(typeof(FailedViewsResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview() =>
        Ok(await service.GetFailedViewsOverview(
            x => (x.EntityId, x.EntityType),
            new QueryParams<EventFailure, DateTimeOffset>
            {
                OrderBy = e => e.FailureOccurredAt,
                IsAsc = true,
            }));
}