using ECK1.QueriesAPI.Queries;
using ECK1.QueriesAPI.Views.Sample2s;
using ECK1.VersionTracker.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECK1.QueriesAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
public class Sample2sController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IVersionTrackerService _versionTracker;

    public Sample2sController(IMediator mediator, IVersionTrackerService versionTracker)
    {
        _mediator = mediator;
        _versionTracker = versionTracker;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EntityResponse<Sample2View>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetSample2ByIdQuery(id));
        if (result is null) return NotFound();

        var versionResponse = await _versionTracker.GetVersion(new GetVersionRequest
        {
            EntityType = "ECK1.Sample2",
            EntityId = id.ToString(),
            ExpectedVersion = result.Version
        });

        var isRebuilding = versionResponse.Version > result.Version;
        return Ok(new EntityResponse<Sample2View>(result, isRebuilding));
    }

    [HttpGet]
    [ProducesResponseType(typeof(Sample2View[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] GetSample2sPagedQuery request)
    {
        var result = await _mediator.Send(request);
        return this.ToResult(result);
    }
}
